using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Valuation
{
    public class CostValuationService : ICostValuationService
    {
        public Task<ValuationResult> CalculateAsync(Property property, decimal replacementCost, decimal depreciationFactor)
        {
            var estimate = replacementCost * (1 - depreciationFactor);
            return Task.FromResult(new ValuationResult
            {
                EstimatedValue = decimal.Round(estimate, 2),
                Methodology = "Cost",
                Notes = "Replacement cost minus depreciation."
            });
        }
    }
}
