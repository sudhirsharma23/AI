using System.Threading.Tasks;
using RealEstate.AI.ML.Services;
using RealEstate.AI.Infrastructure.Services;
using RealEstate.AI.Core.Domain;
using Xunit;

namespace RealEstate.AI.Tests
{
    public class RiskDetectionTests
    {
        [Fact]
        public async Task Assess_FlagsOutlierPricePerSqft()
        {
            var img = new ImageAnalysisServiceStub();
            var nlp = new NlpServiceStub();
            var svc = new RiskDetectionService(img, nlp);

            var prop = new Property
            {
                Id = System.Guid.NewGuid(),
                AreaSqFt = 100m
            };

            var features = new PropertyFeatureVector
            {
                PropertyId = prop.Id,
                AreaSqFt = prop.AreaSqFt,
                ListedPrice = 200000m,
                YearBuilt = 2000
            };

            var res = await svc.AssessAsync(prop, features);
            Assert.Contains("EXTREME_OUTLIER_PRICE_PER_SQFT", res.RiskFlags);
        }
    }
}
