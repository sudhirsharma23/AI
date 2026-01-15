using Swashbuckle.AspNetCore.Filters;
using RealEstate.AI.WebApi.DTOs;

namespace RealEstate.AI.WebApi.Swagger.ExampleProviders
{
    public class RiskAssessmentExample : IExamplesProvider<RiskAssessmentDto>
    {
        public RiskAssessmentDto GetExamples()
        {
            return new RiskAssessmentDto
            {
                PropertyId = System.Guid.NewGuid(),
                RiskScore = 0.45,
                RiskFlags = new System.Collections.Generic.List<string> { "EXTREME_OUTLIER_PRICE_PER_SQFT" },
                SuggestedActions = new System.Collections.Generic.List<string> { "Manual review of listing price required." }
            };
        }
    }
}
