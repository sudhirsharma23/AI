# TextractProcessor - Prompt Service Implementation Guide

## Overview

The TextractProcessor project now implements the same PromptService architecture used in ImageTextExtractor, providing:

? **Template-based prompts** - System prompts stored as files  
? **Extraction rules** - Reusable rules for percentage calculation, name parsing, date formatting  
? **Few-shot examples** - JSON examples for better extraction accuracy  
? **Version support** - Multiple prompt versions (v1, v2)  
? **Caching** - Automatic caching of prompts and rules  
? **Dynamic composition** - Build prompts from modular components  

## Architecture

```
TextractProcessor/
??? Prompts/
?   ??? SystemPrompts/
? ?   ??? document_extraction_v1.txt
?   ?   ??? document_extraction_v2.txt
?   ??? Rules/
?   ?   ??? percentage_calculation.md
?   ?   ??? name_parsing.md
?   ?   ??? date_format.md
?   ??? Examples/
?       ??? default/
?  ??? single_owner.json
?           ??? two_owners.json
?           ??? three_owners.json
??? Services/
?   ??? PromptService.cs
?   ??? SchemaMapperService.cs (updated)
?   ??? BedrockService.cs (updated)
??? Models/
 ??? ...
```

## What Changed

### 1. **New PromptService.cs**
- Loads system prompts from files
- Loads extraction rules from markdown files
- Loads few-shot examples from JSON files
- Builds complete prompts from templates
- Caches everything for performance

### 2. **Updated SchemaMapperService.cs**
- Now uses PromptService to build prompts
- Combines multiple Textract documents
- Supports versioning (v1 schema-based, v2 dynamic)

### 3. **Updated BedrockService.cs**
- Accepts custom system and user prompts
- Maintains backward compatibility

### 4. **New Prompt Files**
- System prompts for document extraction
- Rules for consistent data extraction
- Examples for few-shot learning

## How to Use

### Basic Usage (Default Behavior)

The service automatically uses v1 prompts with all rules and examples:

```csharp
var schemaMapper = new SchemaMapperService(bedrockService, cache, outputDir);
var result = await schemaMapper.ProcessAndMapSchema(
  textractResults,
    schemaPath,
    fileName
);
```

### Custom Prompt Building

```csharp
// Initialize PromptService
var promptService = new PromptService(cache);

// Build V1 prompt (schema-based with rules and examples)
var v1Prompt = await promptService.BuildCompletePromptAsync(new PromptRequest
{
    TemplateType = "document_extraction",
    Version = "v1",
    IncludeExamples = true,
    ExampleSet = "default",
    IncludeRules = true,
    RuleNames = new List<string> { 
        "percentage_calculation", 
        "name_parsing", 
        "date_format" 
    },
    SchemaJson = yourSchema,
    SourceData = textractData
});

// Build V2 prompt (dynamic extraction, no schema)
var v2Prompt = await promptService.BuildCompletePromptAsync(new PromptRequest
{
    TemplateType = "document_extraction",
    Version = "v2",
    IncludeExamples = false,
    IncludeRules = true,
  RuleNames = new List<string> { 
     "percentage_calculation", 
        "name_parsing" 
    },
    SourceData = textractData
});

// Use the built prompt
var (result, inputTokens, outputTokens) = await bedrockService.ProcessTextractResults(
    textractData,
    v1Prompt.SystemMessage,
    v1Prompt.UserMessage,
    context
);
```

## Prompt Templates

### V1: Schema-Based Extraction (document_extraction_v1.txt)

**Purpose**: Extract data according to a predefined schema with ability to extend dynamically

**Features**:
- Schema-guided extraction
- Dynamic schema extension
- Comprehensive field mapping
- Textract-specific guidance (RawText, FormFields, TableData)

**Use When**:
- You have a predefined schema
- Need consistent output structure
- Want to allow schema extensions
- Processing structured documents (deeds, forms)

### V2: Dynamic Extraction (document_extraction_v2.txt)

**Purpose**: Extract ALL relevant information without schema constraints

**Features**:
- No predefined schema
- Comprehensive extraction
- Flexible output structure
- Discovers fields dynamically

**Use When**:
- Don't have a predefined schema
- Want to discover what's in documents
- Need maximum flexibility
- Exploring new document types

## Extraction Rules

### 1. Percentage Calculation (`percentage_calculation.md`)

