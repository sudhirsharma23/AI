using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Services
{
    public class RiskDetectionService : IRiskDetectionService
    {
        private readonly IImageAnalysisService _image;
        private readonly INlpService _nlp;

        public RiskDetectionService(IImageAnalysisService image, INlpService nlp)
        {
            _image = image;
            _nlp = nlp;
        }

        public async Task<RiskAssessmentResult> AssessAsync(Property property, PropertyFeatureVector features)
        {
            var result = new RiskAssessmentResult
            {
                PropertyId = property.Id,
                RiskScore = 0.0,
            };

            // Simple outlier: price per sqft extreme
            if (features.ListedPrice > 0 && features.AreaSqFt > 0)
            {
                var ppsf = features.ListedPrice / features.AreaSqFt;
                if (ppsf > 1000m || ppsf < 10m)
                {
                    result.RiskScore += 0.5;
                    result.RiskFlags.Add("EXTREME_OUTLIER_PRICE_PER_SQFT");
                    result.SuggestedActions.Add("Manual review of listing price required.");
                }
            }

            // Image-text mismatch placeholder
            if (property.Photos != null && property.Photos.Any())
            {
                var imgScore = await _image.AnalyzeConditionScore(property.Photos.First());
                var inconsistent = await _image.DetectInconsistencies(property.Description, null);
                if (inconsistent)
                {
                    result.RiskScore += 0.3;
                    result.RiskFlags.Add("IMAGE_DESCRIPTION_MISMATCH");
                }

                // low image quality
                if (imgScore < 0.2m)
                {
                    result.RiskScore += 0.2;
                    result.RiskFlags.Add("LOW_IMAGE_QUALITY");
                }
            }

            // Year built outlier
            if (features.YearBuilt < 1800 || features.YearBuilt > DateTime.UtcNow.Year + 1)
            {
                result.RiskScore += 0.2;
                result.RiskFlags.Add("EXTREME_YEAR_BUILT");
            }

            // clamp
            if (result.RiskScore > 1) result.RiskScore = 1;

            return result;
        }
    }
}
