using AzureTextReader.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace AzureTextReader.Services.Ocr
{
    /// <summary>
    /// Factory to create the appropriate OCR service based on configuration
    /// </summary>
    public class OcrServiceFactory
    {
        private readonly OcrConfig _ocrConfig;
        private readonly AzureAIConfig _azureConfig;
  private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;

  public OcrServiceFactory(
        OcrConfig ocrConfig,
            AzureAIConfig azureConfig,
  IMemoryCache cache,
        HttpClient httpClient)
    {
   _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
  _azureConfig = azureConfig;
      _cache = cache ?? throw new ArgumentNullException(nameof(cache));
   _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
  /// Create OCR service based on configuration
 /// </summary>
     public IOcrService CreateOcrService()
{
       if (_ocrConfig.IsAzureEnabled)
            {
       if (_azureConfig == null)
      {
 throw new InvalidOperationException("Azure OCR is enabled but AzureAIConfig is not provided");
   }

    Console.WriteLine($"? Creating Azure Document Analyzer OCR Service");
    return new AzureOcrService(_azureConfig, _ocrConfig, _cache, _httpClient);
      }
         else if (_ocrConfig.IsAsposeEnabled)
 {
   Console.WriteLine($"? Creating Aspose.Total OCR Service");
       return new AsposeOcrService(_ocrConfig, _cache, _httpClient);
   }
else
       {
       throw new InvalidOperationException($"Unknown OCR engine: {_ocrConfig.Engine}");
     }
        }

        /// <summary>
        /// Create specific OCR service (useful for A/B testing or comparison)
  /// </summary>
        public IOcrService CreateOcrService(string engineType)
 {
            if (engineType.Equals("Azure", StringComparison.OrdinalIgnoreCase))
       {
      if (_azureConfig == null)
      {
    throw new InvalidOperationException("AzureAIConfig is not provided");
  }
      return new AzureOcrService(_azureConfig, _ocrConfig, _cache, _httpClient);
    }
            else if (engineType.Equals("Aspose", StringComparison.OrdinalIgnoreCase))
{
return new AsposeOcrService(_ocrConfig, _cache, _httpClient);
         }
else
{
       throw new ArgumentException($"Unknown OCR engine type: {engineType}");
            }
     }

   /// <summary>
/// Get current OCR engine name from configuration
 /// </summary>
        public string GetCurrentEngineName()
     {
   return _ocrConfig.Engine;
        }
    }
}
