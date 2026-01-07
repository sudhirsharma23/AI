using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.ApplicationInsights.Extensibility;
using Prometheus;
using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Oasis.DeedProcessor.BusinessEntities.Configuration;
using Oasis.DeedProcessor.Interface.Ocr;
using Oasis.DeedProcessor.Host.Ocr;
using Oasis.DeedProcessor.Host.Services;

namespace Oasis.DeedProcessor
{
    internal static class Program
    {
        private const string OutputDirectory = "OutputFiles";

        private static async Task Main(string[] args)
        {
            Directory.CreateDirectory(OutputDirectory);
            Console.WriteLine($"Output directory created/verified: {Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory)}");

            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            builder.Services.Configure<FileMonitorOptions>(builder.Configuration.GetSection("FileMonitor"));

            var aiKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY") ?? builder.Configuration["ApplicationInsights:InstrumentationKey"];
            if (!string.IsNullOrWhiteSpace(aiKey))
            {
                builder.Services.AddSingleton<TelemetryConfiguration>(sp => TelemetryConfiguration.CreateDefault());
                builder.Services.AddApplicationInsightsTelemetryWorkerService(options => { options.InstrumentationKey = aiKey; });
            }

            builder.Services.AddSingleton<ICollectorRegistry>(_ => Metrics.DefaultRegistry);

            var ocrConfig = OcrConfig.Load();
            ocrConfig.Validate();
            builder.Services.AddSingleton(ocrConfig);

            AzureAIConfig? azureConfig = null;
            if (ocrConfig.IsAzureEnabled)
            {
                azureConfig = AzureAIConfig.Load();
                azureConfig.Validate();
                builder.Services.AddSingleton(azureConfig);
            }

            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient();

            var sbConn = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTION") ?? builder.Configuration["ServiceBus:ConnectionString"];
            if (!string.IsNullOrWhiteSpace(sbConn))
            {
                builder.Services.AddSingleton(new ServiceBusClient(sbConn));
            }

            builder.Services.AddSingleton<OcrServiceFactory>(sp =>
            {
                var cache = sp.GetRequiredService<IMemoryCache>();
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
                var http = httpFactory.CreateClient();
                return new OcrServiceFactory(ocrConfig, azureConfig, cache, http);
            });

            builder.Services.AddSingleton<IOcrService>(sp =>
            {
                var factory = sp.GetRequiredService<OcrServiceFactory>();
                return factory.CreateOcrService();
            });

            builder.Services.AddHostedService<FileMonitorBackgroundService>();

            var app = builder.Build();

            app.MapGet("/health/ready", () => "OK");
            app.MapGet("/health/live", () => "OK");
            app.MapMetrics();

            app.MapPost("/api/process", async (HttpRequest req, IServiceProvider sp) =>
            {
                try
                {
                    var json = await new StreamReader(req.Body).ReadToEndAsync();
                    if (string.IsNullOrWhiteSpace(json)) return Results.BadRequest("Empty body");
                    var doc = JsonSerializer.Deserialize<JsonElement>(json);
                    string incomingFolder = sp.GetService<IOptions<FileMonitorOptions>>()?.Value.IncomingFolder ?? "incoming";
                    Directory.CreateDirectory(incomingFolder);
                    string jobId = Guid.NewGuid().ToString();

                    if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("imageUrl", out var urlProp) && !string.IsNullOrWhiteSpace(urlProp.GetString()))
                    {
                        var url = urlProp.GetString()!;
                        var ext = Path.GetExtension(new Uri(url).LocalPath);
                        var fileName = $"job_{jobId}{(string.IsNullOrEmpty(ext) ? ".dat" : ext)}";
                        var dest = Path.Combine(incomingFolder, fileName);
                        using var http = new HttpClient();
                        var data = await http.GetByteArrayAsync(url);
                        await File.WriteAllBytesAsync(dest, data);
                        return Results.Accepted($"/api/status/{jobId}", new { jobId, path = dest });
                    }

                    if (doc.ValueKind == JsonValueKind.Object && doc.TryGetProperty("localPath", out var lp) && !string.IsNullOrWhiteSpace(lp.GetString()))
                    {
                        var src = lp.GetString()!;
                        if (!File.Exists(src)) return Results.BadRequest("localPath does not exist");
                        var dest = Path.Combine(incomingFolder, Path.GetFileName(src));
                        File.Copy(src, dest, overwrite: true);
                        return Results.Accepted($"/api/status/{jobId}", new { jobId, path = dest });
                    }

                    return Results.BadRequest("Provide imageUrl or localPath in body");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            app.MapGet("/api/status/{hash}", (string hash) =>
            {
                try
                {
                    var opts = app.Services.GetService<IOptions<FileMonitorOptions>>()?.Value;
                    var state = opts?.StateFolder ?? "state";
                    var idx = Path.Combine(state, "processed_hashes.json");
                    if (!File.Exists(idx)) return Results.NotFound(new { hash, status = "unknown" });
                    var arr = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(idx)) ?? new List<string>();
                    var found = arr.Contains(hash, StringComparer.OrdinalIgnoreCase);
                    return Results.Ok(new { hash, processed = found });
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            await app.RunAsync();
        }
    }
}

