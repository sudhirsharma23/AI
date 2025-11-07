using System.Text.Json.Serialization;

namespace TextractProcessor.Models
{
    public class BedrockModelConfig
    {
        public string ModelId { get; set; }
        public int MaxTokens { get; set; }
        public float Temperature { get; set; }
        public float TopP { get; set; }
        public string[] StopSequences { get; set; }
        public string Version { get; set; }
        public RequestFormat RequestFormat { get; set; }
        public ResponseFormat ResponseFormat { get; set; }

        public static BedrockModelConfig TitanTextExpress => new()
        {
            ModelId = "amazon.titan-text-express-v1",
            MaxTokens = 4000,
            Temperature = 0.7f,
            TopP = 1.0f,
            StopSequences = new[] { "```" },
            RequestFormat = RequestFormat.Titan,
            ResponseFormat = ResponseFormat.Titan
        };

        public static BedrockModelConfig Claude3Haiku => new()
        {
            ModelId = "anthropic.claude-3-haiku-20240307",
            MaxTokens = 4000,
            Temperature = 0.7f,
            TopP = 1.0f,
            StopSequences = new[] { "Human:", "Assistant:" },
            Version = "bedrock-2023-05-31",
            RequestFormat = RequestFormat.Claude,
            ResponseFormat = ResponseFormat.Claude
        };
    }

    public enum RequestFormat
    {
        Titan,
        Claude
    }

    public enum ResponseFormat
    {
        Titan,
        Claude
    }

    public class BedrockResponse
    {
        [JsonPropertyName("results")]
        public List<TitanResult> Results { get; set; } = new();

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("stop_reason")]
        public string StopReason { get; set; } = string.Empty;

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; }
    }

    public class TitanResult
    {
        [JsonPropertyName("outputText")]
        public string OutputText { get; set; } = string.Empty;

        [JsonPropertyName("completionReason")]
        public string CompletionReason { get; set; } = string.Empty;

        [JsonPropertyName("inputTextTokenCount")]
        public int InputTextTokenCount { get; set; }

        [JsonPropertyName("outputTextTokenCount")]
        public int OutputTextTokenCount { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}