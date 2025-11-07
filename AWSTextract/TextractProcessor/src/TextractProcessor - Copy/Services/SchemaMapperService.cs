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

        public SchemaMapperService(BedrockService bedrockService, IMemoryCache cache, string outputDirectory)
        {
            _bedrockService = bedrockService;
            _cache = cache;
            _outputDirectory = outputDirectory;
        
            // Get the directory where the application is running
            var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _schemaFilePath = Path.Combine(baseDirectory!, "invoice_schema.json");

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
            TextractResponse textractResults,
            string schemaFilePath,
            string originalFileName)
        {
            try
            {
                // Use the resolved schema path instead of the parameter
                var targetSchema = await File.ReadAllTextAsync(_schemaFilePath);

                (string mappedJson, int inputTokens, int outputTokens) = await _bedrockService.ProcessTextractResults(
                    textractResults,
                    targetSchema
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

        private decimal CalculateCost(int inputTokens, int outputTokens)
        {
            const decimal INPUT_COST_PER_1K_TOKENS = 0.008m;
            const decimal OUTPUT_COST_PER_1K_TOKENS = 0.024m;

            var inputCost = (inputTokens / 1000m) * INPUT_COST_PER_1K_TOKENS;
            var outputCost = (outputTokens / 1000m) * OUTPUT_COST_PER_1K_TOKENS;

            return inputCost + outputCost;
        }
    }

    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string MappedFilePath { get; set; }
        public string Error { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal TotalCost { get; set; }
    }
}