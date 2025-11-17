using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TextractProcessor.Models;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Amazon.Lambda.Core;

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

        /// <summary>
        /// Process with BOTH V1 and V2 extraction simultaneously
        /// </summary>
        public async Task<ProcessingResult> ProcessAndMapSchema(
        List<SimplifiedTextractResponse> textractResults,
            string schemaFilePath,
            string originalFileName,
  ILambdaContext context = null)
        {
        try
            {
    Console.WriteLine("\n=== Processing with Bedrock - Dual Version Extraction ===");
        context?.Logger.LogLine("\n=== Processing with Bedrock - Dual Version Extraction ===");

 // Combine multiple documents into single source data
      var combinedData = CombineTextractResults(textractResults);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
  var baseFileName = Path.GetFileNameWithoutExtension(originalFileName);

    // Save combined Textract data for reference
       var combinedDataPath = Path.Combine(_outputDirectory, $"{baseFileName}_textract_combined_{timestamp}.txt");
     await File.WriteAllTextAsync(combinedDataPath, combinedData);
    Console.WriteLine($"? Saved combined Textract data to: {combinedDataPath}");
    context?.Logger.LogLine($"? Saved combined Textract data to: {combinedDataPath}");

             // Process Version 1: Schema-Based Extraction
      Console.WriteLine("\n--- Version 1: Schema-Based Extraction ---");
       context?.Logger.LogLine("\n--- Version 1: Schema-Based Extraction ---");
                var v1Result = await ProcessVersion1(textractResults, combinedData, baseFileName, timestamp, context);

    // Process Version 2: Dynamic Extraction (No Schema)
           Console.WriteLine("\n--- Version 2: Dynamic Extraction (No Schema) ---");
        context?.Logger.LogLine("\n--- Version 2: Dynamic Extraction (No Schema) ---");
 var v2Result = await ProcessVersion2(textractResults, combinedData, baseFileName, timestamp, context);

 // Return the primary result (V1), but both are saved
                Console.WriteLine("\n=== Dual Version Processing Complete ===");
    Console.WriteLine($"? V1 Output: {v1Result.MappedFilePath}");
       Console.WriteLine($"? V2 Output: {v2Result.MappedFilePath}");
                
    context?.Logger.LogLine("\n=== Dual Version Processing Complete ===");
    context?.Logger.LogLine($"? V1 Output: {v1Result.MappedFilePath}");
      context?.Logger.LogLine($"? V2 Output: {v2Result.MappedFilePath}");

         // Return V1 as primary, but include V2 info in metadata
    return new ProcessingResult
     {
            Success = v1Result.Success && v2Result.Success,
 MappedFilePath = v1Result.MappedFilePath,
     Error = v1Result.Error ?? v2Result.Error,
    InputTokens = v1Result.InputTokens + v2Result.InputTokens,
       OutputTokens = v1Result.OutputTokens + v2Result.OutputTokens,
      TotalCost = v1Result.TotalCost + v2Result.TotalCost
      };
            }
      catch (Exception ex)
     {
     Console.WriteLine($"? Error in dual version processing: {ex.Message}");
         context?.Logger.LogLine($"? Error in dual version processing: {ex.Message}");
   
         return new ProcessingResult
         {
       Success = false,
         Error = $"Schema processing error: {ex.Message}"
    };
   }
    }

        /// <summary>
    /// VERSION 1: Schema-based extraction with rules and examples
        /// </summary>
        private async Task<ProcessingResult> ProcessVersion1(
  List<SimplifiedTextractResponse> textractResults,
   string combinedData,
  string baseFileName,
      string timestamp,
            ILambdaContext context = null)
      {
  try
 {
         // Load the schema
  var targetSchema = await File.ReadAllTextAsync(_schemaFilePath);
     Console.WriteLine("Building V1 prompt from templates (with schema, rules, examples)...");
        context?.Logger.LogLine("Building V1 prompt from templates (with schema, rules, examples)...");

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
                Console.WriteLine($"V1 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");
             context?.Logger.LogLine($"V1 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

      // Process with Bedrock using the built prompt
       Console.WriteLine("Calling Bedrock for V1 extraction...");
    context?.Logger.LogLine("Calling Bedrock for V1 extraction...");

                (string mappedJson, int inputTokens, int outputTokens) = await _bedrockService.ProcessTextractResults(
     combinedData,
          builtPrompt.SystemMessage,
            builtPrompt.UserMessage,
         context
         );

    var mappedFilePath = Path.Combine(_outputDirectory, $"{baseFileName}_v1_schema_{timestamp}.json");
         await File.WriteAllTextAsync(mappedFilePath, mappedJson);

   Console.WriteLine($"? V1 processing complete:");
        Console.WriteLine($"  - Input tokens: {inputTokens}");
    Console.WriteLine($"  - Output tokens: {outputTokens}");
   Console.WriteLine($"  - Cost: ${CalculateCost(inputTokens, outputTokens):F4}");
     Console.WriteLine($"- Saved to: {mappedFilePath}");

      context?.Logger.LogLine($"? V1 processing complete: {inputTokens} input tokens, {outputTokens} output tokens, saved to {mappedFilePath}");

       // Analyze schema extensions for V1
    await AnalyzeSchemaExtensions(mappedJson, baseFileName, timestamp, context);

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
    Console.WriteLine($"? V1 processing failed: {ex.Message}");
        context?.Logger.LogLine($"? V1 processing failed: {ex.Message}");
        
          return new ProcessingResult
      {
      Success = false,
        Error = $"V1 processing error: {ex.Message}"
       };
            }
 }

        /// <summary>
        /// VERSION 2: Dynamic extraction without schema constraints
    /// </summary>
        private async Task<ProcessingResult> ProcessVersion2(
    List<SimplifiedTextractResponse> textractResults,
       string combinedData,
          string baseFileName,
    string timestamp,
            ILambdaContext context = null)
        {
          try
      {
     Console.WriteLine("Building V2 prompt from templates (dynamic, no schema)...");
            context?.Logger.LogLine("Building V2 prompt from templates (dynamic, no schema)...");

   // Build V2 prompt - NO schema, NO examples, NO rules - pure dynamic extraction
                var promptRequest = new PromptRequest
   {
  TemplateType = "document_extraction",
         Version = "v2",
          IncludeExamples = false,  // No examples for dynamic extraction
          IncludeRules = false,     // No rules, let AI figure it out
       RuleNames = new List<string>(),
          SchemaJson = "",     // NO SCHEMA!
           SourceData = combinedData,
    UserMessageTemplate = "Analyze the Textract OCR results below and extract ALL relevant information dynamically. Focus on:\n" +
         "- Buyer/Grantee information (names, addresses, percentages, ownership details)\n" +
        "- Seller/Grantor information (old owners, previous ownership)\n" +
                  "- Property details (address, legal description, parcel info, APN)\n" +
         "- Land records information (lot, block, tract, subdivision)\n" +
       "- Transaction details (sale price, date, recording info)\n" +
              "- PCOR information (checkboxes, tax info, exemptions)\n" +
          "- Document details (recording number, fees, notary info)\n\n" +
        "Return a comprehensive JSON with all findings.\n\n" +
       $"TEXTRACT OCR RESULTS:\n\n{combinedData}"
        };

      var builtPrompt = await _promptService.BuildCompletePromptAsync(promptRequest);
 Console.WriteLine($"V2 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");
   context?.Logger.LogLine($"V2 Prompt built successfully (System: {builtPrompt.SystemMessage.Length} chars, User: {builtPrompt.UserMessage.Length} chars)");

       // Process with Bedrock using the built prompt
  Console.WriteLine("Calling Bedrock for V2 extraction...");
      context?.Logger.LogLine("Calling Bedrock for V2 extraction...");

 (string mappedJson, int inputTokens, int outputTokens) = await _bedrockService.ProcessTextractResults(
            combinedData,
    builtPrompt.SystemMessage,
         builtPrompt.UserMessage,
        context
       );

      var mappedFilePath = Path.Combine(_outputDirectory, $"{baseFileName}_v2_dynamic_{timestamp}.json");
        await File.WriteAllTextAsync(mappedFilePath, mappedJson);

   Console.WriteLine($"? V2 processing complete:");
       Console.WriteLine($"  - Input tokens: {inputTokens}");
  Console.WriteLine($"  - Output tokens: {outputTokens}");
                Console.WriteLine($"  - Cost: ${CalculateCost(inputTokens, outputTokens):F4}");
           Console.WriteLine($"  - Saved to: {mappedFilePath}");

      context?.Logger.LogLine($"? V2 processing complete: {inputTokens} input tokens, {outputTokens} output tokens, saved to {mappedFilePath}");

    // Create V2 extraction summary
    await CreateV2ExtractionSummary(mappedJson, baseFileName, timestamp, context);

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
      Console.WriteLine($"? V2 processing failed: {ex.Message}");
                context?.Logger.LogLine($"? V2 processing failed: {ex.Message}");
      
        return new ProcessingResult
      {
            Success = false,
          Error = $"V2 processing error: {ex.Message}"
       };
       }
        }

        /// <summary>
        /// Analyze V1 extracted JSON to identify fields not in base schema
        /// </summary>
private async Task AnalyzeSchemaExtensions(string extractedJson, string baseFileName, string timestamp, ILambdaContext context = null)
   {
            try
         {
    Console.WriteLine("\n=== Analyzing Schema Extensions (V1) ===");
    context?.Logger.LogLine("=== Analyzing Schema Extensions (V1) ===");

     // Load base schema
     var baseSchema = JsonNode.Parse(await File.ReadAllTextAsync(_schemaFilePath));
         var extractedData = JsonNode.Parse(extractedJson);

     var extensions = new List<string>();
          FindExtensions(baseSchema, extractedData, "", extensions);

                if (extensions.Count > 0)
   {
                    Console.WriteLine($"Found {extensions.Count} extended fields not in base schema");
           context?.Logger.LogLine($"Found {extensions.Count} extended fields not in base schema");

     var extensionsReport = new StringBuilder();
       extensionsReport.AppendLine("# Schema Extensions Report (V1)");
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
         var reportPath = Path.Combine(_outputDirectory, $"{baseFileName}_schema_extensions_{timestamp}.md");
  await File.WriteAllTextAsync(reportPath, extensionsReport.ToString());
            Console.WriteLine($"? Saved schema extensions report to: {reportPath}");
   context?.Logger.LogLine($"? Saved schema extensions report to: {reportPath}");
       }
     else
         {
          Console.WriteLine("No schema extensions detected. All fields match base schema.");
   context?.Logger.LogLine("No schema extensions detected.");
                }
            }
            catch (Exception ex)
       {
     Console.WriteLine($"Warning: Could not analyze schema extensions: {ex.Message}");
     context?.Logger.LogLine($"Warning: Could not analyze schema extensions: {ex.Message}");
       }
   }

        /// <summary>
/// Create a summary report for V2 dynamic extraction
        /// </summary>
    private async Task CreateV2ExtractionSummary(string extractedJson, string baseFileName, string timestamp, ILambdaContext context = null)
        {
            try
            {
                Console.WriteLine("\n=== Creating V2 Extraction Summary ===");
     context?.Logger.LogLine("=== Creating V2 Extraction Summary ===");

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
         // Try to extract common fields
    var buyerInfo = extractedData?["buyerInformation"] ?? extractedData?["buyers"] ?? extractedData?["grantees"];
    if (buyerInfo != null)
    {
       var totalBuyers = buyerInfo["totalBuyers"]?.ToString() ?? buyerInfo["count"]?.ToString() ?? "N/A";
      summary.AppendLine($"  - Total Buyers/Grantees: {totalBuyers}");
        }

      var sellerInfo = extractedData?["sellerInformation"] ?? extractedData?["sellers"] ?? extractedData?["grantors"];
       if (sellerInfo != null)
      {
     var totalSellers = sellerInfo["totalSellers"]?.ToString() ?? sellerInfo["count"]?.ToString() ?? "N/A";
        summary.AppendLine($"  - Total Sellers/Grantors: {totalSellers}");
   }

       var propertyInfo = extractedData?["propertyInformation"] ?? extractedData?["property"];
  if (propertyInfo != null)
       {
         var address = propertyInfo["address"]?["fullAddress"]?.ToString() ?? 
       propertyInfo["propertyAddress"]?.ToString() ?? "N/A";
  summary.AppendLine($"  - Property Address: {address}");
         }

        var transactionDetails = extractedData?["transactionDetails"] ?? extractedData?["transaction"];
      if (transactionDetails != null)
     {
                 var salePrice = transactionDetails["salePrice"]?.ToString() ?? 
     transactionDetails["considerationAmount"]?.ToString() ?? "N/A";
      summary.AppendLine($"  - Sale Price/Consideration: ${salePrice}");
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
        var reportPath = Path.Combine(_outputDirectory, $"{baseFileName}_v2_extraction_summary_{timestamp}.md");
  await File.WriteAllTextAsync(reportPath, summary.ToString());
        Console.WriteLine($"? Saved V2 extraction summary to: {reportPath}");
  context?.Logger.LogLine($"? Saved V2 extraction summary to: {reportPath}");
   }
            catch (Exception ex)
  {
         Console.WriteLine($"Warning: Could not create V2 summary: {ex.Message}");
                context?.Logger.LogLine($"Warning: Could not create V2 summary: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively count fields in JSON
        /// </summary>
 private int CountJsonFields(JsonNode node)
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

        /// <summary>
  /// Recursively find fields in extracted data that don't exist in base schema
        /// </summary>
        private void FindExtensions(JsonNode baseNode, JsonNode extractedNode, string path, List<string> extensions)
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

        /// <summary>
    /// Get JSON value type as string
        /// </summary>
        private string GetJsonValueType(JsonNode node)
        {
   if (node == null) return "null";
          if (node is JsonValue jv)
      {
            var value = jv.ToString();
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
     combined.AppendLine($"{field.Key}: {field.Value}");
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