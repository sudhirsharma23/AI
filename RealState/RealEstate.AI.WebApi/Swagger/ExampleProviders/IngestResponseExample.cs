using Swashbuckle.AspNetCore.Filters;
using RealEstate.AI.WebApi.DTOs;

namespace RealEstate.AI.WebApi.Swagger.ExampleProviders
{
    public class IngestResponseExample : IExamplesProvider<IngestResponseDto>
    {
        public IngestResponseDto GetExamples()
        {
            return new IngestResponseDto
            {
                Saved = new System.Collections.Generic.List<PropertyDto>
                {
                    new PropertyDto { Id = System.Guid.NewGuid(), Address = "10 Test St", AreaSqFt = 1200, YearBuilt = 1999 }
                },
                Errors = new System.Collections.Generic.List<string>()
            };
        }
    }
}
