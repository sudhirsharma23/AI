using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextractProcessor.Models;
using Amazon.Lambda.Core;

namespace TextractProcessor.Services
{
    public class BedrockService
    {
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private readonly IMemoryCache _cache;
        private readonly BedrockModelConfig _modelConfig;
        private const int CACHE_DURATION_MINUTES = 60;

        public BedrockService(
            IAmazonBedrockRuntime bedrockClient,
            IMemoryCache cache,
     BedrockModelConfig modelConfig = null)
        {
            _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _modelConfig = modelConfig ?? BedrockModelConfig.TitanTextExpress;
        }

        public async Task<(string response, int inputTokens, int outputTokens)> ProcessTextractResults(
            SimplifiedTextractResponse textractResults,
            string targetSchema,
       ILambdaContext context = null)
        {
            try
            {
                if (textractResults == null) throw new ArgumentNullException(nameof(textractResults));
                if (string.IsNullOrWhiteSpace(targetSchema)) throw new ArgumentException("Target schema cannot be empty", nameof(targetSchema));

                context?.Logger.LogLine("Creating prompt and checking cache...");
                var prompt = CreatePrompt(textractResults, targetSchema);
                var promptHash = CalculateHash(prompt);

                if (_cache.TryGetValue<CachedResponse>(promptHash, out var cachedResponse))
                {
                    context?.Logger.LogLine("Cache hit - returning cached response");
                    return (cachedResponse.Response, cachedResponse.InputTokens, cachedResponse.OutputTokens);
                }

                context?.Logger.LogLine($"Invoking Bedrock model {_modelConfig.ModelId}...");

                var request = CreateModelRequest(prompt);
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var requestJson = JsonSerializer.Serialize(request, jsonOptions);
                context?.Logger.LogLine($"Request body: {requestJson}");

                var modelRequest = new InvokeModelRequest
                {
                    ModelId = _modelConfig.ModelId,
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
                };

                context?.Logger.LogLine("Sending request to Bedrock...");
                var response = await _bedrockClient.InvokeModelAsync(modelRequest);

                context?.Logger.LogLine("Reading response from Bedrock...");
                using var reader = new StreamReader(response.Body);
                var responseJson = await reader.ReadToEndAsync();
                context?.Logger.LogLine($"Raw response: {responseJson}");

                var (outputText, inputTokens, outputTokens) = ParseResponse(responseJson);

                context?.Logger.LogLine($"Response received. Input tokens: {inputTokens}, Output tokens: {outputTokens}");

                var cacheEntry = new CachedResponse
                {
                    Response = outputText,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens
                };

                _cache.Set(promptHash, cacheEntry, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                return (outputText, inputTokens, outputTokens);
            }
            catch (Exception e)
            {
                context?.Logger.LogLine($"Error in Bedrock service: {e.Message}");
                throw;
            }
        }

        private object CreateModelRequest(string prompt)
        {
            return _modelConfig.RequestFormat switch
            {
                RequestFormat.Titan => new
                {
                    inputText = prompt,
                    textGenerationConfig = new
                    {
                        maxTokenCount = _modelConfig.MaxTokens,
                        temperature = _modelConfig.Temperature,
                        topP = _modelConfig.TopP,
                        stopSequences = new[] { "Human:", "Assistant:" }  // Updated stop sequences
                    }
                },
                RequestFormat.Claude => new
                {
                    anthropic_version = _modelConfig.Version,
                    max_tokens = _modelConfig.MaxTokens,
                    temperature = _modelConfig.Temperature,
                    messages = new[]
                        {
           new { role = "user", content = prompt }
        }
                },
                _ => throw new ArgumentException($"Unsupported model format: {_modelConfig.RequestFormat}")
            };
        }

        private (string outputText, int inputTokens, int outputTokens) ParseResponse(string responseJson)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var response = JsonSerializer.Deserialize<BedrockResponse>(responseJson, options);

            return _modelConfig.ResponseFormat switch
            {
                ResponseFormat.Titan when response?.Results?.Count > 0 => (
                         response.Results[0].OutputText,
                   response.Results[0].InputTextTokenCount,
                     response.Results[0].OutputTextTokenCount
               ),
                ResponseFormat.Claude => (
                          response?.Content ?? string.Empty,
                 response?.Usage?.InputTokens ?? EstimateTokenCount(responseJson),
                   response?.Usage?.OutputTokens ?? EstimateTokenCount(response?.Content)
                            ),
                _ => throw new InvalidOperationException($"Invalid response format or empty response: {responseJson}")
            };
        }

        private static string CreatePrompt(SimplifiedTextractResponse textractResults, string targetSchema)
        {
            var sourceData = JsonSerializer.Serialize(textractResults, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            return $@"Transform this extracted document data into a specific JSON format according to the provided schema.

Source Data:
{sourceData}

Target Schema:
{targetSchema}

Rules:
1. Create a JSON object that exactly follows the target schema structure
2. Convert data types appropriately:
   * numbers for numeric fields
   * strings for text fields
   * true/false for boolean fields
   * YYYY-MM-DD for dates
3. Use null for required fields that cannot be mapped
4. Return only the JSON object, no additional text

Generate a valid JSON object as the response.";
        }

        private static string CalculateHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private static int EstimateTokenCount(string text)
        {
            return text?.Length / 4 ?? 0;
        }
    }

    public class CachedResponse
    {
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}