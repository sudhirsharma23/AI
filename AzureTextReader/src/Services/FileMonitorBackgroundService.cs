using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using AzureTextReader.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
 public int PollIntervalSeconds { get; set; } =5;
 public int FileStableSeconds { get; set; } =3; // consider file stable if size unchanged for this many seconds
 public int MaxDegreeOfParallelism { get; set; } =2;
 public int MaxRetries { get; set; } =3;
 public int RetryBaseDelayMs { get; set; } =1000;
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

 public FileMonitorBackgroundService(
 ILogger<FileMonitorBackgroundService> logger,
 IOcrService ocrService,
 IOptions<FileMonitorOptions> options)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 _ocrService = ocrService ?? throw new ArgumentNullException(nameof(ocrService));
 _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

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

 // Start worker consumers
 var workers = new List<Task>();
 for (int i =0; i < _options.MaxDegreeOfParallelism; i++)
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

 var files = incoming.GetFiles().OrderBy(f => f.CreationTimeUtc).ToArray();
 foreach (var fi in files)
 {
 ct.ThrowIfCancellationRequested();

 try
 {
 // ensure file is stable — size not changing for FileStableSeconds
 var now = DateTime.UtcNow;
 var lastSeen = _fileLastSeen.TryGetValue(fi.FullName, out var t) ? t : fi.LastWriteTimeUtc;
 var stable = (now - lastSeen).TotalSeconds >= _options.FileStableSeconds;

 if (!stable)
 {
 // update last seen and skip
 _fileLastSeen[fi.FullName] = now;
 continue;
 }

 // attempt to move to staging (atomic rename)
 var stagingPath = Path.Combine(_options.StagingFolder, Path.GetFileName(fi.Name));
 var uniqueStaging = GetUniquePath(stagingPath);

 try
 {
 File.Move(fi.FullName, uniqueStaging);
 }
 catch (IOException)
 {
 // file may be in use; skip this round
 _logger.LogDebug("File in use or moved by another process: {File}", fi.FullName);
 continue;
 }

 // enqueue staging path
 await _channel.Writer.WriteAsync(uniqueStaging, ct);
 _logger.LogInformation("Enqueued file for processing: {File}", uniqueStaging);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Error while scanning incoming files");
 }
 }
 }

 private static string GetUniquePath(string path)
 {
 var dir = Path.GetDirectoryName(path) ?? string.Empty;
 var name = Path.GetFileNameWithoutExtension(path);
 var ext = Path.GetExtension(path);
 var candidate = path;
 int i =0;
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

 // compute hash for idempotency
 var hash = await ComputeFileHashAsync(processingPath);
 if (_processedHashes.Contains(hash))
 {
 _logger.LogInformation("File already processed (hash match). Moving to processed: {File}", processingPath);
 MoveFileSafe(processingPath, _options.ProcessedFolder);
 return;
 }

 // attempt OCR with retries
 int attempt =0;
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
 var delay = _options.RetryBaseDelayMs * (int)Math.Pow(2, attempt -1);
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

 private static async Task<string> ComputeFileHashAsync(string path)
 {
 using var stream = File.OpenRead(path);
 using var sha = SHA256.Create();
 var hash = await sha.ComputeHashAsync(stream);
 return Convert.ToHexString(hash);
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
