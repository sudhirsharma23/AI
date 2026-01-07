using Microsoft.Extensions.Caching.Memory;
using Oasis.DeedProcessor.BusinessEntities.Configuration;
using Oasis.DeedProcessor.Interface.Ocr;
using Oasis.DeedProcessor.ServiceAgent.Aspose.Ocr;
using Oasis.DeedProcessor.ServiceAgent.Azure.Ocr;

namespace Oasis.DeedProcessor.Host.Ocr
{
    public class OcrServiceFactory
    {
        private readonly OcrConfig _ocrConfig;
        private readonly AzureAIConfig? _azureConfig;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;

        public OcrServiceFactory(
            OcrConfig ocrConfig,
            AzureAIConfig? azureConfig,
            IMemoryCache cache,
            HttpClient httpClient)
        {
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
            _azureConfig = azureConfig;
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public IOcrService CreateOcrService()
        {
            if (_ocrConfig.IsAzureEnabled)
            {
                if (_azureConfig == null)
                    throw new InvalidOperationException("Azure OCR is enabled but AzureAIConfig is not provided");

                return new AzureOcrService(_azureConfig, _ocrConfig, _cache, _httpClient);
            }

            if (_ocrConfig.IsAsposeEnabled)
                return new AsposeOcrService(_ocrConfig, _cache, _httpClient);

            throw new InvalidOperationException($"Unknown OCR engine: {_ocrConfig.Engine}");
        }

        public IOcrService CreateOcrService(string engineType)
        {
            if (engineType.Equals("Azure", StringComparison.OrdinalIgnoreCase))
            {
                if (_azureConfig == null)
                    throw new InvalidOperationException("AzureAIConfig is not provided");
                return new AzureOcrService(_azureConfig, _ocrConfig, _cache, _httpClient);
            }

            if (engineType.Equals("Aspose", StringComparison.OrdinalIgnoreCase))
                return new AsposeOcrService(_ocrConfig, _cache, _httpClient);

            throw new ArgumentException($"Unknown OCR engine type: {engineType}");
        }

        public string GetCurrentEngineName() => _ocrConfig.Engine;
    }
}
