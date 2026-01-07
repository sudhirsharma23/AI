using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oasis.DeedProcessor.Interface.Llm;
using Oasis.DeedProcessor.Interface.Ocr;

namespace Oasis.DeedProcessor.Host.Services
{
    public class FileMonitorOptions
    {
        public string IncomingFolder { get; set; } = "incoming";
        public string StagingFolder { get; set; } = "staging";
        public string ProcessingFolder { get; set; } = "processing";
        public string ProcessedFolder { get; set; } = "processed";
        public string FailedFolder { get; set; } = "failed";
        public int PollIntervalSeconds { get; set; } = 5;
        public int FileStableSeconds { get; set; } = 3;
        public int MaxDegreeOfParallelism { get; set; } = 2;
        public int MaxRetries { get; set; } = 3;
        public int RetryBaseDelayMs { get; set; } = 1000;
        public bool UseFileSystemWatcher { get; set; } = false;
        public string StateFolder { get; set; } = "state";
    }

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
        private readonly IMemoryCache _memoryCache;
        private readonly ILlmService? _llmService;

        public FileMonitorBackgroundService(
            ILogger<FileMonitorBackgroundService> logger,
            IOcrService ocrService,
            IOptions<FileMonitorOptions> options,
            IServiceProvider sp,
            IMemoryCache memoryCache,
            ILlmService? llmService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _llmService = llmService;

            try
            {
                var cfg = sp.GetService(typeof(ServiceBusClient)) as ServiceBusClient;
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
                                try
                                {
                                    await ProcessFileAsync(path, linkedCts.Token);
                                    await args.CompleteMessageAsync(args.Message, linkedCts.Token);
                                }
                                finally
                                {
                                    _workerSemaphore.Release();
                                }
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

            var workers = new List<Task>();
            for (int i = 0; i < _options.MaxDegreeOfParallelism; i++)
            {
                workers.Add(Task.Run(() => WorkerLoopAsync(linkedCts.Token), linkedCts.Token));
            }

            try
            {
                if (_options.UseFileSystemWatcher)
                {
                    using var watcher = CreateFileSystemWatcher(_options.IncomingFolder);
                    watcher.EnableRaisingEvents = true;

                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        await ScanAndEnqueueAsync(linkedCts.Token);
                        await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), linkedCts.Token);
                    }
                }
                else
                {
                    while (!linkedCts.Token.IsCancellationRequested)
                    {
                        await ScanAndEnqueueAsync(linkedCts.Token);
                        await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), linkedCts.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
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

            watcher.Created += async (_, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Changed += async (_, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Renamed += async (_, e) => await OnFileSystemEventAsync(e.FullPath);
            watcher.Error += (_, e) => _logger.LogWarning(e.GetException(), "FileSystemWatcher error");

            return watcher;
        }

        private Task OnFileSystemEventAsync(string path)
        {
            _fileLastSeen[path] = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        private async Task ScanAndEnqueueAsync(CancellationToken ct)
        {
            var incoming = new DirectoryInfo(_options.IncomingFolder);
            if (!incoming.Exists) return;

            var files = incoming.GetFiles().OrderBy(f => f.CreationTimeUtc).ToArray();
            var fileMap = files.ToDictionary(f => Path.GetFileName(f.Name), f => f, StringComparer.OrdinalIgnoreCase);
            var processedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fi in files)
            {
                if (processedFiles.Contains(fi.FullName)) continue;
                ct.ThrowIfCancellationRequested();

                try
                {
                    var fileName = Path.GetFileName(fi.Name);
                    var ext = Path.GetExtension(fileName);
                    var baseNoExt = Path.GetFileNameWithoutExtension(fileName);

                    string counterpartName;
                    bool thisIsMinusOne = baseNoExt.EndsWith("-1", StringComparison.Ordinal);
                    if (thisIsMinusOne)
                    {
                        var baseBase = baseNoExt.Substring(0, baseNoExt.Length - 2);
                        counterpartName = baseBase + ext;
                    }
                    else
                    {
                        counterpartName = baseNoExt + "-1" + ext;
                    }

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

                        var staging1 = GetUniquePath(Path.Combine(_options.StagingFolder, Path.GetFileName(fi.Name)));
                        var staging2 = GetUniquePath(Path.Combine(_options.StagingFolder, Path.GetFileName(counterpartFile.Name)));

                        try
                        {
                            File.Move(fi.FullName, staging1);
                            File.Move(counterpartFile.FullName, staging2);
                        }
                        catch (IOException)
                        {
                            continue;
                        }

                        var jobDescriptor = new { Files = new[] { staging1, staging2 } };
                        var jobPath = Path.Combine(_options.StagingFolder, GetUniqueFileName(Path.GetFileNameWithoutExtension(fileName), ".pair.json"));
                        File.WriteAllText(jobPath, JsonSerializer.Serialize(jobDescriptor), Encoding.UTF8);

                        if (_serviceBusClient != null && !string.IsNullOrWhiteSpace(_serviceBusQueueName))
                        {
                            try
                            {
                                var sender = _serviceBusClient.CreateSender(_serviceBusQueueName);
                                var msgBody = JsonSerializer.Serialize(new { Path = jobPath });
                                await sender.SendMessageAsync(new ServiceBusMessage(msgBody) { ContentType = "application/json" }, ct);
                            }
                            catch
                            {
                                await _channel.Writer.WriteAsync(jobPath, ct);
                            }
                        }
                        else
                        {
                            await _channel.Writer.WriteAsync(jobPath, ct);
                        }

                        processedFiles.Add(fi.FullName);
                        processedFiles.Add(counterpartFile.FullName);
                        continue;
                    }

                    var nowSingle = DateTime.UtcNow;
                    var lastSeenSingle = _fileLastSeen.TryGetValue(fi.FullName, out var tSingle) ? tSingle : fi.LastWriteTimeUtc;
                    var stableSingle = FileMonitorUtils.IsFileStable(fi.FullName, lastSeenSingle, _options.FileStableSeconds);
                    if (!stableSingle)
                    {
                        _fileLastSeen[fi.FullName] = nowSingle;
                        continue;
                    }

                    var uniqueStaging = GetUniquePath(Path.Combine(_options.StagingFolder, Path.GetFileName(fi.Name)));
                    try { File.Move(fi.FullName, uniqueStaging); }
                    catch (IOException) { continue; }

                    if (_serviceBusClient != null && !string.IsNullOrWhiteSpace(_serviceBusQueueName))
                    {
                        try
                        {
                            var sender = _serviceBusClient.CreateSender(_serviceBusQueueName);
                            var msgBody = JsonSerializer.Serialize(new { Path = uniqueStaging });
                            await sender.SendMessageAsync(new ServiceBusMessage(msgBody) { ContentType = "application/json" }, ct);
                        }
                        catch
                        {
                            await _channel.Writer.WriteAsync(uniqueStaging, ct);
                        }
                    }
                    else
                    {
                        await _channel.Writer.WriteAsync(uniqueStaging, ct);
                    }

                    processedFiles.Add(fi.FullName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while scanning incoming files");
                }
            }
        }

        private static string GetUniqueFileName(string baseName, string suffix) => baseName + "_" + Guid.NewGuid().ToString("N") + suffix;

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
                    try { await ProcessFileAsync(stagingPath, ct); }
                    catch (Exception ex) { _logger.LogError(ex, "Unhandled error processing file {File}", stagingPath); }
                    finally { _workerSemaphore.Release(); }
                }, ct);
            }
        }

        private async Task ProcessFileAsync(string stagingPath, CancellationToken ct)
        {
            var fileName = Path.GetFileName(stagingPath);
            var processingPath = Path.Combine(_options.ProcessingFolder, fileName);

            try
            {
                var uniqueProcessing = GetUniquePath(processingPath);
                File.Move(stagingPath, uniqueProcessing);
                processingPath = uniqueProcessing;

                if (processingPath.EndsWith(".pair.json", StringComparison.OrdinalIgnoreCase))
                {
                    var jobTxt = File.ReadAllText(processingPath);
                    try
                    {
                        var job = JsonSerializer.Deserialize<JsonElement>(jobTxt);
                        if (job.TryGetProperty("Files", out var filesElem) && filesElem.ValueKind == JsonValueKind.Array && filesElem.GetArrayLength() >= 2)
                        {
                            var path1 = filesElem[0].GetString();
                            var path2 = filesElem[1].GetString();
                            if (string.IsNullOrWhiteSpace(path1) || string.IsNullOrWhiteSpace(path2)) throw new Exception("Invalid pair job descriptor");

                            var descriptorDest = Path.Combine(_options.ProcessedFolder, Path.GetFileName(processingPath));
                            File.Move(processingPath, GetUniquePath(descriptorDest));

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

                var hash = await FileMonitorUtils.ComputeFileHashAsync(processingPath);
                if (_processedHashes.Contains(hash))
                {
                    MoveFileSafe(processingPath, _options.ProcessedFolder);
                    return;
                }

                int attempt = 0;
                Exception? lastEx = null;

                while (attempt <= _options.MaxRetries)
                {
                    attempt++;
                    try
                    {
                        var ocrResult = await _ocrService.ExtractTextFromFileAsync(processingPath, cacheKey: null);
                        if (ocrResult != null && ocrResult.Success)
                        {
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
                            File.WriteAllText(outPath, json, Encoding.UTF8);

                            _processedHashes.Add(hash);
                            SaveProcessedIndex();

                            MoveFileSafe(processingPath, _options.ProcessedFolder);

                            try
                            {
                                if (_llmService != null)
                                    await _llmService.InvokeAfterOcrAsync(ct);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "LLM service invocation failed after OCR for file {File}", processingPath);
                            }

                            return;
                        }

                        lastEx = new Exception(ocrResult?.ErrorMessage ?? "OCR returned unsuccessful result");
                    }
                    catch (Exception ex)
                    {
                        lastEx = ex;
                    }

                    if (attempt <= _options.MaxRetries)
                    {
                        var delay = _options.RetryBaseDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delay, ct);
                    }
                }

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
                if (!File.Exists(path1) || !File.Exists(path2))
                {
                    try { MoveFileSafe(path1, _options.FailedFolder); } catch { }
                    try { MoveFileSafe(path2, _options.FailedFolder); } catch { }
                    return;
                }

                var h1 = await FileMonitorUtils.ComputeFileHashAsync(path1);
                var h2 = await FileMonitorUtils.ComputeFileHashAsync(path2);
                var combinedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(h1 + h2)));

                if (_processedHashes.Contains(combinedHash))
                {
                    MoveFileSafe(path1, _options.ProcessedFolder);
                    MoveFileSafe(path2, _options.ProcessedFolder);
                    return;
                }

                var res1 = await _ocrService.ExtractTextFromFileAsync(path1, cacheKey: null);
                var res2 = await _ocrService.ExtractTextFromFileAsync(path2, cacheKey: null);

                if ((res1 == null || !res1.Success) && (res2 == null || !res2.Success))
                {
                    MoveFileSafe(path1, _options.FailedFolder);
                    MoveFileSafe(path2, _options.FailedFolder);
                    return;
                }

                var merged = new Dictionary<string, object>
                {
                    {
                        "Files", new[]
                        {
                            new { File = Path.GetFileName(path1), Engine = res1?.Engine, PlainText = res1?.PlainText, Markdown = res1?.Markdown, ProcessingTimeSeconds = res1?.ProcessingTime.TotalSeconds, Metadata = res1?.Metadata },
                            new { File = Path.GetFileName(path2), Engine = res2?.Engine, PlainText = res2?.PlainText, Markdown = res2?.Markdown, ProcessingTimeSeconds = res2?.ProcessingTime.TotalSeconds, Metadata = res2?.Metadata }
                        }
                    },
                    { "MergedPlainText", string.Join("\n\n---\n\n", new[] { res1?.PlainText ?? string.Empty, res2?.PlainText ?? string.Empty }) },
                    { "MergedMarkdown", string.Join("\n\n---\n\n", new[] { res1?.Markdown ?? string.Empty, res2?.Markdown ?? string.Empty }) },
                    { "MergedEngines", new[] { res1?.Engine, res2?.Engine } }
                };

                var json = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });

                var baseName = Path.GetFileNameWithoutExtension(path1) + "__" + Path.GetFileNameWithoutExtension(path2);
                var outPath = Path.Combine(_options.ProcessedFolder, baseName + ".merged.json");
                File.WriteAllText(outPath, json, Encoding.UTF8);

                _processedHashes.Add(combinedHash);
                SaveProcessedIndex();

                MoveFileSafe(path1, _options.ProcessedFolder);
                MoveFileSafe(path2, _options.ProcessedFolder);

                try
                {
                    _memoryCache.Set("last_merged_output", json, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
                    if (_llmService != null)
                        await _llmService.InvokeAfterOcrAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM service invocation failed after paired OCR");
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
            _internalCts.Cancel();
            await base.StopAsync(cancellationToken);
        }
    }
}
