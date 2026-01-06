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
using System.Text.RegularExpressions;

namespace AzureTextReader.Services
{
    internal static class AzureLLMService
    {
        private const string OutputDirectory = "OutputFiles";

        // Configure which model to use - easy to switch!
        private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;

        // Locate the invoice schema path using appsettings.json (Schema:InvoiceSchemaPath) or environment, fallback to common locations
        private static string LocateSchemaPath()
        {
            var tried = new List<string>();
            try
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddEnvironmentVariables();
                var configuration = builder.Build();

                // Prefer explicit configuration key
                var configured = configuration["Schema:InvoiceSchemaPath"] ?? configuration["InvoiceSchemaPath"] ?? Environment.GetEnvironmentVariable("INVOICE_SCHEMA_PATH");
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    // If absolute and exists, return
                    if (Path.IsPathRooted(configured))
                    {
                        tried.Add(Path.GetFullPath(configured));
                        if (File.Exists(configured)) return configured;
                    }
                    else
                    {
                        // Try several bases for relative configured path
                        var relCandidates = new[] {
                            Path.Combine(Directory.GetCurrentDirectory(), configured),
                            Path.Combine(AppContext.BaseDirectory ?? string.Empty, configured),
                            Path.Combine(AppContext.BaseDirectory ?? string.Empty, "..", "..", "..", configured),
                            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", configured),
                            Path.Combine(Directory.GetCurrentDirectory(), "src", configured),
                            Path.Combine(Directory.GetCurrentDirectory(), "src", "Schemas", configured)
                        };

                        foreach (var p in relCandidates)
                        {
                            var full = Path.GetFullPath(p);
                            tried.Add(full);
                            if (File.Exists(full)) return full;
                        }
                    }
                }
            }
            catch
            {
                // ignore config errors but record attempted base
            }

            // fallback locations to search
            var candidatePaths = new[] {
                // new preferred location
                Path.Combine(Directory.GetCurrentDirectory(), "src", "Schemas", "invoice_schema.json"),

                // previous locations (kept for backward compatibility)
                Path.Combine(Directory.GetCurrentDirectory(), "src", "invoice_schema.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "invoice_schema.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "invoice_schema.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "Schemas", "invoice_schema.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "..", "..", "..", "src", "invoice_schema.json"),
                Path.Combine(AppContext.BaseDirectory ?? string.Empty, "..", "..", "..", "src", "Schemas", "invoice_schema.json")
            };

            foreach (var p in candidatePaths)
            {
                var full = Path.GetFullPath(p);
                tried.Add(full);
                if (File.Exists(full)) return full;
            }

