using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace TextractProcessor.Services
{
    /// <summary>
    /// Service for managing and building prompts from template files
  /// Supports versioning, caching, and dynamic prompt composition
    /// Mirrors the ImageTextExtractor PromptService architecture
    /// </summary>
    public class PromptService
    {
        private readonly string _promptsDirectory;
        private readonly IMemoryCache _cache;
        private const int CACHE_DURATION_HOURS = 24;

        public PromptService(IMemoryCache cache, string promptsDirectory = null)
        {
    _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            
            // For Lambda, prompts are in the deployment package root
            _promptsDirectory = promptsDirectory ?? Path.Combine(
          AppDomain.CurrentDomain.BaseDirectory,
     "Prompts"
          );
     }

        /// <summary>
/// Load a system prompt template from file
   /// </summary>
        public async Task<string> LoadSystemPromptAsync(string templateName, string version = "v1")
        {
     var cacheKey = $"prompt_{templateName}_{version}";

            if (_cache.TryGetValue<string>(cacheKey, out var cachedPrompt))
            {
       return cachedPrompt;
         }

        var promptPath = Path.Combine(
  _promptsDirectory,
   "SystemPrompts",
        $"{templateName}_{version}.txt"
   );

            if (!File.Exists(promptPath))
{
   throw new FileNotFoundException($"Prompt template not found: {promptPath}");
   }

            var prompt = await File.ReadAllTextAsync(promptPath);

 _cache.Set(cacheKey, prompt, TimeSpan.FromHours(CACHE_DURATION_HOURS));

        return prompt;
   }

      /// <summary>
        /// Load examples for few-shot learning
        /// </summary>
        public async Task<List<PromptExample>> LoadExamplesAsync(string exampleSet)
        {
          var cacheKey = $"examples_{exampleSet}";

  if (_cache.TryGetValue<List<PromptExample>>(cacheKey, out var cachedExamples))
            {
      return cachedExamples;
            }

            var examples = new List<PromptExample>();
          var examplesDir = Path.Combine(_promptsDirectory, "Examples", exampleSet);

   if (!Directory.Exists(examplesDir))
        {
    throw new DirectoryNotFoundException($"Examples directory not found: {examplesDir}");
   }

        foreach (var file in Directory.GetFiles(examplesDir, "*.json"))
  {
      var json = await File.ReadAllTextAsync(file);
     var example = JsonSerializer.Deserialize<PromptExample>(json);
    if (example != null)
     {
         examples.Add(example);
          }
         }

     _cache.Set(cacheKey, examples, TimeSpan.FromHours(CACHE_DURATION_HOURS));

         return examples;
        }

     /// <summary>
        /// Load rules markdown file
     /// </summary>
        public async Task<string> LoadRulesAsync(string ruleName)
        {
            var cacheKey = $"rules_{ruleName}";

            if (_cache.TryGetValue<string>(cacheKey, out var cachedRules))
            {
   return cachedRules;
      }

 var rulesPath = Path.Combine(_promptsDirectory, "Rules", $"{ruleName}.md");

      if (!File.Exists(rulesPath))
            {
   throw new FileNotFoundException($"Rules file not found: {rulesPath}");
          }

     var rules = await File.ReadAllTextAsync(rulesPath);

         _cache.Set(cacheKey, rules, TimeSpan.FromHours(CACHE_DURATION_HOURS));

     return rules;
        }

        /// <summary>
        /// Build complete prompt from request
        /// </summary>
        public async Task<BuiltPrompt> BuildCompletePromptAsync(PromptRequest request)
        {
            var systemPrompt = await LoadSystemPromptAsync(request.TemplateType, request.Version);

     // Replace placeholders
    var sb = new StringBuilder(systemPrompt);

 // Insert schema if provided
            if (!string.IsNullOrEmpty(request.SchemaJson))
      {
    sb.Replace("{{SCHEMA}}", request.SchemaJson);
   }

       // Insert examples if requested
    if (request.IncludeExamples && !string.IsNullOrEmpty(request.ExampleSet))
 {
 try
                {
               var examples = await LoadExamplesAsync(request.ExampleSet);
          var examplesText = FormatExamples(examples);
         sb.Replace("{{EXAMPLES}}", examplesText);
    }
    catch (DirectoryNotFoundException)
         {
 // Examples are optional, continue without them
                sb.Replace("{{EXAMPLES}}", "");
         }
            }
    else
      {
   sb.Replace("{{EXAMPLES}}", "");
            }

      // Insert rules if specified
            if (request.IncludeRules && request.RuleNames != null)
 {
       foreach (var ruleName in request.RuleNames)
   {
             try
    {
     var rules = await LoadRulesAsync(ruleName);
            sb.Replace($"{{{{RULES_{ruleName.ToUpper()}}}}}", rules);
 }
  catch (FileNotFoundException)
             {
          // Rules are optional, continue without them
            sb.Replace($"{{{{RULES_{ruleName.ToUpper()}}}}}", "");
         }
       }
            }

            // Build user message
            var userMessage = request.UserMessageTemplate ??
                $"Please extract and analyze ALL data from the documents below.\n\nDOCUMENTS:\n\n{request.SourceData}";

      return new BuiltPrompt
   {
       SystemMessage = sb.ToString(),
     UserMessage = userMessage,
       TemplateType = request.TemplateType,
            Version = request.Version,
      GeneratedAt = DateTime.UtcNow
            };
        }

   private string FormatExamples(List<PromptExample> examples)
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
                {
       sb.AppendLine($"Explanation: {example.Explanation}");
      }
            sb.AppendLine();
            }

    return sb.ToString();
      }
    }

    /// <summary>
    /// Request for building a prompt
    /// </summary>
    public class PromptRequest
    {
        public string TemplateType { get; set; } = "document_extraction";
        public string Version { get; set; } = "v1";
        public bool IncludeExamples { get; set; } = true;
        public string ExampleSet { get; set; } = "default";
  public bool IncludeRules { get; set; } = true;
     public List<string> RuleNames { get; set; } = new() { "percentage_calculation", "name_parsing", "date_format" };
        public string SchemaJson { get; set; }
        public string SourceData { get; set; }
      public string UserMessageTemplate { get; set; }
    }

    /// <summary>
    /// Built prompt ready for model consumption
    /// </summary>
    public class BuiltPrompt
    {
   public string SystemMessage { get; set; }
        public string UserMessage { get; set; }
 public string TemplateType { get; set; }
     public string Version { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    /// <summary>
    /// Example for few-shot learning
    /// </summary>
  public class PromptExample
    {
        public string Title { get; set; }
        public string Input { get; set; }
     public string ExpectedOutput { get; set; }
        public string Explanation { get; set; }
    }
}
