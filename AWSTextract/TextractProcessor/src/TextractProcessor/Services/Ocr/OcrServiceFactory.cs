using Amazon.S3;
using Amazon.Textract;
using Microsoft.Extensions.Caching.Memory;
using TextractProcessor.Configuration;
using TextractProcessor.Services;

namespace TextractProcessor.Services.Ocr
{
    /// <summary>
    /// Factory to create the appropriate OCR service based on configuration
    /// </summary>
    public class OcrServiceFactory
    {
        private readonly OcrConfig _ocrConfig;
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonTextract _textractClient;
        private readonly IMemoryCache _cache;
        private readonly TextractCacheService _textractCache;
        private readonly string _textractRoleArn;
        private readonly string _snsTopicArn;

        public OcrServiceFactory(
            OcrConfig ocrConfig,
 IAmazonS3 s3Client,
        IAmazonTextract textractClient,
 IMemoryCache cache,
            TextractCacheService textractCache,
 string textractRoleArn,
            string snsTopicArn)
        {
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
            _s3Client = s3Client;
            _textractClient = textractClient;
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _textractCache = textractCache;
            _textractRoleArn = textractRoleArn;
            _snsTopicArn = snsTopicArn;
        }

        /// <summary>
        /// Create OCR service based on configuration
        /// </summary>
        public IOcrService CreateOcrService()
        {
            if (_ocrConfig.IsTextractEnabled)
            {
                if (_s3Client == null || _textractClient == null)
                {
                    throw new InvalidOperationException("Textract OCR is enabled but AWS clients are not provided");
                }

                Console.WriteLine($"✓ Creating AWS Textract OCR Service");
                return new TextractOcrService(
        _s3Client,
 _textractClient,
          _cache,
          _ocrConfig,
    _textractCache,
   _textractRoleArn,
           _snsTopicArn);
            }
            else if (_ocrConfig.IsAsposeEnabled)
            {
                Console.WriteLine($"✓ Creating Aspose.Total OCR Service");
                return new AsposeOcrService(_cache, _ocrConfig);
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
            if (engineType.Equals("Textract", StringComparison.OrdinalIgnoreCase))
            {
                if (_s3Client == null || _textractClient == null)
                {
                    throw new InvalidOperationException("AWS clients are not provided");
                }
                return new TextractOcrService(
                _s3Client,
                   _textractClient,
             _cache,
                  _ocrConfig,
               _textractCache,
                      _textractRoleArn,
                       _snsTopicArn);
            }
            else if (engineType.Equals("Aspose", StringComparison.OrdinalIgnoreCase))
            {
                return new AsposeOcrService(_cache, _ocrConfig);
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
