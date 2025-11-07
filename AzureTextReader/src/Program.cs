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

namespace ImageTextExtractorApp
{
    internal static class Program
    {
        private static async Task Main()
        {
            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await RunAsync(memoryCache);
        }

        // Asynchronous method to process all image URLs
        private static async Task RunAsync(IMemoryCache memoryCache)
        {
            //Azure endpoint and credentials(replace with your actual values)
            var endpoint = "https://sudhir-ai-test.openai.azure.com/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
            var subscriptionKey = "5j4M8CypX3SHzhuzdsddwhGrHyDMlnhIKtve7cqMwgHtjURmiA1DJQQJ99BJAC4f1cMXJ3w3AAAAACOGbrak";
            var imageUrls = new[] { "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065660-1.tif?raw=true",
                                    "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065660.tif?raw=true"
 };

            // Use a single HttpClient instance for all requests
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

            foreach (var imageUrl in imageUrls)
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
                    continue;
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
                        await Task.Delay(3000); // Wait3 seconds before retrying
                    }
                } while (extractionOperation.status != "Succeeded");

                // Output the extracted content if available
                if (extractionOperation?.result?.contents != null && extractionOperation.result.contents.Count > 0)
                {
                    Console.WriteLine($"Parsed ExtractionOperation: status={extractionOperation.status}, contents={extractionOperation.result.contents[0].markdown}");

                    // Initialize the AzureOpenAIClient
                    var credential = new ApiKeyCredential(subscriptionKey);
                    var azureClient = new AzureOpenAIClient(new Uri("https://sudhir-ai-test.openai.azure.com/"), credential);

                    string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema - Copy.json");
                    JsonNode jsonSchema = JsonNode.Parse(schemaText);


                    // Initialize the ChatClient with the specified deployment name
                    ChatClient chatClient = azureClient.GetChatClient("gpt-4o-mini");

                    // Create a list of chat messages
                    var messages = new List<OpenAI.Chat.ChatMessage>
                                         {
                                         new SystemChatMessage(@" You are an OCR-like data extraction tool that extracts deed data from tif.

                                        1. Extract all values from the attached Deed PDF. Identify and return the following details for each property transaction:

                                        2. Grantor (seller/transferor) name

                                        3. Grantee (buyer/transferee) name

                                        4. Property address

                                        5. Legal description

                                        6. Sale price or transaction amount

                                        7. Tax information (if present)

                                        8. Recording date

                                        9. Document number or reference

                                        10. Notary information and date

                                         Any other relevant fields or notes mentioned in the deed

                                         You are a data transformation tool that takes in JSON data and a reference JSON schema, and outputs JSON data according to the schema.
                                         Not all of the data in the input JSON will fit the schema, so you may need to omit some data or add null values to the output JSON.
                                         Translate all data into English if not already in English.
                                         Ensure values are formatted as specified in the schema (e.g. dates as YYYY-MM-DD).
                                         Here is the schema: " + $"{jsonSchema}"),
                                         new UserChatMessage($"Please extract the data, grouping data according to theme/sub groups, and then output into JSON and provide the clean json data {extractionOperation.result.contents[0].markdown}")
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
                        // Compute a cache key for this combination of messages + options + imageUrl
                        var cacheKey = ComputeCacheKey(messages, options, imageUrl);

                        // Try to pull cached response
                        if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                        {
                            Console.WriteLine($"Cache hit for {imageUrl} - using cached chat completion.");
                            Console.WriteLine(cachedJson);
                        }
                        else
                        {
                            Console.WriteLine($"Cache miss for {imageUrl} - calling ChatClient.CompleteChat...");
                            // Create the chat completion request
                            ChatCompletion completion = chatClient.CompleteChat(messages, options);

                            // Serialize the response to JSON for consistent caching / logging
                            var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true });

                            // Try to extract model text from the completion and write clean JSON file if present
                            try
                            {
                                var modelText = ExtractModelText(completion);
                                if (string.IsNullOrWhiteSpace(modelText))
                                {
                                    // fallback to searching the serialized completion JSON
                                    modelText = completionJson;
                                }

                                var jsonObjects = ExtractJsonObjects(modelText);
                                if (jsonObjects.Count > 0)
                                {
                                    // take the first JSON object/array found and pretty-print to file
                                    var cleanJson = jsonObjects[0];
                                    try
                                    {
                                        var node = JsonNode.Parse(cleanJson);
                                        var pretty = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
                                        var outPath = Path.Combine(Directory.GetCurrentDirectory(), $"clean_output_{Path.GetFileNameWithoutExtension(imageUrl)}.json");
                                        File.WriteAllText(outPath, pretty, Encoding.UTF8);
                                        Console.WriteLine($"Wrote clean JSON to: {outPath}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Found JSON but failed to parse/serialize it: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("No JSON object found in model output.");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error extracting JSON from completion: {ex.Message}");
                            }

                            // Cache the serialized completion with reasonable expiration
                            var cacheEntryOptions = new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                                SlidingExpiration = TimeSpan.FromHours(24)
                            };

                            memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);
                            Console.WriteLine($"Cached result for {imageUrl}");

                            // Print the response
                            if (completion != null)
                            {
                                Console.WriteLine(completionJson);
                            }
                            else
                            {
                                Console.WriteLine("No response received.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"An error occurred: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("No contents found in extraction result.");
                }
            }
        }

        // Helper: compute stable cache key from messages + options + imageUrl
        private static string ComputeCacheKey(IList<OpenAI.Chat.ChatMessage> messages, ChatCompletionOptions options, string imageUrl)
        {
            var sb = new StringBuilder();
            // include imageUrl to differentiate cache for each URL
            sb.Append($"url:{imageUrl};");
            // include options that affect the output
            sb.Append($"temp:{options.Temperature};maxTokens:{options.MaxOutputTokenCount};topP:{options.TopP};freq:{options.FrequencyPenalty};pres:{options.PresencePenalty};");
            foreach (var m in messages)
            {
                // Role and content via reflection to avoid compile-time dependency on SDK internal shape
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
                    // fallback to ToString()
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

        // Extract model text from completion via reflection, and extract/clean JSON from it and save to file
        private static string ExtractModelText(object completion)
        {
            if (completion == null) return string.Empty;
            var t = completion.GetType();

            // First, try to read a top-level 'Content' collection (seen on OpenAI.Chat.ChatCompletion)
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
                            // fallback: ToString()
                            sbTop.AppendLine(part.ToString() ?? string.Empty);
                        }
                    }

                    var collected = sbTop.ToString();
                    // attempt to extract JSON and save
                    var jsonObjectsTop = ExtractJsonObjects(collected);
                    if (jsonObjectsTop.Count > 0)
                    {
                        try
                        {
                            CleanAndSaveJson(jsonObjectsTop);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to clean/save JSON from top-level Content: {ex.Message}");
                        }
                    }

                    return collected;
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
                        // choice may have a "Message" property or "Text"
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

                    var fullText = sb.ToString();

                    // Try to extract JSON objects/arrays from the assembled text
                    var jsonObjects = ExtractJsonObjects(fullText);
                    if (jsonObjects.Count > 0)
                    {
                        try
                        {
                            // Clean and save each found JSON (writes first cleaned JSON to file)
                            CleanAndSaveJson(jsonObjects);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to clean/save JSON from completion: {ex.Message}");
                        }
                    }

                    return fullText;
                }
            }

