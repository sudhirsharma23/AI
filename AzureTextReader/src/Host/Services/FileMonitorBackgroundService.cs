using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AzureTextReader.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using AzureTextReader.Services; // for AzureLLMService

namespace AzureTextReader.Services.Ocr
{
    /// <summary>
    /// Options for file monitor service - bound from configuration
    /// </summary>
    public class FileMonitorOptions
    {
        public string IncomingFolder { get; set; } = "incoming";
        public string StagingFolder { get; set; } = "staging";
        public string ProcessingFolder { get; set; } = "processing";
        public string ProcessedFolder { get; set; } = "processed";
        public string FailedFolder { get; set; } = "failed";
        public int PollIntervalSeconds { get; set; } = 5;
        public int FileStableSeconds { get; set; } = 3; // consider file stable if size unchanged for this many seconds
        public int MaxDegreeOfParallelism { get; set; } = 2;
        public int MaxRetries { get; set; } = 3;
        public int RetryBaseDelayMs { get; set; } = 1000;
        public bool UseFileSystemWatcher { get; set; } = false; // optional
        public string StateFolder { get; set; } = "state";
    }

    /// <summary>
    /// Background service which monitors a folder and processes incoming files using IOcrService
    /// </summary>
    public class FileMonitorBackgroundService : BackgroundService
    {
        private readonly ILogger<FileMonitorBackgroundService> _logger;
        private readonly IOcrService _ocrService;
        private readonly FileMonitorOptions _options;
        private readonly Channel<string> _channel;
        private readonly SemaphoreSlim _workerSemaphore;
        private readonly ConcurrentDictionary<string, DateTime> _fileLastSeen = new();
        private readonly HashSet<string> _processedHashes = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _processedIndexPath;
        private readonly CancellationTokenSource _internalCts = new();
        private readonly ServiceBusClient? _serviceBusClient;
        private readonly string? _serviceBusQueueName;
        private readonly IMemoryCache _memoryCache; // injected cache

        public FileMonitorBackgroundService(
        ILogger<FileMonitorBackgroundService> logger,
        IOcrService ocrService,
        IOptions<FileMonitorOptions> options,
        IServiceProvider sp,
        IMemoryCache memoryCache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));

            // ServiceBus client optional
            try
            {
                var cfg = sp.GetService(typeof(Azure.Messaging.ServiceBus.ServiceBusClient)) as ServiceBusClient;
                if (cfg != null)
                {
                    _serviceBusClient = cfg;
                    _serviceBusQueueName = Environment.GetEnvironmentVariable("SERVICE_BUS_QUEUE") ?? "document-jobs";
                    _logger.LogInformation("Service Bus configured; will enqueue into queue: {Queue}", _serviceBusQueueName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ServiceBus client not configured; falling back to in-memory channel");
            }

            // Ensure folders exist
            Directory.CreateDirectory(_options.IncomingFolder);
            Directory.CreateDirectory(_options.StagingFolder);
            Directory.CreateDirectory(_options.ProcessingFolder);
            Directory.CreateDirectory(_options.ProcessedFolder);
            Directory.CreateDirectory(_options.FailedFolder);
            Directory.CreateDirectory(_options.StateFolder);

            _channel = Channel.CreateBounded<string>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });

