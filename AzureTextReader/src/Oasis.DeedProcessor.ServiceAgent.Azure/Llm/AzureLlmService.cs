using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Oasis.DeedProcessor.BusinessEntities.Configuration;
using Oasis.DeedProcessor.BusinessEntities.Models;
using Oasis.DeedProcessor.BusinessEntities.Ocr;
using Oasis.DeedProcessor.Interface.Llm;
using Oasis.DeedProcessor.ServiceAgent.Azure.OpenAI;
using Oasis.DeedProcessor.ServiceAgent.Prompts;

namespace Oasis.DeedProcessor.ServiceAgent.Azure.Llm
{
    public sealed class AzureLlmService : ILlmService
    {
        private const string OutputDirectory = "OutputFiles";

        private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;

        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;

        public AzureLlmService(IMemoryCache memoryCache, IConfiguration configuration)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task InvokeAfterOcrAsync(CancellationToken cancellationToken = default) => RunAsync(cancellationToken);

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ocrConfig = OcrConfig.Load();
            ocrConfig.Validate();

            AzureAIConfig? azureConfig = null;
            if (ocrConfig.IsAzureEnabled)
            {
                azureConfig = AzureAIConfig.Load();
                azureConfig.Validate();
            }

            var processedFolder = _configuration.GetSection("FileMonitor")?["ProcessedFolder"];
            if (string.IsNullOrWhiteSpace(processedFolder)) processedFolder = "processed";

            var allOcrResults = new List<OcrResult>();
            var combinedMarkdown = new StringBuilder();

            if (!Directory.Exists(processedFolder)) return;

            var allJsonFiles = Directory.GetFiles(processedFolder, "*.json");
            if (allJsonFiles.Length == 0) return;

