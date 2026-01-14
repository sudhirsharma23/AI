using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Valuation
{
    public class ValuationOrchestrator
    {
        private readonly IRegressionModelService _regression;
        private readonly ISalesComparisonValuationService _sales;
        private readonly IIncomeValuationService _income;
        private readonly ICostValuationService _cost;

        public ValuationOrchestrator(
            IRegressionModelService regression,
            ISalesComparisonValuationService sales,
            IIncomeValuationService income,
            ICostValuationService cost)
        {
            _regression = regression;
            _sales = sales;
            _income = income;
            _cost = cost;
        }

        public async Task<ValuationSummary> OrchestrateAsync(Property property, PropertyFeatureVector features, System.Collections.Generic.IEnumerable<Property> comps)
        {
            var aiEstimate = await _regression.PredictMarketValueAsync(features);
            var sales = await _sales.CalculateAsync(property, comps);
            var income = await _income.CalculateAsync(property, noi: 20000m, capRate: 0.05m);
            var cost = await _cost.CalculateAsync(property, replacementCost: 300000m, depreciationFactor: 0.1m);

            var summary = new ValuationSummary
            {
                PropertyId = property.Id,
                AiEstimate = aiEstimate,
                SalesComparison = sales,
                IncomeApproach = income,
                CostApproach = cost,
            };

            summary.Notes.Add("AI model is a placeholder linear estimator. Replace with real model.");
            summary.Notes.Add("Sales comparison uses simple comp average. Add geographic and feature adjustments.");

            return summary;
        }
    }
}
