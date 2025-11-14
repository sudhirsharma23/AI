# ImageTextExtractor - Prompt System Refactoring

## Overview
This document outlines the refactoring of the ImageTextExtractor project's prompt system to improve maintainability, testability, and model training preparation.

## Current State Analysis

### Existing Prompt Location
- **File**: `..\AzureTextReader\src\Program.cs`
- **Method**: `ProcessWithChatCompletion()`
- **Lines**: ~196-400 (embedded SystemChatMessage)
- **Schema**: `invoice_schema - Copy.json`

### Issues with Current Implementation
1. **Hardcoded Prompts** - Embedded directly in code (difficult to modify/test)
2. **Mixed Concerns** - Business logic mixed with prompt engineering
3. **No Version Control** - Changes to prompts require code deployment
4. **Training Prep** - Not structured for easy model fine-tuning
5. **Maintenance** - Hard to A/B test different prompt variations

## Proposed Solution

### New Architecture

```
..\AzureTextReader\
??? src\
?   ??? Program.cs (cleaned, delegates to services)
?   ??? Configuration\
?   ?   ??? AzureAIConfig.cs (existing)
?   ??? Services\
?   ? ??? PromptService.cs (NEW - manages prompts)
?   ?   ??? OcrExtractionService.cs (NEW - OCR logic)
?   ?   ??? SchemaValidationService.cs (NEW - schema validation)
?   ??? Models\
?   ?   ??? PromptTemplate.cs (NEW)
?   ?   ??? ExtractionRequest.cs (NEW)
?   ?   ??? ExtractionResponse.cs (NEW)
?   ??? Prompts\
?       ??? SystemPrompts\
?       ?   ??? base_extraction_v1.txt
?       ?   ??? deed_extraction_v1.txt
? ?   ??? ownership_transfer_v1.txt
?       ??? Examples\
?     ?   ??? single_owner_example.json
?       ?   ??? two_owners_example.json
?       ?   ??? three_owners_example.json
?       ??? Rules\
?           ??? percentage_calculation_rules.md
?     ??? name_parsing_rules.md
?           ??? date_format_rules.md
??? Schemas\
?   ??? invoice_schema_v1.json (renamed from Copy)
?   ??? schema_versions.md (changelog)
??? Training\
  ??? TrainingDataGenerator.cs (NEW)
    ??? PromptTestRunner.cs (NEW)
 ??? README.md
```

## Implementation Plan

### Phase 1: Extract Prompt Components (Week 1)

#### Task 1.1: Create Prompt Service
**File**: `Services\PromptService.cs`
```csharp
public class PromptService
{
    private readonly string _promptsDirectory;
    private readonly IMemoryCache _cache;
    
    public Task<string> LoadSystemPromptAsync(string templateName);
    public Task<List<Example>> LoadExamplesAsync(string exampleSet);
    public Task<string> LoadRulesAsync(string ruleName);
  public Task<string> BuildCompletePromptAsync(PromptRequest request);
}
```

#### Task 1.2: Extract Prompts to Files
1. Create `Prompts\SystemPrompts\deed_extraction_v1.txt`
2. Extract current prompt from Program.cs
3. Split into logical sections:
   - Base instructions
   - Dynamic schema rules
   - Owner/buyer handling
   - Validation rules

#### Task 1.3: Extract Examples
1. Create JSON files for each example
2. Structure: `{prompt_input, expected_output, explanation}`
3. Separate by scenario (1 owner, 2 owners, 3 owners, etc.)

### Phase 2: Refactor Program.cs (Week 1)

#### Task 2.1: Clean Main Logic
**Before** (Current):
```csharp
var messages = new List<ChatMessage> {
    new SystemChatMessage(@"You are an intelligent OCR-like data extraction tool...
    [200+ lines of embedded text]
    "),
    new UserChatMessage($"Please extract...")
};
```

**After** (Proposed):
```csharp
var promptService = new PromptService(_cache);
var systemPrompt = await promptService.BuildCompletePromptAsync(new PromptRequest
{
    TemplateType = "deed_extraction",
    Version = "v1",
    IncludeExamples = true,
    SchemaPath = schemaPath,
    SourceData = combinedMarkdown
});

var messages = new List<ChatMessage> {
    new SystemChatMessage(systemPrompt.SystemMessage),
    new UserChatMessage(systemPrompt.UserMessage)
};
```

#### Task 2.2: Create Extraction Service
**File**: `Services\OcrExtractionService.cs`
```csharp
public class OcrExtractionService
{
    public async Task<string> ExtractFromImageAsync(string imageUrl);
    public async Task<CombinedOcrResult> ProcessMultipleImagesAsync(string[] imageUrls);
}
```

#### Task 2.3: Simplify ProcessWithChatCompletion
- Delegate prompt building to PromptService
- Delegate OCR to OcrExtractionService
- Keep only orchestration logic

### Phase 3: Schema Management (Week 2)

#### Task 3.1: Rename and Version Schema
1. Rename `invoice_schema - Copy.json` ? `invoice_schema_v1.json`
2. Create `schema_versions.md`:
```markdown
# Schema Version History

## v1.0.0 (2025-01-28)
- Initial version
- Base fields for deed extraction
- Owner/buyer arrays
- Transfer information object

## Future Versions
- v1.1.0: Add common extensions (property_tax_year, etc.)
- v2.0.0: Add validation rules in schema
```

