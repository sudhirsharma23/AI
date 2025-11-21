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
using AzureTextReader.Configuration;
using AzureTextReader.Services;
using AzureTextReader.Models;
using AzureTextReader.Services.Ocr;  // Add OCR services
using Microsoft.Extensions.Configuration;

namespace AzureTextReaderApp
{
    internal static class Program
    {
        private const string OutputDirectory = "OutputFiles";

        // Configure which model to use - easy to switch!
        private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;

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
            Console.WriteLine("\n=== AzureTextReader - OCR Processing ===\n");

            // Load OCR configuration first
            Console.WriteLine("Loading OCR configuration...");
            var ocrConfig = OcrConfig.Load();
            ocrConfig.Validate();

            // Load Azure AI config (may be null if using Aspose only)
            AzureAIConfig azureConfig = null;
            if (ocrConfig.IsAzureEnabled)
            {
                Console.WriteLine("Loading Azure AI configuration...");
                azureConfig = AzureAIConfig.Load();
                azureConfig.Validate();
            }

            var imageUrls = new[] {
           "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065659-1.tif?raw=true",
         "https://github.com/sudhirsharma23/azure-ai-foundry-image-text-extractor/blob/main/src/images/2025000065659.tif?raw=true"
         };

            // Create HTTP client for OCR services
            using var httpClient = new HttpClient();

            // Create OCR service factory and get the configured service
            var ocrFactory = new OcrServiceFactory(ocrConfig, azureConfig, memoryCache, httpClient);
            var ocrService = ocrFactory.CreateOcrService();

            Console.WriteLine($"\n✓ Using OCR Engine: {ocrService.GetEngineName()}\n");

            // Step 1: Extract OCR data from all images using the configured OCR service
            var allOcrResults = new List<AzureTextReader.Services.Ocr.OcrResult>();
            var combinedMarkdown = new StringBuilder();

            foreach (var imageUrl in imageUrls)
            {
                Console.WriteLine($"\n=== Processing image: {imageUrl} ===");

                var result = await ocrService.ExtractTextAsync(imageUrl);

                if (result.Success)
                {
                    allOcrResults.Add(result);
                    combinedMarkdown.AppendLine($"### Document: {Path.GetFileName(imageUrl)}");
                    combinedMarkdown.AppendLine($"**OCR Engine**: {result.Engine}");
                    combinedMarkdown.AppendLine($"**Processing Time**: {result.ProcessingTime.TotalSeconds:F2}s");
                    combinedMarkdown.AppendLine();
                    combinedMarkdown.AppendLine(result.Markdown);
                    combinedMarkdown.AppendLine("\n---\n");

                    Console.WriteLine($"✓ Success! Extracted {result.PlainText?.Length ?? 0} characters in {result.ProcessingTime.TotalSeconds:F2}s");
                }
                else
                {
                    Console.WriteLine($"✗ Failed: {result.ErrorMessage}");
                }
            }

            if (allOcrResults.Count == 0)
            {
                Console.WriteLine("\n✗ No OCR data extracted. Exiting.");
                return;
            }

            // Step 2: Save combined OCR results to OutputFiles directory
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var engineName = ocrConfig.Engine.ToLowerInvariant();
            var combinedOcrPath = Path.Combine(OutputDirectory, $"combined_ocr_results_{engineName}_{timestamp}.md");
            File.WriteAllText(combinedOcrPath, combinedMarkdown.ToString(), Encoding.UTF8);
            Console.WriteLine($"\n✓ Saved combined OCR results to: {combinedOcrPath}");

            // Step 3: Process with ChatCompletion (with caching)
            // Only load Azure OpenAI config if not already loaded
            if (azureConfig == null)
            {
                Console.WriteLine("\nLoading Azure AI configuration for LLM processing...");
                azureConfig = AzureAIConfig.Load();
                azureConfig.Validate();
            }

            await ProcessWithChatCompletion(memoryCache, azureConfig, combinedMarkdown.ToString(), timestamp);

            Console.WriteLine("\n=== Processing Complete ===\n");
        }

