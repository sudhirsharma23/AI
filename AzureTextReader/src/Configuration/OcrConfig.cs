using Microsoft.Extensions.Configuration;

namespace AzureTextReader.Configuration
{
    /// <summary>
    /// OCR Engine configuration - choose between Azure Document Analyzer or Aspose OCR
    /// </summary>
    public class OcrConfig
    {
        /// <summary>
        /// OCR Engine to use: "Azure" or "Aspose"
        /// </summary>
        public string Engine { get; set; } = "Azure";

        /// <summary>
        /// Aspose license file path (relative to application directory)
        /// </summary>
        public string AsposeLicensePath { get; set; } = "Aspose.Total.NET.lic";

        /// <summary>
        /// Enable caching for OCR results
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// Cache duration in days for OCR results
        /// </summary>
        public int CacheDurationDays { get; set; } = 30;

        /// <summary>
        /// Loads OCR configuration from environment variables or appsettings
        /// Priority: Environment Variables > User Secrets > appsettings.json
        /// </summary>
        public static OcrConfig Load()
        {
            // Try environment variable first
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

            // Fall back to configuration file
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

        /// <summary>
        /// Validates the OCR configuration
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Engine))
            {
                throw new ArgumentException("OCR Engine must be specified (Azure or Aspose)");
            }

            var validEngines = new[] { "Azure", "Aspose" };
            if (!validEngines.Contains(Engine, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid OCR Engine '{Engine}'. Must be one of: {string.Join(", ", validEngines)}");
            }

            if (Engine.Equals("Aspose", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(AsposeLicensePath))
                {
                    throw new ArgumentException("Aspose license path is required when using Aspose OCR");
                }

                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AsposeLicensePath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException($"Aspose license file not found at: {fullPath}");
                }
            }

            Console.WriteLine($"? OCR Configuration validated: Engine={Engine}");
        }

        /// <summary>
        /// Check if Azure OCR is enabled
        /// </summary>
        public bool IsAzureEnabled => Engine.Equals("Azure", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Check if Aspose OCR is enabled
        /// </summary>
        public bool IsAsposeEnabled => Engine.Equals("Aspose", StringComparison.OrdinalIgnoreCase);
    }
}
