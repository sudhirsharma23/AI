using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Caching.Memory;
using System.Collections;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.IO;
using ImageTextExtractor.Configuration;
using ImageTextExtractor.Services;  // Add this for PromptService

namespace ImageTextExtractorApp
{
    internal static class Program
    {
        private const string OutputDirectory = "OutputFiles";

        private static async Task Main()
        {
            // Create OutputFiles directory if it doesn't exist
            Directory.CreateDirectory(OutputDirectory);
            Console.WriteLine($"Output directory created/verified: {Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory)}");

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await RunAsync(memoryCache);
        }

        // Asynchronous method to process all image URLs
        private static async Task RunAsync(IMemoryCache memoryCache)
        {
            // SECURE: Load configuration from environment variables, user secrets, or appsettings
            Console.WriteLine("Loading Azure AI configuration...");
            var config = AzureAIConfig.Load();
            config.Validate();

            // Azure endpoint and credentials loaded securely
            var endpoint = config.Endpoint + "/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
            var subscriptionKey = config.SubscriptionKey;

            var imageUrls = new[] {
          "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065659-1.tif?raw=true",
          "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065659.tif?raw=true"
            };

            // Use a single HttpClient instance for all requests
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            // Step 1: Extract OCR data from all images and cache it
            var allOcrResults = new List<OcrResult>();
            var combinedMarkdown = new StringBuilder();

            foreach (var imageUrl in imageUrls)
            {
                Console.WriteLine($"\n=== Processing image: {imageUrl} ===");

                // Generate cache key for OCR result
                var ocrCacheKey = ComputeSimpleCacheKey($"OCR_{imageUrl}");
                string markdown;

                // Try to get cached OCR result
                if (memoryCache.TryGetValue(ocrCacheKey, out var cachedOcr) && cachedOcr is string cachedMarkdown)
                {
                    Console.WriteLine($"Cache hit for OCR data: {imageUrl}");
                    markdown = cachedMarkdown;
                }
                else
                {
                    Console.WriteLine($"Cache miss - Extracting OCR data from: {imageUrl}");
                    markdown = await ExtractOcrFromImage(client, endpoint, subscriptionKey, imageUrl);

                    // Cache the OCR result
                    var ocrCacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30), // OCR results don't change
                        SlidingExpiration = TimeSpan.FromDays(7)
                    };
                    memoryCache.Set(ocrCacheKey, markdown, ocrCacheOptions);
                    Console.WriteLine($"Cached OCR result for: {imageUrl}");
                }

                if (!string.IsNullOrEmpty(markdown))
                {
                    allOcrResults.Add(new OcrResult { ImageUrl = imageUrl, Markdown = markdown });
                    combinedMarkdown.AppendLine($"### Document: {Path.GetFileName(imageUrl)}");
                    combinedMarkdown.AppendLine(markdown);
                    combinedMarkdown.AppendLine("\n---\n");
                }
            }

            if (allOcrResults.Count == 0)
            {
                Console.WriteLine("No OCR data extracted. Exiting.");
                return;
            }

            // Step 2: Save combined OCR results to OutputFiles directory
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var combinedOcrPath = Path.Combine(OutputDirectory, $"combined_ocr_results_{timestamp}.md");
            File.WriteAllText(combinedOcrPath, combinedMarkdown.ToString(), Encoding.UTF8);
            Console.WriteLine($"\nSaved combined OCR results to: {combinedOcrPath}");

