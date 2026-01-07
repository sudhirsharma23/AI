using System;

namespace Oasis.DeedProcessor.BusinessEntities.Models
{
    public class AzureOpenAIModelConfig
    {
        public string ModelId { get; set; } = string.Empty;
        public string DeploymentName { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
        public float TopP { get; set; }
        public float FrequencyPenalty { get; set; }
        public float PresencePenalty { get; set; }

        public static AzureOpenAIModelConfig GPT4oMini => new()
        {
            ModelId = "gpt-4o-mini",
            DeploymentName = "gpt-4o-mini",
            MaxTokens = 13107,
            Temperature = 0.7f,
            TopP = 0.95f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f
        };

        public static AzureOpenAIModelConfig GPT4o => new()
        {
            ModelId = "gpt-4o",
            DeploymentName = "gpt-4o",
            MaxTokens = 16384,
            Temperature = 0.7f,
            TopP = 0.95f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f
        };

        public static AzureOpenAIModelConfig GPT4Turbo => new()
        {
            ModelId = "gpt-4-turbo",
            DeploymentName = "gpt-4-turbo",
            MaxTokens = 4096,
            Temperature = 0.7f,
            TopP = 0.95f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f
        };

        public static AzureOpenAIModelConfig GPT35Turbo => new()
        {
            ModelId = "gpt-35-turbo",
            DeploymentName = "gpt-35-turbo",
            MaxTokens = 4096,
            Temperature = 0.7f,
            TopP = 0.95f,
            FrequencyPenalty = 0.0f,
            PresencePenalty = 0.0f
        };
    }

    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string Result { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    public class CachedResponse
    {
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public string ModelId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