**Critical Rules**:
- Count owners/buyers FIRST, calculate percentages SECOND
- 1 owner = 100% (NOT 50%!)
- 2 owners = 50% each
- 3 owners = 33.33%, 33.33%, 33.34%
- Always sum to 100%

**Example**:
```json
// WRONG - Single owner with 50%
{
  "oldOwners": [{
    "firstName": "John",
    "lastName": "Smith",
    "percentage": 50
  }]
}

// CORRECT - Single owner with 100%
{
  "oldOwners": [{
    "firstName": "John",
    "lastName": "Smith",
    "percentage": 100
  }]
}
```

### 2. Name Parsing (`name_parsing.md`)

**Core Rules**:
- Split names: firstName, middleName, lastName
- Convert to Title Case (not ALL CAPS)
- Handle suffixes (Jr, Sr, III)
- Detect multiple people ("AND" keyword)
- Distinguish entities from people

**Example**:
```json
// Input: "JOHN DAVID SMITH"
{
  "firstName": "John",
  "middleName": "David",
  "lastName": "Smith"
}

// Input: "JOHN SMITH AND JANE SMITH"
[
  { "firstName": "John", "lastName": "Smith" },
  { "firstName": "Jane", "lastName": "Smith" }
]
```

### 3. Date Formatting (`date_format.md`)

**Standard Format**: `YYYY-MM-DD`

**Conversions**:
- "January 15, 2025" ? `2025-01-15`
- "01/15/2025" ? `2025-01-15`
- "1/5/2025" ? `2025-01-05` (zero-pad)

**Validation**:
- Month: 01-12
- Day: 01-31 (respect month limits)
- Year: 4 digits
- Invalid dates ? `null`

## Few-Shot Examples

### Example 1: Single Owner (single_owner.json)

```json
{
  "title": "Single Owner - 100% Ownership",
  "input": "GRANT DEED from JOHN SMITH, AN INDIVIDUAL",
  "expectedOutput": {
    "oldOwners": [{
      "firstName": "John",
      "lastName": "Smith",
    "percentage": 100,
      "principal": true
    }]
  },
  "explanation": "Single owner = 100%. CRITICAL: Do NOT use 50%!"
}
```

### Example 2: Two Owners (two_owners.json)

```json
{
  "title": "Two Owners - 50% Each",
  "input": "CHARLES D. SHAPIRO AND SUZANNE D. SHAPIRO",
  "expectedOutput": {
    "oldOwners": [
   {
    "firstName": "Charles",
   "middleName": "D.",
  "lastName": "Shapiro",
        "percentage": 50,
        "principal": true
      },
      {
        "firstName": "Suzanne",
        "middleName": "D.",
        "lastName": "Shapiro",
        "percentage": 50,
        "principal": false
      }
    ]
  },
  "explanation": "Two owners split 50/50. 'AND' means separate people."
}
```

### Example 3: Three Owners (three_owners.json)

```json
{
  "title": "Three Owners - 33.33% Each",
  "input": "DAVID CHEN, LISA MARTINEZ, AND JAMES BROWN",
  "expectedOutput": {
    "buyer_names_component": [
      { "firstName": "David", "lastName": "Chen", "buyerPercentage": 33.33 },
      { "firstName": "Lisa", "lastName": "Martinez", "buyerPercentage": 33.33 },
      { "firstName": "James", "lastName": "Brown", "buyerPercentage": 33.34 }
    ]
  },
  "explanation": "Three owners: 33.33%, 33.33%, 33.34% (totals 100%)."
}
```

## Adding New Prompts

### Create New System Prompt

1. Create file: `Prompts/SystemPrompts/your_template_v1.txt`
2. Use placeholders:
   - `{{SCHEMA}}` - Will be replaced with JSON schema
   - `{{EXAMPLES}}` - Will be replaced with formatted examples
   - `{{RULES_PERCENTAGE_CALCULATION}}` - Specific rule
   - `{{RULES_NAME_PARSING}}` - Another rule

3. Use the template:
```csharp
var prompt = await promptService.LoadSystemPromptAsync("your_template", "v1");
```

### Create New Rule

1. Create file: `Prompts/Rules/your_rule.md`
2. Write markdown with clear examples
3. Reference in prompt: `{{RULES_YOUR_RULE}}`

### Create New Example Set

