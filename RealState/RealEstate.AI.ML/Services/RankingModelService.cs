using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.ML.Services
{
    public class RankingModelService : IRankingModelService
    {
        public Task<IList<RankedPropertyResult>> RankPropertiesAsync(IEnumerable<PropertyFeatureVector> properties, RankingCriterion criterion)
        {
            var list = properties.Select(p => new RankedPropertyResult
            {
                PropertyId = p.PropertyId,
                EstimatedValue = p.ListedPrice,
                Score = p.ImageConditionScore + p.DescriptionSentiment
            }).ToList();

            // Simple sorting depending on criterion
            if (criterion == RankingCriterion.ValueForMoney)
            {
                list = list.OrderByDescending(r => r.Score / (r.EstimatedValue > 0 ? r.EstimatedValue : 1)).ToList();
            }
            else
            {
                list = list.OrderByDescending(r => r.Score).ToList();
            }

            return Task.FromResult<IList<RankedPropertyResult>>(list);
        }
    }
}
