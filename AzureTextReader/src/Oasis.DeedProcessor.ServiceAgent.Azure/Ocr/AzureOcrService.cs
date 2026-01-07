using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Oasis.DeedProcessor.BusinessEntities.Configuration;
using Oasis.DeedProcessor.BusinessEntities.Models;
using Oasis.DeedProcessor.BusinessEntities.Ocr;
using Oasis.DeedProcessor.Interface.Ocr;
using Oasis.DeedProcessor.ServiceAgent.Azure.OpenAI;
using Oasis.DeedProcessor.ServiceAgent.Prompts;

namespace Oasis.DeedProcessor.ServiceAgent.Azure.Ocr
{
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

            if (!_httpClient.DefaultRequestHeaders.Contains("Ocp-Apim-Subscription-Key") && !string.IsNullOrWhiteSpace(_azureConfig.SubscriptionKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _azureConfig.SubscriptionKey);
            }
        }

        public string GetEngineName() => "Azure Document Analyzer";

        public async Task<OcrResult> ExtractTextAsync(string imageUrl, string? cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Azure_OCR_{ComputeHash(imageUrl)}";

            try
            {
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
                    return cachedResult;

                var endpoint = _azureConfig.Endpoint?.TrimEnd('/') + "/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
                var markdown = await ExtractOcrFromImageAsync(endpoint, _azureConfig.SubscriptionKey, imageUrl);

                var result = new OcrResult
                {
                    ImageUrl = imageUrl,
                    Markdown = markdown ?? string.Empty,
                    PlainText = markdown ?? string.Empty,
                    Success = !string.IsNullOrEmpty(markdown),
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        { "Endpoint", endpoint ?? string.Empty },
                        { "ApiVersion", "2025-05-01-preview" }
                    }
                };

                if (_ocrConfig.EnableCaching && result.Success)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_ocrConfig.CacheDurationDays),
                        SlidingExpiration = TimeSpan.FromDays(Math.Min(7, _ocrConfig.CacheDurationDays))
                    };
                    _cache.Set(cacheKey, result, cacheOptions);
                }

                return result;
            }
            catch (Exception ex)
            {
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

        public async Task<OcrResult> ExtractTextFromFileAsync(string filePath, string? cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Azure_OCR_File_{ComputeHash(filePath)}";

            try
            {
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cached))
                    return cached;

                if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

                var endpoint = _azureConfig.Endpoint?.TrimEnd('/') + "/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
                var markdown = await ExtractOcrFromFileAsync(endpoint, _azureConfig.SubscriptionKey, filePath);

                var result = new OcrResult
                {
                    ImageUrl = Path.GetFileName(filePath),
                    Markdown = markdown ?? string.Empty,
                    PlainText = markdown ?? string.Empty,
                    Success = !string.IsNullOrEmpty(markdown),
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        { "FileType", Path.GetExtension(filePath) },
                        { "FileSize", new FileInfo(filePath).Length }
                    }
                };

                if (_ocrConfig.EnableCaching && result.Success)
                {
                    var opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_ocrConfig.CacheDurationDays) };
                    _cache.Set(cacheKey, result, opts);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new OcrResult
                {
                    ImageUrl = Path.GetFileName(filePath),
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string? cacheKey = null)
        {
            var temp = Path.GetTempFileName() + ".bin";
            await File.WriteAllBytesAsync(temp, imageBytes);
            try
            {
                var r = await ExtractTextFromFileAsync(temp, cacheKey);
                r.ImageUrl = Path.GetFileName(temp);
                return r;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            }
        }

        public async Task<string> ExtractGrantDeedJsonV3Async(string ocrMarkdown, string? cacheKey = null)
        {
            ocrMarkdown ??= string.Empty;

            cacheKey ??= $"GrantDeed_V3_{ComputeHash(ocrMarkdown)}";
            if (_cache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                return cachedJson;

            var promptService = new PromptService(_cache);
            var systemPrompt = await promptService.LoadSystemPromptAsync("document_extraction", "v3");

            var messages = new List<object>
            {
                new { Role = "system", Content = systemPrompt },
                new { Role = "user", Content = ocrMarkdown }
            };

            var credential = new LocalApiKeyCredential(_azureConfig.SubscriptionKey);
            var azureClient = new LocalAzureOpenAIClient(new Uri(_azureConfig.Endpoint), credential, _httpClient);
            var chatClient = azureClient.GetChatClient(AzureOpenAIModelConfig.GPT4oMini.DeploymentName);

            var options = new LocalChatCompletionOptions
            {
                Temperature = 0.0f,
                MaxOutputTokenCount = 2048,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f
            };

            var completion = chatClient.CompleteChat(messages, options);
            var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

            _cache.Set(cacheKey, completionJson, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                SlidingExpiration = TimeSpan.FromHours(24)
            });

            return completionJson;
        }

        private async Task<string> ExtractOcrFromImageAsync(string endpoint, string subscriptionKey, string imageUrl)
        {
            var requestBody = JsonSerializer.Serialize(new { url = imageUrl });
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            var postResponse = await _httpClient.PostAsync(endpoint, content);
            postResponse.EnsureSuccessStatusCode();

            if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
                throw new InvalidOperationException("Operation-Location header not found in Azure response");

            var operationLocation = values.First();

            ExtractionOperation? extractionOperation = null;
            int retryCount = 0;
            const int maxRetries = 60;

            do
            {
                var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                if (!string.IsNullOrWhiteSpace(subscriptionKey))
                    getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var getResponse = await _httpClient.SendAsync(getRequest);
                getResponse.EnsureSuccessStatusCode();

                var result = await getResponse.Content.ReadAsStringAsync();
                extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);

                if (extractionOperation == null)
                    throw new InvalidOperationException("Failed to parse Azure extraction operation");

                if (extractionOperation.status != "Succeeded")
                {
                    if (retryCount >= maxRetries)
                        throw new TimeoutException($"Azure OCR operation did not complete within {maxRetries * 3} seconds");

                    await Task.Delay(3000);
                    retryCount++;
                }

            } while (extractionOperation.status != "Succeeded");

            if (extractionOperation.result?.contents != null && extractionOperation.result.contents.Count > 0)
                return extractionOperation.result.contents[0].markdown ?? string.Empty;

            return string.Empty;
        }

        private async Task<string> ExtractOcrFromFileAsync(string endpoint, string subscriptionKey, string filePath)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = ext switch
            {
                ".pdf" => "application/pdf",
                ".tif" or ".tiff" => "image/tiff",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };

            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            var postResponse = await _httpClient.PostAsync(endpoint, content);
            postResponse.EnsureSuccessStatusCode();

            if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
                throw new InvalidOperationException("Operation-Location header not found in Azure response");

            var operationLocation = values.First();

            ExtractionOperation? extractionOperation = null;
            int retryCount = 0;
            const int maxRetries = 60;

            do
            {
                var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                if (!string.IsNullOrWhiteSpace(subscriptionKey))
                    getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var getResponse = await _httpClient.SendAsync(getRequest);
                getResponse.EnsureSuccessStatusCode();

                var result = await getResponse.Content.ReadAsStringAsync();
                extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);

                if (extractionOperation == null)
                    throw new InvalidOperationException("Failed to parse Azure extraction operation");

                if (extractionOperation.status != "Succeeded")
                {
                    if (retryCount >= maxRetries)
                        throw new TimeoutException($"Azure OCR operation did not complete within {maxRetries * 3} seconds");

                    await Task.Delay(3000);
                    retryCount++;
                }

            } while (extractionOperation.status != "Succeeded");

            if (extractionOperation.result?.contents != null && extractionOperation.result.contents.Count > 0)
                return extractionOperation.result.contents[0].markdown ?? string.Empty;

            return string.Empty;
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

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
