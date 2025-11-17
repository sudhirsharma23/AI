# Migration Guide: Using the New Refactored Services

## Quick Start

### Step 1: Update Program.cs Imports

```csharp
using ImageTextExtractor.Configuration;
using ImageTextExtractor.Models;
using ImageTextExtractor.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Caching.Memory;
```

### Step 2: Initialize Services

**Before (Old Way):**
```csharp
var config = AzureAIConfig.Load();
var client = new AzureOpenAIClient(new Uri(config.Endpoint), new AzureKeyCredential(config.SubscriptionKey));
// Hardcoded prompts in code
```

**After (New Way):**
```csharp
// Load configuration
var config = AzureAIConfig.Load();
config.Validate();

// Create Azure OpenAI client
var client = new AzureOpenAIClient(
    new Uri(config.Endpoint), 
    new AzureKeyCredential(config.SubscriptionKey)
);

// Create cache
var cache = new MemoryCache(new MemoryCacheOptions
{
    SizeLimit = 1024 // Optional: limit cache size
});

// Create services with dependency injection
var promptService = new PromptService(cache);
var modelConfig = AzureOpenAIModelConfig.GPT4oMini; // or GPT4o
var openAIService = new AzureOpenAIService(
    client, 
    cache, 
    promptService, 
    modelConfig
);
```

### Step 3: Process Documents

**Before (Old Way):**
```csharp
var prompt = CreateHardcodedPrompt(ocrText, schema); // Inline prompt
var response = await CallOpenAI(prompt);
```

**After (New Way):**
```csharp
// Simplified OCR response
var ocrResponse = new SimplifiedOCRResponse
{
    RawText = extractedText,
    Tables = tables,
    KeyValuePairs = keyValuePairs
};

// Process with versioning
var result = await openAIService.ProcessOCRResultsAsync(
    ocrResponse,
    targetSchema,
    version: "v1",  // or "v2" for enhanced extraction
  requestId: Guid.NewGuid().ToString()
);

// Check result
if (result.Success)
{
    Console.WriteLine($"? Processing successful");
    Console.WriteLine($"Input tokens: {result.InputTokens}");
    Console.WriteLine($"Output tokens: {result.OutputTokens}");
    Console.WriteLine($"Processing time: {result.ProcessingTime.TotalSeconds:F2}s");
    Console.WriteLine($"Model used: {result.ModelUsed}");
    
    // Parse JSON result
    var jsonResult = JsonSerializer.Deserialize<YourResultType>(result.Result);
}
else
{
  Console.WriteLine($"? Processing failed: {result.ErrorMessage}");
}
```

---

## Complete Example: Updated Program.cs

