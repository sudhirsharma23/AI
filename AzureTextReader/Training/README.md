# Training Data Preparation Guide

## Overview
This directory contains tools and documentation for preparing training data to fine-tune AI models for deed and property document extraction.

## Directory Structure

```
Training/
??? README.md (this file)
??? TrainingDataGenerator.cs (generates training datasets)
??? PromptTestRunner.cs (tests prompt variations)
??? Datasets/
?   ??? raw/ (original documents)
?   ??? processed/ (extracted data)
?   ??? validated/ (human-verified)
??? Outputs/
  ??? azure_openai_format.jsonl
    ??? bedrock_format.jsonl
    ??? evaluation_results.json
```

## Step-by-Step Guide

### Step 1: Collect Historical Data

#### Option A: From OutputFiles Directory
```bash
# Copy processed outputs to training data
cp ../OutputFiles/final_output_*.json ./Datasets/processed/
```

#### Option B: Run New Extractions
```csharp
// In Program.cs, enable training mode
var config = new TrainingConfig {
    SaveRawOcr = true,
    SaveIntermediateResults = true,
    EnableHumanReview = true
};
```

### Step 2: Validate and Clean Data

#### Manual Validation Process
1. Review each `final_output_*.json` file
2. Check for accuracy:
   - Owner/buyer percentages sum to 100
   - All names extracted correctly
   - Dates in proper format (YYYY-MM-DD)
   - Schema extensions are appropriate
3. Mark as validated: move to `Datasets/validated/`

#### Automated Validation
```csharp
var validator = new TrainingDataValidator();
var results = await validator.ValidateBatchAsync("Datasets/processed/");

// Review validation report
results.PrintReport();
// Invalid Percentage Distribution: 3 files
// Missing Required Fields: 1 file
// Schema Violations: 0 files
```

### Step 3: Generate Training Dataset

#### Using TrainingDataGenerator
```csharp
var generator = new TrainingDataGenerator();

// Add validated examples
foreach (var file in Directory.GetFiles("Datasets/validated/", "*.json"))
{
    var ocrText = File.ReadAllText($"corresponding_ocr_{file}");
    var expectedOutput = File.ReadAllText(file);
    var schema = File.ReadAllText("../Schemas/invoice_schema_v1.json");
    
    generator.AddExample(ocrText, schema, expectedOutput);
}

// Export for Azure OpenAI
await generator.ExportForAzureOpenAIAsync("Outputs/azure_openai_training.jsonl");

// Export for AWS Bedrock
await generator.ExportForBedrockAsync("Outputs/bedrock_training.jsonl");
```

#### Expected Output Format

**Azure OpenAI Format** (`azure_openai_training.jsonl`):
```jsonl
{"prompt": "...", "completion": "..."}
{"prompt": "...", "completion": "..."}
{"prompt": "...", "completion": "..."}
```

**AWS Bedrock Format** (`bedrock_training.jsonl`):
```jsonl
{"input": "...", "output": "..."}
{"input": "...", "output": "..."}
{"input": "...", "output": "..."}
```

### Step 4: Quality Assurance

#### Data Quality Checklist
- [ ] Minimum 50 high-quality examples
- [ ] Diverse document types (deeds, trusts, transfers)
- [ ] Cover edge cases (single owner, multiple owners, corporations)
- [ ] Balanced distribution (not all same pattern)
- [ ] Schema extensions validated
- [ ] All percentages verified manually
- [ ] Date formats consistent
- [ ] No PII exposed (redact if necessary)

#### Run Quality Report
```csharp
var report = generator.ValidateTrainingData();
report.PrintReport();

// Output:
// Total Examples: 127
// Valid Examples: 125 (98.4%)
// Invalid JSON: 0
// Empty Prompts: 0
// Invalid Percentages: 2
```

### Step 5: Upload to Cloud Storage

