# Azure OpenAI Model Configuration Guide

## Overview

The `AzureOpenAIModelConfig` class provides an easy way to switch between different GPT models without changing code throughout your application.

## Quick Start

### 1. Choose Your Model

In `Program.cs`, change the model configuration:

```csharp
// At the top of Program class
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;
```

### 2. Available Models

#### GPT-4o-mini (Recommended)
- **Best for**: Most use cases
- **Cost**: Low ($0.150/1M input tokens, $0.600/1M output tokens)
- **Speed**: Fast
- **Max Tokens**: 13,107
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4oMini;
```

#### GPT-4o
- **Best for**: Complex reasoning, highest accuracy
- **Cost**: High ($5.00/1M input tokens, $15.00/1M output tokens)
- **Speed**: Moderate
- **Max Tokens**: 16,384
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4o;
```

#### GPT-4-turbo
- **Best for**: Previous generation, still powerful
- **Cost**: Medium ($10.00/1M input tokens, $30.00/1M output tokens)
- **Speed**: Moderate
- **Max Tokens**: 4,096
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4Turbo;
```

#### GPT-3.5-turbo
- **Best for**: Simple extractions, budget option
- **Cost**: Very Low ($0.50/1M input tokens, $1.50/1M output tokens)
- **Speed**: Very Fast
- **Max Tokens**: 4,096
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT35Turbo;
```

## Model Configuration Properties

Each model configuration includes:

| Property | Description | Example |
|----------|-------------|---------|
| `ModelId` | OpenAI model identifier | `gpt-4o-mini` |
| `DeploymentName` | Azure deployment name | `gpt-4o-mini` |
| `MaxTokens` | Maximum output tokens | `13107` |
| `Temperature` | Creativity (0.0-2.0) | `0.7` |
| `TopP` | Nucleus sampling | `0.95` |
| `FrequencyPenalty` | Penalize repetition | `0.0` |
| `PresencePenalty` | Penalize topics | `0.0` |

## Custom Configuration

You can create your own custom configuration:

```csharp
private static readonly AzureOpenAIModelConfig CustomModel = new()
{
    ModelId = "gpt-4o",
  DeploymentName = "my-custom-deployment",
    MaxTokens = 8000,
    Temperature = 0.5f,
    TopP = 0.9f,
    FrequencyPenalty = 0.2f,
    PresencePenalty = 0.1f
};
```

## Cost Comparison

Based on 1000 documents with 10,000 tokens input + 2,000 tokens output:

| Model | Input Cost | Output Cost | Total Cost |
|-------|------------|-------------|------------|
| GPT-4o-mini | $1.50 | $1.20 | **$2.70** |
| GPT-3.5-turbo | $5.00 | $3.00 | **$8.00** |
| GPT-4-turbo | $100.00 | $60.00 | **$160.00** |
| GPT-4o | $50.00 | $30.00 | **$80.00** |

## Performance Characteristics

| Model | Accuracy | Speed | Cost | Recommended Use |
|-------|----------|-------|------|-----------------|
| GPT-4o-mini | High | Fast | Low | ? Default choice |
| GPT-3.5-turbo | Medium | Very Fast | Very Low | Simple extractions |
| GPT-4-turbo | Very High | Moderate | High | Complex documents |
| GPT-4o | Highest | Moderate | Medium-High | Critical accuracy |

## How It Works

The `CreateChatOptions()` method reads from `ModelConfig`:

```csharp
private static ChatCompletionOptions CreateChatOptions()
{
    return new ChatCompletionOptions
    {
        Temperature = ModelConfig.Temperature,
        MaxOutputTokenCount = ModelConfig.MaxTokens,
      TopP = ModelConfig.TopP,
   FrequencyPenalty = ModelConfig.FrequencyPenalty,
        PresencePenalty = ModelConfig.PresencePenalty,
    };
}
```

This is called in both `ProcessVersion1()` and `ProcessVersion2()`.

## Switching Models

To switch models:

1. Change the configuration line in `Program.cs`:
   ```csharp
   private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4o;
   ```

2. Rebuild and run:
   ```bash
   dotnet build
   dotnet run
   ```

3. All processing will use the new model configuration!

## Model Selection Guide

### When to use GPT-4o-mini (Default)
? Most document extraction tasks  
? Cost-conscious deployments  
? High-volume processing  
? Real-time applications  

### When to use GPT-4o
? Complex reasoning required  
? Highest accuracy needed  
? Mission-critical extractions  
? Multi-step analysis  

### When to use GPT-4-turbo
? Previous generation fallback  
? Specific deployment requirements  
? Compatibility needs  

### When to use GPT-3.5-turbo
? Simple field extraction  
? Budget constraints  
? Very high volume (millions of docs)  
? Prototype/development  

## Best Practices

1. **Start with GPT-4o-mini**: It provides the best balance of cost, speed, and accuracy

2. **Monitor accuracy**: Track extraction quality metrics

3. **A/B Test**: Compare models on your specific documents

4. **Adjust temperature**:
   - `0.0` = Deterministic, same output every time
   - `0.7` = Balanced (default)
   - `1.5+` = Creative, varied output

5. **Monitor costs**: Track token usage and costs

## Troubleshooting

### Issue: Model not found
**Error**: `Deployment 'gpt-4o-mini' not found`

**Solution**: Update `DeploymentName` to match your Azure deployment:
```csharp
DeploymentName = "your-actual-deployment-name"
```

### Issue: Max tokens exceeded
**Error**: `Maximum token limit exceeded`

**Solution**: Reduce `MaxTokens` or split your documents:
```csharp
MaxTokens = 8000  // Reduce from 13107
```

### Issue: Slow processing
**Solution**: Switch to a faster model:
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT35Turbo;
```

### Issue: Poor accuracy
**Solution**: Switch to a more powerful model:
```csharp
private static readonly AzureOpenAIModelConfig ModelConfig = AzureOpenAIModelConfig.GPT4o;
```

## Summary

? **Easy model switching** - Change one line of code  
? **Predefined configurations** - No guessing parameters  
? **Cost-aware** - Know what you're spending  
? **Flexible** - Create custom configurations  
? **Maintainable** - Centralized configuration  

**Current configuration**: GPT-4o-mini (recommended for most use cases)

