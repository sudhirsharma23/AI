using System.Threading.Tasks;
using RealEstate.AI.Infrastructure.Services;
using RealEstate.AI.ML.Services;
using Xunit;

namespace RealEstate.AI.Tests
{
    public class FeatureEngineeringTests
    {
        [Fact]
        public async Task BuildFeatureVector_ReturnsValues()
        {
            var nlp = new NlpServiceStub();
            var img = new ImageAnalysisServiceStub();
            var svc = new FeatureEngineeringService(nlp, img);

            var prop = new RealEstate.AI.Core.Domain.Property
            {
                Id = System.Guid.NewGuid(),
                Address = "Test",
                AreaSqFt = 1000m,
                Bedrooms = 2,
                Bathrooms = 1,
                YearBuilt = 2000,
                Description = "A lovely updated property"
            };

            var v = await svc.BuildFeatureVectorAsync(prop);
            Assert.Equal(prop.Id, v.PropertyId);
            Assert.True(v.AreaSqFt > 0);
        }
    }
}
