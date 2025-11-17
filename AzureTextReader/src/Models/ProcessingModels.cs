namespace ImageTextExtractor.Models
{
    /// <summary>
    /// Azure OpenAI Model Configuration with predefined settings
    /// Allows easy switching between different GPT models
    /// </summary>
    public class AzureOpenAIModelConfig
    {
        public string ModelId { get; set; }
        public string DeploymentName { get; set; }
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
        public float TopP { get; set; }
        public float FrequencyPenalty { get; set; }
        public float PresencePenalty { get; set; }

        /// <summary>
        /// GPT-4o-mini - Cost-effective, fast (recommended for most use cases)
        /// </summary>
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

        /// <summary>
        /// GPT-4o - More powerful, higher cost
        /// </summary>
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

        /// <summary>
        /// GPT-4-turbo - Previous generation, still powerful
        /// </summary>
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

        /// <summary>
        /// GPT-3.5-turbo - Budget option for simple extractions
        /// </summary>
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

    /// <summary>
    /// Processing result with metrics
    /// Used to track success, tokens, and timing of LLM processing
    /// </summary>
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string Result { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string ErrorMessage { get; set; }
        public string ModelUsed { get; set; }
        public string Version { get; set; }
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Cached response with metadata
    /// Used for in-memory caching of LLM responses to avoid redundant API calls
    /// </summary>
    public class CachedResponse
    {
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.UtcNow;
        public string ModelId { get; set; }
        public string Version { get; set; }
    }
}
