using System;

namespace TextractProcessor.Configuration
{
    /// <summary>
    /// Configuration for OCR engine selection and settings
    /// </summary>
    public class OcrConfig
    {
        // Default to AWS Textract (keep Bedrock as an accepted alias)
        public string Engine { get; set; } = "Textract";
        public string AsposeLicensePath { get; set; } = "Aspose.Total.NET.lic";
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationDays { get; set; } = 30;

        // Fallback to Textract when Aspose returns insufficient results
        public bool EnableTextractFallback { get; set; } = true;
        // Minimum characters required from Aspose result before considering fallback
        public int TextractFallbackThresholdChars { get; set; } = 50;

        /// <summary>
        /// Load configuration from environment variables with fallback to defaults
        /// </summary>
        public static OcrConfig Load()
        {
            return new OcrConfig
            {
                Engine = Environment.GetEnvironmentVariable("OCR_ENGINE") ?? "Textract",
                AsposeLicensePath = Environment.GetEnvironmentVariable("ASPOSE_LICENSE_PATH") ?? "Aspose.Total.NET.lic",
                EnableCaching = bool.TryParse(Environment.GetEnvironmentVariable("OCR_ENABLE_CACHING"), out var cache) ? cache : true,
                CacheDurationDays = int.TryParse(Environment.GetEnvironmentVariable("OCR_CACHE_DURATION_DAYS"), out var days) ? days : 30,
                EnableTextractFallback = bool.TryParse(Environment.GetEnvironmentVariable("OCR_TEXTRACT_FALLBACK_ENABLED"), out var fb) ? fb : true,
                TextractFallbackThresholdChars = int.TryParse(Environment.GetEnvironmentVariable("OCR_TEXTRACT_FALLBACK_THRESHOLD_CHARS"), out var t) ? t : 50
            };
        }

        /// <summary>
        /// Check if AWS Textract (or Bedrock alias) is enabled
        /// </summary>
        public bool IsTextractEnabled => Engine.Equals("Textract", StringComparison.OrdinalIgnoreCase)
                                     || Engine.Equals("Bedrock", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Check if Aspose OCR is enabled
        /// </summary>
        public bool IsAsposeEnabled => Engine.Equals("Aspose", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Validate configuration
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Engine))
            {
                throw new InvalidOperationException("OCR_ENGINE must be configured (Textract, Bedrock, or Aspose)");
            }

            if (!IsTextractEnabled && !IsAsposeEnabled)
            {
                throw new InvalidOperationException($"Unknown OCR engine: {Engine}. Supported values: Textract, Bedrock, Aspose");
            }

            if (IsAsposeEnabled && string.IsNullOrWhiteSpace(AsposeLicensePath))
            {
                Console.WriteLine("? Warning: Aspose license path not configured. Aspose will run in evaluation mode.");
            }

            if (CacheDurationDays < 0)
            {
                throw new InvalidOperationException("OCR_CACHE_DURATION_DAYS must be >=0");
            }

            if (TextractFallbackThresholdChars < 0)
            {
                throw new InvalidOperationException("OCR_TEXTRACT_FALLBACK_THRESHOLD_CHARS must be >=0");
            }
        }
    }
}
