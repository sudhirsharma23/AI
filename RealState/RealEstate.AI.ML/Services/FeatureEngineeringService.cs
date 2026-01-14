using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Services
{
    public class FeatureEngineeringService : IFeatureEngineeringService
    {
        private readonly INlpService _nlp;
        private readonly IImageAnalysisService _image;

        public FeatureEngineeringService(INlpService nlp, IImageAnalysisService image)
        {
            _nlp = nlp;
            _image = image;
        }

        public async Task<PropertyFeatureVector> BuildFeatureVectorAsync(Property property)
        {
            var phrases = await _nlp.ExtractKeyPhrases(property.Description);
            var sentiment = await _nlp.DetectSentiment(property.Description);
            var imgScore = 0.0m;
            if (property.Photos != null && property.Photos.Any())
            {
                imgScore = await _image.AnalyzeConditionScore(property.Photos.First());
            }

            // simple aggregated economic index placeholder
            var features = new PropertyFeatureVector
            {
                PropertyId = property.Id,
                AreaSqFt = property.AreaSqFt,
                YearBuilt = property.YearBuilt,
                Bedrooms = property.Bedrooms,
                Bathrooms = property.Bathrooms,
                ListedPrice = property.ListedPrice ?? 0m,
                AssessedValue = 0m,
                AvgEconomicIndex = 1.0m,
                DescriptionSentiment = sentiment,
                KeyPhrases = phrases.ToList(),
                ImageConditionScore = imgScore
            };

            return features;
        }
    }
}