#### Azure OpenAI
```bash
# Upload to Azure Storage
az storage blob upload \
  --account-name <storage-account> \
  --container-name training-data \
  --file Outputs/azure_openai_training.jsonl \
  --name deed-extraction-v1.jsonl
```

#### AWS Bedrock
```bash
# Upload to S3
aws s3 cp Outputs/bedrock_training.jsonl \
  s3://my-bedrock-training-bucket/datasets/deed-extraction-v1.jsonl
```

### Step 6: Create Fine-Tuning Job

#### Azure OpenAI (via Azure CLI)
```bash
az cognitiveservices account deployment create \
  --resource-group <resource-group> \
  --name <openai-resource-name> \
  --deployment-name deed-extraction-ft-v1 \
  --model gpt-4o-mini \
  --training-file-id <file-id> \
  --validation-file-id <validation-file-id>
```

#### AWS Bedrock (via AWS Console or CLI)
```bash
aws bedrock create-model-customization-job \
  --job-name deed-extraction-ft-v1 \
  --custom-model-name deed-extraction-custom \
  --base-model-identifier amazon.titan-text-express-v1 \
  --training-data-config s3Uri=s3://my-bucket/datasets/deed-extraction-v1.jsonl \
  --output-data-config s3Uri=s3://my-bucket/outputs/ \
  --role-arn arn:aws:iam::account-id:role/BedrockCustomizationRole
```

### Step 7: Monitor Training

#### Check Training Status
```csharp
var monitor = new TrainingJobMonitor();

// Azure OpenAI
var azureStatus = await monitor.CheckAzureOpenAIJobAsync(jobId);
Console.WriteLine($"Status: {azureStatus.Status}");
Console.WriteLine($"Progress: {azureStatus.TrainedTokens}/{azureStatus.TotalTokens}");

// AWS Bedrock
var bedrockStatus = await monitor.CheckBedrockJobAsync(jobName);
Console.WriteLine($"Status: {bedrockStatus.Status}");
Console.WriteLine($"Training Loss: {bedrockStatus.TrainingLoss}");
```

### Step 8: Evaluate Fine-Tuned Model

#### Create Test Set
```csharp
// Hold out 20% of data for testing
var splitter = new TrainingDataSplitter();
var (training, testing) = splitter.Split(allExamples, testRatio: 0.2);

generator.ExportTestSetAsync("Outputs/test_set.json", testing);
```

#### Run Evaluation
```csharp
var evaluator = new ModelEvaluator();

// Test original model
var baselineResults = await evaluator.EvaluateAsync(
    modelName: "gpt-4o-mini",
    testSet: "Outputs/test_set.json"
);

// Test fine-tuned model
var fineTunedResults = await evaluator.EvaluateAsync(
    modelName: "deed-extraction-ft-v1",
    testSet: "Outputs/test_set.json"
);

// Compare results
var comparison = evaluator.Compare(baselineResults, fineTunedResults);
comparison.PrintReport();
```

#### Evaluation Metrics
- **Accuracy**: Percentage of correctly extracted fields
- **Precision**: Of extracted owners, how many are correct
- **Recall**: Of actual owners, how many were extracted
- **F1 Score**: Harmonic mean of precision and recall
- **Percentage Accuracy**: % of correct ownership distributions
- **Schema Compliance**: % following schema structure

### Step 9: Deploy Fine-Tuned Model

#### Update Configuration
```csharp
// In AzureAIConfig or similar
public class AzureAIConfig
{
    public string DeploymentName { get; set; } = "deed-extraction-ft-v1"; // Updated
    // ... other config
}
```

#### A/B Testing Setup
```csharp
// Route % of traffic to fine-tuned model
var router = new ModelRouter();
router.AddRoute("gpt-4o-mini", weight: 0.2);
router.AddRoute("deed-extraction-ft-v1", weight: 0.8);

var selectedModel = router.SelectModel();
```

### Step 10: Continuous Improvement