            // nothing found - throw with helpful message
            throw new FileNotFoundException($"invoice_schema.json not found. Searched paths: {string.Join(";", tried.Distinct())}");
        }

        // Public entry point so the background worker can invoke LLM processing after OCR
        public static async Task InvokeAfterOcrAsync(IMemoryCache memoryCache)
        {
            if (memoryCache == null) throw new ArgumentNullException(nameof(memoryCache));
            await RunAsync(memoryCache);
        }

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
                var allJsonFiles = Directory.GetFiles(processedFolder, "*.json");

                if (allJsonFiles.Length == 0)
                {
                    Console.WriteLine("No processed OCR JSON files found. Exiting.");
                    return;
                }

                // Prefer merged/combined JSON if present, otherwise use the most recently written JSON file
                var mergedCandidates = allJsonFiles
                    .Where(f => Path.GetFileName(f).IndexOf("merged", StringComparison.OrdinalIgnoreCase) >= 0
                             || Path.GetFileName(f).IndexOf("combined", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .ToArray();

                string[] jsonFilesToProcess;
                if (mergedCandidates.Length > 0)
                {
                    jsonFilesToProcess = new[] { mergedCandidates[0] };
                    Console.WriteLine($"Found merged/combined JSON candidate to process: {Path.GetFileName(mergedCandidates[0])}");
                }
                else
                {
                    // Fall back to newest file overall
                    var newest = allJsonFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
                    jsonFilesToProcess = new[] { newest };
                    Console.WriteLine($"No merged file found - will process newest JSON: {Path.GetFileName(newest)}");
                }

                foreach (var jf in jsonFilesToProcess)
                {
                    Console.WriteLine($"\n=== Reading processed file: {jf} ===");
                    try
                    {
                        var txt = File.ReadAllText(jf, Encoding.UTF8);

                        string imageUrl = Path.GetFileName(jf);
                        string engine = "unknown";
                        string resolvedMarkdown = string.Empty;
                        bool resolvedHasPerFileHeaders = false; // track if resolvedMarkdown already includes per-file headers

                        try
                        {
                            // Try parse as JSON first
                            var doc = JsonSerializer.Deserialize<JsonElement>(txt);

                            // Prefer explicit image/url/engine when available (case-insensitive)
                            //if (!TryGetStringPropIgnoreCase(doc, "ImageUrl", out var iuVal) || string.IsNullOrWhiteSpace(iuVal))
                            //{
                            //    imageUrl = Path.GetFileName(jf);
                            //}
                            //else imageUrl = iuVal;

                            //if (!TryGetStringPropIgnoreCase(doc, "Engine", out var engineVal) || string.IsNullOrWhiteSpace(engineVal))
                            //{
                            //    engine = "unknown";
                            //}
                            //else engine = engineVal;


                            if (doc.TryGetProperty("Files", out var filesElem) && filesElem.ValueKind == JsonValueKind.Array)
                            {
                                var mdParts = new List<string>();
                                var plainParts = new List<string>();
                                var engines = new List<string>();

                                foreach (var item in filesElem.EnumerateArray())
                                {
                                    if (item.ValueKind != JsonValueKind.Object) continue;

                                    // Per-item metadata
                                    string itemFile = TryGetStringPropIgnoreCase(item, "File", out var itf) && !string.IsNullOrWhiteSpace(itf) ? itf : "(unknown)";
                                    string itemEngine = TryGetStringPropIgnoreCase(item, "Engine", out var ite) && !string.IsNullOrWhiteSpace(ite) ? ite : "unknown";
                                    engines.Add(itemEngine);

                                    // Extract Markdown and PlainText (case-insensitive)
                                    string itemMarkdown = null;
                                    string itemPlain = null;
                                    if (TryGetStringPropIgnoreCase(item, "Markdown", out var imdPropStr) && !string.IsNullOrWhiteSpace(imdPropStr))
                                    {
                                        itemMarkdown = imdPropStr;
                                    }
                                    if (TryGetStringPropIgnoreCase(item, "PlainText", out var iptPropStr) && !string.IsNullOrWhiteSpace(iptPropStr))
                                    {
                                        itemPlain = iptPropStr;
                                    }

                                    // Use Markdown when available, otherwise convert PlainText to simple markdown
                                    if (!string.IsNullOrWhiteSpace(itemMarkdown))
                                    {
                                        var partSb = new StringBuilder();
                                        partSb.AppendLine($"### Document: {itemFile}");
                                        partSb.AppendLine($"**OCR Engine**: {itemEngine}");
                                        partSb.AppendLine();
                                        partSb.AppendLine(itemMarkdown);
                                        mdParts.Add(partSb.ToString());
                                    }
                                    else if (!string.IsNullOrWhiteSpace(itemPlain))
                                    {
                                        var partSb = new StringBuilder();
                                        partSb.AppendLine($"### Document: {itemFile}");
                                        partSb.AppendLine($"**OCR Engine**: {itemEngine}");
                                        partSb.AppendLine();
                                        partSb.AppendLine("# Extracted Text\n\n" + itemPlain);
                                        mdParts.Add(partSb.ToString());
                                    }

                                    // Always collect plain text (prefer PlainText, fallback to Markdown)
                                    if (!string.IsNullOrWhiteSpace(itemPlain)) plainParts.Add(itemPlain);
                                    else if (!string.IsNullOrWhiteSpace(itemMarkdown)) plainParts.Add(itemMarkdown);
                                }

                                if (mdParts.Count > 0)
                                {
                                    // Build merged markdown and plain text
                                    resolvedMarkdown = string.Join("\n\n---\n\n", mdParts);
                                    var mergedPlain = string.Join("\n\n---\n\n", plainParts);

                                    // Optionally expose merged fields via resolvedHasPerFileHeaders flag
                                    resolvedHasPerFileHeaders = true;
                                    imageUrl = Path.GetFileName(jf);

                                    // For debugging and future use, append merged plain and engines info to resolvedMarkdown as a block comment
                                    // (kept minimal to avoid polluting model input) -- include MergedEngines line
                                    if (engines.Count > 0)
                                    {
                                        resolvedMarkdown = resolvedMarkdown + "\n\n" + "**MergedEngines**: [" + string.Join(", ", engines) + "]";
                                    }
                                }
                            }

                            else if (TryGetStringPropIgnoreCase(doc, "Markdown", out var mdTopStr) && !string.IsNullOrWhiteSpace(mdTopStr))
                            {
                                resolvedMarkdown = mdTopStr;
                            }
                            else if (TryGetStringPropIgnoreCase(doc, "PlainText", out var ptTopStr) && !string.IsNullOrWhiteSpace(ptTopStr))
                            {
                                resolvedMarkdown = "# Extracted Text\n\n" + ptTopStr;
                            }
                            else if (TryGetStringPropIgnoreCase(doc, "MergedMarkdown", out var mmStr) && !string.IsNullOrWhiteSpace(mmStr))
                            {
                                resolvedMarkdown = mmStr;
                            }
                            else if (TryGetStringPropIgnoreCase(doc, "MergedPlainText", out var mpStr) && !string.IsNullOrWhiteSpace(mpStr))
                            {
                                resolvedMarkdown = "# Extracted Text\n\n" + mpStr;
                            }
                            else
                            {
                                // No recognized properties - leave resolvedMarkdown empty
                                resolvedMarkdown = string.Empty;
                            }
                        }
                        catch (JsonException)
                        {
                            // File is not valid JSON (may contain raw markdown). Treat entire file as markdown content.
                            resolvedMarkdown = txt ?? string.Empty;
                            imageUrl = Path.GetFileName(jf);
                            engine = "raw";
                        }

                        // Add to results and combined markdown
                        allOcrResults.Add(new Ocr.OcrResult
                        {
                            ImageUrl = imageUrl,
                            Markdown = resolvedMarkdown
                        });

                        // If resolvedMarkdown already contains per-file headers (from Files array), don't prepend another top-level header
                        if (resolvedHasPerFileHeaders)
                        {
                            combinedMarkdown.AppendLine(resolvedMarkdown ?? string.Empty);
                        }
                        else
                        {
                            combinedMarkdown.AppendLine($"### Document: {Path.GetFileName(imageUrl)}");
                            combinedMarkdown.AppendLine($"**OCR Engine**: {engine}");
                            combinedMarkdown.AppendLine();
                            combinedMarkdown.AppendLine(resolvedMarkdown ?? string.Empty);
                            combinedMarkdown.AppendLine("\n---\n");
                        }

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
            Directory.CreateDirectory(OutputDirectory);
            var combinedOcrPath = Path.Combine(OutputDirectory, $"combined_ocr_results_{engineName}_{timestamp}.md");
            File.WriteAllText(combinedOcrPath, combinedMarkdown.ToString(), Encoding.UTF8);
            Console.WriteLine($"\n? Saved combined OCR results to: {combinedOcrPath}");

            // Generate comprehensive superset exports for Deed and PCOR to retain all OCR-extracted fields
            try
            {
                GenerateSupersetExports(combinedMarkdown.ToString(), timestamp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating superset exports: {ex.Message}");
            }

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

            Console.WriteLine("\n--- Version 3: Grant Deed Focused Extraction ---");
            await ProcessVersion3(memoryCache, chatClient, combinedMarkdown, timestamp);
        }

        // VERSION 1: Schema-based extraction (existing logic)
        private static async Task ProcessVersion1(IMemoryCache memoryCache, LocalChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                // Load JSON schema (locate relative to current working directory or app base directory)
                var schemaPath = LocateSchemaPath();
                string schemaText = File.ReadAllText(schemaPath);
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
                Console.WriteLine("UserMessage preview (first 1000 chars):\n" + (builtPrompt.UserMessage?.Length > 1000 ? builtPrompt.UserMessage.Substring(0, 1000) : builtPrompt.UserMessage ?? "<null>"));

                // Create messages using built prompt (use simple objects so our local client can reflect over them)
                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                // Use configured options from ModelConfig
                var options = CreateChatOptions();

                // Serialize messages for diagnostic logging to confirm user content
                try
                {
                    var msgsJson = JsonSerializer.Serialize(messages);
                    Console.WriteLine("Prepared messages for CompleteChat: " + msgsJson.Substring(0, Math.Min(2000, msgsJson.Length)));
                }
                catch { }

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
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat for V1... (user message length: {builtPrompt.UserMessage?.Length})");
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
                    SourceData = combinedMarkdown, // Pass combined markdown as SourceData
                    UserMessageTemplate = "Analyze the documents below and extract ALL relevant information dynamically. " +
                                         "You MUST use the entire input (all documents/pages/sections) and include all relevant data.\n\n" +
                                         "Focus on:\n" +
                                         "- Buyer information (names, addresses, percentages, details)\n" +
                                         "- Seller information (old owners)\n" +
                                         "- Property details (address, legal description, parcel info)\n" +
                                         "- Land records information\n" +
                                         "- Transaction details\n" +
                                         "- PCOR information (if present)\n\n" +
                                         "Return a comprehensive JSON with all findings.\n\n" +
                                         "DOCUMENTS:\n\n{{SOURCE_DATA}}"
                });

                Console.WriteLine($"V2 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");
                Console.WriteLine("UserMessage preview (first 1000 chars):\n" + (builtPrompt.UserMessage?.Length > 1000 ? builtPrompt.UserMessage.Substring(0, 1000) : builtPrompt.UserMessage ?? "<null>"));

                // Create messages using built prompt (use simple objects so our local client can reflect over them)
                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                // Use configured options from ModelConfig
                var options = CreateChatOptions();

                // Serialize messages for diagnostic logging to confirm user content
                try
                {
                    var msgsJson = JsonSerializer.Serialize(messages);
                    Console.WriteLine("Prepared messages for CompleteChat (V2): " + msgsJson.Substring(0, Math.Min(2000, msgsJson.Length)));
                }
                catch { }

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
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat for V2... (user message length: {builtPrompt.UserMessage?.Length})");
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

        // VERSION 3: Grant Deed-focused extraction using document_extraction_v3.txt
        private static async Task ProcessVersion3(IMemoryCache memoryCache, LocalChatClient chatClient, string combinedMarkdown, string timestamp)
        {
            try
            {
                Console.WriteLine("Building V3 prompt from templates (Grant Deed focused)...");
                var promptService = new PromptService(memoryCache);

                // V3 uses the system prompt in Prompts/SystemPrompts/document_extraction_v3.txt
                // and references schema from the new Schemas folder (for consistency, even if not directly embedded by the v3 prompt).
                string schemaJson = string.Empty;
                try
                {
                    var schemaPath = LocateSchemaPath();
                    schemaJson = await File.ReadAllTextAsync(schemaPath);
                }
                catch
                {
                    schemaJson = string.Empty;
                }

                var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                {
                    TemplateType = "document_extraction",
                    Version = "v3",
                    IncludeExamples = false,
                    IncludeRules = false,
                    RuleNames = new List<string>(),
                    SchemaJson = schemaJson,
                    SourceData = combinedMarkdown,
                    UserMessageTemplate = "DOCUMENTS:\n\n{{SOURCE_DATA}}"
                });

                Console.WriteLine($"V3 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                var options = CreateChatOptions();
                var cacheKey = ComputeCacheKey(messages, options, "v3_grant_deed_documents");

                if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    Console.WriteLine("Cache hit for V3 ChatCompletion - using cached response.");
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v3_grant_deed");
                }
                else
                {
                    Console.WriteLine($"Cache miss - Calling ChatClient.CompleteChat for V3... (user message length: {builtPrompt.UserMessage?.Length})");
                    LocalChatCompletion completion = chatClient.CompleteChat(messages, options);
                    var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions() { WriteIndented = true });

                    var cacheEntryOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                        SlidingExpiration = TimeSpan.FromHours(24)
                    };

                    memoryCache.Set(cacheKey, completionJson, cacheEntryOptions);
                    Console.WriteLine("Cached V3 ChatCompletion result");

                    await SaveFinalCleanedJson(completionJson, timestamp, "_v3_grant_deed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during V3 ChatCompletion: {ex.Message}");
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
                summary.AppendLine("## Statistics");
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
                var schemaPath = LocateSchemaPath();
                var baseSchema = JsonNode.Parse(File.ReadAllText(schemaPath));
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

        // Helper method to try get string property value from JsonElement, case-insensitively
        private static bool TryGetStringPropIgnoreCase(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        value = prop.Value.GetString() ?? string.Empty;
                    }
                    return true;
                }
            }
            return false;
        }

        // Helper method to try get string property value from JsonNode, case-insensitively
        private static bool TryGetStringPropIgnoreCase(JsonNode node, string propertyName, out string value)
        {
            value = string.Empty;
            if (node is JsonObject obj)
            {
                // Case-insensitive search through properties
                foreach (var kvp in obj)
                {
                    if (!string.Equals(kvp.Key, propertyName, StringComparison.OrdinalIgnoreCase)) continue;

                    var v = kvp.Value;
                    if (v == null)
                    {
                        value = string.Empty;
                        return true;
                    }

                    if (v is JsonValue jv)
                    {
                        try
                        {
                            var s = jv.GetValue<string>();
                            value = s ?? string.Empty;
                            return true;
                        }
                        catch
                        {
                            value = jv.ToString() ?? string.Empty;
                            return true;
                        }
                    }

                    // Fallback to ToString for objects/arrays
                    value = v.ToString() ?? string.Empty;
                    return true;
                }
            }
            return false;
        }

        // Build comprehensive superset JSON outputs for Deed and PCOR from the combined markdown
        private static void GenerateSupersetExports(string combinedMarkdown, string timestamp)
        {
            if (string.IsNullOrWhiteSpace(combinedMarkdown)) return;

            // Split into document blocks using the same separator used earlier
            var blocks = combinedMarkdown.Split(new[] { "\n\n---\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim()).Where(b => !string.IsNullOrWhiteSpace(b)).ToArray();

            var deedDocs = new List<JsonObject>();
            var pcorDocs = new List<JsonObject>();
            var otherDocs = new List<JsonObject>();

            foreach (var block in blocks)
            {
                var jo = ExtractFieldsFromBlock(block);

                // Classify by presence of keywords
                var blockLower = block.ToLowerInvariant();
                if (blockLower.Contains("grant deed") || blockLower.Contains("grantor") || blockLower.Contains("grantee") || blockLower.Contains("legal description"))
                {
                    deedDocs.Add(jo);
                }
                else if (blockLower.Contains("preliminary change of ownership report") || blockLower.Contains("pcor") || blockLower.Contains("assessor"))
                {
                    pcorDocs.Add(jo);
                }
                else
                {
                    otherDocs.Add(jo);
                }
            }

            // Build superset schemas by aggregating discovered field names
            var deedSuperset = BuildSupersetDocument(deedDocs, "deed");
            var pcorSuperset = BuildSupersetDocument(pcorDocs, "pcor");

            // Always include otherDocs under 'other' for retention
            var otherSuperset = BuildSupersetDocument(otherDocs, "other");

            // Write JSON outputs
            var options = new JsonSerializerOptions { WriteIndented = true };
            var deedPath = Path.Combine(OutputDirectory, $"deed_superset_{timestamp}.json");
            File.WriteAllText(deedPath, JsonSerializer.Serialize(deedSuperset, options), Encoding.UTF8);
            Console.WriteLine($"Saved Deed superset to: {deedPath}");

            var pcorPath = Path.Combine(OutputDirectory, $"pcor_superset_{timestamp}.json");
            File.WriteAllText(pcorPath, JsonSerializer.Serialize(pcorSuperset, options), Encoding.UTF8);
            Console.WriteLine($"Saved PCOR superset to: {pcorPath}");

            var otherPath = Path.Combine(OutputDirectory, $"other_superset_{timestamp}.json");
            File.WriteAllText(otherPath, JsonSerializer.Serialize(otherSuperset, options), Encoding.UTF8);
            Console.WriteLine($"Saved Other superset to: {otherPath}");

            // Save a short README documenting the superset schema and extension process
            var docSb = new StringBuilder();
            docSb.AppendLine("# Superset Schema Documentation");
            docSb.AppendLine();
            docSb.AppendLine("This document describes the superset schema generated from OCR combined markdown.");
            docSb.AppendLine();
            docSb.AppendLine("Files:");
            docSb.AppendLine($"- {Path.GetFileName(deedPath)} (Deed superset)");
            docSb.AppendLine($"- {Path.GetFileName(pcorPath)} (PCOR superset)");
            docSb.AppendLine($"- {Path.GetFileName(otherPath)} (Other documents)");
            docSb.AppendLine();
            docSb.AppendLine("Schema notes:");
            docSb.AppendLine("- Each superset contains:\n  - documents: array of per-block extracted data (file title, engine, raw markdown, plainText, keyValuePairs, dates, amounts, apn, phones, emails)\n  - aggregatedFields: dictionary of discovered simple keys and their collected values\n  - schemaFields: list of all discovered simple keys (union)\n");
            docSb.AppendLine();
            docSb.AppendLine("Extending schema:");
            docSb.AppendLine("- New fields discovered in future scans will be added automatically to schemaFields and aggregatedFields. To permanently add a new typed field to downstream consumers, update the consumer code to reference that key.");

            File.WriteAllText(Path.Combine(OutputDirectory, $"superset_readme_{timestamp}.md"), docSb.ToString(), Encoding.UTF8);
        }

        private static JsonObject BuildSupersetDocument(List<JsonObject> docs, string docType)
        {
            var root = new JsonObject();
            root["documentType"] = docType;
            var arr = new JsonArray();
            var aggregated = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in docs)
            {
                arr.Add(d);

                // collect keyValuePairs if any
                if (d.TryGetPropertyValue("keyValuePairs", out var kvNode) && kvNode is JsonObject kvObj)
                {
                    foreach (var kv in kvObj)
                    {
                        var k = kv.Key;
                        var v = kv.Value?.ToString() ?? string.Empty;
                        if (!aggregated.TryGetValue(k, out var list)) { list = new List<string>(); aggregated[k] = list; }
                        if (!string.IsNullOrWhiteSpace(v) && !list.Contains(v)) list.Add(v);
                    }
                }
            }

            root["documents"] = arr;

            var aggObj = new JsonObject();
            foreach (var kv in aggregated)
            {
                var ja = new JsonArray();
                foreach (var v in kv.Value) ja.Add(v);
                aggObj[kv.Key] = ja;
            }

            root["aggregatedFields"] = aggObj;
            var schemaArr = new JsonArray();
            foreach (var k in aggregated.Keys) schemaArr.Add(k);
            root["schemaFields"] = schemaArr;
            return root;
        }

        // Extract many heuristics from a block of markdown/plain text
        private static JsonObject ExtractFieldsFromBlock(string block)
        {
            var jo = new JsonObject();
            jo["rawText"] = block;

            // Attempt to split title/engine if present in first lines
            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
            if (lines.Length > 0)
            {
                jo["titleLine"] = lines[0];
                if (lines.Length > 1 && lines[1].StartsWith("**OCR Engine**", StringComparison.OrdinalIgnoreCase))
                {
                    // lines[1] like "**OCR Engine**: Aspose.Total OCR"
                    var idx = lines[1].IndexOf(':');
                    if (idx >= 0) jo["engine"] = lines[1].Substring(idx + 1).Trim();
                }
            }

            // Build keyValuePairs from lines containing ':' reasonably
            var kv = new JsonObject();
            foreach (var l in lines)
            {
                var parts = l.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    if (!string.IsNullOrWhiteSpace(key)) kv[key] = val;
                }
            }
            jo["keyValuePairs"] = kv;

            // Simple pattern extracts
            var plain = StripMarkdown(block);
            jo["plainText"] = plain;

            var dates = new JsonArray();
            foreach (Match m in System.Text.RegularExpressions.Regex.Matches(plain, @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b"))
            {
                dates.Add(m.Value);
            }
            jo["dates"] = dates;

            var amounts = new JsonArray();
            foreach (Match m in System.Text.RegularExpressions.Regex.Matches(plain, @"\$\s?[0-9\,]+(\.[0-9]{2})?")) amounts.Add(m.Value);
            jo["amounts"] = amounts;

            var apnMatch = System.Text.RegularExpressions.Regex.Match(plain, @"\b\d{1,4}[- ]?\d[- ]?\d{1,4}[- ]?\d{1,4}\b");
            jo["apn"] = apnMatch.Success ? apnMatch.Value : string.Empty;

            var phones = new JsonArray();
            foreach (Match m in System.Text.RegularExpressions.Regex.Matches(plain, @"\(?\d{3}\)?[-. ]?\d{3}[-. ]?\d{4}")) phones.Add(m.Value);
            jo["phones"] = phones;

            var emails = new JsonArray();
            foreach (Match m in System.Text.RegularExpressions.Regex.Matches(plain, @"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+")) emails.Add(m.Value);
            jo["emails"] = emails;

            return jo;
        }

        private static string StripMarkdown(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;
            // Very small markdown stripper: remove headings markers and bold/italic markers
            var s = md.Replace("###", "").Replace("**", "").Replace("__", "");
            // remove image/link formats [text](url)
            s = Regex.Replace(s, @"\[(.*?)\]\((.*?)\)", "$1");
            return s;
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