            _workerSemaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);
            _processedIndexPath = Path.Combine(_options.StateFolder, "processed_hashes.json");

            LoadProcessedIndex();
        }

        private void LoadProcessedIndex()
        {
            try
            {
                if (File.Exists(_processedIndexPath))
                {
                    var txt = File.ReadAllText(_processedIndexPath);
                    var arr = JsonSerializer.Deserialize<List<string>>(txt) ?? new List<string>();
                    foreach (var h in arr) _processedHashes.Add(h);
                    _logger.LogInformation("Loaded {Count} processed hashes from state", _processedHashes.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load processed index");
            }
        }

        private void SaveProcessedIndex()
        {
            try
            {
                var arr = _processedHashes.ToList();
                var txt = JsonSerializer.Serialize(arr);
                File.WriteAllText(_processedIndexPath, txt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist processed index");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("FileMonitorBackgroundService starting. Monitoring '{Incoming}'", _options.IncomingFolder);

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _internalCts.Token);

            // If service bus configured - create processor
            ServiceBusProcessor? processor = null;
            if (_serviceBusClient != null && !string.IsNullOrWhiteSpace(_serviceBusQueueName))
            {
                processor = _serviceBusClient.CreateProcessor(_serviceBusQueueName, new ServiceBusProcessorOptions
                {
                    MaxConcurrentCalls = _options.MaxDegreeOfParallelism,
                    AutoCompleteMessages = false
                });
                processor.ProcessMessageAsync += async args =>
                {
                    var body = args.Message.Body.ToString();
                    try
                    {
                        var obj = JsonSerializer.Deserialize<JsonElement>(body);
                        if (obj.TryGetProperty("Path", out var p))
                        {
                            var path = p.GetString();
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                await _workerSemaphore.WaitAsync(linkedCts.Token);
                                try { await ProcessFileAsync(path, linkedCts.Token); await args.CompleteMessageAsync(args.Message, linkedCts.Token); }
                                finally { _workerSemaphore.Release(); }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ServiceBus message processing failed");
                        await args.DeadLetterMessageAsync(args.Message, cancellationToken: linkedCts.Token);
                    }
                };
                processor.ProcessErrorAsync += args =>
                {
                    _logger.LogError(args.Exception, "ServiceBus processor error");
                    return Task.CompletedTask;
                };
                await processor.StartProcessingAsync(linkedCts.Token);
            }

            // Start in-memory workers as well to handle fallback or direct enqueued files
            var workers = new List<Task>();
            for (int i = 0; i < _options.MaxDegreeOfParallelism; i++)
            {
                workers.Add(Task.Run(() => WorkerLoopAsync(linkedCts.Token), linkedCts.Token));
            }

            // Start monitor loop
            try
            {
                if (_options.UseFileSystemWatcher)
                {
                    using var watcher = CreateFileSystemWatcher(_options.IncomingFolder);
                    watcher.EnableRaisingEvents = true;

                    // Also run periodic sweep to catch missed files
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await ScanAndEnqueueAsync(linkedCts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { _logger.LogError(ex, "Error during periodic scan"); }

                        await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), linkedCts.Token);
                    }
                }
                else
                {
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await ScanAndEnqueueAsync(linkedCts.Token);
                        }
                        catch (OperationCanceledException) { break; }
                        catch (Exception ex) { _logger.LogError(ex, "Error during periodic scan"); }

                        await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), linkedCts.Token);
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Stopping monitor; completing channel");
                _channel.Writer.Complete();
                try { await Task.WhenAll(workers); } catch { }
                if (processor != null)
                {
                    try { await processor.StopProcessingAsync(); } catch { }
                }
                SaveProcessedIndex();
            }
        }

        private FileSystemWatcher CreateFileSystemWatcher(string folder)
        {
            var watcher = new FileSystemWatcher(folder)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = false
            };

            watcher.Created += async (s, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Changed += async (s, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Renamed += async (s, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Error += (s, e) => _logger.LogWarning(e.GetException(), "FileSystemWatcher error");

            return watcher;
        }

        private Task OnFileSystemEventAsync(string path)
        {
            // record last seen time - actual enqueuing is done by periodic scan to ensure stability
            _fileLastSeen[path] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private async Task ScanAndEnqueueAsync(CancellationToken ct)
        {
            var incoming = new DirectoryInfo(_options.IncomingFolder);
            if (!incoming.Exists) return;

            // List all files directly in the incoming folder (no subfolders)
            var files = incoming.GetFiles().OrderBy(f => f.CreationTimeUtc).ToArray();

            // Map file name -> FileInfo for quick lookup
            var fileMap = files.ToDictionary(f => Path.GetFileName(f.Name), f => f, StringComparer.OrdinalIgnoreCase);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Documented procedure:
            // 1. For each file, check for an exact matching counterpart where one filename is the other with a "-1" suffix
            //    immediately before the extension (e.g., "Invoice.pdf" and "Invoice-1.pdf").
            // 2. If both exist, treat those two files as a related pair: ensure both are stable, move both to staging, create
            //    a small job descriptor (.pair.json) referencing the two staged files, and enqueue the descriptor for processing.
            // 3. If no matching "-1" counterpart exists, treat the file as a standalone document: ensure stability, move to staging
            //    and enqueue for single-file processing.

            foreach (var fi in files)
            {
                if (processedFiles.Contains(fi.FullName)) continue;
                ct.ThrowIfCancellationRequested();

                try
                {
                    var fileName = Path.GetFileName(fi.Name);
                    var ext = Path.GetExtension(fileName);
                    var baseNoExt = Path.GetFileNameWithoutExtension(fileName);

                    // Determine the expected counterpart name using exact "-1" suffix rule
                    string counterpartName;
                    bool thisIsMinusOne = baseNoExt.EndsWith("-1", StringComparison.Ordinal);
                    if (thisIsMinusOne)
                    {
                        var baseBase = baseNoExt.Substring(0, baseNoExt.Length - 2);
                        counterpartName = baseBase + ext; // e.g., Invoice-1.pdf -> counterpart Invoice.pdf
                    }
                    else
                    {
                        counterpartName = baseNoExt + "-1" + ext; // e.g., Invoice.pdf -> counterpart Invoice-1.pdf
                    }

                    // If counterpart exists and hasn't been processed yet, attempt paired handling
                    if (fileMap.TryGetValue(counterpartName, out var counterpartFile) && !processedFiles.Contains(counterpartFile.FullName))
                    {
                        var now = DateTime.UtcNow;
                        var lastSeen1 = _fileLastSeen.TryGetValue(fi.FullName, out var t1) ? t1 : fi.LastWriteTimeUtc;
                        var lastSeen2 = _fileLastSeen.TryGetValue(counterpartFile.FullName, out var t2) ? t2 : counterpartFile.LastWriteTimeUtc;
                        var stable1 = FileMonitorUtils.IsFileStable(fi.FullName, lastSeen1, _options.FileStableSeconds);
                        var stable2 = FileMonitorUtils.IsFileStable(counterpartFile.FullName, lastSeen2, _options.FileStableSeconds);

                        if (!stable1 || !stable2)
                        {
                            _fileLastSeen[fi.FullName] = now;
                            _fileLastSeen[counterpartFile.FullName] = now;
                            continue;
                        }

                        // Move both to staging (atomic rename) and create job descriptor
                        var staging1 = GetUniquePath(Path.Combine(_options.StagingFolder, Path.GetFileName(fi.Name)));
                        var staging2 = GetUniquePath(Path.Combine(_options.StagingFolder, Path.GetFileName(counterpartFile.Name)));
                        try { File.Move(fi.FullName, staging1); File.Move(counterpartFile.FullName, staging2); }
                        catch (IOException)
                        {
                            _logger.LogDebug("File in use or moved by another process: {F1} or {F2}", fi.FullName, counterpartFile.FullName);
                            continue;
                        }

                        var jobDescriptor = new { Files = new[] { staging1, staging2 } };
                        var jobPath = Path.Combine(_options.StagingFolder, GetUniqueFileName(Path.GetFileNameWithoutExtension(fileName), ".pair.json"));
                        File.WriteAllText(jobPath, JsonSerializer.Serialize(jobDescriptor), Encoding.UTF8);

                        // Enqueue descriptor
                        if (_serviceBusClient != null && !string.IsNullOrWhiteSpace(_serviceBusQueueName))
                        {
                            try
                            {
                                var sender = _serviceBusClient.CreateSender(_serviceBusQueueName);
                                var msgBody = JsonSerializer.Serialize(new { Path = jobPath });
                                var msg = new ServiceBusMessage(msgBody) { ContentType = "application/json" };
                                await sender.SendMessageAsync(msg, ct);
                                _logger.LogInformation("Sent paired job to ServiceBus: {Job}", jobPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to send paired job to ServiceBus, falling back to in-memory channel");
                                await _channel.Writer.WriteAsync(jobPath, ct);
                            }
                        }
                        else
                        {
                            await _channel.Writer.WriteAsync(jobPath, ct);
                            _logger.LogInformation("Enqueued paired job (in-memory): {Job}", jobPath);
                        }

                        processedFiles.Add(fi.FullName);
                        processedFiles.Add(counterpartFile.FullName);
                        continue;
                    }

                    // Otherwise treat as standalone: ensure stable and move to staging
                    var nowSingle = DateTime.UtcNow;
                    var lastSeenSingle = _fileLastSeen.TryGetValue(fi.FullName, out var tSingle) ? tSingle : fi.LastWriteTimeUtc;
                    var stableSingle = FileMonitorUtils.IsFileStable(fi.FullName, lastSeenSingle, _options.FileStableSeconds);
                    if (!stableSingle)
                    {
                        _fileLastSeen[fi.FullName] = nowSingle;
                        continue;
                    }

                    var stagingPath = Path.Combine(_options.StagingFolder, Path.GetFileName(fi.Name));
                    var uniqueStaging = GetUniquePath(stagingPath);

                    try
                    {
                        File.Move(fi.FullName, uniqueStaging);
                    }
                    catch (IOException)
                    {
                        _logger.LogDebug("File in use or moved by another process: {File}", fi.FullName);
                        continue;
                    }

                    // enqueue single file path
                    if (_serviceBusClient != null && !string.IsNullOrWhiteSpace(_serviceBusQueueName))
                    {
                        try
                        {
                            var sender = _serviceBusClient.CreateSender(_serviceBusQueueName);
                            var msgBody = JsonSerializer.Serialize(new { Path = uniqueStaging });
                            var msg = new ServiceBusMessage(msgBody)
                            {
                                ContentType = "application/json"
                            };
                            await sender.SendMessageAsync(msg, ct);
                            _logger.LogInformation("Sent job to ServiceBus for file: {File}", uniqueStaging);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send message to ServiceBus, falling back to in-memory channel");
                            await _channel.Writer.WriteAsync(uniqueStaging, ct);
                        }
                    }
                    else
                    {
                        await _channel.Writer.WriteAsync(uniqueStaging, ct);
                        _logger.LogInformation("Enqueued file for processing (in-memory): {File}", uniqueStaging);
                    }

                    processedFiles.Add(fi.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while scanning incoming files");
                }
            }
        }

        private static string GetPairKey(string fileName)
        {
            // Remove extension
            var name = Path.GetFileNameWithoutExtension(fileName) ?? string.Empty;
            // Remove trailing separators and numbers (e.g., doc-1, doc_01, doc1)
            var m = System.Text.RegularExpressions.Regex.Match(name, "^(.*?)(?:[-_ ]?\\d+)?$");
            var key = m.Success ? m.Groups[1].Value : name;
            return key.ToLowerInvariant();
        }

        private static string GetUniqueFileName(string baseName, string suffix)
        {
            var fileName = baseName + "_" + Guid.NewGuid().ToString("N") + suffix;
            return fileName;
        }

        private static string GetUniquePath(string path)
        {
            var dir = Path.GetDirectoryName(path) ?? string.Empty;
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var candidate = path;
            int i = 0;
            while (File.Exists(candidate) || Directory.Exists(candidate))
            {
                i++;
                candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            }
            return candidate;
        }

        private async Task WorkerLoopAsync(CancellationToken ct)
        {
            await foreach (var stagingPath in _channel.Reader.ReadAllAsync(ct))
            {
                await _workerSemaphore.WaitAsync(ct);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessFileAsync(stagingPath, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled error processing file {File}", stagingPath);
                    }
                    finally
                    {
                        _workerSemaphore.Release();
                    }
                }, ct);
            }
        }

        private async Task ProcessFileAsync(string stagingPath, CancellationToken ct)
        {
            var fileName = Path.GetFileName(stagingPath);
            var processingPath = Path.Combine(_options.ProcessingFolder, fileName);
            try
            {
                // move to processing
                var uniqueProcessing = GetUniquePath(processingPath);
                File.Move(stagingPath, uniqueProcessing);
                processingPath = uniqueProcessing;

                // If this is a pair job descriptor (.pair.json), read the two file paths and process them together
                if (processingPath.EndsWith(".pair.json", StringComparison.OrdinalIgnoreCase))
                {
                    string jobTxt = File.ReadAllText(processingPath);
                    try
                    {
                        var job = JsonSerializer.Deserialize<JsonElement>(jobTxt);
                        if (job.TryGetProperty("Files", out var filesElem) && filesElem.ValueKind == JsonValueKind.Array && filesElem.GetArrayLength() >= 2)
                        {
                            var path1 = filesElem[0].GetString();
                            var path2 = filesElem[1].GetString();
                            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2)) throw new Exception("Invalid pair job descriptor");

                            // Move the descriptor out of the way after reading
                            var descriptorDest = Path.Combine(_options.ProcessedFolder, Path.GetFileName(processingPath));
                            File.Move(processingPath, GetUniquePath(descriptorDest));

                            // Process both files (they are currently in staging folder)
                            await ProcessPairFilesAsync(path1, path2, ct);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Invalid pair job descriptor: {File}", processingPath);
                        MoveFileSafe(processingPath, _options.FailedFolder);
                        return;
                    }
                }

                // compute hash for idempotency
                var hash = await FileMonitorUtils.ComputeFileHashAsync(processingPath);
                if (_processedHashes.Contains(hash))
                {
                    _logger.LogInformation("File already processed (hash match). Moving to processed: {File}", processingPath);
                    MoveFileSafe(processingPath, _options.ProcessedFolder);
                    return;
                }

                // attempt OCR with retries
                int attempt = 0;
                Exception lastEx = null;
                while (attempt <= _options.MaxRetries)
                {
                    attempt++;
                    try
                    {
                        _logger.LogInformation("Processing {File} (attempt {Attempt})", processingPath, attempt);
                        var ocrResult = await _ocrService.ExtractTextFromFileAsync(processingPath, cacheKey: null);

                        if (ocrResult != null && ocrResult.Success)
                        {
                            // write JSON output next to file in processed folder
                            var cleaned = new Dictionary<string, object>
                            {
                                { "ImageUrl", ocrResult.ImageUrl },
                                { "Engine", ocrResult.Engine },
                                { "ProcessingTimeSeconds", ocrResult.ProcessingTime.TotalSeconds },
                                { "PlainText", ocrResult.PlainText },
                                { "Markdown", ocrResult.Markdown },
                                { "Metadata", ocrResult.Metadata }
                            };

                            var json = JsonSerializer.Serialize(cleaned, new JsonSerializerOptions { WriteIndented = true });

                            var outputName = Path.GetFileNameWithoutExtension(processingPath) + ".json";
                            var outPath = Path.Combine(_options.ProcessedFolder, outputName);
                            File.WriteAllText(outPath, json, System.Text.Encoding.UTF8);

                            // record processed hash
                            _processedHashes.Add(hash);
                            SaveProcessedIndex();

                            // move original file to processed (archive)
                            MoveFileSafe(processingPath, _options.ProcessedFolder);

                            _logger.LogInformation("Successfully processed {File}. Output: {Out}", processingPath, outPath);

                            // Invoke LLM processing after successful OCR
                            try
                            {
                                await AzureLLMService.InvokeAfterOcrAsync(_memoryCache);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "AzureLLMService invocation failed after OCR for file {File}", processingPath);
                            }

                            return;
                        }
                        else
                        {
                            lastEx = new Exception(ocrResult?.ErrorMessage ?? "OCR returned unsuccessful result");
                            _logger.LogWarning(lastEx, "OCR returned unsuccessful result for {File}", processingPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                        _logger.LogWarning(ex, "Attempt {Attempt} failed for {File}", attempt, processingPath);
                    }

                    // backoff
                    if (attempt <= _options.MaxRetries)
                    {
                        var delay = _options.RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delay, ct);
                    }
                }

                // if we get here, all attempts failed
                _logger.LogError(lastEx, "All attempts failed for {File}. Moving to failed folder.", processingPath);
                MoveFileSafe(processingPath, _options.FailedFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Processing failed for staging file {File}", stagingPath);
                try { MoveFileSafe(stagingPath, _options.FailedFolder); } catch { }
            }
        }

        private async Task ProcessPairFilesAsync(string path1, string path2, CancellationToken ct)
        {
            try
            {
                // Ensure files exist
                if (!File.Exists(path1) || !File.Exists(path2))
                {
                    _logger.LogError("Paired files not found: {P1}, {P2}", path1, path2);
                    try { MoveFileSafe(path1, _options.FailedFolder); } catch { }
                    try { MoveFileSafe(path2, _options.FailedFolder); } catch { }
                    return;
                }

                // Compute combined hash for idempotency
                var h1 = await FileMonitorUtils.ComputeFileHashAsync(path1);
                var h2 = await FileMonitorUtils.ComputeFileHashAsync(path2);
                var combinedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(h1 + h2)));
                if (_processedHashes.Contains(combinedHash))
                {
                    _logger.LogInformation("Paired files already processed (hash match). Moving to processed: {P1}, {P2}", path1, path2);
                    MoveFileSafe(path1, _options.ProcessedFolder);
                    MoveFileSafe(path2, _options.ProcessedFolder);
                    return;
                }

                // OCR both files
                var res1 = await _ocrService.ExtractTextFromFileAsync(path1, cacheKey: null);
                var res2 = await _ocrService.ExtractTextFromFileAsync(path2, cacheKey: null);

                if ((res1 == null || !res1.Success) && (res2 == null || !res2.Success))
                {
                    _logger.LogError("Both OCR attempts failed for paired files: {P1}, {P2}", path1, path2);
                    MoveFileSafe(path1, _options.FailedFolder);
                    MoveFileSafe(path2, _options.FailedFolder);
                    return;
                }

                // Merge results
                var merged = new Dictionary<string, object>
                {
                    { "Files", new[] {
                        new { File = Path.GetFileName(path1), Engine = res1?.Engine, PlainText = res1?.PlainText, Markdown = res1?.Markdown, ProcessingTimeSeconds = res1?.ProcessingTime.TotalSeconds, Metadata = res1?.Metadata },
                        new { File = Path.GetFileName(path2), Engine = res2?.Engine, PlainText = res2?.PlainText, Markdown = res2?.Markdown, ProcessingTimeSeconds = res2?.ProcessingTime.TotalSeconds, Metadata = res2?.Metadata }
                    } },
                    { "MergedPlainText", string.Join("\n\n---\n\n", new[] { res1?.PlainText ?? string.Empty, res2?.PlainText ?? string.Empty }) },
                    { "MergedMarkdown", string.Join("\n\n---\n\n", new[] { res1?.Markdown ?? string.Empty, res2?.Markdown ?? string.Empty }) },
                    { "MergedEngines", new[] { res1?.Engine, res2?.Engine } }
                };

                var json = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });

                // Save merged output
                var baseName = Path.GetFileNameWithoutExtension(path1) + "__" + Path.GetFileNameWithoutExtension(path2);
                var outPath = Path.Combine(_options.ProcessedFolder, baseName + ".merged.json");
                File.WriteAllText(outPath, json, Encoding.UTF8);

                // record processed hash
                _processedHashes.Add(combinedHash);
                SaveProcessedIndex();

                // move originals to processed
                MoveFileSafe(path1, _options.ProcessedFolder);
                MoveFileSafe(path2, _options.ProcessedFolder);

                _logger.LogInformation("Successfully processed paired files. Output: {Out}", outPath);

                try
                {
                    // Put merged content into memory cache for downstream LLM invocation
                    _memoryCache.Set("last_merged_output", json, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
                    await AzureLLMService.InvokeAfterOcrAsync(_memoryCache);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AzureLLMService invocation failed after paired OCR");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing paired files {P1}, {P2}", path1, path2);
                try { MoveFileSafe(path1, _options.FailedFolder); } catch { }
                try { MoveFileSafe(path2, _options.FailedFolder); } catch { }
            }
        }

        private static void MoveFileSafe(string sourcePath, string destinationFolder)
        {
            try
            {
                Directory.CreateDirectory(destinationFolder);
                var dest = Path.Combine(destinationFolder, Path.GetFileName(sourcePath));
                var unique = GetUniquePath(dest);
                File.Move(sourcePath, unique);
            }
            catch
            {
                try { File.Delete(sourcePath); } catch { }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("FileMonitorBackgroundService stopping");
            _internalCts.Cancel();
            // let base stop
            await base.StopAsync(cancellationToken);
        }
    }
}