        // Process combined OCR data with ChatCompletion
        private static async Task ProcessWithChatCompletion(IMemoryCache memoryCache, AzureAIConfig config, string combinedMarkdown, string timestamp)
        {
            Console.WriteLine("\n=== Processing with Azure OpenAI ChatCompletion ===");

            // Initialize the AzureOpenAIClient with secure credentials
            var credential = new ApiKeyCredential(config.SubscriptionKey);
            var azureClient = new AzureOpenAIClient(new Uri(config.Endpoint), credential);

            // Initialize the ChatClient with the specified deployment name
            ChatClient chatClient = azureClient.GetChatClient(ModelConfig.DeploymentName);

            // Process with BOTH versions for comparison
            Console.WriteLine("\n--- Version 1: Schema-Based Extraction ---");
            await ProcessVersion1(memoryCache, chatClient, combinedMarkdown, timestamp);

            Console.WriteLine("\n--- Version 2: Dynamic Extraction (No Schema) ---");
            await ProcessVersion2(memoryCache, chatClient, combinedMarkdown, timestamp);
        }

        // VERSION 1: Schema-based extraction (existing logic)
        private static async Task ProcessVersion1(IMemoryCache memoryCache, ChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                // Load JSON schema
                string schemaText = File.ReadAllText("E:\\Sudhir\\Code\\AzureTextReader\\src\\invoice_schema.json");
                JsonNode jsonSchema = JsonNode.Parse(schemaText);

                // Initialize PromptService
                Console.WriteLine("Building V1 prompt from templates (with schema)...");
                var promptService = new PromptService(memoryCache);

                // Build complete prompt using PromptService
                var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                {
                    TemplateType = "document_extraction",
                    Version = "v1",
                    IncludeExamples = true,
                    ExampleSet = "default",
                    IncludeRules = true,
                    RuleNames = new List<string> { "percentage_calculation", "name_parsing", "date_format" },
                    SchemaJson = jsonSchema.ToJsonString(),
                    SourceData = combinedMarkdown
                });

