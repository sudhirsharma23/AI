using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextractProcessor.Models;

namespace TextractProcessor.Services
{
    public class BedrockService
    {
        private readonly IAmazonBedrockRuntime _bedrockClient;
        private readonly IMemoryCache _cache;
        private readonly BedrockModelConfig _modelConfig;
        private const int CACHE_DURATION_MINUTES = 60;
        private const string PROMPT_CACHE_PREFIX = "prompt_";

        public BedrockService(IAmazonBedrockRuntime bedrockClient, IMemoryCache cache, BedrockModelConfig modelConfig = null)
        {
            _bedrockClient = bedrockClient ?? throw new ArgumentNullException(nameof(bedrockClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _modelConfig = modelConfig ?? BedrockModelConfig.TitanTextExpress;
        }

        private static class JsonExtractor
        {
            public static string ExtractFirstJsonObject(string text)
            {
                // Try to find records array first
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("records", out var records) &&
                        records.ValueKind == JsonValueKind.Array)
                    {
                        // Get the first record
                        var recordArray = records.EnumerateArray();
                        if (recordArray.Any())
                        {
                            var options = new JsonSerializerOptions { WriteIndented = true };
                            return JsonSerializer.Serialize(recordArray.First(), options);
                        }
                    }
                }
                catch
                {
                    // Fall through to regular extraction if JSON parsing fails
                }

                // Fallback to normal JSON extraction (object or array)
                return ExtractFirstJsonValue(text);
            }

            private static string ExtractFirstJsonValue(string text)
            {
                int objIndex = text.IndexOf('{');
                int arrIndex = text.IndexOf('[');

                int startIndex;
                char openChar;
                char closeChar;

                if (objIndex == -1 && arrIndex == -1) return null;
                if (objIndex == -1 || (arrIndex != -1 && arrIndex < objIndex))
                {
                    startIndex = arrIndex;
                    openChar = '['; closeChar = ']';
                }
                else
                {
                    startIndex = objIndex;
                    openChar = '{'; closeChar = '}';
                }

                return ExtractBalanced(text, startIndex, openChar, closeChar);
            }

            private static string ExtractBalanced(string text, int startIndex, char openChar, char closeChar)
            {
                int balance = 0;
                bool inQuotes = false;
                bool escaped = false;

                for (int i = startIndex; i < text.Length; i++)
                {
                    char c = text[i];

                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                        continue;
                    }

                    if (inQuotes) continue;

                    if (c == openChar)
                    {
                        balance++;
                    }
                    else if (c == closeChar)
                    {
                        balance--;
                        if (balance == 0)
                        {
                            return text.Substring(startIndex, i - startIndex + 1);
                        }
                    }
                }

                return null;
            }

