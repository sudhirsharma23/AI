using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Data
{
    public class SimplePropertyDataSource : IPropertyDataSource
    {
        private readonly List<Property> _properties = new()
        {
            new Property
            {
                Id = Guid.NewGuid(),
                Address = "123 Main St",
                AreaSqFt = 1800,
                YearBuilt = 1995,
                Bedrooms = 3,
                Bathrooms = 2,
                ListedPrice = 350000,
                Description = "A lovely single-family home with updated kitchen."
            },
            new Property
            {
                Id = Guid.NewGuid(),
                Address = "456 Oak Ave",
                AreaSqFt = 2400,
                YearBuilt = 2005,
                Bedrooms = 4,
                Bathrooms = 3,
                ListedPrice = 475000,
                Description = "Spacious property with large yard and great schools nearby."
            }
        };

        public Task<IEnumerable<Property>> GetPropertiesAsync()
        {
            return Task.FromResult<IEnumerable<Property>>(_properties);
        }

        public Task<Property> GetPropertyByIdAsync(Guid id)
        {
            var p = _properties.Find(x => x.Id == id);
            return Task.FromResult(p);
        }
    }
}