                Console.WriteLine($"V1 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

                // Create messages using built prompt
                var messages = new List<OpenAI.Chat.ChatMessage>
                {
                    new SystemChatMessage(builtPrompt.SystemMessage),
                    new UserChatMessage(builtPrompt.UserMessage)
                };

                // Use configured options from ModelConfig
                var options = CreateChatOptions();

                // Compute a cache key for this combination of messages + options
                var cacheKey = ComputeCacheKey(messages, options, "v1_combined_documents");

                // Try to pull cached response
                if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    Console.WriteLine($"Cache hit for V1 ChatCompletion - using cached response.");
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v1_schema");
                }
                else
                {
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat for V1...");
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
                    Console.WriteLine($"Cached V1 ChatCompletion result");

                    // Extract and save the final cleaned JSON
                    await SaveFinalCleanedJson(completionJson, timestamp, "_v1_schema");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during V1 ChatCompletion: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // VERSION 2: Dynamic extraction (no schema, purely from OCR)
        private static async Task ProcessVersion2(IMemoryCache memoryCache, ChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                Console.WriteLine("Building V2 prompt from templates (dynamic, no schema) with chunked processing...");
                var promptService = new PromptService(memoryCache);

                // Chunk the combined markdown to avoid context/token limits
                const int chunkSize = 6000; // characters per chunk (tunable)
                var chunks = SplitTextIntoChunks(combinedMarkdown, chunkSize);

                var options = CreateChatOptions();
                var chunkJsonNodes = new List<JsonNode>();

                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    Console.WriteLine($"Processing chunk {i + 1}/{chunks.Count} (approx {chunk.Length} chars)");

                    // Build V2 prompt for this chunk
                    var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                    {
                        TemplateType = "document_extraction",
                        Version = "v2",
                        IncludeExamples = false,
                        IncludeRules = false,
                        RuleNames = new List<string>(),
                        SchemaJson = "",
                        SourceData = chunk,
                        UserMessageTemplate = "Analyze the documents below and extract ALL relevant information dynamically. Return a comprehensive JSON with all findings.\n\nDOCUMENTS:\n\n" + chunk
                    });

                    var messages = new List<OpenAI.Chat.ChatMessage>
                    {
                        new SystemChatMessage(builtPrompt.SystemMessage),
                        new UserChatMessage(builtPrompt.UserMessage)
                    };

                    // Compute a per-chunk cache key
                    var cacheKey = ComputeCacheKey(messages, options, $"v2_chunk_{i}");

                    string completionJson = null;

                    if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                    {
                        Console.WriteLine($"Cache hit for V2 chunk {i + 1}");
                        completionJson = cachedJson;
                    }
                    else
                    {
                        Console.WriteLine($"Calling ChatClient.CompleteChat for chunk {i + 1}...");
                        var completion = chatClient.CompleteChat(messages, options);
                        completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

                        var cacheEntryOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                            SlidingExpiration = TimeSpan.FromHours(24)
                        };
                        memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);
                    }

                    // Extract model text from completion
                    var completionElement = JsonSerializer.Deserialize<JsonElement>(completionJson);
                    var modelText = ExtractModelTextFromJson(completionElement);

                    if (string.IsNullOrWhiteSpace(modelText))
                    {
                        Console.WriteLine($"No model text found for chunk {i + 1}, skipping.");
                        continue;
                    }

                    var jsonObjects = ExtractJsonObjects(modelText);
                    if (jsonObjects.Count == 0)
                    {
                        Console.WriteLine($"No JSON object found in model output for chunk {i + 1}.");
                        continue;
                    }

                    // Clean and parse first valid JSON from this chunk
                    var cleaned = CleanAndReturnJson(jsonObjects);
                    if (string.IsNullOrEmpty(cleaned))
                    {
                        Console.WriteLine($"No valid JSON after cleaning for chunk {i + 1}.");
                        continue;
                    }

                    try
                    {
                        var node = JsonNode.Parse(cleaned);
                        if (node != null) chunkJsonNodes.Add(node);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed parsing cleaned JSON for chunk {i + 1}: {ex.Message}");
                    }
                }

                if (chunkJsonNodes.Count == 0)
                {
                    Console.WriteLine("No JSON extracted from any chunk. Exiting V2 processing.");
                    return;
                }

                // Merge all chunk JSON objects into one consolidated JsonNode
                JsonNode merged = chunkJsonNodes[0].DeepClone();
                for (int i = 1; i < chunkJsonNodes.Count; i++)
                {
                    merged = MergeJsonNodes(merged, chunkJsonNodes[i]);
                }

                // Clean merged node
                CleanJsonNode(merged);

                var mergedPretty = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
                var finalOutputPath = Path.Combine(OutputDirectory, $"final_output_{timestamp}_v2_dynamic.json");
                File.WriteAllText(finalOutputPath, mergedPretty, Encoding.UTF8);
                Console.WriteLine($"Saved merged V2 final JSON to: {finalOutputPath}");

                // Create summary report and field list
                // Final normalization pass: ask the model to reconcile/normalize the merged JSON
                try
                {
                    Console.WriteLine("Running final normalization pass to reconcile merged JSON...");

                    // Build normalization prompt
                    var normSystem = "You are a JSON normalization assistant. Given a merged JSON produced from multiple document chunks, reconcile duplicate fields, normalize date/number formats, merge arrays, and ensure output is a single clean JSON object following snake_case naming. Return ONLY the final JSON object with no explanation.";
                    var normUser = "Here is the merged JSON. Normalize and reconcile it, returning a single JSON object only:\n\n" + mergedPretty;

                    var normMessages = new List<OpenAI.Chat.ChatMessage>
                    {
                        new SystemChatMessage(normSystem),
                        new UserChatMessage(normUser)
                    };

                    var normOptions = CreateChatOptions();
                    // Lower temperature for deterministic normalization
                    // Set a low temperature for deterministic normalization (use the lower of configured and 0.2)
                    var currentTemp = normOptions.Temperature ?? ModelConfig.Temperature;
                    var maxNormTemp = GetNormalizationMaxTemperature();
                    normOptions.Temperature = MathF.Min(currentTemp, maxNormTemp);

                    var normCacheKey = ComputeCacheKey(normMessages, normOptions, "v2_reconcile_merged_json");

                    string normCompletionJson = null;
                    if (memoryCache.TryGetValue(normCacheKey, out var normCached) && normCached is string nc)
                    {
                        Console.WriteLine("Cache hit for normalization pass.");
                        normCompletionJson = nc;
                    }
                    else
                    {
                        var normCompletion = chatClient.CompleteChat(normMessages, normOptions);
                        normCompletionJson = JsonSerializer.Serialize(normCompletion, new JsonSerializerOptions { WriteIndented = true });
                        var cacheEntryOptions = new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                            SlidingExpiration = TimeSpan.FromHours(24)
                        };
                        memoryCache.Set(normCacheKey, normCompletionJson, cacheEntryOptions);
                    }

                    var normElement = JsonSerializer.Deserialize<JsonElement>(normCompletionJson);
                    var normModelText = ExtractModelTextFromJson(normElement);
                    var normJsonObjects = ExtractJsonObjects(normModelText);
                    string normalizedPretty = string.Empty;
                    if (normJsonObjects.Count > 0)
                    {
                        normalizedPretty = CleanAndReturnJson(normJsonObjects);
                    }

                    if (!string.IsNullOrEmpty(normalizedPretty))
                    {
                        var normalizedPath = Path.Combine(OutputDirectory, $"final_output_{timestamp}_v2_normalized.json");
                        File.WriteAllText(normalizedPath, normalizedPretty, Encoding.UTF8);
                        Console.WriteLine($"Saved normalized V2 JSON to: {normalizedPath}");

                        // Use normalized JSON for summary
                        await CreateV2ExtractionSummary(normalizedPretty, timestamp);
                    }
                    else
                    {
                        Console.WriteLine("Normalization did not produce valid JSON; falling back to merged JSON for summary.");
                        await CreateV2ExtractionSummary(mergedPretty, timestamp);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Normalization pass failed: {ex.Message}");
                    Console.WriteLine("Proceeding with merged JSON for summary.");
                    await CreateV2ExtractionSummary(mergedPretty, timestamp);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during V2 ChatCompletion: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Split text into chunks trying to cut at paragraph boundaries
        private static List<string> SplitTextIntoChunks(string text, int maxChunkSize)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text)) return chunks;

            var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();

            foreach (var p in paragraphs)
            {
                if (sb.Length + p.Length + 2 <= maxChunkSize)
                {
                    if (sb.Length > 0) sb.Append("\n\n");
                    sb.Append(p);
                }
                else
                {
                    if (sb.Length > 0)
                    {
                        chunks.Add(sb.ToString());
                        sb.Clear();
                    }

                    if (p.Length + 2 > maxChunkSize)
                    {
                        // paragraph itself too big, split by lines
                        var lines = p.Split('\n');
                        var sbl = new StringBuilder();
                        foreach (var line in lines)
                        {
                            if (sbl.Length + line.Length + 1 <= maxChunkSize)
                            {
                                if (sbl.Length > 0) sbl.Append('\n');
                                sbl.Append(line);
                            }
                            else
                            {
                                if (sbl.Length > 0)
                                {
                                    chunks.Add(sbl.ToString());
                                    sbl.Clear();
                                }
                                if (line.Length > maxChunkSize)
                                {
                                    // last resort: hard split
                                    for (int i = 0; i < line.Length; i += maxChunkSize)
                                    {
                                        var part = line.Substring(i, Math.Min(maxChunkSize, line.Length - i));
                                        chunks.Add(part);
                                    }
                                }
                                else
                                {
                                    sbl.Append(line);
                                }
                            }
                        }
                        if (sbl.Length > 0) chunks.Add(sbl.ToString());
                    }
                    else
                    {
                        sb.Append(p);
                    }
                }
            }

            if (sb.Length > 0) chunks.Add(sb.ToString());
            return chunks;
        }

        // Merge two JsonNode structures into one (target gets merged with source)
        private static JsonNode MergeJsonNodes(JsonNode target, JsonNode source)
        {
            if (source == null) return target;
            if (target == null) return source.DeepClone();

            if (target is JsonObject tobj && source is JsonObject sobj)
            {
                foreach (var kvp in sobj)
                {
                    if (!tobj.ContainsKey(kvp.Key))
                    {
                        tobj[kvp.Key] = kvp.Value.DeepClone();
                    }
                    else
                    {
                        var existing = tobj[kvp.Key];
                        var incoming = kvp.Value;

                        if (existing == null)
                        {
                            tobj[kvp.Key] = incoming.DeepClone();
                        }
                        else if (existing is JsonObject && incoming is JsonObject)
                        {
                            tobj[kvp.Key] = MergeJsonNodes(existing, incoming);
                        }
                        else if (existing is JsonArray && incoming is JsonArray)
                        {
                            var arr = new JsonArray();
                            var set = new HashSet<string>();
                            foreach (var item in (JsonArray)existing)
                            {
                                var s = item?.ToJsonString() ?? string.Empty;
                                if (set.Add(s)) arr.Add(item.DeepClone());
                            }
                            foreach (var item in (JsonArray)incoming)
                            {
                                var s = item?.ToJsonString() ?? string.Empty;
                                if (set.Add(s)) arr.Add(item.DeepClone());
                            }
                            tobj[kvp.Key] = arr;
                        }
                        else
                        {
                            // For scalar conflicts: prefer existing non-empty value, otherwise take incoming
                            var existingStr = existing?.ToString();
                            var incomingStr = incoming?.ToString();
                            if (string.IsNullOrWhiteSpace(existingStr) && !string.IsNullOrWhiteSpace(incomingStr))
                            {
                                tobj[kvp.Key] = incoming.DeepClone();
                            }
                            // else keep existing
                        }
                    }
                }
                return tobj;
            }
            else if (target is JsonArray tarr && source is JsonArray sarr)
            {
                var arr = new JsonArray();
                var set = new HashSet<string>();
                foreach (var item in tarr)
                {
                    var s = item?.ToJsonString() ?? string.Empty;
                    if (set.Add(s)) arr.Add(item.DeepClone());
                }
                foreach (var item in sarr)
                {
                    var s = item?.ToJsonString() ?? string.Empty;
                    if (set.Add(s)) arr.Add(item.DeepClone());
                }
                return arr;
            }

            // Fallback: return target
            return target;
        }

        // Save the final cleaned JSON output with version suffix
        private static async Task SaveFinalCleanedJson(string completionJson, string timestamp, string versionSuffix = "")
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
                        var finalOutputPath = Path.Combine(OutputDirectory, $"final_output_{timestamp}{versionSuffix}.json");
                        File.WriteAllText(finalOutputPath, cleanedJson, Encoding.UTF8);
                        Console.WriteLine($"Saved final cleaned JSON to: {finalOutputPath}");

                        // Only analyze schema extensions for V1 (schema-based)
                        if (versionSuffix.Contains("v1"))
                        {
                            await AnalyzeSchemaExtensions(cleanedJson, timestamp);
                        }
                        else if (versionSuffix.Contains("v2"))
                        {
                            // For V2, create a summary report of what was extracted
                            await CreateV2ExtractionSummary(cleanedJson, timestamp);
                        }
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

        // Create a summary report for V2 dynamic extraction
        private static async Task CreateV2ExtractionSummary(string extractedJson, string timestamp)
        {
            try
            {
                Console.WriteLine("\n=== Analyzing V2 Dynamic Extraction ===");

                var extractedData = JsonNode.Parse(extractedJson);
                var summary = new StringBuilder();

                summary.AppendLine("# V2 Dynamic Extraction Summary");
                summary.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                summary.AppendLine();

                // Count total fields extracted
                int totalFields = CountJsonFields(extractedData);
                summary.AppendLine($"## Statistics");
                summary.AppendLine($"- Total fields extracted: {totalFields}");
                summary.AppendLine();

                // Analyze main sections
                summary.AppendLine("## Sections Extracted:");
                summary.AppendLine("```");

                if (extractedData is JsonObject obj)
                {
                    foreach (var section in obj)
                    {
                        var fieldCount = CountJsonFields(section.Value);
                        summary.AppendLine($"  - {section.Key}: {fieldCount} fields");
                    }
                }

                summary.AppendLine("```");
                summary.AppendLine();

                // Extract key metrics
                summary.AppendLine("## Key Information:");
                summary.AppendLine("```");

                try
                {
                    // Buyer count
                    var buyerInfo = extractedData?["buyerInformation"];
                    if (buyerInfo != null)
                    {
                        var totalBuyers = buyerInfo["totalBuyers"]?.ToString() ?? "N/A";
                        summary.AppendLine($"  - Total Buyers: {totalBuyers}");
                    }

                    // Seller count
                    var sellerInfo = extractedData?["sellerInformation"];
                    if (sellerInfo != null)
                    {
                        var totalSellers = sellerInfo["totalSellers"]?.ToString() ?? "N/A";
                        summary.AppendLine($"  - Total Sellers: {totalSellers}");
                    }

                    // Property address
                    var propertyInfo = extractedData?["propertyInformation"];
                    if (propertyInfo != null)
                    {
                        var address = propertyInfo["address"]?["fullAddress"]?.ToString() ?? "N/A";
                        summary.AppendLine($"  - Property Address: {address}");
                    }

                    // Transaction amount
                    var transactionDetails = extractedData?["transactionDetails"];
                    if (transactionDetails != null)
                    {
                        var salePrice = transactionDetails["salePrice"]?.ToString() ?? "N/A";
                        summary.AppendLine($"  - Sale Price: ${salePrice}");
                    }

                    // PCOR present?
                    var pcorInfo = extractedData?["pcorInformation"];
                    if (pcorInfo != null)
                    {
                        var pcorPresent = pcorInfo["documentPresent"]?.ToString() ?? "N/A";
                        summary.AppendLine($"  - PCOR Document: {pcorPresent}");
                    }
                }
                catch
                {
                    summary.AppendLine("  - Could not extract key metrics from structure");
                }

                summary.AppendLine("```");
                summary.AppendLine();

                summary.AppendLine("## Comparison Notes:");
                summary.AppendLine("Compare this V2 output with the V1 schema-based output to see:");
                summary.AppendLine("- What additional fields were extracted dynamically");
                summary.AppendLine("- Whether the schema covers all relevant information");
                summary.AppendLine("- Opportunities to enhance the schema");

                // Save summary report
                var reportPath = Path.Combine(OutputDirectory, $"v2_extraction_summary_{timestamp}.md");
                File.WriteAllText(reportPath, summary.ToString(), Encoding.UTF8);
                Console.WriteLine($"Saved V2 extraction summary to: {reportPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating V2 summary: {ex.Message}");
            }
        }

        // Helper method to count fields in JSON
        private static int CountJsonFields(JsonNode node)
        {
            if (node == null) return 0;

            int count = 0;

            if (node is JsonObject obj)
            {
                foreach (var kvp in obj)
                {
                    count++; // Count this field
                    count += CountJsonFields(kvp.Value); // Recursively count nested fields
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    count += CountJsonFields(item);
                }
            }
            else if (node is JsonValue)
            {
                count = 1; // Leaf node
            }

            return count;
        }

        // Analyze extracted JSON to identify fields not in base schema
        private static async Task AnalyzeSchemaExtensions(string extractedJson, string timestamp)
        {
            try
            {
                Console.WriteLine("\n=== Analyzing Schema Extensions ===");

                // Load base schema
                string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema.json");
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
        private static string ExtractModelTextFromJson(JsonElement completion)
        {
            // Helper: recursively search JsonElement for JSON-like string (contains '{' or '[')
            string? FindJsonString(JsonElement el)
            {
                try
                {
                    switch (el.ValueKind)
                    {
                        case JsonValueKind.String:
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && (s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("[")))
                            {
                                return s;
                            }
                            return null;

                        case JsonValueKind.Object:
                            // Prefer properties named Text/content/Content
                            foreach (var prop in el.EnumerateObject())
                            {
                                var name = prop.Name ?? string.Empty;
                                if (string.Equals(name, "Text", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(name, "Content", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase))
                                {
                                    var res = FindJsonString(prop.Value);
                                    if (!string.IsNullOrWhiteSpace(res)) return res;
                                }
                            }

                            // Otherwise search all properties
                            foreach (var prop in el.EnumerateObject())
                            {
                                var res = FindJsonString(prop.Value);
                                if (!string.IsNullOrWhiteSpace(res)) return res;
                            }
                            return null;

                        case JsonValueKind.Array:
                            foreach (var item in el.EnumerateArray())
                            {
                                var res = FindJsonString(item);
                                if (!string.IsNullOrWhiteSpace(res)) return res;
                            }
                            return null;

                        default:
                            return null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            // Try targeted spots first: Value or value wrapper
            if (completion.ValueKind == JsonValueKind.Object && (completion.TryGetProperty("Value", out var v) || completion.TryGetProperty("value", out v)))
            {
                var found = FindJsonString(v);
                if (!string.IsNullOrWhiteSpace(found)) return found;
            }

            // Try Content/choices top-level
            if (completion.ValueKind == JsonValueKind.Object)
            {
                if (completion.TryGetProperty("Content", out var c) || completion.TryGetProperty("content", out c))
                {
                    var f = FindJsonString(c);
                    if (!string.IsNullOrWhiteSpace(f)) return f;
                }

                if (completion.TryGetProperty("Choices", out var ch) || completion.TryGetProperty("choices", out ch))
                {
                    var f = FindJsonString(ch);
                    if (!string.IsNullOrWhiteSpace(f)) return f;
                }
            }

            // Fallback: search entire element tree for first JSON-like string
            var fallback = FindJsonString(completion);
            if (!string.IsNullOrWhiteSpace(fallback)) return fallback;

            // Last resort: try to extract strings from common nested patterns (Value->Content[0].Text)
            try
            {
                if (completion.ValueKind == JsonValueKind.Object && completion.TryGetProperty("Value", out var maybeVal))
                {
                    if (maybeVal.ValueKind == JsonValueKind.Object && (maybeVal.TryGetProperty("Content", out var mContent) || maybeVal.TryGetProperty("content", out mContent)))
                    {
                        if (mContent.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var part in mContent.EnumerateArray())
                            {
                                if (part.ValueKind == JsonValueKind.Object && (part.TryGetProperty("Text", out var tp) || part.TryGetProperty("text", out tp)))
                                {
                                    var s = tp.GetString();
                                    if (!string.IsNullOrWhiteSpace(s)) return s;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        // Create chat completion options
        private static ChatCompletionOptions CreateChatOptions()
        {
            return new ChatCompletionOptions
            {
                Temperature = ModelConfig.Temperature,
                MaxOutputTokenCount = ModelConfig.MaxTokens,
                TopP = ModelConfig.TopP,
                FrequencyPenalty = ModelConfig.FrequencyPenalty,
                PresencePenalty = ModelConfig.PresencePenalty,
            };
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

        // Read normalization maximum temperature from config or environment, default 0.2f
        private static float GetNormalizationMaxTemperature()
        {
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables();
                var config = builder.Build();

                // Environment variable override
                var env = Environment.GetEnvironmentVariable("NORMALIZATION_MAX_TEMPERATURE");
                if (!string.IsNullOrWhiteSpace(env) && float.TryParse(env, out var envVal))
                {
                    return envVal;
                }

                var cfgVal = config["Normalization:MaxTemperature"];
                if (!string.IsNullOrWhiteSpace(cfgVal) && float.TryParse(cfgVal, out var parsed))
                {
                    return parsed;
                }
            }
            catch { }

            return 0.2f; // default
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