            public static string NormalizeJson(string jsonText)
            {
                try
                {
                    // First, try to extract Converse-style content: output.message.content[0].text
                    try
                    {
                        using var root = JsonDocument.Parse(jsonText);
                        if (root.RootElement.TryGetProperty("output", out var output) &&
                            output.TryGetProperty("message", out var message) &&
                            message.TryGetProperty("content", out var contentArr) &&
                            contentArr.ValueKind == JsonValueKind.Array)
                        {
                            var first = contentArr.EnumerateArray().FirstOrDefault();
                            if (first.ValueKind == JsonValueKind.Object &&
                                first.TryGetProperty("text", out var textEl) &&
                                textEl.ValueKind == JsonValueKind.String)
                            {
                                var text = textEl.GetString();
                                if (!string.IsNullOrEmpty(text))
                                {
                                    text = StripCodeFences(text);
                                    var extracted = ExtractFirstJsonValue(text);
                                    if (!string.IsNullOrEmpty(extracted))
                                    {
                                        using (JsonDocument.Parse(extracted))
                                        {
                                            return extracted;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore and try other strategies
                    }

                    // Legacy content string at root
                    if (jsonText.Contains("\"content\""))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(jsonText);
                            if (doc.RootElement.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                            {
                                var content = contentProp.GetString();
                                if (!string.IsNullOrEmpty(content))
                                {
                                    var extractedJson = ExtractFirstJsonObject(content);
                                    if (!string.IsNullOrEmpty(extractedJson))
                                    {
                                        using (JsonDocument.Parse(extractedJson))
                                        {
                                            return extractedJson;
                                        }
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Fall through to normal processing if content extraction fails
                        }
                    }

                    // Try to parse as-is
                    using (JsonDocument.Parse(jsonText))
                    {
                        return jsonText;
                    }
                }
                catch
                {
                    // If parsing fails, try to extract JSON object/array from text
                    var extractedJson = ExtractFirstJsonObject(jsonText);
                    if (string.IsNullOrEmpty(extractedJson))
                    {
                        throw new JsonException($"Could not extract valid JSON from response: {jsonText}");
                    }

                    using (JsonDocument.Parse(extractedJson))
                    {
                        return extractedJson;
                    }
                }
            }

            public static string StripCodeFences(string text)
            {
                // Remove ```json ... ``` fences
                if (text.StartsWith("```"))
                {
                    // Trim leading fence
                    int firstNewLine = text.IndexOf('\n');
                    if (firstNewLine > 0)
                    {
                        text = text.Substring(firstNewLine + 1);
                    }
                    // Trim trailing fence
                    int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                    if (lastFence > 0)
                    {
                        text = text.Substring(0, lastFence);
                    }
                }
                return text.Trim();
            }
        }

        public async Task<(string response, int inputTokens, int outputTokens)> ProcessTextractResults(
         string sourceData,
            string systemPrompt,
   string userPrompt,
            ILambdaContext context = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceData)) throw new ArgumentException("Source data cannot be empty", nameof(sourceData));
                if (string.IsNullOrWhiteSpace(systemPrompt)) throw new ArgumentException("System prompt cannot be empty", nameof(systemPrompt));
                if (string.IsNullOrWhiteSpace(userPrompt)) throw new ArgumentException("User prompt cannot be empty", nameof(userPrompt));

                var requestId = context?.AwsRequestId ?? Guid.NewGuid().ToString();
                context?.Logger.LogLine($"[RequestId: {requestId}] Starting Bedrock processing with custom prompts");

                // Calculate cache key based on all inputs
                var cacheInput = $"{sourceData}|{systemPrompt}|{userPrompt}|{_modelConfig.ModelId}";
                var promptCacheKey = $"{PROMPT_CACHE_PREFIX}{CalculateHash(cacheInput)}";

                context?.Logger.LogLine($"[RequestId: {requestId}] Generated prompt cache key: {promptCacheKey}");

                // Try to get from prompt cache first
                if (_cache.TryGetValue<CachedResponse>(promptCacheKey, out var promptCachedResponse))
                {
                    context?.Logger.LogLine($"[RequestId: {requestId}] ✓ Prompt cache HIT - returning cached response");
                    return (promptCachedResponse.Response, promptCachedResponse.InputTokens, promptCachedResponse.OutputTokens);
                }

                context?.Logger.LogLine($"[RequestId: {requestId}] ✗ Prompt cache MISS");
                context?.Logger.LogLine($"[RequestId: {requestId}] Cache miss - processing with Bedrock");
                context?.Logger.LogLine($"[RequestId: {requestId}] Invoking Bedrock model {_modelConfig.ModelId}");

                // Create model-specific request with custom prompt
                var request = CreateModelRequestWithPrompt(systemPrompt, userPrompt);
                var requestOptions = GetRequestSerializerOptions();
                var requestJson = JsonSerializer.Serialize(request, requestOptions);
                context?.Logger.LogLine($"[RequestId: {requestId}] Request JSON: {requestJson}");

                var modelRequest = new InvokeModelRequest
                {
                    ModelId = _modelConfig.ModelId,
                    ContentType = "application/json",
                    Accept = "application/json",
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(requestJson))
                };

                context?.Logger.LogLine($"[RequestId: {requestId}] Sending request to Bedrock");
                var startTime = DateTime.UtcNow;
                var response = await _bedrockClient.InvokeModelAsync(modelRequest);
                var duration = DateTime.UtcNow - startTime;
                context?.Logger.LogLine($"[RequestId: {requestId}] Bedrock response received in {duration.TotalMilliseconds:F0}ms");

                // Read and parse response
                string responseJson;
                using (var ms = new MemoryStream())
                {
                    await response.Body.CopyToAsync(ms);
                    ms.Position = 0;
                    using var reader = new StreamReader(ms);
                    responseJson = await reader.ReadToEndAsync();
                }

                // First, try to extract completion text and save clean JSON derived from it
                var completionText = TryExtractCompletionText(responseJson);
                if (!string.IsNullOrWhiteSpace(completionText))
                {
                    await SaveCompletionArtifacts(completionText, requestId, context);
                }

                // Log full response to files (raw + normalized best-effort)
                await SaveResponseToFile(responseJson, requestId, context);

                var (outputText, inputTokens, outputTokens) = ParseResponse(responseJson);
                context?.Logger.LogLine($"[RequestId: {requestId}] Processing metrics:" +
                                        $"\n- Input tokens: {inputTokens}" +
                                        $"\n- Output tokens: {outputTokens}" +
                                        $"\n- Response size: {outputText.Length} chars");

                var cacheEntry = new CachedResponse
                {
                    Response = outputText,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens
                };

                // Cache response with prompt key
                _cache.Set(promptCacheKey, cacheEntry, TimeSpan.FromMinutes(CACHE_DURATION_MINUTES));

                context?.Logger.LogLine($"[RequestId: {requestId}] ✓ Response cached successfully");
                context?.Logger.LogLine($"[RequestId: {requestId}] - Prompt cache key: {promptCacheKey}");
                context?.Logger.LogLine($"[RequestId: {requestId}] - Cache duration: {CACHE_DURATION_MINUTES} minutes");
                context?.Logger.LogLine($"[RequestId: {requestId}] - Cache entry created at: {cacheEntry.CachedAt:yyyy-MM-dd HH:mm:ss} UTC");

                return (outputText, inputTokens, outputTokens);
            }
            catch (Exception e)
            {
                context?.Logger.LogLine($"Error in Bedrock service: {e.Message}\nStack trace: {e.StackTrace}");
                throw;
            }
        }

        private JsonSerializerOptions GetRequestSerializerOptions()
        {
            // Titan must preserve exact camelCase keys; Nova/Claude are already using exact identifiers.
            return _modelConfig.RequestFormat switch
            {
                Models.RequestFormat.Titan => new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                },
                Models.RequestFormat.Nova => new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                },
                Models.RequestFormat.Claude => new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                },
                Models.RequestFormat.Qwen => new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                },
                _ => new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }
            };
        }

        private static string TryExtractCompletionText(string responseJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);

                // Qwen style: choices[0].message.content (at root level)
                if (doc.RootElement.TryGetProperty("choices", out var qwenChoices) &&
                    qwenChoices.ValueKind == JsonValueKind.Array)
                {
                    var firstChoice = qwenChoices.EnumerateArray().FirstOrDefault();
                    if (firstChoice.ValueKind == JsonValueKind.Object &&
                        firstChoice.TryGetProperty("message", out var qwenMessage) &&
                        qwenMessage.TryGetProperty("content", out var qwenContent) &&
                        qwenContent.ValueKind == JsonValueKind.String)
                    {
                        return qwenContent.GetString();
                    }
                }

                // Converse output style: output.message.content[0].text
                if (doc.RootElement.TryGetProperty("output", out var output) &&
                    output.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentArr) &&
                    contentArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in contentArr.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                        {
                            var text = textEl.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                return text;
                            }
                        }
                    }
                }

                // Titan style: results[0].outputText
                if (doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    var first = results.EnumerateArray().FirstOrDefault();
                    if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("outputText", out var ot) && ot.ValueKind == JsonValueKind.String)
                    {
                        return ot.GetString();
                    }
                }

                // Claude style: content string
                if (doc.RootElement.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                {
                    return contentProp.GetString();
                }

                // Generic completion field
                if (doc.RootElement.TryGetProperty("completion", out var comp) && comp.ValueKind == JsonValueKind.String)
                {
                    return comp.GetString();
                }
            }
            catch
            {
                // ignore and return null below
            }
            return null;
        }

        private static async Task SaveCompletionArtifacts(string completionText, string requestId, ILambdaContext context)
        {
            try
            {
                var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CachedFiles_OutputFiles");
                Directory.CreateDirectory(outputPath);

                // Save raw completion text
                var rawTextFile = $"completion_text_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{requestId}.txt";
                await File.WriteAllTextAsync(Path.Combine(outputPath, rawTextFile), completionText);

                // Strip code fences then try to normalize JSON from completion text
                var stripped = JsonExtractor.StripCodeFences(completionText);
                try
                {
                    var normalized = JsonExtractor.NormalizeJson(stripped);
                    using var doc = JsonDocument.Parse(normalized);
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var pretty = JsonSerializer.Serialize(doc.RootElement, options);

                    var jsonFile = $"completion_json_{DateTime.UtcNow:yyyyMMdd_HHmms}_{requestId}.json";
                    await File.WriteAllTextAsync(Path.Combine(outputPath, jsonFile), pretty);

                    context?.Logger.LogLine($"Saved completion artifacts: {rawTextFile}, {jsonFile}");
                }
                catch (Exception jsonEx)
                {
                    context?.Logger.LogLine($"Could not parse completion JSON: {jsonEx.Message}");
                }
            }
            catch (Exception ex)
            {
                context?.Logger.LogLine($"Warning saving completion artifacts: {ex.Message}");
            }
        }

        private static async Task SaveResponseToFile(string responseJson, string requestId, ILambdaContext context)
        {
            try
            {
                var outputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CachedFiles_OutputFiles");
                Directory.CreateDirectory(outputPath);

                var rawFileName = $"raw_response_{DateTime.UtcNow:yyyyMMdd_HHmms}_{requestId}.txt";
                await File.WriteAllTextAsync(Path.Combine(outputPath, rawFileName), responseJson);

                try
                {
                    var normalizedJson = JsonExtractor.NormalizeJson(responseJson);
                    using var doc = JsonDocument.Parse(normalizedJson);
                    var element = doc.RootElement;

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var prettyJson = JsonSerializer.Serialize(element, options);

                    var jsonFileName = $"formatted_response_{DateTime.UtcNow:yyyyMMdd_HHmms}_{requestId}.json";
                    await File.WriteAllTextAsync(Path.Combine(outputPath, jsonFileName), prettyJson);

                    context?.Logger.LogLine($"Response saved to {rawFileName} and {jsonFileName}");
                }
                catch (Exception jsonEx)
                {
                    context?.Logger.LogLine($"Warning: Could not format JSON response: {jsonEx.Message}");
                }
            }
            catch (Exception ex)
            {
                context?.Logger.LogLine($"Warning: Could not save response to file: {ex.Message}");
            }
        }

        private (string outputText, int inputTokens, int outputTokens) ParseResponse(string responseJson)
        {
            try
            {
                // Normalize JSON before parsing where appropriate for content-only expectations
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                return _modelConfig.ResponseFormat switch
                {
                    Models.ResponseFormat.Titan => ParseTitanResponse(JsonExtractor.NormalizeJson(responseJson), options),
                    Models.ResponseFormat.Nova => ParseNovaResponse(responseJson, options),
                    Models.ResponseFormat.Claude => ParseClaudeResponse(JsonExtractor.NormalizeJson(responseJson), options),
                    Models.ResponseFormat.Qwen => ParseQwenResponse(responseJson, options),
                    _ => throw new ArgumentException($"Unsupported response format: {_modelConfig.ResponseFormat}")
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse response: {ex.Message}\nResponse: {responseJson}", ex);
            }
        }

        private static (string, int, int) ParseTitanResponse(string responseJson, JsonSerializerOptions options)
        {
            var response = JsonSerializer.Deserialize<TitanResponse>(responseJson, options);
            if (response?.Results == null || response.Results.Count == 0)
            {
                throw new InvalidOperationException($"Invalid Titan response format: {responseJson}");
            }

            return (
        response.Results[0].OutputText,
      response.Results[0].InputTextTokenCount,
            response.Results[0].OutputTextTokenCount
          );
        }

        private static (string, int, int) ParseClaudeResponse(string responseJson, JsonSerializerOptions options)
        {
            var response = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, options);
            return (
 response?.Content ?? string.Empty,
     response?.Usage?.InputTokens ?? EstimateTokenCount(responseJson),
      response?.Usage?.OutputTokens ?? EstimateTokenCount(response?.Content)
            );
        }

        private static (string, int, int) ParseQwenResponse(string responseJson, JsonSerializerOptions options)
        {
            var response = JsonSerializer.Deserialize<QwenChatResponse>(responseJson, options);
            var outputText = response?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            var inputTokens = response?.Usage?.PromptTokens ?? 0;
            var outputTokens = response?.Usage?.CompletionTokens ?? 0;

            return (outputText, inputTokens, outputTokens);
        }

        private static (string, int, int) ParseNovaResponse(string responseJson, JsonSerializerOptions options)
   {
            // Extract the model completion text and then extract JSON from within it
            var text = TryExtractCompletionText(responseJson);
            if (!string.IsNullOrWhiteSpace(text))
         {
       text = JsonExtractor.StripCodeFences(text);
       var extracted = JsonExtractor.ExtractFirstJsonObject(text) ?? text;
         // If extracted looks like JSON, keep as-is; otherwise return raw text
                try
  {
      using (JsonDocument.Parse(extracted)) { /* ok */ }
   return (extracted, EstimateTokenCount(responseJson), EstimateTokenCount(extracted));
          }
                catch
         {
           return (text, EstimateTokenCount(responseJson), EstimateTokenCount(text));
      }
         }

            // Fallback: normalize entire response
       var normalized = JsonExtractor.NormalizeJson(responseJson);
         return (normalized, EstimateTokenCount(responseJson), EstimateTokenCount(normalized));
        }

        private object CreateModelRequestWithPrompt(string systemPrompt, string userPrompt)
        {
            return _modelConfig.RequestFormat switch
   {
      Models.RequestFormat.Titan => new
       {
           inputText = userPrompt,
          textGenerationConfig = new
    {
  maxTokenCount = _modelConfig.InferenceParameters.MaxTokens,
         temperature = _modelConfig.InferenceParameters.Temperature,
    topP = _modelConfig.InferenceParameters.TopP,
  stopSequences = _modelConfig.InferenceParameters.StopSequences ?? Array.Empty<string>()
     }
              },
    Models.RequestFormat.Nova => new
   {
  messages = new[]
    {
    new
   {
    role = "system",
                content = new[] { new { text = systemPrompt } }
            },
     new
             {
   role = "user",
     content = new[] { new { text = userPrompt } }
       }
          }
 },
  Models.RequestFormat.Claude => new
      {
            anthropic_version = _modelConfig.InferenceParameters.Version,
               system = systemPrompt,
    max_tokens = _modelConfig.InferenceParameters.MaxTokens,
      temperature = _modelConfig.InferenceParameters.Temperature,
              top_p = _modelConfig.InferenceParameters.TopP,
   messages = new[]
               {
         new { role = "user", content = userPrompt }
     }
},
 Models.RequestFormat.Qwen => new
          {
          messages = new[]
  {
new { role = "system", content = systemPrompt },
   new { role = "user", content = userPrompt }
 },
      top_p = _modelConfig.InferenceParameters.TopP,
                  temperature = _modelConfig.InferenceParameters.Temperature,
      max_tokens = _modelConfig.InferenceParameters.MaxTokens
          },
     _ => throw new ArgumentException($"Unsupported model format: {_modelConfig.RequestFormat}")
            };
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

    // Response Models
    public class TitanResponse
    {
    public List<TitanResult> Results { get; set; } = new();
    }

    public class ClaudeResponse
    {
        public string Content { get; set; } = string.Empty;
        public Usage Usage { get; set; }
    }

    public class QwenResponse
    {
        [JsonPropertyName("output")]
     public QwenOutput Output { get; set; }
   [JsonPropertyName("usage")]
  public QwenUsage Usage { get; set; }
    }

    public class QwenChatResponse
    {
        [JsonPropertyName("choices")]
        public List<QwenChatChoice> Choices { get; set; }
        [JsonPropertyName("usage")]
      public QwenChatUsage Usage { get; set; }
    }

    public class QwenChatChoice
    {
        [JsonPropertyName("message")]
        public QwenChatMessage Message { get; set; }
    [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class QwenChatMessage
    {
        [JsonPropertyName("content")]
        public string Content { get; set; }
    [JsonPropertyName("role")]
        public string Role { get; set; }
    }

    public class QwenChatUsage
 {
    [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

  public class QwenOutput
  {
     [JsonPropertyName("choices")]
   public List<QwenChatChoice> Choices { get; set; }
    }

    public class QwenChoice
    {
     [JsonPropertyName("message")]
        public QwenMessage Message { get; set; }
     [JsonPropertyName("stop_reason")]
 public string StopReason { get; set; }
    }

    public class QwenMessage
    {
      [JsonPropertyName("content")]
  public string Content { get; set; }
    }

  public class QwenUsage
    {
     [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

      [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    public class TitanResult
    {
        [JsonPropertyName("outputText")]
    public string OutputText { get; set; } = string.Empty;

        [JsonPropertyName("completionReason")]
        public string CompletionReason { get; set; } = string.Empty;

    [JsonPropertyName("inputTextTokenCount")]
     public int InputTextTokenCount { get; set; }

  [JsonPropertyName("outputTextTokenCount")]
        public int OutputTextTokenCount { get; set; }
 }

    public class CachedResponse
    {
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
     public DateTime CachedAt { get; set; } = DateTime.UtcNow;
  }
}