```csharp
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using Azure;
using Microsoft.Extensions.Caching.Memory;
using ImageTextExtractor.Configuration;
using ImageTextExtractor.Models;
using ImageTextExtractor.Services;

class Program
{
    static async Task Main(string[] args)
 {
        Console.WriteLine("ImageTextExtractor - Refactored Version");
        Console.WriteLine("========================================\n");

      try
        {
   // Step 1: Load configuration
      var config = AzureAIConfig.Load();
            config.Validate();

            // Step 2: Initialize services
            var client = new AzureOpenAIClient(
          new Uri(config.Endpoint),
   new AzureKeyCredential(config.SubscriptionKey)
  );

            var cache = new MemoryCache(new MemoryCacheOptions());
         var promptService = new PromptService(cache);

            // Choose model configuration
   var modelConfig = AzureOpenAIModelConfig.GPT4oMini; // Cost-effective
         // var modelConfig = AzureOpenAIModelConfig.GPT4o; // More powerful

        var openAIService = new AzureOpenAIService(
 client,
        cache,
   promptService,
         modelConfig
 );

      // Step 3: Load your TIF/PDF file
            var imageUrl = args.Length > 0 ? args[0] : "path/to/your/document.tif";
            
            if (!File.Exists(imageUrl))
      {
       Console.WriteLine($"File not found: {imageUrl}");
           return;
            }

   Console.WriteLine($"Processing: {imageUrl}");

// Step 4: Run OCR (your existing OCR logic)
var ocrText = await RunOCRAsync(imageUrl, config);

            // Create simplified response
          var ocrResponse = new SimplifiedOCRResponse
{
        RawText = ocrText,
         Tables = new List<TableData>(), // Add if you extract tables
KeyValuePairs = new List<KeyValuePair>() // Add if you extract key-values
            };

            // Step 5: Load target schema
        var schemaPath = "path/to/your/schema.json";
            var targetSchema = await File.ReadAllTextAsync(schemaPath);

    // Step 6: Process with AI (V1 - Schema-based)
        Console.WriteLine("\n--- Processing with V1 (Schema-based) ---");
 var resultV1 = await openAIService.ProcessOCRResultsAsync(
     ocrResponse,
       targetSchema,
          version: "v1",
            requestId: Guid.NewGuid().ToString()
       );

       PrintResult("V1", resultV1);

  // Step 7: Process with AI (V2 - Dynamic extraction)
            Console.WriteLine("\n--- Processing with V2 (Dynamic) ---");
  var resultV2 = await openAIService.ProcessOCRResultsAsync(
        ocrResponse,
                targetSchema,
           version: "v2",
          requestId: Guid.NewGuid().ToString()
            );

       PrintResult("V2", resultV2);

         // Step 8: Save results
            if (resultV1.Success)
            {
        await File.WriteAllTextAsync("output_v1.json", resultV1.Result);
                Console.WriteLine("\n? V1 result saved to: output_v1.json");
          }

            if (resultV2.Success)
            {
      await File.WriteAllTextAsync("output_v2.json", resultV2.Result);
       Console.WriteLine("? V2 result saved to: output_v2.json");
            }

      // Step 9: Check cache statistics
  var cacheStats = openAIService.GetCacheStatistics();
 Console.WriteLine($"\nCache Duration: {cacheStats.CacheDurationMinutes} minutes");
            Console.WriteLine($"Model: {cacheStats.ModelId}");

        }
catch (Exception ex)
        {
   Console.WriteLine($"\n? Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static void PrintResult(string version, ProcessingResult result)
    {
        if (result.Success)
        {
            Console.WriteLine($"? {version} Processing successful");
   Console.WriteLine($"  Input tokens: {result.InputTokens}");
    Console.WriteLine($"  Output tokens: {result.OutputTokens}");
            Console.WriteLine($"Total tokens: {result.InputTokens + result.OutputTokens}");
   Console.WriteLine($"  Processing time: {result.ProcessingTime.TotalSeconds:F2}s");
  Console.WriteLine($"  Model used: {result.ModelUsed}");
    Console.WriteLine($"  Result length: {result.Result.Length} characters");
        }
    else
        {
            Console.WriteLine($"? {version} Processing failed");
Console.WriteLine($"  Error: {result.ErrorMessage}");
        }
    }

    static async Task<string> RunOCRAsync(string imageUrl, AzureAIConfig config)
    {
   // Your existing OCR logic here
        // This is just a placeholder
        Console.WriteLine("Running OCR...");
        
     // Example: Return mock OCR text
        return @"GRANT DEED
        
Recording Requested by: Sample Title Company
When Recorded Mail to:
John Smith
123 Main Street
Anytown, CA 12345

Previous Owners: CHARLES D. SHAPIRO AND SUZANNE D. SHAPIRO, HUSBAND AND WIFE AS JOINT TENANTS

For valuable consideration, grant(s) to:
Charles David Shapiro and Suzanne Denise Shapiro, as co-trustees of the Shapiro Family Trust dated March 15, 2020

Property Address: 123 Main Street, Anytown, CA 12345

Dated: January 15, 2024";
    }
}
```

---

## Switching Between Versions

### V1: Schema-based Extraction (Stable, Deterministic)
```csharp
var result = await openAIService.ProcessOCRResultsAsync(
    ocrResponse,
    targetSchema,
    version: "v1"
);
```

**Use when:**
- You have a fixed, well-defined schema
- You need consistent, predictable output
- You want lower token usage

### V2: Dynamic Extraction (Enhanced, Flexible)
```csharp
var result = await openAIService.ProcessOCRResultsAsync(
    ocrResponse,
    targetSchema,
    version: "v2"
);
```

**Use when:**
- Documents have varying structures
- You need better entity recognition
- You want more intelligent field detection

---

## Switching Between Models

### GPT-4o-mini (Recommended for most cases)
```csharp
var modelConfig = AzureOpenAIModelConfig.GPT4oMini;
```
- **Cost:** Low
- **Speed:** Fast
- **Use for:** Standard deed extraction, high volume processing

