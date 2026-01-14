using System.Threading.Tasks;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Services
{
    public class ImageAnalysisServiceStub : IImageAnalysisService
    {
        public Task<decimal> AnalyzeConditionScore(byte[] imageBytes)
        {
            // Placeholder: return neutral score
            return Task.FromResult(0.5m);
        }

        public Task<bool> DetectInconsistencies(string description, object imageAnalysisResult)
        {
            // Placeholder: no inconsistency detected
            return Task.FromResult(false);
        }
    }
}
