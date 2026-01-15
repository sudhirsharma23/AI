using Swashbuckle.AspNetCore.Filters;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.WebApi.Swagger.ExampleProviders
{
    public class ValuationSummaryExample : IExamplesProvider<ValuationSummary>
    {
        public ValuationSummary GetExamples()
        {
            return new ValuationSummary
            {
                PropertyId = Guid.NewGuid(),
                AiEstimate = 320000m,
                SalesComparison = new ValuationResult { EstimatedValue = 310000m, Methodology = "SalesComparison", Notes = "Simple comp adjustment" },
                IncomeApproach = new ValuationResult { EstimatedValue = 400000m, Methodology = "Income", Notes = "NOI / cap rate" },
                CostApproach = new ValuationResult { EstimatedValue = 280000m, Methodology = "Cost", Notes = "Replacement cost minus depreciation" },
                Notes = new System.Collections.Generic.List<string> { "AI model is a placeholder linear estimator." }
            };
        }
    }
}
