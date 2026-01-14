using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Valuation
{
    public class IncomeValuationService : IIncomeValuationService
    {
        public Task<ValuationResult> CalculateAsync(Property property, decimal noi, decimal capRate)
        {
            // NOI / cap rate = value
            var estimate = capRate > 0 ? noi / capRate : noi;
            return Task.FromResult(new ValuationResult
            {
                EstimatedValue = decimal.Round(estimate, 2),
                Methodology = "Income",
                Notes = "Simple income approach using provided NOI and cap rate."
            });
        }
    }
}