            // fallback to ToString()
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
                            // quick validation: try parse
                            try
                            {
                                JsonNode.Parse(candidate);
                                results.Add(candidate);
                                i = j; // advance outer loop
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
            // JsonValue
            if (node is JsonValue val)
            {
                var s = val.ToString();
                return !(s == "null" || string.IsNullOrWhiteSpace(s));
            }
            return false;
        }

        private static void CleanAndSaveJson(List<string> jsonStrings)
        {
            if (jsonStrings == null || jsonStrings.Count == 0) return;
            // We'll save the first cleaned JSON object/array found
            for (int i = 0; i < jsonStrings.Count; i++)
            {
                var candidate = jsonStrings[i];
                try
                {
                    var node = JsonNode.Parse(candidate);
                    if (node == null) continue;

                    // Clean the node
                    var keep = CleanJsonNode(node);
                    if (!keep) continue; // nothing useful after cleaning

                    // Pretty-print and save
                    var pretty = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
                    var filename = $"cleaned_output_{DateTime.UtcNow:yyyyMMddHHmmssfff}.json";
                    var outPath = Path.Combine(Directory.GetCurrentDirectory(), filename);
                    File.WriteAllText(outPath, pretty, Encoding.UTF8);
                    Console.WriteLine($"Wrote cleaned JSON to: {outPath}");
                    return;
                }
                catch (Exception ex)
                {
                    // ignore and try next
                    Console.WriteLine($"Candidate JSON parse failed: {ex.Message}");
                }
            }
        }
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

