using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Collections;
using System.Linq;
using System.Net;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.IO;
using AzureTextReader.Configuration;
using AzureTextReader.Models;
using AzureTextReader.Services.Ocr;

namespace AzureTextReader.Services
{
    internal static class AzureLLMService
    {
        private const string OutputDirectory = "OutputFiles";

        // Configure which model to use - easy to switch!
        private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;

        // Renamed to avoid duplicate entry point in the project
        private static async Task RunExampleAsync()
        {
            // Create OutputFiles directory if it doesn't exist
            Directory.CreateDirectory(OutputDirectory);
            Console.WriteLine($"Output directory created/verified: {Path.Combine(Directory.GetCurrentDirectory(), OutputDirectory)}");

            using var memoryCache = new MemoryCache(new MemoryCacheOptions());
            await RunAsync(memoryCache);
        }

        // Asynchronous method to process all image URLs (now reads processed OCR JSON files)
        private static async Task RunAsync(IMemoryCache memoryCache)
        {
            Console.WriteLine("\n=== AzureTextReader - OCR Processing (from processed files) ===\n");

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

            // Determine processed folder (use default from FileMonitorOptions if available)
            var processedFolder = "processed"; // default
            try
            {
                // If configuration file contains FileMonitor section, try to read processed folder
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
                var configuration = builder.Build();
                var fmSection = configuration.GetSection("FileMonitor");
                var pf = fmSection?["ProcessedFolder"];
                if (!string.IsNullOrWhiteSpace(pf)) processedFolder = pf;
            }
            catch { /* ignore and use default */ }

            Console.WriteLine($"Looking for OCR output JSON files in folder: {processedFolder}");

            // Step 1: Read all processed JSON files and build combined markdown
            var allOcrResults = new List<Ocr.OcrResult>();
            var combinedMarkdown = new StringBuilder();

            if (Directory.Exists(processedFolder))
            {
                var jsonFiles = Directory.GetFiles(processedFolder, "*.json").OrderBy(f => f).ToArray();

                if (jsonFiles.Length == 0)
                {
                    Console.WriteLine("No processed OCR JSON files found. Exiting.");
                    return;
                }

                foreach (var jf in jsonFiles)
                {
                    Console.WriteLine($"\n=== Reading processed file: {jf} ===");
                    try
                    {
                        var txt = File.ReadAllText(jf, Encoding.UTF8);
                        var doc = JsonSerializer.Deserialize<JsonElement>(txt);

                        string imageUrl = doc.TryGetProperty("ImageUrl", out var iu) ? iu.GetString() ?? Path.GetFileName(jf) : Path.GetFileName(jf);
                        string engine = doc.TryGetProperty("Engine", out var en) ? en.GetString() ?? "unknown" : "unknown";
                        string markdown = doc.TryGetProperty("Markdown", out var md) ? md.GetString() ?? string.Empty : string.Empty;
                        string plain = doc.TryGetProperty("PlainText", out var pt) ? pt.GetString() ?? string.Empty : string.Empty;

                        var ocrResult = new Ocr.OcrResult
                        {
                            ImageUrl = imageUrl,
                            Markdown = string.IsNullOrWhiteSpace(markdown) ? string.IsNullOrWhiteSpace(plain) ? string.Empty : ""  : markdown
                        };

                        // Prefer Markdown if present, otherwise construct a simple markdown from plain text
                        if (string.IsNullOrWhiteSpace(ocrResult.Markdown) && !string.IsNullOrWhiteSpace(plain))
                        {
                            ocrResult.Markdown = "# Extracted Text\n\n" + plain;
                        }

                        allOcrResults.Add(new Ocr.OcrResult
                        {
                            ImageUrl = imageUrl,
                            Markdown = ocrResult.Markdown
                        });

                        combinedMarkdown.AppendLine($"### Document: {Path.GetFileName(imageUrl)}");
                        combinedMarkdown.AppendLine($"**OCR Engine**: {engine}");
                        combinedMarkdown.AppendLine();
                        combinedMarkdown.AppendLine(ocrResult.Markdown ?? string.Empty);
                        combinedMarkdown.AppendLine("\n---\n");

                        Console.WriteLine($"Loaded processed OCR content from: {jf}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to read/parse {jf}: {ex.Message}");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Processed folder does not exist: {processedFolder}. Exiting.");
                return;
            }

            if (allOcrResults.Count == 0)
            {
                Console.WriteLine("\n? No OCR data extracted from processed files. Exiting.");
                return;
            }

            // Step 2: Save combined OCR results to OutputFiles directory
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var engineName = ocrConfig.Engine.ToLowerInvariant();
            var combinedOcrPath = Path.Combine(OutputDirectory, $"combined_ocr_results_{engineName}_{timestamp}.md");
            File.WriteAllText(combinedOcrPath, combinedMarkdown.ToString(), Encoding.UTF8);
            Console.WriteLine($"\n? Saved combined OCR results to: {combinedOcrPath}");

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

            // Initialize the LocalAzureOpenAIClient with secure credentials (use local wrapper types)
            var credential = new LocalApiKeyCredential(config.SubscriptionKey);
            var azureClient = new LocalAzureOpenAIClient(new Uri(config.Endpoint), credential);

            // Initialize the LocalChatClient with the specified deployment name
            LocalChatClient chatClient = azureClient.GetChatClient(ModelConfig.DeploymentName);

            // Process with BOTH versions for comparison
            Console.WriteLine("\n--- Version 1: Schema-Based Extraction ---");
            await ProcessVersion1(memoryCache, chatClient, combinedMarkdown, timestamp);

            Console.WriteLine("\n--- Version 2: Dynamic Extraction (No Schema) ---");
            await ProcessVersion2(memoryCache, chatClient, combinedMarkdown, timestamp);
        }

        // VERSION 1: Schema-based extraction (existing logic)
        private static async Task ProcessVersion1(IMemoryCache memoryCache, LocalChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                // Load JSON schema
                string schemaText = File.ReadAllText("E:\\Sudhir\\Prj\\files\\zip\\src\\invoice_schema.json");
                JsonNode jsonSchema = JsonNode.Parse(schemaText);

                // Initialize PromptService
                Console.WriteLine("Building V1 prompt from templates (with schema)...");
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

                Console.WriteLine($"V1 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

                // Create messages using built prompt (use simple objects so our local client can reflect over them)
                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
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
                    // Create the chat completion request (returns our minimal wrapper)
                    LocalChatCompletion completion = chatClient.CompleteChat(messages, options);

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
        private static async Task ProcessVersion2(IMemoryCache memoryCache, LocalChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                // Initialize PromptService for V2
                Console.WriteLine("Building V2 prompt from templates (dynamic, no schema)...");
                var promptService = new PromptService(memoryCache);

                // Build V2 prompt - NO schema, NO examples, NO rules - pure dynamic extraction
                var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                {
                    TemplateType = "deed_extraction",
                    Version = "v2",
                    IncludeExamples = false,  // No examples for dynamic extraction
                    IncludeRules = false,     // No rules, let AI figure it out
                    RuleNames = new List<string>(),
                    SchemaJson = "",     // NO SCHEMA!
                    SourceData = combinedMarkdown,
                    UserMessageTemplate = "Analyze the documents below and extract ALL relevant information dynamically. Focus on:\n" +
                                         "- Buyer information (names, addresses, percentages, details)\n" +
                                         "- Seller information (old owners)\n" +
                                         "- Property details (address, legal description, parcel info)\n" +
                                         "- Land records information\n" +
                                         "- Transaction details\n" +
                                         "- PCOR information (if present)\n\n" +
                                         "Return a comprehensive JSON with all findings.\n\n" +
                                         $"DOCUMENTS:\n\n{combinedMarkdown}"
                });

                Console.WriteLine($"V2 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

                // Create messages using built prompt (use simple objects so our local client can reflect over them)
                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                // Use configured options from ModelConfig
                var options = CreateChatOptions();

                // Compute a cache key for V2
                var cacheKey = ComputeCacheKey(messages, options, "v2_dynamic_documents");

                // Try to pull cached response
                if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    Console.WriteLine($"Cache hit for V2 ChatCompletion - using cached response.");
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v2_dynamic");
                }
                else
                {
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat for V2...");
                    // Create the chat completion request (returns our minimal wrapper)
                    LocalChatCompletion completion = chatClient.CompleteChat(messages, options);

                    // Serialize the response to JSON for consistent caching / logging
                    var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true });

                    // Cache the serialized completion
                    var cacheEntryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                        SlidingExpiration = TimeSpan.FromHours(24)
                    };

                    memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);
                    Console.WriteLine($"Cached V2 ChatCompletion result");

                    // Extract and save the final cleaned JSON
                    await SaveFinalCleanedJson(completionJson, timestamp, "_v2_dynamic");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during V2 ChatCompletion: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        // Save the final cleaned JSON output with version suffix
        private static async Task SaveFinalCleanedJson(string completionJson, string timestamp, string versionSuffix = "")
        {
            try
            {
                // Deserialize to get the completion object
                var completion = JsonSerializer.Deserialize<JsonElement>(completionJson);

                // Extract model text from the completion (attempt to handle both our wrapper and OpenAI style)
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
        private static string ComputeCacheKey(IList<object> messages, LocalChatCompletionOptions options, string identifier)
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

        // Create chat completion options
        private static LocalChatCompletionOptions CreateChatOptions()
        {
            return new LocalChatCompletionOptions
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
                    char close = open == '{' ? '}' : ']';
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