### GPT-4o (When you need more power)
```csharp
var modelConfig = AzureOpenAIModelConfig.GPT4o;
```
- **Cost:** Higher
- **Speed:** Moderate
- **Use for:** Complex documents, edge cases, better accuracy

### GPT-4-turbo (Previous generation)
```csharp
var modelConfig = AzureOpenAIModelConfig.GPT4Turbo;
```
- **Cost:** Medium
- **Speed:** Moderate
- **Use for:** Legacy compatibility

---

## Updating Prompts Without Code Changes

### Modify System Prompt
1. Open `Prompts/SystemPrompts/deed_extraction_v1.txt`
2. Edit the prompt text
3. Save file
4. Restart application (prompts are cached for 24 hours)
5. Changes take effect immediately

### Add New Rule
1. Create `Prompts/Rules/my_new_rule.md`
2. Write your rule in markdown format
3. Update your code to include it:
```csharp
RuleNames = new List<string> 
{ 
    "percentage_calculation", 
    "name_parsing", 
    "date_format",
    "my_new_rule" // Add your new rule
}
```

### Add New Examples
1. Create JSON file: `Prompts/Examples/default/example_my_case.json`
2. Format:
```json
{
  "title": "My Example Case",
  "input": "Source text here",
  "expectedOutput": { "your": "expected json" },
  "explanation": "Why this is important"
}
```
3. Restart application

---

## Caching Behavior

### First Call (Cache Miss)
```
[RequestId] ? Prompt cache MISS
[RequestId] ? Document cache MISS
[RequestId] Calling Azure OpenAI
[RequestId] Response received in 2500ms
[RequestId] ? Response cached successfully
```

### Second Call (Cache Hit)
```
[RequestId] ? Prompt cache HIT - age: 0.02 minutes
[RequestId] Response returned immediately (from cache)
```

### Cache Keys
- **Prompt cache:** Based on prompt text hash + version
- **Document cache:** Based on document + schema + model hash + version

### Cache Duration
- **Default:** 60 minutes
- **Configurable:** Change `CACHE_DURATION_MINUTES` in AzureOpenAIService.cs

---

## Monitoring and Debugging

### Check Logs
Look for these log messages:
```
[RequestId] Starting OCR processing with version v1
[RequestId] Prompt cache key: prompt_AbCd123...
[RequestId] Document cache key: doc_XyZ789...
[RequestId] Calling Azure OpenAI with model: gpt-4o-mini
[RequestId] Input tokens: 1500, Output tokens: 800
[RequestId] ? Response cached successfully
```

### Check Saved Responses
Responses are automatically saved to:
```
OutputFiles/
??? response_20250128_143022_abc123.json
??? response_20250128_143045_def456.json
```

### Monitor Token Usage
```csharp
if (result.Success)
{
    var totalTokens = result.InputTokens + result.OutputTokens;
    var estimatedCost = CalculateCost(totalTokens, result.ModelUsed);
    Console.WriteLine($"Estimated cost: ${estimatedCost:F4}");
}
```

---

## Troubleshooting

### Issue: Prompts not updating
**Solution:** Clear cache or restart application (24-hour cache)

### Issue: Different results on each run
**Solution:** Ensure temperature is 0.0 in model config

### Issue: Missing prompt files
**Solution:** Check `Prompts/` directory structure, ensure files are copied to output

### Issue: Cache not working
**Solution:** Verify MemoryCache is properly initialized

### Issue: High token usage
**Solution:** 
- Use GPT-4o-mini instead of GPT-4o
- Simplify prompts and rules
- Remove unnecessary examples

---

## Performance Tips

1. **Use GPT-4o-mini** for most cases (faster + cheaper)
2. **Enable caching** to avoid repeated API calls
3. **Use V1** for standard cases (simpler prompt = fewer tokens)
4. **Batch processing** - process multiple documents in parallel
5. **Monitor token usage** - optimize prompts to reduce tokens

---

## Next Steps

1. ? Update Program.cs with new services
2. ? Test with sample documents
3. ? Compare V1 vs V2 results
4. ? Monitor cache hit rates
5. ? Optimize prompts based on results
6. ? Add unit tests
7. ? Deploy to production

---

**Status:** Ready for Integration  
**Estimated Migration Time:** 30 minutes  
**Breaking Changes:** Yes (old prompt code needs to be updated)  
**Benefits:** Better maintainability, versioning, configurability, caching