            var mergedCandidates = allJsonFiles
                .Where(f => Path.GetFileName(f).IndexOf("merged", StringComparison.OrdinalIgnoreCase) >= 0
                         || Path.GetFileName(f).IndexOf("combined", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                .ToArray();

            var jsonFilesToProcess = mergedCandidates.Length > 0
                ? new[] { mergedCandidates[0] }
                : new[] { allJsonFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First() };

            foreach (var jf in jsonFilesToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var txt = await File.ReadAllTextAsync(jf, Encoding.UTF8, cancellationToken);

                string imageUrl = Path.GetFileName(jf);
                string engine = "unknown";
                string resolvedMarkdown = string.Empty;
                bool resolvedHasPerFileHeaders = false;

                try
                {
                    var doc = JsonSerializer.Deserialize<JsonElement>(txt);

                    if (doc.TryGetProperty("Files", out var filesElem) && filesElem.ValueKind == JsonValueKind.Array)
                    {
                        var mdParts = new List<string>();
                        var plainParts = new List<string>();
                        var engines = new List<string>();

                        foreach (var item in filesElem.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;

                            string itemFile = TryGetStringPropIgnoreCase(item, "File", out var itf) && !string.IsNullOrWhiteSpace(itf) ? itf : "(unknown)";
                            string itemEngine = TryGetStringPropIgnoreCase(item, "Engine", out var ite) && !string.IsNullOrWhiteSpace(ite) ? ite : "unknown";
                            engines.Add(itemEngine);

                            string? itemMarkdown = null;
                            string? itemPlain = null;
                            if (TryGetStringPropIgnoreCase(item, "Markdown", out var imdPropStr) && !string.IsNullOrWhiteSpace(imdPropStr)) itemMarkdown = imdPropStr;
                            if (TryGetStringPropIgnoreCase(item, "PlainText", out var iptPropStr) && !string.IsNullOrWhiteSpace(iptPropStr)) itemPlain = iptPropStr;

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

                            if (!string.IsNullOrWhiteSpace(itemPlain)) plainParts.Add(itemPlain);
                            else if (!string.IsNullOrWhiteSpace(itemMarkdown)) plainParts.Add(itemMarkdown);
                        }

                        if (mdParts.Count > 0)
                        {
                            resolvedMarkdown = string.Join("\n\n---\n\n", mdParts);
                            resolvedHasPerFileHeaders = true;
                            imageUrl = Path.GetFileName(jf);

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
                }
                catch (JsonException)
                {
                    resolvedMarkdown = txt ?? string.Empty;
                    imageUrl = Path.GetFileName(jf);
                    engine = "raw";
                }

                allOcrResults.Add(new OcrResult { ImageUrl = imageUrl, Markdown = resolvedMarkdown });

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
            }

            if (allOcrResults.Count == 0) return;

            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var engineName = ocrConfig.Engine.ToLowerInvariant();
            Directory.CreateDirectory(OutputDirectory);

            var combinedOcrPath = Path.Combine(OutputDirectory, $"combined_ocr_results_{engineName}_{timestamp}.md");
            await File.WriteAllTextAsync(combinedOcrPath, combinedMarkdown.ToString(), Encoding.UTF8, cancellationToken);

            try { GenerateSupersetExports(combinedMarkdown.ToString(), timestamp); } catch { }

            if (azureConfig == null)
            {
                azureConfig = AzureAIConfig.Load();
                azureConfig.Validate();
            }

            await ProcessWithChatCompletion(azureConfig, combinedMarkdown.ToString(), timestamp, cancellationToken);
        }

        private async Task ProcessWithChatCompletion(AzureAIConfig config, string combinedMarkdown, string timestamp, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var credential = new LocalApiKeyCredential(config.SubscriptionKey);
            var azureClient = new LocalAzureOpenAIClient(new Uri(config.Endpoint), credential);
            var chatClient = azureClient.GetChatClient(ModelConfig.DeploymentName);

            await ProcessVersion1(chatClient, combinedMarkdown, timestamp, cancellationToken);
            await ProcessVersion2(chatClient, combinedMarkdown, timestamp, cancellationToken);
            await ProcessVersion3(chatClient, combinedMarkdown, timestamp, cancellationToken);
        }

        private async Task ProcessVersion1(LocalChatClient chatClient, string combinedMarkdown, string timestamp, CancellationToken cancellationToken)
        {
            try
            {
                var schemaPath = LocateSchemaPath();
                var schemaText = await File.ReadAllTextAsync(schemaPath, cancellationToken);
                var jsonSchema = JsonNode.Parse(schemaText);

                var promptService = new PromptService(_memoryCache);
                var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                {
                    TemplateType = "deed_extraction",
                    Version = "v1",
                    IncludeExamples = true,
                    ExampleSet = "default",
                    IncludeRules = true,
                    RuleNames = new List<string> { "percentage_calculation", "name_parsing", "date_format" },
                    SchemaJson = jsonSchema?.ToJsonString() ?? string.Empty,
                    SourceData = combinedMarkdown
                });

                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                var options = CreateChatOptions();
                var cacheKey = ComputeCacheKey(messages, options, "v1_combined_documents");

                if (_memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v1_schema", cancellationToken);
                    return;
                }

                var completion = chatClient.CompleteChat(messages, options);
                var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

                _memoryCache.Set(cacheKey, completionJson, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(24)
                });

                await SaveFinalCleanedJson(completionJson, timestamp, "_v1_schema", cancellationToken);
            }
            catch
            {
            }
        }

        private async Task ProcessVersion2(LocalChatClient chatClient, string combinedMarkdown, string timestamp, CancellationToken cancellationToken)
        {
            try
            {
                var promptService = new PromptService(_memoryCache);
                var builtPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
                {
                    TemplateType = "deed_extraction",
                    Version = "v2",
                    IncludeExamples = false,
                    IncludeRules = false,
                    RuleNames = new List<string>(),
                    SchemaJson = string.Empty,
                    SourceData = combinedMarkdown,
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

                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                var options = CreateChatOptions();
                var cacheKey = ComputeCacheKey(messages, options, "v2_dynamic_documents");

                if (_memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v2_dynamic", cancellationToken);
                    return;
                }

                var completion = chatClient.CompleteChat(messages, options);
                var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

                _memoryCache.Set(cacheKey, completionJson, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(24)
                });

                await SaveFinalCleanedJson(completionJson, timestamp, "_v2_dynamic", cancellationToken);
            }
            catch
            {
            }
        }

        private async Task ProcessVersion3(LocalChatClient chatClient, string combinedMarkdown, string timestamp, CancellationToken cancellationToken)
        {
            try
            {
                var promptService = new PromptService(_memoryCache);

                string schemaJson;
                try
                {
                    var schemaPath = LocateSchemaPath();
                    schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken);
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

                var messages = new List<object>
                {
                    new { Role = "system", Content = builtPrompt.SystemMessage },
                    new { Role = "user", Content = builtPrompt.UserMessage }
                };

                var options = CreateChatOptions();
                var cacheKey = ComputeCacheKey(messages, options, "v3_grant_deed_documents");

                if (_memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
                {
                    await SaveFinalCleanedJson(cachedJson, timestamp, "_v3_grant_deed", cancellationToken);
                    return;
                }

                var completion = chatClient.CompleteChat(messages, options);
                var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

                _memoryCache.Set(cacheKey, completionJson, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                    SlidingExpiration = TimeSpan.FromHours(24)
                });

                await SaveFinalCleanedJson(completionJson, timestamp, "_v3_grant_deed", cancellationToken);
            }
            catch
            {
            }
        }

        private static string LocateSchemaPath()
        {
            var tried = new List<string>();

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();
            var configuration = builder.Build();

            var configured = configuration["Schema:InvoiceSchemaPath"] ?? configuration["InvoiceSchemaPath"] ?? Environment.GetEnvironmentVariable("INVOICE_SCHEMA_PATH");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                if (Path.IsPathRooted(configured))
                {
                    tried.Add(Path.GetFullPath(configured));
                    if (File.Exists(configured)) return configured;
                }
                else
                {
                    var relCandidates = new[]
                    {
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

            var candidatePaths = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "src", "Schemas", "invoice_schema.json"),
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

            throw new FileNotFoundException($"invoice_schema.json not found. Searched paths: {string.Join(";", tried.Distinct())}");
        }

        private async Task SaveFinalCleanedJson(string completionJson, string timestamp, string versionSuffix, CancellationToken cancellationToken)
        {
            try
            {
                var completion = JsonSerializer.Deserialize<JsonElement>(completionJson);
                var modelText = ExtractModelTextFromJson(completion);
                if (string.IsNullOrWhiteSpace(modelText)) return;

                var jsonObjects = ExtractJsonObjects(modelText);
                if (jsonObjects.Count == 0) return;

                var cleanedJson = CleanAndReturnJson(jsonObjects);
                if (string.IsNullOrEmpty(cleanedJson)) return;

                Directory.CreateDirectory(OutputDirectory);
                var finalOutputPath = Path.Combine(OutputDirectory, $"final_output_{timestamp}{versionSuffix}.json");
                await File.WriteAllTextAsync(finalOutputPath, cleanedJson, Encoding.UTF8, cancellationToken);
            }
            catch
            {
            }
        }

        private static string ExtractModelTextFromJson(JsonElement completion)
        {
            var sb = new StringBuilder();

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

            if (completion.TryGetProperty("Choices", out var choicesProp) || completion.TryGetProperty("choices", out choicesProp))
            {
                if (choicesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var choice in choicesProp.EnumerateArray())
                    {
                        if (choice.TryGetProperty("Message", out var msgProp) || choice.TryGetProperty("message", out msgProp))
                        {
                            if (msgProp.TryGetProperty("Content", out var msgContent) || msgProp.TryGetProperty("content", out msgContent))
                                sb.AppendLine(msgContent.GetString());
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

        private static LocalChatCompletionOptions CreateChatOptions() => new()
        {
            Temperature = ModelConfig.Temperature,
            MaxOutputTokenCount = ModelConfig.MaxTokens,
            TopP = ModelConfig.TopP,
            FrequencyPenalty = ModelConfig.FrequencyPenalty,
            PresencePenalty = ModelConfig.PresencePenalty,
        };

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

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

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
                            }
                        }
                    }
                }
            }

            return results;
        }

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
                        if (!keep) obj.Remove(key);
                    }
                }
                return obj.Count > 0;
            }

            if (node is JsonArray arr)
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

        private static string CleanAndReturnJson(List<string> jsonStrings)
        {
            if (jsonStrings == null || jsonStrings.Count == 0) return string.Empty;

            foreach (var candidate in jsonStrings)
            {
                try
                {
                    var node = JsonNode.Parse(candidate);
                    if (node == null) continue;

                    var keep = CleanJsonNode(node);
                    if (!keep) continue;

                    return JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static bool TryGetStringPropIgnoreCase(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        value = prop.Value.GetString() ?? string.Empty;
                    return true;
                }
            }
            return false;
        }

        private static void GenerateSupersetExports(string combinedMarkdown, string timestamp)
        {
            if (string.IsNullOrWhiteSpace(combinedMarkdown)) return;

            var blocks = combinedMarkdown
                .Split(new[] { "\n\n---\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(b => b.Trim())
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToArray();

            var deedDocs = new List<JsonObject>();
            var pcorDocs = new List<JsonObject>();
            var otherDocs = new List<JsonObject>();

            foreach (var block in blocks)
            {
                var jo = ExtractFieldsFromBlock(block);

                var blockLower = block.ToLowerInvariant();
                if (blockLower.Contains("grant deed") || blockLower.Contains("grantor") || blockLower.Contains("grantee") || blockLower.Contains("legal description"))
                    deedDocs.Add(jo);
                else if (blockLower.Contains("preliminary change of ownership report") || blockLower.Contains("pcor") || blockLower.Contains("assessor"))
                    pcorDocs.Add(jo);
                else
                    otherDocs.Add(jo);
            }

            var deedSuperset = BuildSupersetDocument(deedDocs, "deed");
            var pcorSuperset = BuildSupersetDocument(pcorDocs, "pcor");
            var otherSuperset = BuildSupersetDocument(otherDocs, "other");

            var options = new JsonSerializerOptions { WriteIndented = true };
            Directory.CreateDirectory(OutputDirectory);

            File.WriteAllText(Path.Combine(OutputDirectory, $"deed_superset_{timestamp}.json"), JsonSerializer.Serialize(deedSuperset, options), Encoding.UTF8);
            File.WriteAllText(Path.Combine(OutputDirectory, $"pcor_superset_{timestamp}.json"), JsonSerializer.Serialize(pcorSuperset, options), Encoding.UTF8);
            File.WriteAllText(Path.Combine(OutputDirectory, $"other_superset_{timestamp}.json"), JsonSerializer.Serialize(otherSuperset, options), Encoding.UTF8);
        }

        private static JsonObject BuildSupersetDocument(List<JsonObject> docs, string docType)
        {
            var root = new JsonObject { ["documentType"] = docType };
            var arr = new JsonArray();
            var aggregated = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var d in docs)
            {
                arr.Add(d);

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

        private static JsonObject ExtractFieldsFromBlock(string block)
        {
            var jo = new JsonObject { ["rawText"] = block };

            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).ToArray();
            if (lines.Length > 0)
            {
                jo["titleLine"] = lines[0];
                if (lines.Length > 1 && lines[1].StartsWith("**OCR Engine**", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = lines[1].IndexOf(':');
                    if (idx >= 0) jo["engine"] = lines[1].Substring(idx + 1).Trim();
                }
            }

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

            var plain = StripMarkdown(block);
            jo["plainText"] = plain;

            var dates = new JsonArray();
            foreach (Match m in Regex.Matches(plain, @"\b(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})\b")) dates.Add(m.Value);
            jo["dates"] = dates;

            var amounts = new JsonArray();
            foreach (Match m in Regex.Matches(plain, @"\$\s?[0-9\,]+(\.[0-9]{2})?")) amounts.Add(m.Value);
            jo["amounts"] = amounts;

            var apnMatch = Regex.Match(plain, @"\b\d{1,4}[- ]?\d[- ]?\d{1,4}[- ]?\d{1,4}\b");
            jo["apn"] = apnMatch.Success ? apnMatch.Value : string.Empty;

            var phones = new JsonArray();
            foreach (Match m in Regex.Matches(plain, @"\(?\d{3}\)?[-. ]?\d{3}[-. ]?\d{4}")) phones.Add(m.Value);
            jo["phones"] = phones;

            var emails = new JsonArray();
            foreach (Match m in Regex.Matches(plain, @"[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+")) emails.Add(m.Value);
            jo["emails"] = emails;

            return jo;
        }

        private static string StripMarkdown(string md)
        {
            if (string.IsNullOrEmpty(md)) return string.Empty;
            var s = md.Replace("###", "").Replace("**", "").Replace("__", "");
            s = Regex.Replace(s, @"\[(.*?)\]\((.*?)\)", "$1");
            return s;
        }
    }
}
