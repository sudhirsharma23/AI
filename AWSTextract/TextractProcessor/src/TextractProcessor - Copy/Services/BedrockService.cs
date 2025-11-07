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
    /// <summary>
    /// Service for processing Textract results through Amazon Bedrock's language models
    /// </summary>
    public class BedrockService
    {
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private readonly IMemoryCache _cache;

        // Using Claude 3 Haiku model
        private const string MODEL_ID = "anthropic.claude-3-haiku-20240307";
        private const int CACHE_DURATION_MINUTES = 60;
        private const int MAX_TOKENS = 4000;
        private const float TEMPERATURE = 0.7f;

        public BedrockService(IAmazonBedrockRuntime bedrockClient, IMemoryCache cache)
        {
            _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Processes Textract results and maps them to the target schema using Bedrock's AI model
        /// </summary>
        /// <param name="textractResults">The results from Textract processing</param>
        /// <param name="targetSchema">The target schema to map the results to</param>
        /// <returns>Tuple containing the response, input token count, and output token count</returns>
        public async Task<(string response, int inputTokens, int outputTokens)> ProcessTextractResults(
            TextractResponse textractResults,
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

                // Try to get from cache
                if (_cache.TryGetValue<CachedResponse>(promptHash, out var cachedResponse))
                {
                    context?.Logger.LogLine("Cache hit - returning cached response");
                    return (cachedResponse.Response, cachedResponse.InputTokens, cachedResponse.OutputTokens);
                }

                context?.Logger.LogLine($"Invoking Bedrock model {MODEL_ID}...");

                // Claude-specific request format
                var claudeRequest = new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = MAX_TOKENS,
                    temperature = TEMPERATURE,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var requestJson = JsonSerializer.Serialize(claudeRequest, jsonOptions);
                context?.Logger.LogLine($"Request body: {requestJson}");

                var request = new InvokeModelRequest
                {
                    ModelId = MODEL_ID,
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
                };

                context?.Logger.LogLine("Sending request to Bedrock...");
                var response = await _bedrockClient.InvokeModelAsync(request);

                context?.Logger.LogLine("Reading response from Bedrock...");
                using var reader = new StreamReader(response.Body);
                var responseJson = await reader.ReadToEndAsync();
                context?.Logger.LogLine($"Raw response: {responseJson}");

                var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, jsonOptions);
                if (string.IsNullOrEmpty(claudeResponse?.Content))
                {
                    throw new InvalidOperationException($"Invalid Bedrock response: {responseJson}");
                }

                // Claude provides usage information in the response
                var inputTokens = claudeResponse.Usage?.InputTokens ?? EstimateTokenCount(prompt);
                var outputTokens = claudeResponse.Usage?.OutputTokens ?? EstimateTokenCount(claudeResponse.Content);

                context?.Logger.LogLine($"Response received. Input tokens: {inputTokens}, Output tokens: {outputTokens}");

                var cacheEntry = new CachedResponse
                {
                    Response = claudeResponse.Content,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens
                };

                _cache.Set(promptHash, cacheEntry, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                return (claudeResponse.Content, inputTokens, outputTokens);
            }
            catch (AmazonBedrockRuntimeException e)
            {
                context?.Logger.LogLine($"Bedrock API Error: {e.Message}");
                context?.Logger.LogLine($"Error Type: {e.ErrorType}, Error Code: {e.ErrorCode}");
                throw new ApplicationException($"Bedrock service error: {e.Message}", e);
            }
            catch (Exception e)
            {
                context?.Logger.LogLine($"Unexpected error in Bedrock service: {e.Message}");
                context?.Logger.LogLine($"Stack trace: {e.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Creates the prompt for the AI model
        /// </summary>
        private static string CreatePrompt(TextractResponse textractResults, string targetSchema)
        {
            return $@"You are a JSON transformation expert. Transform the provided Textract extraction data into the specified target schema format.

SOURCE DATA:
```json
{JsonSerializer.Serialize(textractResults, new JsonSerializerOptions { WriteIndented = true })}
```

TARGET SCHEMA:
```json
{targetSchema}
```

Requirements:
1. Generate a valid JSON object that strictly follows the target schema structure
2. Map values from the Textract results to their corresponding fields
3. Ensure proper data type conversion:
   - Use numbers for numeric fields
   - Use strings for text fields
   - Use true/false for boolean fields
   - Use YYYY-MM-DD format for dates
4. Use null for required fields that cannot be confidently mapped

Return only the JSON object without any additional text or explanations.";
        }

        /// <summary>
        /// Creates a hash of the input string for caching purposes
        /// </summary>
        private static string CalculateHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Estimates the token count for a given text
        /// </summary>
        private static int EstimateTokenCount(string text)
        {
            // Rough estimation: GPT tokens are roughly 4 characters
            return (int)(text.Length / 4);
        }
    }

    public class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; } = string.Empty;

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    /// <summary>
    /// Represents a cached response from the model
    /// </summary>
    public class CachedResponse
    {
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
    }
}