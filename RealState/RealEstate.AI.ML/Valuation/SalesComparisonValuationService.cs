using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Valuation
{
    public class SalesComparisonValuationService : ISalesComparisonValuationService
    {
        public Task<ValuationResult> CalculateAsync(Property property, System.Collections.Generic.IEnumerable<Property> comps)
        {
            // Simple approach: average of comps' listed price adjusted by area ratio
            var compList = comps?.ToList() ?? new System.Collections.Generic.List<Property>();
            if (!compList.Any())
            {
                return Task.FromResult(new ValuationResult
                {
                    EstimatedValue = property.ListedPrice ?? 0m,
                    Methodology = "SalesComparison",
                    Notes = "No comps available, returning listed price as fallback."
                });
            }

            var avg = compList.Average(c => c.ListedPrice ?? 0m);
            var areaRatio = property.AreaSqFt > 0 ? (property.AreaSqFt / (compList.Average(c => c.AreaSqFt))) : 1m;
            var estimate = avg * areaRatio;
            return Task.FromResult(new ValuationResult
            {
                EstimatedValue = decimal.Round(estimate, 2),
                Methodology = "SalesComparison",
                Notes = "Simple average of comps adjusted by area ratio."
            });
        }
    }
}
