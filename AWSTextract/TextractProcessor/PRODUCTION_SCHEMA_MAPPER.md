# Production Schema Mapper Process

**Dual-Version Extraction: Schema-Based + Dynamic**

> **Service:** SchemaMapperService  
> **Purpose:** Extract data using both constrained (V1) and flexible (V2) approaches  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Overview](#overview)
2. [Dual-Version Strategy](#dual-version-strategy)
3. [Version 1: Schema-Based](#version-1-schema-based)
4. [Version 2: Dynamic](#version-2-dynamic)
5. [Processing Flow](#processing-flow)
6. [Output Files](#output-files)
7. [Analysis Reports](#analysis-reports)
8. [Configuration](#configuration)
9. [Best Practices](#best-practices)

---

## Overview

### What Schema Mapper Does

The Schema Mapper service orchestrates the complete extraction pipeline:
1. **Combines multiple documents** into single source
2. **Runs V1 extraction** with schema constraints
3. **Runs V2 extraction** with dynamic discovery
4. **Generates analysis reports** comparing both versions
5. **Returns structured results** for downstream processing

### Why Dual-Version Extraction?

| Version | Purpose | Benefits |
|---------|---------|----------|
| **V1: Schema-Based** | Consistent, predictable output | ? Matches database schema<br>? Validates against rules<br>? Production-ready |
| **V2: Dynamic** | Discover all available data | ? Finds unexpected fields<br>? Improves schema design<br>? Research & development |

### Architecture

```
???????????????????????????????????????????????????????????????????
? SCHEMA MAPPER DUAL-VERSION ARCHITECTURE  ?
???????????????????????????????????????????????????????????????????
?     ?
?  [Multiple Textract Responses]?
?       ? Combine   ?
?  [Combined Source Data]  ?
?       ? Split    ?
?    ??????????????????????? ?
?    ?   ? ?
?  [V1 Pipeline] [V2 Pipeline]?
?  Schema-based     Dynamic    ?
?  + Rules  No constraints ?
?  + Examples    Pure AI-driven   ?
?    ?    ?  ?
?  [V1 JSON] [V2 JSON]?
?  Constrained   Full discovery  ?
?  ?      ? ?
?  [Extensions]      [Summary]   ?
?  Analysis  Report   ?
?    ???????????????????????  ?
?   ?  ?
?  [ProcessingResult]?
?  Combined metrics?
???????????????????????????????????????????????????????????????????
```

---

## Dual-Version Strategy

### Processing Approach

```csharp
public async Task<ProcessingResult> ProcessAndMapSchema(
  List<SimplifiedTextractResponse> textractResults,
    string schemaFilePath,
    string originalFileName,
    ILambdaContext context = null)
{
    // 1. Combine all documents
    var combinedData = CombineTextractResults(textractResults);
    
    // 2. Process V1: Schema-Based Extraction
    var v1Result = await ProcessVersion1(
        textractResults, combinedData, baseFileName, timestamp, context);
    
    // 3. Process V2: Dynamic Extraction
    var v2Result = await ProcessVersion2(
textractResults, combinedData, baseFileName, timestamp, context);
    
    // 4. Return combined metrics
    return new ProcessingResult
    {
        Success = v1Result.Success && v2Result.Success,
        MappedFilePath = v1Result.MappedFilePath,
        InputTokens = v1Result.InputTokens + v2Result.InputTokens,
     OutputTokens = v1Result.OutputTokens + v2Result.OutputTokens,
     TotalCost = v1Result.TotalCost + v2Result.TotalCost
    };
}
```

### Comparison Matrix

| Aspect | V1 (Schema-Based) | V2 (Dynamic) |
|--------|-------------------|--------------|
| **Schema** | ? invoice_schema.json | ? None |
| **Rules** | ? percentage_calculation.md<br>? name_parsing.md<br>? date_format.md | ? None |
| **Examples** | ? Few-shot learning | ? Zero-shot |
| **Prompt Template** | `document_extraction_v1.txt` | `document_extraction_v2.txt` |
| **Output Structure** | Fixed, predictable | Flexible, comprehensive |
| **Use Case** | Production database | Research & schema improvement |

---

## Version 1: Schema-Based

### Purpose

Extract data that **matches a predefined schema** with validation rules and examples.

### Process Flow

```
????????????????????????????????????????????????????????????????
? V1: SCHEMA-BASED EXTRACTION PIPELINE    ?
????????????????????????????????????????????????????????????????
?    ?
? Step 1: Load Schema  ?
?   ? Read invoice_schema.json?
? Step 2: Build Prompt ?
?   ? PromptService loads: ?
?   - document_extraction_v1.txt   ?
?   - Rules (percentage, names, dates)?
? - Examples (few-shot learning)  ?
?     ?
? Step 3: Insert Components?
?   ? Replace placeholders:?
?   - {{SCHEMA}} ? Schema JSON?
?   - {{RULES_*}} ? Rule markdown ?
?   - {{EXAMPLES}} ? Example cases ?
?      ?
? Step 4: Invoke Bedrock   ?
?   ? Send to model:   ?
?   - System: Complete prompt  ?
?   - User: Combined OCR data  ?
?      ?
? Step 5: Extract JSON ?
?   ? Parse model response?
?     ?
? Step 6: Save Output?
?   ? {filename}_v1_schema_{timestamp}.json ?
?     ?
? Step 7: Analyze Extensions   ?
?   ? Compare extracted fields vs base schema  ?
?   ? {filename}_schema_extensions_{timestamp}.md ?
????????????????????????????????????????????????????????????????
```

### Code Implementation

```csharp
private async Task<ProcessingResult> ProcessVersion1(
 List<SimplifiedTextractResponse> textractResults,
    string combinedData,
    string baseFileName,
    string timestamp,
ILambdaContext context = null)
{
    // 1. Load schema
    var targetSchema = await File.ReadAllTextAsync(_schemaFilePath);
 
    // 2. Build prompt with schema, rules, examples
    var promptRequest = new PromptRequest
    {
        TemplateType = "document_extraction",
  Version = "v1",
 IncludeExamples = true,
        ExampleSet = "default",
        IncludeRules = true,
        RuleNames = new List<string> 
        { 
 "percentage_calculation", 
            "name_parsing", 
    "date_format" 
        },
        SchemaJson = targetSchema,
        SourceData = combinedData
    };
    
    var builtPrompt = await _promptService.BuildCompletePromptAsync(promptRequest);
    
    // 3. Process with Bedrock
  (string mappedJson, int inputTokens, int outputTokens) = 
        await _bedrockService.ProcessTextractResults(
     combinedData,
            builtPrompt.SystemMessage,
  builtPrompt.UserMessage,
            context
        );
    
    // 4. Save output
    var mappedFilePath = Path.Combine(
        _outputDirectory, 
     $"{baseFileName}_v1_schema_{timestamp}.json"
    );
    await File.WriteAllTextAsync(mappedFilePath, mappedJson);
    
    // 5. Analyze schema extensions
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
```

### Example V1 Output

```json
{
  "buyerInformation": {
    "totalBuyers": 2,
    "buyers": [
      {
        "name": {
          "firstName": "John",
          "lastName": "Doe",
          "fullName": "John Doe"
        },
 "ownershipPercentage": 50.0,
        "address": {
          "street": "123 Main St",
          "city": "Los Angeles",
          "state": "CA",
          "zipCode": "90001"
        }
      },
      {
 "name": {
          "firstName": "Jane",
          "lastName": "Smith",
  "fullName": "Jane Smith"
        },
        "ownershipPercentage": 50.0,
        "address": {
          "street": "456 Oak Ave",
  "city": "Los Angeles",
          "state": "CA",
 "zipCode": "90002"
        }
    }
    ]
  },
  "propertyInformation": {
    "address": {
      "street": "789 Property Ln",
      "city": "Los Angeles",
      "state": "CA",
      "zipCode": "90003"
    },
    "apn": "1234-567-890"
  },
  "transactionDetails": {
    "salePrice": 500000.00,
    "recordingDate": "2025-01-15"
  }
}
```

---

## Version 2: Dynamic

### Purpose

Discover **all available data** without schema constraints using pure AI reasoning.

### Process Flow

```
????????????????????????????????????????????????????????????????
? V2: DYNAMIC EXTRACTION PIPELINE ?
????????????????????????????????????????????????????????????????
?     ?
? Step 1: Build Dynamic Prompt ?
?   ? PromptService loads:?
? - document_extraction_v2.txt?
?   - NO schema    ?
?   - NO rules  ?
?   - NO examples   ?
?   ?
? Step 2: Create User Prompt   ?
?   ? Instruct AI to:?
?   - Analyze all OCR data?
?   - Extract ALL relevant information?
?   - Focus on key categories  ?
?   - Return comprehensive JSON ?
?  ?
? Step 3: Invoke Bedrock?
?   ? Send to model: ?
?   - System: General extraction instructions ?
?   - User: Combined OCR + discovery request  ?
?      ?
? Step 4: Extract JSON ?
?   ? Parse dynamic model response   ?
?     ?
? Step 5: Save Output  ?
?   ? {filename}_v2_dynamic_{timestamp}.json ?
? ?
? Step 6: Create Summary   ?
?   ? Analyze discovered structure ?
?   ? {filename}_v2_extraction_summary_{timestamp}.md  ?
????????????????????????????????????????????????????????????????
```

### Code Implementation

```csharp
private async Task<ProcessingResult> ProcessVersion2(
List<SimplifiedTextractResponse> textractResults,
    string combinedData,
    string baseFileName,
    string timestamp,
    ILambdaContext context = null)
{
    // 1. Build dynamic prompt (NO schema, NO rules, NO examples)
    var promptRequest = new PromptRequest
  {
        TemplateType = "document_extraction",
        Version = "v2",
        IncludeExamples = false,  // Zero-shot
        IncludeRules = false,     // AI-inferred
        SchemaJson = "", // No constraints
        SourceData = combinedData,
        UserMessageTemplate = 
            "Analyze the Textract OCR results below and extract ALL relevant information dynamically. " +
        "Focus on:\n" +
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
    
  // 2. Process with Bedrock
    (string mappedJson, int inputTokens, int outputTokens) = 
   await _bedrockService.ProcessTextractResults(
    combinedData,
     builtPrompt.SystemMessage,
            builtPrompt.UserMessage,
            context
        );
    
    // 3. Save output
    var mappedFilePath = Path.Combine(
        _outputDirectory, 
        $"{baseFileName}_v2_dynamic_{timestamp}.json"
    );
    await File.WriteAllTextAsync(mappedFilePath, mappedJson);
    
    // 4. Create summary report
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
```

### Example V2 Output

```json
{
  "documentType": "Grant Deed",
  "recordingInformation": {
    "recordingNumber": "2025-001234",
    "recordingDate": "January 15, 2025",
    "recordingTime": "10:30 AM",
    "county": "Los Angeles",
    "bookPage": "Book 12345, Page 678"
  },
  "grantees": [
    {
      "fullName": "John Doe",
      "ownershipType": "Joint Tenancy",
      "ownershipPercentage": 50.0,
      "maritalStatus": "Married",
 "address": {
        "fullAddress": "123 Main Street, Los Angeles, CA 90001",
        "parsed": {
          "street": "123 Main Street",
    "city": "Los Angeles",
 "state": "California",
        "zip": "90001"
   }
      }
    },
    {
      "fullName": "Jane Smith",
      "ownershipType": "Joint Tenancy",
      "ownershipPercentage": 50.0,
      "address": {
        "fullAddress": "456 Oak Avenue, Los Angeles, CA 90002"
      }
    }
  ],
  "grantors": [
    {
"fullName": "Previous Owner LLC",
      "entityType": "LLC"
    }
  ],
  "propertyDescription": {
    "siteAddress": "789 Property Lane, Los Angeles, CA 90003",
    "apn": "1234-567-890",
    "legalDescription": "Lot 10, Block 5, Tract 12345...",
    "subdivision": "Sunset Heights",
    "lotNumber": "10",
    "blockNumber": "5",
    "tract": "12345"
  },
  "transactionDetails": {
"considerationAmount": 500000.00,
    "documentaryTransferTax": 550.00,
    "recordingFees": 75.00,
    "totalFees": 625.00
  },
  "pcorInformation": {
  "preliminaryChangeOfOwnership": true,
    "exemptFromReassessment": false,
    "transferType": "Purchase"
  },
  "notaryInformation": {
    "notaryName": "Mary Johnson",
    "notaryNumber": "12345678",
    "notaryExpiration": "12/31/2026",
    "signatureDate": "January 10, 2025"
  },
  "additionalNotes": [
    "Document includes PCOR form",
    "Joint tenancy with right of survivorship",
    "Property tax information attached"
  ]
}
```

**Notice:** V2 discovered many fields not in the base schema:
- Recording time, county, book/page
- Marital status
- Entity type for grantors
- Notary details
- Additional notes

---

## Processing Flow

### Complete End-to-End Flow

```
????????????????????????????????????????????????????????????????
? STEP 1: INITIALIZE ?
????????????????????????????????????????????????????????????????
? Input: List<SimplifiedTextractResponse>?
?  ?
? Each response contains:   ?
?   - RawText ?
?   - FormFields   ?
?- TableData    ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 2: COMBINE DOCUMENTS ?
????????????????????????????????????????????????????????????????
? CombineTextractResults():?
?   ?
? For each document:?
?   === Document ===?
?   RAW TEXT:   ?
?   {raw text}  ?
?    ?
?   FORM FIELDS:?
?   {key: value pairs} ?
?    ?
?   TABLE DATA: ?
?   {table rows}?
?   === End Document === ?
?     ?
? Output: Combined string with all documents?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 3: SAVE COMBINED DATA  ?
????????????????????????????????????????????????????????????????
? File: {filename}_textract_combined_{timestamp}.txt   ?
? Purpose: Reference for debugging  ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 4: PROCESS VERSION 1 (Schema-Based) ?
????????????????????????????????????????????????????????????????
? ? Load schema  ?
? ? Build prompt with schema + rules + examples?
? ? Invoke Bedrock?
? ? Save V1 JSON ?
? ? Analyze schema extensions  ?
?  ?
? Output:?
?   - {filename}_v1_schema_{timestamp}.json ?
?   - {filename}_schema_extensions_{timestamp}.md?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 5: PROCESS VERSION 2 (Dynamic)?
????????????????????????????????????????????????????????????????
? ? Build dynamic prompt (no constraints) ?
? ? Invoke Bedrock   ?
? ? Save V2 JSON ?
? ? Create extraction summary?
?    ?
? Output:?
?   - {filename}_v2_dynamic_{timestamp}.json?
?   - {filename}_v2_extraction_summary_{timestamp}.md  ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 6: RETURN COMBINED RESULT ?
????????????????????????????????????????????????????????????????
? ProcessingResult:?
?   - Success: V1 && V2 both succeeded ?
?   - MappedFilePath: V1 output (primary)?
?   - InputTokens: V1 + V2 combined ?
?   - OutputTokens: V1 + V2 combined?
?   - TotalCost: V1 + V2 combined   ?
????????????????????????????????????????????????????????????????
```

---

## Output Files

### Generated Files

```
CachedFiles_OutputFiles/
??? merged_documents_textract_combined_20250120_103045.txt
??? merged_documents_v1_schema_20250120_103045.json
??? merged_documents_v2_dynamic_20250120_103045.json
??? merged_documents_schema_extensions_20250120_103045.md
??? merged_documents_v2_extraction_summary_20250120_103045.md
```

### File Purposes

| File | Purpose | Use Case |
|------|---------|----------|
| `*_textract_combined_*.txt` | Combined OCR from all documents | Debugging, manual review |
| `*_v1_schema_*.json` | Schema-constrained extraction | **Production database** |
| `*_v2_dynamic_*.json` | Unconstrained extraction | Research, schema improvement |
| `*_schema_extensions_*.md` | Fields in V1 not in schema | Schema design |
| `*_v2_extraction_summary_*.md` | V2 statistics and analysis | Discovery report |

---

## Analysis Reports

### Schema Extensions Report (V1)

**Purpose:** Identify fields extracted that don't exist in base schema

**Example Report:**

```markdown
# Schema Extensions Report (V1)
Generated: 2025-01-20 10:30:45 UTC

Total Extended Fields: 8

## Extended Fields:
```
  + buyerInformation.buyers[0].maritalStatus : string
  + buyerInformation.buyers[0].phoneNumber : string
  + propertyInformation.lotNumber : string
  + propertyInformation.blockNumber : string
  + propertyInformation.tract : string
  + transactionDetails.documentaryTransferTax : number
  + transactionDetails.recordingFees : number
  + notaryInformation : object
```

## Recommendations:
Consider updating the base schema to include frequently occurring extended fields.
```

**How It Works:**

```csharp
private void FindExtensions(JsonNode baseNode, JsonNode extractedNode, string path, List<string> extensions)
{
    if (extractedNode is JsonObject extractedObj)
    {
        var baseObj = baseNode as JsonObject;
   
        foreach (var kvp in extractedObj)
        {
      var fieldPath = string.IsNullOrEmpty(path) ? kvp.Key : $"{path}.{kvp.Key}";
         
          // Field not in base schema - this is an extension
            if (baseObj == null || !baseObj.ContainsKey(kvp.Key))
            {
 var valueType = GetJsonValueType(kvp.Value);
           extensions.Add($"{fieldPath} : {valueType}");
            }
   else
      {
                // Recurse to check nested fields
   FindExtensions(baseObj[kvp.Key], kvp.Value, fieldPath, extensions);
     }
        }
    }
}
```

---

### V2 Extraction Summary

**Purpose:** Analyze comprehensive data discovered by V2

**Example Report:**

```markdown
# V2 Dynamic Extraction Summary
Generated: 2025-01-20 10:30:45 UTC

## Statistics
- Total fields extracted: 47

## Sections Extracted:
```
  - documentType: 1 fields
  - recordingInformation: 6 fields
  - grantees: 12 fields
  - grantors: 3 fields
  - propertyDescription: 8 fields
  - transactionDetails: 5 fields
  - pcorInformation: 4 fields
  - notaryInformation: 5 fields
  - additionalNotes: 3 fields
```

## Key Information:
```
  - Total Buyers/Grantees: 2
  - Total Sellers/Grantors: 1
  - Property Address: 789 Property Lane, Los Angeles, CA 90003
  - Sale Price/Consideration: $500000.00
```

## Comparison Notes:
Compare this V2 output with the V1 schema-based output to see:
- What additional fields were extracted dynamically
- Whether the schema covers all relevant information
- Opportunities to enhance the schema
```

---

## Configuration

### Schema Configuration

```csharp
// SchemaMapperService constructor
public SchemaMapperService(BedrockService bedrockService, IMemoryCache cache, string outputDirectory)
{
    _bedrockService = bedrockService;
    _cache = cache;
    _outputDirectory = outputDirectory;
    
    // Schema file location
    var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    _schemaFilePath = Path.Combine(baseDirectory!, "invoice_schema.json");
    
    // Initialize PromptService
    _promptService = new PromptService(cache);
}
```

### Cost Calculation

```csharp
private decimal CalculateCost(int inputTokens, int outputTokens)
{
    // Qwen 3 pricing
 const decimal INPUT_COST_PER_1K_TOKENS = 0.0008m;
    const decimal OUTPUT_COST_PER_1K_TOKENS = 0.0024m;
    
    var inputCost = (inputTokens / 1000m) * INPUT_COST_PER_1K_TOKENS;
    var outputCost = (outputTokens / 1000m) * OUTPUT_COST_PER_1K_TOKENS;
    
    return inputCost + outputCost;
}
```

---

## Best Practices

### 1. Use V1 for Production
? V1 output matches database schema  
? Consistent structure for all documents  
? Validated against rules

### 2. Use V2 for Discovery
? Find missing schema fields  
? Understand document variations  
? Research new document types

### 3. Regular Schema Updates
? Review schema extensions weekly  
? Add frequently occurring fields to schema  
? Remove unused fields

### 4. Compare Outputs
```powershell
# Compare V1 and V2 side-by-side
code --diff merged_documents_v1_schema_*.json merged_documents_v2_dynamic_*.json
```

### 5. Monitor Metrics
```csharp
Console.WriteLine($"V1: {v1Result.InputTokens} in, {v1Result.OutputTokens} out, ${v1Result.TotalCost:F4}");
Console.WriteLine($"V2: {v2Result.InputTokens} in, {v2Result.OutputTokens} out, ${v2Result.TotalCost:F4}");
Console.WriteLine($"Total: ${(v1Result.TotalCost + v2Result.TotalCost):F4}");
```

---

## Next Steps

After schema mapping:

1. **Review outputs** ? Check JSON files in `CachedFiles_OutputFiles/`
2. **Read analysis** ? Review extension reports and summaries
3. **Update schema** ? Add discovered fields to `invoice_schema.json`
4. **Process results** ? Import V1 JSON into database

---

## Quick Reference

### Run Dual Extraction
```csharp
var result = await _schemaMapper.ProcessAndMapSchema(
    textractResponses,
    schemaFilePath,
    "merged_documents"
);
```

### Output Files
```
*_textract_combined_*.txt   - Source OCR data
*_v1_schema_*.json     - Production output (USE THIS)
*_v2_dynamic_*.json - Discovery output (RESEARCH)
*_schema_extensions_*.md    - Missing schema fields
*_v2_extraction_summary_*.md - V2 statistics
```

### Cost Estimate
```
Typical 3-page document:
V1: 5,000 input + 1,000 output = $0.0064
V2: 5,000 input + 1,500 output = $0.0076
Total: $0.014 per document
```

---

**Need to manage prompts?** See [PRODUCTION_PROMPT_MANAGEMENT.md](PRODUCTION_PROMPT_MANAGEMENT.md) for template system details.
