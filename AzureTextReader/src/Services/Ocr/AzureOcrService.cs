using ImageTextExtractor.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ImageTextExtractor.Services.Ocr
{
    /// <summary>
    /// Azure Document Analyzer OCR Service
/// </summary>
    public class AzureOcrService : IOcrService
    {
        private readonly AzureAIConfig _azureConfig;
        private readonly OcrConfig _ocrConfig;
        private readonly IMemoryCache _cache;
        private readonly HttpClient _httpClient;

   public AzureOcrService(AzureAIConfig azureConfig, OcrConfig ocrConfig, IMemoryCache cache, HttpClient httpClient)
        {
        _azureConfig = azureConfig ?? throw new ArgumentNullException(nameof(azureConfig));
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
   _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

      // Configure HTTP client
            if (!_httpClient.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key"))
            {
         _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _azureConfig.SubscriptionKey);
            }
        }

        public string GetEngineName() => "Azure Document Analyzer";

    public async Task<OcrResult> ExtractTextAsync(string imageUrl, string cacheKey = null)
        {
          var startTime = DateTime.UtcNow;
       cacheKey ??= $"Azure_OCR_{ComputeHash(imageUrl)}";

     try
         {
             // Check cache first
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
         {
          Console.WriteLine($"? Azure OCR Cache HIT for: {imageUrl}");
         return cachedResult;
   }

       Console.WriteLine($"? Azure OCR processing: {imageUrl}");

          var endpoint = _azureConfig.Endpoint + "/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
           var markdown = await ExtractOcrFromImageAsync(endpoint, _azureConfig.SubscriptionKey, imageUrl);

      var result = new OcrResult
    {
              ImageUrl = imageUrl,
        Markdown = markdown,
   PlainText = markdown, // Azure returns markdown which can be used as plain text
          Success = !string.IsNullOrEmpty(markdown),
              Engine = GetEngineName(),
     ProcessingTime = DateTime.UtcNow - startTime,
          Metadata = new Dictionary<string, object>
     {
                 { "Endpoint", endpoint },
         { "ApiVersion", "2025-05-01-preview" }
  }
                };

         // Cache the result
   if (_ocrConfig.EnableCaching && result.Success)
                {
    var cacheOptions = new MemoryCacheEntryOptions
         {
    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_ocrConfig.CacheDurationDays),
             SlidingExpiration = TimeSpan.FromDays(Math.Min(7, _ocrConfig.CacheDurationDays))
      };
 _cache.Set(cacheKey, result, cacheOptions);
    Console.WriteLine($"? Cached Azure OCR result for: {imageUrl}");
       }

           return result;
          }
  catch (Exception ex)
            {
        Console.WriteLine($"? Azure OCR Error: {ex.Message}");
       return new OcrResult
           {
    ImageUrl = imageUrl,
               Success = false,
          ErrorMessage = ex.Message,
          Engine = GetEngineName(),
 ProcessingTime = DateTime.UtcNow - startTime
  };
            }
   }

        public async Task<OcrResult> ExtractTextFromFileAsync(string filePath, string cacheKey = null)
        {
            // For Azure, we need to upload to a URL or use local file upload API
            // This is a simplified implementation - you may need to implement file upload
            throw new NotImplementedException("Azure OCR from local file requires file upload implementation. Use ExtractTextAsync with a publicly accessible URL instead.");
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string cacheKey = null)
      {
          // Similar to file - would need to implement Azure's binary upload API
        throw new NotImplementedException("Azure OCR from bytes requires binary upload implementation. Use ExtractTextAsync with a publicly accessible URL instead.");
  }

        private async Task<string> ExtractOcrFromImageAsync(string endpoint, string subscriptionKey, string imageUrl)
    {
            var requestBody = $"{{\"url\":\"{imageUrl}\"}}";
   var content = new System.Net.Http.StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");

            var postResponse = await _httpClient.PostAsync(endpoint, content);
        postResponse.EnsureSuccessStatusCode();

            if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
            {
 throw new InvalidOperationException("Operation-Location header not found in Azure response");
            }

            var operationLocation = values.First();
            Console.WriteLine($"  Azure Operation-Location: {operationLocation}");

            ExtractionOperation? extractionOperation = null;
  int retryCount = 0;
const int maxRetries = 60;

            do
            {
  var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
     getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
        
      var getResponse = await _httpClient.SendAsync(getRequest);
           getResponse.EnsureSuccessStatusCode();
            
                var result = await getResponse.Content.ReadAsStringAsync();
  extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);

         if (extractionOperation == null)
    {
            throw new InvalidOperationException("Failed to parse Azure extraction operation");
         }

         if (extractionOperation.status != "Succeeded")
        {
   if (retryCount >= maxRetries)
                    {
           throw new TimeoutException($"Azure OCR operation did not complete within {maxRetries * 3} seconds");
     }

  Console.WriteLine($"  Status: {extractionOperation.status}, waiting... (attempt {retryCount + 1}/{maxRetries})");
           await Task.Delay(3000);
         retryCount++;
  }

        } while (extractionOperation?.status != "Succeeded");

          if (extractionOperation?.result?.contents != null && extractionOperation.result.contents.Count > 0)
 {
         return extractionOperation.result.contents[0].markdown ?? string.Empty;
       }

         return string.Empty;
        }

      private static string ComputeHash(string input)
     {
    using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
         var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
 }

      // Azure response models
        private class ExtractionOperation
        {
            public string? id { get; set; }
            public string? status { get; set; }
 public ExtractionResult? result { get; set; }
 }

        private class ExtractionResult
        {
       public List<ContentResult>? contents { get; set; }
   }

        private class ContentResult
        {
      public string? markdown { get; set; }
         public string? kind { get; set; }
        }
    }
}
