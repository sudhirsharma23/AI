using System.Threading.Tasks;
using RealEstate.AI.ML.Services;
using RealEstate.AI.Core.Domain;
using Xunit;

namespace RealEstate.AI.Tests
{
    public class RegressionModelTests
    {
        [Fact]
        public async Task PredictMarketValue_ReturnsPositive()
        {
            var svc = new RegressionModelService();
            var fv = new PropertyFeatureVector
            {
                PropertyId = System.Guid.NewGuid(),
                AreaSqFt = 2000m,
                ImageConditionScore = 0.6m,
                DescriptionSentiment = 0.5m,
                ListedPrice = 400000m
            };

            var v = await svc.PredictMarketValueAsync(fv);
            Assert.True(v > 0);
        }
    }
}
