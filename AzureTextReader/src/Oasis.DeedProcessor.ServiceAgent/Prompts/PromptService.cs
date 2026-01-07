using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Oasis.DeedProcessor.ServiceAgent.Prompts
{
    public class PromptService
    {
        private readonly string _promptsDirectory;
        private readonly IMemoryCache _cache;
        private const int CACHE_DURATION_HOURS = 24;

        public PromptService(IMemoryCache cache, string? promptsDirectory = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _promptsDirectory = promptsDirectory ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "Prompts"
            );
        }

        public async Task<string> LoadSystemPromptAsync(string templateName, string version = "v1")
        {
            var cacheKey = $"prompt_{templateName}_{version}";

            if (_cache.TryGetValue<string>(cacheKey, out var cachedPrompt))
                return cachedPrompt;

            var promptPath = Path.Combine(
                _promptsDirectory,
                "SystemPrompts",
                $"{templateName}_{version}.txt"
            );

            if (!File.Exists(promptPath))
            {
                var systemDir = Path.Combine(_promptsDirectory, "SystemPrompts");
                if (Directory.Exists(systemDir))
                {
                    var candidates = Directory.GetFiles(systemDir, $"*_{version}.txt");
                    if (candidates.Length > 0)
                    {
                        promptPath = candidates[0];
                    }
                    else
                    {
                        var containing = Directory.GetFiles(systemDir, "*.txt")
                            .FirstOrDefault(f => Path.GetFileName(f).IndexOf(templateName ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!string.IsNullOrEmpty(containing)) promptPath = containing;
                    }
                }

                if (!File.Exists(promptPath))
                    throw new FileNotFoundException($"Prompt template not found: {promptPath}");
            }

            var prompt = await File.ReadAllTextAsync(promptPath);
            _cache.Set(cacheKey, prompt, TimeSpan.FromHours(CACHE_DURATION_HOURS));

            return prompt;
        }

        public async Task<List<PromptExample>> LoadExamplesAsync(string exampleSet)
        {
            var cacheKey = $"examples_{exampleSet}";

            if (_cache.TryGetValue<List<PromptExample>>(cacheKey, out var cachedExamples))
                return cachedExamples;

            var examples = new List<PromptExample>();
            var examplesDir = Path.Combine(_promptsDirectory, "Examples", exampleSet);

            if (!Directory.Exists(examplesDir))
                throw new DirectoryNotFoundException($"Examples directory not found: {examplesDir}");

            foreach (var file in Directory.GetFiles(examplesDir, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file);
                var example = JsonSerializer.Deserialize<PromptExample>(json);
                if (example != null) examples.Add(example);
            }

            _cache.Set(cacheKey, examples, TimeSpan.FromHours(CACHE_DURATION_HOURS));
            return examples;
        }

        public async Task<string> LoadRulesAsync(string ruleName)
        {
            var cacheKey = $"rules_{ruleName}";

            if (_cache.TryGetValue<string>(cacheKey, out var cachedRules))
                return cachedRules;

            var rulesPath = Path.Combine(_promptsDirectory, "Rules", $"{ruleName}.md");
            if (!File.Exists(rulesPath))
                throw new FileNotFoundException($"Rules file not found: {rulesPath}");

            var rules = await File.ReadAllTextAsync(rulesPath);
            _cache.Set(cacheKey, rules, TimeSpan.FromHours(CACHE_DURATION_HOURS));

            return rules;
        }

        public async Task<BuiltPrompt> BuildCompletePromptAsync(PromptRequest request)
        {
            var systemPrompt = await LoadSystemPromptAsync(request.TemplateType, request.Version);

            var sb = new StringBuilder(systemPrompt);

            if (!string.IsNullOrEmpty(request.SchemaJson))
                sb.Replace("{{SCHEMA}}", request.SchemaJson);

            if (request.IncludeExamples && !string.IsNullOrEmpty(request.ExampleSet))
            {
                var examples = await LoadExamplesAsync(request.ExampleSet);
                var examplesText = FormatExamples(examples);
                sb.Replace("{{EXAMPLES}}", examplesText);
            }

            if (request.IncludeRules && request.RuleNames != null)
            {
                foreach (var ruleName in request.RuleNames)
                {
                    var rules = await LoadRulesAsync(ruleName);
                    sb.Replace($"{{{{RULES_{ruleName.ToUpperInvariant()}}}}}", rules);
                }
            }

            var sourceData = request.SourceData ?? string.Empty;
            string userMessage;

            if (!string.IsNullOrEmpty(request.UserMessageTemplate))
            {
                if (request.UserMessageTemplate.Contains("{{SOURCE_DATA}}"))
                    userMessage = request.UserMessageTemplate.Replace("{{SOURCE_DATA}}", sourceData);
                else
                    userMessage = request.UserMessageTemplate + "\n\nDOCUMENTS:\n\n" + sourceData;
            }
            else
            {
                userMessage = "Please extract and analyze ALL data from the documents below.\n\nDOCUMENTS:\n\n{{SOURCE_DATA}}"
                    .Replace("{{SOURCE_DATA}}", sourceData);
            }

            return new BuiltPrompt
            {
                SystemMessage = sb.ToString(),
                UserMessage = userMessage,
                TemplateType = request.TemplateType,
                Version = request.Version,
                GeneratedAt = DateTime.UtcNow
            };
        }

        private static string FormatExamples(List<PromptExample> examples)
        {
            var sb = new StringBuilder();
            sb.AppendLine("EXAMPLES:");
            sb.AppendLine();

            for (int i = 0; i < examples.Count; i++)
            {
                var example = examples[i];
                sb.AppendLine($"Example {i + 1}: {example.Title}");
                sb.AppendLine("---");
                sb.AppendLine($"Input: {example.Input}");
                sb.AppendLine($"Output: {example.ExpectedOutput}");
                if (!string.IsNullOrEmpty(example.Explanation))
                    sb.AppendLine($"Explanation: {example.Explanation}");

                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    public class PromptRequest
    {
        public string TemplateType { get; set; } = "deed_extraction";
        public string Version { get; set; } = "v1";
        public bool IncludeExamples { get; set; } = true;
        public string ExampleSet { get; set; } = "default";
        public bool IncludeRules { get; set; } = true;
        public List<string> RuleNames { get; set; } = new() { "percentage_calculation", "name_parsing", "date_format" };
        public string? SchemaJson { get; set; }
        public string? SourceData { get; set; }
        public string? UserMessageTemplate { get; set; }
    }

    public class BuiltPrompt
    {
        public string SystemMessage { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }

    public class PromptExample
    {
        public string Title { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public string ExpectedOutput { get; set; } = string.Empty;
        public string? Explanation { get; set; }
    }
}