#### Collect Feedback
```csharp
// Log all extractions for review
var logger = new ExtractionLogger();
await logger.LogAsync(new ExtractionLog
{
    ModelUsed = "deed-extraction-ft-v1",
    InputDocument = ocrText,
    OutputJson = extractedData,
    ProcessingTime = duration,
    Accuracy = accuracyScore // if known
});
```

#### Periodic Retraining
```
Week 1-4:   Collect 200+ new examples
Week 5:     Validate and clean data
Week 6:     Generate training set v2
Week 7:     Fine-tune model v2
Week 8:     Evaluate and deploy v2
```

## Cost Estimation

### Azure OpenAI Fine-Tuning Costs (Estimated)
- Training: ~$8-20 per 1M tokens
- Inference: ~$12-36 per 1M tokens (fine-tuned models)
- 100 documents x 5000 tokens = 500K tokens
- Training cost: ~$4-10
- Monthly inference (1000 docs): ~$60-180

### AWS Bedrock Fine-Tuning Costs (Estimated)
- Model customization: $0.008 per 1000 tokens
- Inference: Varies by model
- 100 documents x 5000 tokens = 500K tokens
- Customization cost: ~$4
- Storage: ~$0.10/GB/month

## Best Practices

### Data Quality
? **DO:**
- Use real production documents
- Include edge cases
- Verify accuracy manually
- Balance dataset across scenarios
- Update regularly with new examples

? **DON'T:**
- Use synthetic data only
- Include PII without redaction
- Ignore schema violations
- Rush validation process
- Forget to version datasets

### Model Training
? **DO:**
- Start with small dataset (50-100 examples)
- Monitor training metrics
- Compare with baseline
- A/B test before full deployment
- Keep training data versioned

? **DON'T:**
- Train on unvalidated data
- Skip evaluation step
- Deploy without testing
- Ignore training failures
- Mix different schema versions

### Continuous Improvement
? **DO:**
- Log all extractions
- Track accuracy metrics
- Collect user feedback
- Retrain periodically
- Version all models

? **DON'T:**
- Assume perfection
- Ignore failure patterns
- Skip retraining
- Forget to backup models
- Lose track of versions

## Troubleshooting

### Issue: Low Accuracy After Fine-Tuning
**Possible Causes:**
- Insufficient training data (< 50 examples)
- Poor quality examples
- Schema mismatches
- Overfitting to specific patterns

**Solutions:**
- Add more diverse examples
- Validate data quality
- Check schema consistency
- Increase dataset size

### Issue: Model Produces Invalid JSON
**Possible Causes:**
- Training examples had malformed JSON
- Prompt doesn't emphasize JSON format
- Model overfit to text format

**Solutions:**
- Validate all training JSON
- Add explicit JSON format examples
- Include schema in every training example

### Issue: Incorrect Percentage Distributions
**Possible Causes:**
- Training data had errors
- Not enough percentage variation examples
- Prompt unclear about calculation

**Solutions:**
- Review all training percentages
- Add single, double, triple owner examples
- Enhance percentage rules in prompt

## Resources

### Documentation
- Azure OpenAI Fine-Tuning: [Learn More](https://learn.microsoft.com/azure/ai-services/openai/how-to/fine-tuning)
- AWS Bedrock Customization: [Learn More](https://docs.aws.amazon.com/bedrock/latest/userguide/model-customization.html)

### Tools
- `TrainingDataGenerator.cs` - Generate training datasets
- `PromptTestRunner.cs` - Test prompt variations
- `ModelEvaluator.cs` - Evaluate model performance
- `TrainingJobMonitor.cs` - Monitor training jobs

### Support
- GitHub Issues: Report problems
- Slack Channel: #ai-model-training
- Email: ai-team@company.com

---

**Last Updated**: 2025-01-28  
**Version**: 1.0  
**Maintainer**: AI Development Team