#### Task 3.2: Create Schema Validation Service
**File**: `Services\SchemaValidationService.cs`
```csharp
public class SchemaValidationService
{
    public Task<ValidationResult> ValidateAgainstSchemaAsync(string jsonData, string schemaPath);
    public Task<List<SchemaExtension>> FindExtensionsAsync(string baseSchema, string extractedData);
    public Task<SchemaUpdateReport> GenerateUpdateRecommendationsAsync();
}
```

### Phase 4: Training Data Preparation (Week 2)

#### Task 4.1: Create Training Data Generator
**File**: `Training\TrainingDataGenerator.cs`
```csharp
public class TrainingDataGenerator
{
    public async Task<TrainingDataset> GenerateFromHistoricalDataAsync();
    public async Task ExportForBedrockFineTuningAsync(string outputPath);
    public async Task ExportForAzureOpenAIFineTuningAsync(string outputPath);
    public async Task AddManualExampleAsync(string input, string output);
}
```

#### Task 4.2: Create Prompt Test Runner
**File**: `Training\PromptTestRunner.cs`
```csharp
public class PromptTestRunner
{
    public async Task<TestResults> RunTestSuiteAsync(string promptVersion);
    public async Task<ComparisonReport> ComparePromptsAsync(string v1, string v2);
    public async Task<AccuracyMetrics> EvaluateAccuracyAsync();
}
```

### Phase 5: Documentation and Examples (Week 2)

#### Task 5.1: Create Prompt Documentation
**File**: `Prompts\README.md`
- How to modify prompts
- Versioning strategy
- Testing procedures
- Best practices

#### Task 5.2: Create Training Guide
**File**: `Training\README.md`
- How to generate training data
- Model fine-tuning steps
- Evaluation procedures
- Cost estimation

## Benefits of Refactoring

### 1. Maintainability
? Prompts in separate files - easy to edit  
? Version control for prompts  
? A/B testing different variations  
? No code deployment for prompt changes  

### 2. Testability
? Test prompts independently  
? Automated test suites
? Accuracy measurements  
? Regression detection  

### 3. Training Preparation
? Structured training data generation  
? Export to multiple formats (Azure OpenAI, Bedrock)  
? Historical data leveraging  
? Quality validation  

### 4. Team Collaboration
? Prompt engineers work independently  
? Developers focus on code  
? Clear separation of concerns  
? Better code reviews  

### 5. Performance
? Prompt caching by version  
? Lazy loading of examples  
? Reduced memory footprint  
? Faster deployment  

## Migration Strategy

### Step 1: Parallel Implementation (Week 1-2)
- Build new structure alongside existing code
- Keep current implementation working
- Add feature flag: `USE_NEW_PROMPT_SYSTEM`

### Step 2: Testing (Week 2)
- Test with existing documents
- Compare outputs: old vs new
- Verify accuracy maintained
- Performance benchmarking

### Step 3: Gradual Rollout (Week 3)
- Enable for 10% of requests
- Monitor for issues
- Increase to 50%
- Full rollout

### Step 4: Cleanup (Week 3)
- Remove old prompt code
- Update documentation
- Archive old implementations
- Celebrate! ??

## File Changes Summary

### New Files (12 total)
1. `Services\PromptService.cs`
2. `Services\OcrExtractionService.cs`
3. `Services\SchemaValidationService.cs`
4. `Models\PromptTemplate.cs`
5. `Models\ExtractionRequest.cs`
6. `Models\ExtractionResponse.cs`
7. `Prompts\SystemPrompts\deed_extraction_v1.txt`
8. `Prompts\Examples\*.json` (3 files)
9. `Prompts\Rules\*.md` (3 files)
10. `Training\TrainingDataGenerator.cs`
11. `Training\PromptTestRunner.cs`
12. `Training\README.md`

### Modified Files (2 total)
1. `Program.cs` - Simplified, delegates to services
2. `invoice_schema - Copy.json` - Renamed to `invoice_schema_v1.json`

### Total LOC Change
- **Lines Removed**: ~250 (from Program.cs)
- **Lines Added**: ~800 (new services + prompts)
- **Net Change**: +550 lines (better organized)

## Success Metrics

### Technical Metrics
- [ ] Prompt modification time < 5 minutes (no deployment)
- [ ] A/B test setup time < 10 minutes
- [ ] Training data generation < 1 hour
- [ ] Test suite runs in < 2 minutes

### Quality Metrics
- [ ] Extraction accuracy >= 95%
- [ ] Schema extension detection >= 90%
- [ ] Percentage calculation errors < 1%
- [ ] Name parsing accuracy >= 98%

### Business Metrics
- [ ] Deployment frequency increases 3x
- [ ] Bug resolution time decreases 50%
- [ ] New feature development time decreases 40%
- [ ] Team velocity increases 25%

## Timeline

| Week | Tasks | Deliverables |
|------|-------|--------------|
| 1 | Phase 1 & 2 | Prompt extraction, service creation |
| 2 | Phase 3 & 4 | Schema management, training prep |
| 3 | Phase 5 & Migration | Documentation, rollout |

## Next Steps

1. **Review & Approve** this plan
2. **Create Feature Branch**: `feature/prompt-system-refactoring`
3. **Start Phase 1**: Extract prompts to files
4. **Weekly Reviews**: Track progress and adjust

---

**Document Version**: 1.0  
**Created**: 2025-01-28  
**Author**: AI Development Team  
**Status**: ?? Proposal - Awaiting Approval
