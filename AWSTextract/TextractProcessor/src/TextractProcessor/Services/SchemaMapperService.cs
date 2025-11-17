using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TextractProcessor.Models;
using System.Reflection;

namespace TextractProcessor.Services
{
    public class SchemaMapperService
    {
        private readonly BedrockService _bedrockService;
        private readonly IMemoryCache _cache;
        private readonly string _outputDirectory;
        private readonly string _schemaFilePath;
        private readonly PromptService _promptService;

        public SchemaMapperService(BedrockService bedrockService, IMemoryCache cache, string outputDirectory)
        {
            _bedrockService = bedrockService;
            _cache = cache;
            _outputDirectory = outputDirectory;

            // Get the directory where the application is running
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _schemaFilePath = Path.Combine(baseDirectory!, "invoice_schema.json");

            // Initialize PromptService
            _promptService = new PromptService(cache);

            // Ensure the schema file exists in the output directory
            if (!File.Exists(_schemaFilePath))
            {
                var sourceSchemaPath = Path.Combine(
                        Directory.GetCurrentDirectory(),
                    "invoice_schema.json");

                if (File.Exists(sourceSchemaPath))
                {
                    File.Copy(sourceSchemaPath, _schemaFilePath, true);
                }
                else
                {
                    throw new FileNotFoundException(
            $"Schema file not found at either {_schemaFilePath} or {sourceSchemaPath}. " +
                $"Please ensure invoice_schema.json is present in the project directory.");
                }
            }
        }

        public async Task<ProcessingResult> ProcessAndMapSchema(
            List<SimplifiedTextractResponse> textractResults,
            string schemaFilePath,
            string originalFileName)
        {
            try
            {
                // Load the schema
                var targetSchema = await File.ReadAllTextAsync(_schemaFilePath);

                // Combine multiple documents into single source data
                var combinedData = CombineTextractResults(textractResults);

                // Build prompt using PromptService (V1 - schema-based)
                var promptRequest = new PromptRequest
                {
                    TemplateType = "document_extraction",
                    Version = "v1",
                    IncludeExamples = true,
                    ExampleSet = "default",
                    IncludeRules = true,
                    RuleNames = new List<string> { "percentage_calculation", "name_parsing", "date_format" },
                    SchemaJson = targetSchema,
                    SourceData = combinedData
                };

                var builtPrompt = await _promptService.BuildCompletePromptAsync(promptRequest);

                // Process with Bedrock using the built prompt
                (string mappedJson, int inputTokens, int outputTokens) = await _bedrockService.ProcessTextractResults(
                    combinedData,
                    builtPrompt.SystemMessage,
                    builtPrompt.UserMessage
                );

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var baseFileName = Path.GetFileNameWithoutExtension(originalFileName);
                var mappedFilePath = Path.Combine(_outputDirectory, $"{baseFileName}_mapped_{timestamp}.json");

                await File.WriteAllTextAsync(mappedFilePath, mappedJson);

                return new ProcessingResult
                {
                    Success = true,
                    MappedFilePath = mappedFilePath,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    TotalCost = CalculateCost(inputTokens, outputTokens)
                };
            }
            catch (Exception ex)
            {
                return new ProcessingResult
                {
                    Success = false,
                    Error = $"Schema processing error: {ex.Message}"
                };
            }
        }

        private string CombineTextractResults(List<SimplifiedTextractResponse> results)
        {
            var combined = new System.Text.StringBuilder();

            foreach (var result in results)
            {
                combined.AppendLine("=== Document ===");
                combined.AppendLine();
                combined.AppendLine("RAW TEXT:");
                combined.AppendLine(result.RawText);
                combined.AppendLine();

                if (result.FormFields?.Any() == true)
                {
                    combined.AppendLine("FORM FIELDS:");
                    foreach (var field in result.FormFields)
                    {
                        combined.AppendLine($"  {field.Key}: {field.Value}");
                    }
                    combined.AppendLine();
                }

                if (result.TableData?.Any() == true)
                {
                    combined.AppendLine("TABLE DATA:");
                    for (int i = 0; i < result.TableData.Count; i++)
                    {
                        combined.AppendLine($"  Table {i + 1}:");
                        var table = result.TableData[i];
                        foreach (var row in table)
                        {
                            combined.AppendLine($"    {string.Join(" | ", row)}");
                        }
                    }
                    combined.AppendLine();
                }

                combined.AppendLine("=== End Document ===");
                combined.AppendLine();
            }

            return combined.ToString();
        }

        private decimal CalculateCost(int inputTokens, int outputTokens)
        {
            const decimal INPUT_COST_PER_1K_TOKENS = 0.008m;
            const decimal OUTPUT_COST_PER_1K_TOKENS = 0.024m;

            var inputCost = (inputTokens / 1000m) * INPUT_COST_PER_1K_TOKENS;
            var outputCost = (outputTokens / 1000m) * OUTPUT_COST_PER_1K_TOKENS;

            return inputCost + outputCost;
        }
    }
}