using Swashbuckle.AspNetCore.Filters;
using RealEstate.AI.WebApi.DTOs;

namespace RealEstate.AI.WebApi.Swagger.ExampleProviders
{
    public class PropertyExample : IExamplesProvider<PropertyDto>
    {
        public PropertyDto GetExamples()
        {
            return new PropertyDto
            {
                Id = System.Guid.NewGuid(),
                Address = "123 Main St",
                AreaSqFt = 1800,
                YearBuilt = 1995,
                Bedrooms = 3,
                Bathrooms = 2,
                ListedPrice = 350000,
                Description = "Well maintained home"
            };
        }
    }
}
