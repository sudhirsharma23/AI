#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oasis.DeedProcessor.BusinessEntities.Ocr;
using Oasis.DeedProcessor.Host.Services;
using Oasis.DeedProcessor.Interface.Ocr;
using Xunit;
using static Xunit.Assert;

namespace Oasis.DeedProcessor.Tests
{
    public class IntegrationTests : IDisposable
    {
        private readonly string _root;
        private readonly string _incoming;
        private readonly string _processed;
        private IHost? _host;

        public IntegrationTests()
        {
            _root = Path.Combine(Path.GetTempPath(), "atr_integration_tests", Guid.NewGuid().ToString());
            _incoming = Path.Combine(_root, "incoming");
            _processed = Path.Combine(_root, "processed");
            Directory.CreateDirectory(_incoming);
            Directory.CreateDirectory(_processed);
        }

        [Fact(Timeout = 30000)]
        public async Task FileMonitor_ProcessesInputFile_EndToEnd()
        {
            // Arrange: create FileMonitorOptions pointing to temp folders
            var options = new FileMonitorOptions
            {
                IncomingFolder = _incoming,
                StagingFolder = Path.Combine(_root, "staging"),
                ProcessingFolder = Path.Combine(_root, "processing"),
                ProcessedFolder = _processed,
                FailedFolder = Path.Combine(_root, "failed"),
                StateFolder = Path.Combine(_root, "state"),
                PollIntervalSeconds = 1,
                FileStableSeconds = 0,
                MaxDegreeOfParallelism = 1,
                MaxRetries = 1,
                RetryBaseDelayMs = 200
            };

            Directory.CreateDirectory(options.StagingFolder);
            Directory.CreateDirectory(options.ProcessingFolder);
            Directory.CreateDirectory(options.FailedFolder);
            Directory.CreateDirectory(options.StateFolder);

            // Host with the FileMonitorBackgroundService, but using a fake IOcrService
            _host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning))
            .ConfigureServices((ctx, services) =>
            {
                services.AddMemoryCache();
                services.AddSingleton<IOptions<FileMonitorOptions>>(Options.Create(options));
                services.AddSingleton<IOcrService>(new FakeOcrService());
                services.AddHostedService<FileMonitorBackgroundService>();
            })
            .Build();

            await _host.StartAsync();
            
            try
            {
                // Act: write a test file into incoming
                var testFile = Path.Combine(_incoming, "test1.txt");
                await File.WriteAllTextAsync(testFile, "This is a test document.");

                // Wait for processed JSON to appear
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string? foundJson = null;
                while (sw.Elapsed < TimeSpan.FromSeconds(20))
                {
                    var jsonFiles = Directory.GetFiles(_processed, "*.json", SearchOption.TopDirectoryOnly);
                    if (jsonFiles.Length > 0)
                    {
                        foundJson = jsonFiles[0];
                        break;
                    }
                    await Task.Delay(500);
                }

                // Assert
                Assert.False(string.IsNullOrEmpty(foundJson), "Processed JSON was not produced in time");

                var content = await File.ReadAllTextAsync(foundJson);
                Assert.False(string.IsNullOrWhiteSpace(content));
                // verify it contains fields produced by FakeOcrService
                Assert.Contains("PlainText", content);
                Assert.Contains("Hello from fake OCR", content);
            }
            finally
            {
                if (_host != null)
                {
                    await _host.StopAsync();
                    _host.Dispose();
                }
            }
        }

        public void Dispose()
        {
            try { if (_host != null) { _host.StopAsync().GetAwaiter().GetResult(); _host.Dispose(); } } catch { }
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
        }

        private class FakeOcrService : IOcrService
        {
            public string GetEngineName() => "FakeOCR";

            public Task<OcrResult> ExtractTextAsync(string imageUrl, string? cacheKey = null)
            {
                return Task.FromResult(new OcrResult
                {
                    ImageUrl = imageUrl,
                    PlainText = "Hello from fake OCR",
                    Markdown = "# Hello from fake OCR",
                    Success = true,
                    Engine = GetEngineName(),
                    ProcessingTime = TimeSpan.FromSeconds(0.1)
                });
            }

            public Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string? cacheKey = null)
            {
                return Task.FromResult(new OcrResult
                {
                    ImageUrl = "bytes",
                    PlainText = "Hello from fake OCR",
                    Markdown = "# Hello from fake OCR",
                    Success = true,
                    Engine = GetEngineName(),
                    ProcessingTime = TimeSpan.FromSeconds(0.1)
                });
            }

            public Task<OcrResult> ExtractTextFromFileAsync(string filePath, string? cacheKey = null)
            {
                return Task.FromResult(new OcrResult
                {
                    ImageUrl = Path.GetFileName(filePath),
                    PlainText = "Hello from fake OCR",
                    Markdown = "# Hello from fake OCR",
                    Success = true,
                    Engine = GetEngineName(),
                    ProcessingTime = TimeSpan.FromSeconds(0.1)
                });
            }
        }
    }
}
