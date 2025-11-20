using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace AzureTextReader.Services
{
    // Simple credential wrapper used by Program/example.cs
    public class LocalApiKeyCredential
    {
        public string Key { get; }
        public LocalApiKeyCredential(string key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }

    // Minimal options used by existing code
    public class LocalChatCompletionOptions
    {
        public float Temperature { get; set; } = 0.7f;
        public int MaxOutputTokenCount { get; set; } = 1024;
        public float TopP { get; set; } = 1.0f;
        public float FrequencyPenalty { get; set; } = 0.0f;
        public float PresencePenalty { get; set; } = 0.0f;
    }

    // Wrapper client to obtain a ChatClient for a specific deployment
    public class LocalAzureOpenAIClient
    {
        private readonly Uri _endpoint;
        private readonly LocalApiKeyCredential _credential;
        private readonly HttpClient _httpClient;

        public LocalAzureOpenAIClient(Uri endpoint, LocalApiKeyCredential credential, HttpClient? httpClient = null)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _httpClient = httpClient ?? new HttpClient();
            // Do not add header here because we may reuse HttpClient for other calls; add per request
        }

        public LocalChatClient GetChatClient(string deploymentName)
        {
            return new LocalChatClient(_endpoint, deploymentName, _credential, _httpClient);
        }
    }

    // Minimal representation of a chat completion for downstream JSON extraction
    public class LocalChatCompletion
    {
        public List<LocalChatChoice> Choices { get; set; } = new();

        // For convenience expose the raw json if needed
        public string RawJson { get; set; } = string.Empty;
    }

    public class LocalChatChoice
    {
        public LocalChatMessageContent Message { get; set; } = new LocalChatMessageContent();
    }

    public class LocalChatMessageContent
    {
        public string Content { get; set; } = string.Empty;
    }

    // Chat client that calls Azure OpenAI Chat Completions REST endpoint
    public class LocalChatClient
    {
        private readonly Uri _endpoint;
        private readonly string _deployment;
        private readonly LocalApiKeyCredential _credential;
        private readonly HttpClient _http;

        public LocalChatClient(Uri endpoint, string deploymentName, LocalApiKeyCredential credential, HttpClient http)
        {
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _deployment = deploymentName ?? throw new ArgumentNullException(nameof(deploymentName));
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        // Synchronous method to match existing usage in example.cs
        public LocalChatCompletion CompleteChat(IList<object> messages, LocalChatCompletionOptions options)
        {
            var task = CompleteChatAsync(messages, options);
            return task.GetAwaiter().GetResult();
        }

        private async Task<LocalChatCompletion> CompleteChatAsync(IList<object> messages, LocalChatCompletionOptions options)
        {
            // Build request URL: {endpoint}/openai/deployments/{deployment}/chat/completions?api-version=2023-10-01-preview
            var baseUrl = _endpoint.ToString().TrimEnd('/');
            var reqUri = new Uri($"{baseUrl}/openai/deployments/{_deployment}/chat/completions?api-version=2023-10-01-preview");

            // Convert messages to simple role/content pairs using reflection to support different ChatMessage types
            var msgArray = new List<JsonObject>();
            foreach (var m in messages)
            {
                string role = "user";
                string contentStr = m?.ToString() ?? string.Empty;

                try
                {
                    var t = m.GetType();
                    var roleProp = t.GetProperty("Role") ?? t.GetProperty("role") ?? t.GetProperty("Author") ?? t.GetProperty("Name");
                    if (roleProp != null) role = roleProp.GetValue(m)?.ToString() ?? role;

                    var contentProp = t.GetProperty("Content") ?? t.GetProperty("Text") ?? t.GetProperty("Message");
                    if (contentProp != null)
                    {
                        var val = contentProp.GetValue(m);
                        contentStr = val?.ToString() ?? contentStr;
                    }
                }
                catch
                {
                    // ignore reflection failures - fall back to ToString()
                }

                var jo = new JsonObject
                {
                    ["role"] = role,
                    ["content"] = contentStr
                };
                msgArray.Add(jo);
            }

            var payload = new JsonObject
            {
                ["messages"] = new JsonArray(msgArray.ToArray()),
                ["temperature"] = options?.Temperature ?? 0.7f,
                ["max_tokens"] = options?.MaxOutputTokenCount ?? 1024,
                ["top_p"] = options?.TopP ?? 1.0f
            };

            var httpContent = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, reqUri);
            req.Content = httpContent;
            // Azure OpenAI uses 'api-key' header for authentication
            req.Headers.Remove("api-key");
            req.Headers.Add("api-key", _credential.Key);

            var resp = await _http.SendAsync(req);
            var respText = await resp.Content.ReadAsStringAsync();

            var completion = new LocalChatCompletion { RawJson = respText };

            try
            {
                using var doc = JsonDocument.Parse(respText);
                var root = doc.RootElement;

                // Try to extract assistant text from common shapes
                if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in choices.EnumerateArray())
                    {
                        // Try message.content or message.content[0].text or text
                        string extracted = string.Empty;

                        if (ch.TryGetProperty("message", out var msgEl))
                        {
                            if (msgEl.TryGetProperty("content", out var contEl))
                            {
                                if (contEl.ValueKind == JsonValueKind.String)
                                {
                                    extracted = contEl.GetString() ?? string.Empty;
                                }
                                else if (contEl.ValueKind == JsonValueKind.Array)
                                {
                                    // Join array elements if they are strings or objects with 'text'
                                    var sb = new StringBuilder();
                                    foreach (var item in contEl.EnumerateArray())
                                    {
                                        if (item.ValueKind == JsonValueKind.String) sb.AppendLine(item.GetString());
                                        else if (item.ValueKind == JsonValueKind.Object)
                                        {
                                            if (item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                                            {
                                                sb.AppendLine(t.GetString());
                                            }
                                        }
                                    }
                                    extracted = sb.ToString();
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(extracted))
                        {
                            // Fallbacks: look for 'text' or 'text' at top of choice
                            if (ch.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                            {
                                extracted = textEl.GetString() ?? string.Empty;
                            }
                        }

                        // If still empty, try to serialize the choice element as a string
                        if (string.IsNullOrWhiteSpace(extracted)) extracted = ch.ToString();

                        var choice = new LocalChatChoice { Message = new LocalChatMessageContent { Content = extracted } };
                        completion.Choices.Add(choice);
                    }
                }
                else if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                {
                    completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = contentProp.GetString() ?? string.Empty } });
                }
                else
                {
                    // As a last resort, return full response body as single content
                    completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = respText } });
                }
            }
            catch
            {
                // If parsing fails, put entire response into a single choice
                completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = respText } });
            }

            return completion;
        }
    }
}
