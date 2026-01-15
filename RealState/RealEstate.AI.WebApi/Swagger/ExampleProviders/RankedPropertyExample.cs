using Swashbuckle.AspNetCore.Filters;
using RealEstate.AI.WebApi.DTOs;

namespace RealEstate.AI.WebApi.Swagger.ExampleProviders
{
    public class RankedPropertyExample : IExamplesProvider<RankedPropertyDto>
    {
        public RankedPropertyDto GetExamples()
        {
            return new RankedPropertyDto
            {
                PropertyId = System.Guid.NewGuid(),
                Score = 1.23m,
                EstimatedValue = 250000m
            };
        }
    }
}
