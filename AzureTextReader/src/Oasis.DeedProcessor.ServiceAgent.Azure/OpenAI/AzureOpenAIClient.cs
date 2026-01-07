using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Oasis.DeedProcessor.ServiceAgent.Azure.OpenAI
{
    public class LocalApiKeyCredential
    {
        public string Key { get; }
        public LocalApiKeyCredential(string key)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
        }
    }

    public class LocalChatCompletionOptions
    {
        public float Temperature { get; set; } = 0.7f;
        public int MaxOutputTokenCount { get; set; } = 1024;
        public float TopP { get; set; } = 1.0f;
        public float FrequencyPenalty { get; set; } = 0.0f;
        public float PresencePenalty { get; set; } = 0.0f;
    }

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
        }

        public LocalChatClient GetChatClient(string deploymentName)
        {
            return new LocalChatClient(_endpoint, deploymentName, _credential, _httpClient);
        }
    }

    public class LocalChatCompletion
    {
        public List<LocalChatChoice> Choices { get; set; } = new();
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

        public LocalChatCompletion CompleteChat(IList<object> messages, LocalChatCompletionOptions options)
        {
            var task = CompleteChatAsync(messages, options);
            return task.GetAwaiter().GetResult();
        }

        private async Task<LocalChatCompletion> CompleteChatAsync(IList<object> messages, LocalChatCompletionOptions options)
        {
            var baseUrl = _endpoint.ToString().TrimEnd('/');
            var reqUri = new Uri($"{baseUrl}/openai/deployments/{_deployment}/chat/completions?api-version=2023-10-01-preview");

            var msgArray = new List<JsonObject>();
            foreach (var m in messages)
            {
                string role = "user";
                string contentStr = m?.ToString() ?? string.Empty;

                try
                {
                    var t = m!.GetType();
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
                }

                msgArray.Add(new JsonObject
                {
                    ["role"] = role,
                    ["content"] = contentStr
                });
            }

            var payload = new JsonObject
            {
                ["messages"] = new JsonArray(msgArray.ToArray()),
                ["temperature"] = options?.Temperature ?? 0.7f,
                ["max_tokens"] = options?.MaxOutputTokenCount ?? 1024,
                ["top_p"] = options?.TopP ?? 1.0f
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, reqUri);
            req.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            req.Headers.Remove("api-key");
            req.Headers.Add("api-key", _credential.Key);

            var resp = await _http.SendAsync(req);
            var respText = await resp.Content.ReadAsStringAsync();

            var completion = new LocalChatCompletion { RawJson = respText };

            try
            {
                using var doc = JsonDocument.Parse(respText);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ch in choices.EnumerateArray())
                    {
                        string extracted = string.Empty;

                        if (ch.TryGetProperty("message", out var msgEl) && msgEl.TryGetProperty("content", out var contEl))
                        {
                            if (contEl.ValueKind == JsonValueKind.String)
                            {
                                extracted = contEl.GetString() ?? string.Empty;
                            }
                            else if (contEl.ValueKind == JsonValueKind.Array)
                            {
                                var sb = new StringBuilder();
                                foreach (var item in contEl.EnumerateArray())
                                {
                                    if (item.ValueKind == JsonValueKind.String) sb.AppendLine(item.GetString());
                                    else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                                        sb.AppendLine(t.GetString());
                                }
                                extracted = sb.ToString();
                            }
                        }

                        if (string.IsNullOrWhiteSpace(extracted) && ch.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                        {
                            extracted = textEl.GetString() ?? string.Empty;
                        }

                        if (string.IsNullOrWhiteSpace(extracted)) extracted = ch.ToString();

                        completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = extracted } });
                    }
                }
                else
                {
                    completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = respText } });
                }
            }
            catch
            {
                completion.Choices.Add(new LocalChatChoice { Message = new LocalChatMessageContent { Content = respText } });
            }

            return completion;
        }
    }
}
