using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Services
{
    public class NlpServiceStub : INlpService
    {
        public Task<IEnumerable<string>> ExtractKeyPhrases(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult<IEnumerable<string>>(new List<string>());

            var words = text.Split(' ', '\n', '\r');
            var phrases = words.Where(w => w.Length > 4).Take(5).Distinct().ToList();
            return Task.FromResult<IEnumerable<string>>(phrases);
        }

        public Task<decimal> DetectSentiment(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(0m);

            // Dummy sentiment: positive if contains certain words
            var score = 0m;
            var t = text.ToLowerInvariant();
            if (t.Contains("love") || t.Contains("updated") || t.Contains("spacious")) score += 0.6m;
            if (t.Contains("fixer") || t.Contains("needs") || t.Contains("as-is")) score -= 0.4m;
            return Task.FromResult(score);
        }
    }
}
