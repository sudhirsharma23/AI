using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Repositories
{
    public class InMemoryPropertyRepository : IPropertyRepository
    {
        private readonly ConcurrentDictionary<Guid, Property> _store = new();

        public Task SaveAsync(Property property)
        {
            if (property.Id == Guid.Empty)
            {
                property.Id = Guid.NewGuid();
            }

            _store[property.Id] = property;
            return Task.CompletedTask;
        }

        public Task<Property> GetByIdAsync(Guid id)
        {
            _store.TryGetValue(id, out var p);
            return Task.FromResult(p);
        }

        public Task<IEnumerable<Property>> ListAsync()
        {
            return Task.FromResult(_store.Values.AsEnumerable());
        }
    }
}