1. Create directory: `Prompts/Examples/your_set/`
2. Add JSON files with structure:
```json
{
  "title": "Description",
  "input": "Sample input text",
  "expectedOutput": { /* JSON structure */ },
  "explanation": "Why this is correct"
}
```

3. Load examples:
```csharp
var examples = await promptService.LoadExamplesAsync("your_set");
```

## Caching Behavior

### What Gets Cached
- ? System prompts (24 hours)
- ? Rules (24 hours)
- ? Examples (24 hours)
- ? Built prompts (24 hours)
- ? Bedrock responses (60 minutes)

### Cache Keys
- Prompts: `prompt_{templateName}_{version}`
- Rules: `rules_{ruleName}`
- Examples: `examples_{exampleSet}`
- Responses: Hash of complete prompt

### Clear Cache
Cache automatically expires, or restart Lambda function for immediate clear.

## Best Practices

### 1. Version Your Prompts
- v1 for schema-based extraction
- v2 for dynamic extraction
- v3 for experimental approaches

### 2. Use Descriptive Rule Names
- `percentage_calculation` ?
- `calc_pct` ?

### 3. Include Comprehensive Examples
- Show common cases
- Show edge cases
- Show error prevention

### 4. Test Prompt Changes
- Always test with real Textract data
- Compare token usage
- Verify extraction accuracy

### 5. Keep Rules Focused
- One rule = one concern
- Clear examples
- Validation checklists

## Migration from Hardcoded Prompts

### Before (Hardcoded in BedrockService)
```csharp
var prompt = $@"
    Extract data from documents...
    Rules: {hardcodedRules}
  Schema: {schema}
";
```

### After (Template-Based)
```csharp
var prompt = await promptService.BuildCompletePromptAsync(new PromptRequest
{
    TemplateType = "document_extraction",
    Version = "v1",
    IncludeExamples = true,
    IncludeRules = true,
    SchemaJson = schema,
    SourceData = data
});
```

## Troubleshooting

### Error: "Prompt template not found"
**Cause**: File doesn't exist or wrong path

**Solution**:
1. Verify file exists in Prompts directory
2. Check filename matches: `{templateName}_{version}.txt`
3. Ensure files copied to output (check .csproj)

### Error: "Examples directory not found"
**Cause**: Example set doesn't exist

**Solution**:
1. Create directory: `Prompts/Examples/{exampleSet}/`
2. Add at least one JSON file
3. Or set `IncludeExamples = false`

### Error: "Rules file not found"
**Cause**: Rule markdown file doesn't exist

**Solution**:
1. Create file: `Prompts/Rules/{ruleName}.md`
2. Or remove from `RuleNames` list
3. Or set `IncludeRules = false`

## Performance Tips

### 1. Use Caching
All prompts are cached automatically. First Lambda invocation loads files, subsequent invocations use cache.

### 2. Minimize Examples
Only include relevant examples. More examples = longer prompts = higher costs.

### 3. Optimize Rules
Keep rules concise but clear. Remove unnecessary examples from rule files.

### 4. Version Appropriately
Use v1 (schema-based) when you have a schema. Use v2 (dynamic) for exploration only.

## Comparison: ImageTextExtractor vs TextractProcessor

### ImageTextExtractor
- OCR: Azure Document Analyzer or Aspose
- LLM: Azure OpenAI (GPT models)
- Input: Direct image URLs
- Prompts: `deed_extraction_v1`, `deed_extraction_v2`

### TextractProcessor
- OCR: AWS Textract
- LLM: AWS Bedrock (Claude, Titan, Nova, Qwen)
- Input: Textract OCR results (RawText, FormFields, TableData)
- Prompts: `document_extraction_v1`, `document_extraction_v2`

### Shared Architecture
? Same PromptService pattern  
? Same Rules (percentage_calculation, name_parsing, date_format)  
? Same Examples structure  
? Same template system  
? Same caching strategy  

## Summary

? **Implemented** PromptService matching ImageTextExtractor pattern  
? **Created** System prompts (v1 schema-based, v2 dynamic)  
? **Added** Extraction rules (percentage, name parsing, date format)  
? **Included** Few-shot examples (single/two/three owners)  
? **Updated** SchemaMapperService to use PromptService  
? **Updated** BedrockService to accept custom prompts  
? **Maintained** Backward compatibility  
? **Enabled** Version support for easy experimentation  

**The TextractProcessor now follows the same best practices and pattern as ImageTextExtractor!**

