using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Services
{
    // Example integration with Azure Cognitive Services Text Analytics via REST API.
    // This class expects user to set environment variables: AZURE_TEXT_ANALYTICS_ENDPOINT and AZURE_TEXT_ANALYTICS_KEY
    // If those are not present it falls back to the NlpServiceStub behavior.
    public class AzureNlpService : INlpService
    {
        private readonly HttpClient _httpClient;
        private readonly NlpServiceStub _fallback = new NlpServiceStub();
        private readonly string _endpoint;
        private readonly string _key;

        public AzureNlpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _endpoint = Environment.GetEnvironmentVariable("AZURE_TEXT_ANALYTICS_ENDPOINT");
            _key = Environment.GetEnvironmentVariable("AZURE_TEXT_ANALYTICS_KEY");
        }

        public async Task<IEnumerable<string>> ExtractKeyPhrases(string text)
        {
            if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_key))
                return await _fallback.ExtractKeyPhrases(text);

            _httpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);

            var uri = new Uri(new Uri(_endpoint), "/text/analytics/v3.2/keyPhrases");
            var payload = new { documents = new[] { new { id = "1", language = "en", text } } };
            var resp = await _httpClient.PostAsJsonAsync(uri, payload);
            if (!resp.IsSuccessStatusCode) return await _fallback.ExtractKeyPhrases(text);

            var body = await resp.Content.ReadFromJsonAsync<KeyPhraseResponse>();
            return body?.documents?.FirstOrDefault()?.keyPhrases ?? Array.Empty<string>();
        }

        public async Task<decimal> DetectSentiment(string text)
        {
            if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_key))
                return await _fallback.DetectSentiment(text);

            _httpClient.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");
            _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _key);
            var uri = new Uri(new Uri(_endpoint), "/text/analytics/v3.2/sentiment");
            var payload = new { documents = new[] { new { id = "1", language = "en", text } } };
            var resp = await _httpClient.PostAsJsonAsync(uri, payload);
            if (!resp.IsSuccessStatusCode) return await _fallback.DetectSentiment(text);

            var body = await resp.Content.ReadFromJsonAsync<SentimentResponse>();
            var doc = body?.documents?.FirstOrDefault();
            if (doc == null) return 0m;

            // Map Azure scores to -1..1 sentiment roughly
            var score = (decimal)(doc.confidenceScores.positive - doc.confidenceScores.negative);
            return score;
        }

        private class KeyPhraseResponse
        {
            public KeyPhraseDocument[] documents { get; set; }
        }

        private class KeyPhraseDocument
        {
            public string id { get; set; }
            public string[] keyPhrases { get; set; }
        }

        private class SentimentResponse
        {
            public SentimentDocument[] documents { get; set; }
        }

        private class SentimentDocument
        {
            public string id { get; set; }
            public SentimentConfidence confidenceScores { get; set; }
        }

        private class SentimentConfidence
        {
            [JsonPropertyName("positive")]
            public double positive { get; set; }
            [JsonPropertyName("negative")]
            public double negative { get; set; }
            [JsonPropertyName("neutral")]
            public double neutral { get; set; }
        }
    }
}