            // Step 3: Process with ChatCompletion (with caching)
            await ProcessWithChatCompletion(memoryCache, config, subscriptionKey, combinedMarkdown.ToString(), timestamp);
        }

        // Extract OCR data from a single image
        private static async Task<string> ExtractOcrFromImage(HttpClient client, string endpoint, string subscriptionKey, string imageUrl)
        {
            // Prepare the request body for the POST request
            var requestBody = $"{{\"url\":\"{imageUrl}\"}}";
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Send POST request to start extraction
            var postResponse = await client.PostAsync(endpoint, content);
            postResponse.EnsureSuccessStatusCode();

            // Retrieve the Operation-Location header for polling
            if (!postResponse.Headers.TryGetValues("Operation-Location", out var values))
            {
                Console.WriteLine("Operation-Location header not found. Skipping this image.");
                return string.Empty;
            }
            var operationLocation = values.First();
            Console.WriteLine($"Operation-Location: {operationLocation}");

            ExtractionOperation? extractionOperation = null;
            // Poll the operation status until it is 'Succeeded'
            do
            {
                var getRequest = new HttpRequestMessage(HttpMethod.Get, operationLocation);
                getRequest.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
                var getResponse = await client.SendAsync(getRequest);
                getResponse.EnsureSuccessStatusCode();
                var result = await getResponse.Content.ReadAsStringAsync();

                extractionOperation = JsonSerializer.Deserialize<ExtractionOperation>(result);
                if (extractionOperation == null)
                {
                    Console.WriteLine("Failed to parse ExtractionOperation. Skipping this image.");
                    break;
                }
                if (extractionOperation.status != "Succeeded")
                {
                    Console.WriteLine($"Status: {extractionOperation.status}. Waiting before retry...");
                    await Task.Delay(3000); // Wait 3 seconds before retrying
                }
            } while (extractionOperation?.status != "Succeeded");

            // Return the extracted markdown content
            if (extractionOperation?.result?.contents != null && extractionOperation.result.contents.Count > 0)
            {
                return extractionOperation.result.contents[0].markdown ?? string.Empty;
            }

            return string.Empty;
        }

        // Process combined OCR data with ChatCompletion
        private static async Task ProcessWithChatCompletion(IMemoryCache memoryCache, AzureAIConfig config, string subscriptionKey, string combinedMarkdown, string timestamp)
        {
            Console.WriteLine("\n=== Processing with Azure OpenAI ChatCompletion ===");

            // Initialize the AzureOpenAIClient with secure credentials
            var credential = new ApiKeyCredential(subscriptionKey);
            var azureClient = new AzureOpenAIClient(new Uri(config.Endpoint), credential);

            // Load JSON schema
            string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema - Copy.json");
            JsonNode jsonSchema = JsonNode.Parse(schemaText);

            // Initialize the ChatClient with the specified deployment name
            ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");

            // NEW: Initialize PromptService
            Console.WriteLine("Building prompt from templates...");
            var promptService = new PromptService(memoryCache);

            // Build complete prompt using PromptService
            var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
            {
                TemplateType = "deed_extraction",
                Version = "v1",
                IncludeExamples = true,
                ExampleSet = "default",
                IncludeRules = true,
                RuleNames = new List<string> { "percentage_calculation", "name_parsing", "date_format" },
                SchemaJson = jsonSchema.ToJsonString(),
                SourceData = combinedMarkdown
            });

            Console.WriteLine($"Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

            // Create messages using built prompt
            var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(builtPrompt.SystemMessage),
      new UserChatMessage(builtPrompt.UserMessage)
 };

            // Create chat completion options
            var options = new ChatCompletionOptions
            {
                Temperature = (float)0.7,
                MaxOutputTokenCount = 13107,
                TopP = (float)0.95,
                FrequencyPenalty = (float)0,
                PresencePenalty = (float)0,
            };

            try
            {
                // Compute a cache key for this combination of messages + options
                var cacheKey = ComputeCacheKey(messages, options, "combined_documents");

                // Try to pull cached response
                if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    Console.WriteLine($"Cache hit for ChatCompletion - using cached response.");
                    await SaveFinalCleanedJson(cachedJson, timestamp);
                }
                else
                {
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat...");
                    // Create the chat completion request
                    ChatCompletion completion = chatClient.CompleteChat(messages, options);

                    // Serialize the response to JSON for consistent caching / logging
                    var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true });

                    // Cache the serialized completion with reasonable expiration
                    var cacheEntryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                        SlidingExpiration = TimeSpan.FromHours(24)
                    };

                    memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);
                    Console.WriteLine($"Cached ChatCompletion result");

                    // Extract and save the final cleaned JSON
                    await SaveFinalCleanedJson(completionJson, timestamp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during ChatCompletion: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Save the final cleaned JSON output
        private static async Task SaveFinalCleanedJson(string completionJson, string timestamp)
        {
            try
            {
                // Deserialize to get the completion object
                var completion = JsonSerializer.Deserialize<JsonElement>(completionJson);

                // Extract model text from the completion
                var modelText = ExtractModelTextFromJson(completion);

                if (string.IsNullOrWhiteSpace(modelText))
                {
                    Console.WriteLine("No model text found in completion response.");
                    return;
                }

                // Extract JSON objects from the model text
                var jsonObjects = ExtractJsonObjects(modelText);

                if (jsonObjects.Count > 0)
                {
                    // Clean and save the first valid JSON object/array
                    var cleanedJson = CleanAndReturnJson(jsonObjects);

                    if (!string.IsNullOrEmpty(cleanedJson))
                    {
                        var finalOutputPath = Path.Combine(OutputDirectory, $"final_output_{timestamp}.json");
                        File.WriteAllText(finalOutputPath, cleanedJson, Encoding.UTF8);
                        Console.WriteLine($"Saved final cleaned JSON to: {finalOutputPath}");

                        // Analyze and report schema extensions
                        await AnalyzeSchemaExtensions(cleanedJson, timestamp);
                    }
                    else
                    {
                        Console.WriteLine("No valid JSON found after cleaning.");
                    }
                }
                else
                {
                    Console.WriteLine("No JSON object found in model output.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting/saving final JSON: {ex.Message}");
            }
        }

        // Analyze extracted JSON to identify fields not in base schema
        private static async Task AnalyzeSchemaExtensions(string extractedJson, string timestamp)
        {
            try
            {
                Console.WriteLine("\n=== Analyzing Schema Extensions ===");

                // Load base schema
                string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema - Copy.json");
                var baseSchema = JsonNode.Parse(schemaText);
                var extractedData = JsonNode.Parse(extractedJson);

                var extensions = new List<string>();
                FindExtensions(baseSchema, extractedData, "", extensions);

                if (extensions.Count > 0)
                {
                    Console.WriteLine($"Found {extensions.Count} extended fields not in base schema:");

                    var extensionsReport = new StringBuilder();
                    extensionsReport.AppendLine("# Schema Extensions Report");
                    extensionsReport.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                    extensionsReport.AppendLine($"\nTotal Extended Fields: {extensions.Count}");
                    extensionsReport.AppendLine("\n## Extended Fields:");
                    extensionsReport.AppendLine("```");

                    foreach (var ext in extensions.OrderBy(e => e))
                    {
                        Console.WriteLine($"  + {ext}");
                        extensionsReport.AppendLine($"  + {ext}");
                    }

                    extensionsReport.AppendLine("```");
                    extensionsReport.AppendLine("\n## Recommendations:");
                    extensionsReport.AppendLine("Consider updating the base schema to include frequently occurring extended fields.");

                    // Save extensions report
                    var reportPath = Path.Combine(OutputDirectory, $"schema_extensions_{timestamp}.md");
                    File.WriteAllText(reportPath, extensionsReport.ToString(), Encoding.UTF8);
                    Console.WriteLine($"\nSaved schema extensions report to: {reportPath}");
                }
                else
                {
                    Console.WriteLine("No schema extensions detected. All fields match base schema.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing schema extensions: {ex.Message}");
            }
        }

        // Recursively find fields in extracted data that don't exist in base schema
        private static void FindExtensions(JsonNode baseNode, JsonNode extractedNode, string path, List<string> extensions)
        {
            if (extractedNode == null) return;

            if (extractedNode is JsonObject extractedObj)
            {
                var baseObj = baseNode as JsonObject;

                foreach (var kvp in extractedObj)
                {
                    var fieldPath = string.IsNullOrEmpty(path) ? kvp.Key : $"{path}.{kvp.Key}";

                    // Check if this field exists in base schema
                    if (baseObj == null || !baseObj.ContainsKey(kvp.Key))
                    {
                        // Field not in base schema - this is an extension
                        var valueType = GetJsonValueType(kvp.Value);
                        extensions.Add($"{fieldPath} : {valueType}");
                    }
                    else
                    {
                        // Field exists in base, recurse to check nested fields
                        FindExtensions(baseObj[kvp.Key], kvp.Value, fieldPath, extensions);
                    }
                }
            }
            else if (extractedNode is JsonArray extractedArr && baseNode is JsonArray baseArr)
            {
                // For arrays, check the first element if exists
                if (extractedArr.Count > 0 && baseArr.Count > 0)
                {
                    FindExtensions(baseArr[0], extractedArr[0], path + "[0]", extensions);
                }
            }
        }

        // Get JSON value type as string
        private static string GetJsonValueType(JsonNode node)
        {
            if (node == null) return "null";
            if (node is JsonValue jv)
            {
                var value = jv.ToString();
                // Try to infer type
                if (bool.TryParse(value, out _)) return "boolean";
                if (int.TryParse(value, out _)) return "number";
                if (decimal.TryParse(value, out _)) return "number";
                if (DateTime.TryParse(value, out _)) return "date/string";
                return "string";
            }
            if (node is JsonObject) return "object";
            if (node is JsonArray) return "array";
            return "unknown";
        }

        // Extract model text from JsonElement (deserialized completion)
        private static string ExtractModelTextFromJson(JsonElement completion)
        {
            var sb = new StringBuilder();

            // Try to read "Content" property
            if (completion.TryGetProperty("Content", out var contentProp) || completion.TryGetProperty("content", out contentProp))
            {
                if (contentProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in contentProp.EnumerateArray())
                    {
                        if (part.TryGetProperty("Text", out var textProp) || part.TryGetProperty("text", out textProp))
                        {
                            sb.AppendLine(textProp.GetString());
                        }
                    }
                }
                else if (contentProp.ValueKind == JsonValueKind.String)
                {
                    sb.AppendLine(contentProp.GetString());
                }
            }

            // Try "Choices" property
            if (completion.TryGetProperty("Choices", out var choicesProp) || completion.TryGetProperty("choices", out choicesProp))
            {
                if (choicesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choicesProp.EnumerateArray())
                    {
                        if (choice.TryGetProperty("Message", out var msgProp) || choice.TryGetProperty("message", out msgProp))
                        {
                            if (msgProp.TryGetProperty("Content", out var msgContent) || msgProp.TryGetProperty("content", out msgContent))
                            {
                                sb.AppendLine(msgContent.GetString());
                            }
                        }
                        else if (choice.TryGetProperty("Text", out var textProp) || choice.TryGetProperty("text", out textProp))
                        {
                            sb.AppendLine(textProp.GetString());
                        }
                    }
                }
            }

            return sb.ToString();
        }

        // Helper: compute stable cache key from messages + options + identifier
        private static string ComputeCacheKey(IList<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, string identifier)
        {
            var sb = new StringBuilder();
            sb.Append($"id:{identifier};");
            sb.Append($"temp:{options.Temperature};maxTokens:{options.MaxOutputTokenCount};topP:{options.TopP};freq:{options.FrequencyPenalty};pres:{options.PresencePenalty};");

            foreach (var m in messages)
            {
                var t = m.GetType();
                string role = "";
                string content = "";
                var roleProp = t.GetProperty("Role") ?? t.GetProperty("Author") ?? t.GetProperty("Name");
                if (roleProp != null)
                {
                    try { role = roleProp.GetValue(m)?.ToString() ?? ""; } catch { role = ""; }
                }
                var contentProp = t.GetProperty("Content") ?? t.GetProperty("Text") ?? t.GetProperty("Message");
                if (contentProp != null)
                {
                    try { content = contentProp.GetValue(m)?.ToString() ?? ""; } catch { content = ""; }
                }
                else
                {
                    content = m.ToString() ?? "";
                }

                sb.Append($"role:{role};content:{content};");
            }

            // Hash the combined string for compact key
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        // Simple cache key for OCR results
        private static string ComputeSimpleCacheKey(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        // Extract model text from completion via reflection (kept for backward compatibility)
        private static string ExtractModelText(object completion)
        {
            if (completion == null) return string.Empty;
            var t = completion.GetType();

            // First, try to read a top-level 'Content' collection
            var topContentProp = t.GetProperty("Content") ?? t.GetProperty("content");
            if (topContentProp != null)
            {
                var contentColl = topContentProp.GetValue(completion) as IEnumerable;
                if (contentColl != null)
                {
                    var sbTop = new StringBuilder();
                    foreach (var part in contentColl)
                    {
                        if (part == null) continue;
                        var pt = part.GetType();
                        var textProp = pt.GetProperty("Text") ?? pt.GetProperty("text") ?? pt.GetProperty("Content");
                        if (textProp != null)
                        {
                            var txt = textProp.GetValue(part)?.ToString();
                            if (!string.IsNullOrEmpty(txt)) sbTop.AppendLine(txt);
                        }
                        else
                        {
                            sbTop.AppendLine(part.ToString() ?? string.Empty);
                        }
                    }
                    return sbTop.ToString();
                }
            }

            // Try common properties: "Choices" or "choices" or "Responses"
            var choicesProp = t.GetProperty("Choices") ?? t.GetProperty("choices") ?? t.GetProperty("Responses") ?? t.GetProperty("responses");
            if (choicesProp != null)
            {
                var choicesObj = choicesProp.GetValue(completion) as IEnumerable;
                if (choicesObj != null)
                {
                    var sb = new StringBuilder();
                    foreach (var choice in choicesObj)
                    {
                        if (choice == null) continue;
                        var ct = choice.GetType();
                        var messageObj = ct.GetProperty("Message")?.GetValue(choice) ?? ct.GetProperty("message")?.GetValue(choice);
                        if (messageObj != null)
                        {
                            var msgType = messageObj.GetType();
                            var content = msgType.GetProperty("Content")?.GetValue(messageObj) ?? msgType.GetProperty("content")?.GetValue(messageObj) ?? msgType.GetProperty("Text")?.GetValue(messageObj);
                            if (content != null) sb.AppendLine(content.ToString());
                        }
                        else
                        {
                            var text = ct.GetProperty("Text")?.GetValue(choice) ?? ct.GetProperty("text")?.GetValue(choice) ?? ct.GetProperty("Content")?.GetValue(choice);
                            if (text != null) sb.AppendLine(text.ToString());
                        }
                    }
                    return sb.ToString();
                }
            }

            return completion.ToString() ?? string.Empty;
        }

        // Extract JSON objects/arrays from arbitrary text by detecting balanced braces/brackets
        private static List<string> ExtractJsonObjects(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrEmpty(text)) return results;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '{' || text[i] == '[')
                {
                    char open = text[i];
                    char close = (open == '{') ? '}' : ']';
                    int depth = 0;
                    int start = i;
                    for (int j = i; j < text.Length; j++)
                    {
                        if (text[j] == open) depth++;
                        else if (text[j] == close) depth--;

                        if (depth == 0)
                        {
                            var candidate = text.Substring(start, j - start + 1);
                            try
                            {
                                JsonNode.Parse(candidate);
                                results.Add(candidate);
                                i = j;
                                break;
                            }
                            catch
                            {
                                // not valid JSON, continue searching
                            }
                        }
                    }
                }
            }

            return results;
        }

        // Clean a parsed JsonNode recursively: remove properties with null or empty string values, remove empty objects/arrays
        private static bool CleanJsonNode(JsonNode? node)
        {
            if (node == null) return false;
            if (node is JsonObject obj)
            {
                var keys = obj.Select(kvp => kvp.Key).ToList();
                foreach (var key in keys)
                {
                    var child = obj[key];
                    if (child == null || child is JsonValue jv && (jv.ToString() == "null" || string.IsNullOrWhiteSpace(jv.ToString())))
                    {
                        obj.Remove(key);
                        continue;
                    }

                    if (child is JsonObject || child is JsonArray)
                    {
                        var keep = CleanJsonNode(child);
                        if (!keep)
                        {
                            obj.Remove(key);
                        }
                    }
                }
                return obj.Count > 0;
            }
            else if (node is JsonArray arr)
            {
                var newItems = new List<JsonNode?>();
                foreach (var item in arr)
                {
                    if (item == null) continue;
                    if (item is JsonValue iv)
                    {
                        var s = iv.ToString();
                        if (s == "null" || string.IsNullOrWhiteSpace(s)) continue;
                        newItems.Add(item);
                    }
                    else
                    {
                        var keep = CleanJsonNode(item);
                        if (keep) newItems.Add(item);
                    }
                }
                arr.Clear();
                foreach (var it in newItems) arr.Add(it);
                return arr.Count > 0;
            }
            if (node is JsonValue val)
            {
                var s = val.ToString();
                return !(s == "null" || string.IsNullOrWhiteSpace(s));
            }
            return false;
        }

        // Clean and return JSON as string (instead of saving to file)
        private static string CleanAndReturnJson(List<string> jsonStrings)
        {
            if (jsonStrings == null || jsonStrings.Count == 0) return string.Empty;

            for (int i = 0; i < jsonStrings.Count; i++)
            {
                var candidate = jsonStrings[i];
                try
                {
                    var node = JsonNode.Parse(candidate);
                    if (node == null) continue;

                    // Clean the node
                    var keep = CleanJsonNode(node);
                    if (!keep) continue;

                    // Pretty-print and return
                    var pretty = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
                    return pretty;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Candidate JSON parse failed: {ex.Message}");
                }
            }

            return string.Empty;
        }

        // Legacy method kept for compatibility (now calls CleanAndReturnJson internally)
        private static void CleanAndSaveJson(List<string> jsonStrings)
        {
            var cleanedJson = CleanAndReturnJson(jsonStrings);
            if (!string.IsNullOrEmpty(cleanedJson))
            {
                var filename = $"cleaned_output_{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
                var outPath = Path.Combine(OutputDirectory, filename);
                File.WriteAllText(outPath, cleanedJson, Encoding.UTF8);
                Console.WriteLine($"Wrote cleaned JSON to: {outPath}");
            }
        }
    }

    // Helper class to store OCR results
    class OcrResult
    {
        public string ImageUrl { get; set; }
        public string Markdown { get; set; }
    }

    // Model for the extraction operation response
    class ExtractionOperation
    {
        public string? id { get; set; }
        public string? status { get; set; }
        public ExtractionResult? result { get; set; }
    }

    // Model for the result property in the extraction operation
    class ExtractionResult
    {
        public List<ContentResult>? contents { get; set; }
    }

    // Model for each content item in the extraction result
    class ContentResult
    {
        public string? markdown { get; set; }
        public string? kind { get; set; }
    }
}

