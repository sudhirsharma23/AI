using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace Oasis.DeedProcessor.BusinessEntities.Configuration
{
    public class OcrConfig
    {
        public string Engine { get; set; } = "Azure";
        public string AsposeLicensePath { get; set; } = "Aspose.Total.NET.lic";
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationDays { get; set; } = 30;

        public static OcrConfig Load()
        {
            var engine = Environment.GetEnvironmentVariable("OCR_ENGINE");

            if (!string.IsNullOrEmpty(engine))
            {
                Console.WriteLine($"? Loaded OCR Engine from environment variable: {engine}");
                return new OcrConfig
                {
                    Engine = engine,
                    AsposeLicensePath = Environment.GetEnvironmentVariable("ASPOSE_LICENSE_PATH") ?? "Aspose.Total.NET.lic",
                    EnableCaching = bool.TryParse(Environment.GetEnvironmentVariable("OCR_ENABLE_CACHING"), out var cache) ? cache : true,
                    CacheDurationDays = int.TryParse(Environment.GetEnvironmentVariable("OCR_CACHE_DURATION_DAYS"), out var days) ? days : 30,
                };
            }

            var engineFromEnv = Environment.GetEnvironmentVariable("OCR_ENGINE") ?? "default";
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{engineFromEnv}.json", optional: true, reloadOnChange: true)
                .AddUserSecrets<OcrConfig>(optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            var config = configuration.GetSection("Ocr").Get<OcrConfig>() ?? new OcrConfig();

            Console.WriteLine($"? Loaded OCR configuration: Engine={config.Engine}, Caching={config.EnableCaching}");
            return config;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Engine))
                throw new ArgumentException("OCR Engine must be specified (Azure or Aspose)");

            var validEngines = new[] { "Azure", "Aspose" };
            if (!validEngines.Contains(Engine, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid OCR Engine '{Engine}'. Must be one of: {string.Join(", ", validEngines)}");

            if (Engine.Equals("Aspose", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(AsposeLicensePath))
                    throw new ArgumentException("Aspose license path is required when using Aspose OCR");

                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AsposeLicensePath);
                if (!File.Exists(fullPath))
                    throw new FileNotFoundException($"Aspose license file not found at: {fullPath}");
            }

            Console.WriteLine($"? OCR Configuration validated: Engine={Engine}");
        }

        public bool IsAzureEnabled => Engine.Equals("Azure", StringComparison.OrdinalIgnoreCase);
        public bool IsAsposeEnabled => Engine.Equals("Aspose", StringComparison.OrdinalIgnoreCase);
    }
}
