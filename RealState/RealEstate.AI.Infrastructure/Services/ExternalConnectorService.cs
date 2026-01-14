using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Services
{
    // Very small example of an external connector: fetches JSON array of simple property objects from a URL
    public class ExternalConnectorService
    {
        private readonly HttpClient _client;
        private readonly IPropertyRepository _repo;

        public ExternalConnectorService(HttpClient client, IPropertyRepository repo)
        {
            _client = client;
            _repo = repo;
        }

        public async Task<IEnumerable<Property>> FetchAndIngestAsync(string url)
        {
            var resp = await _client.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var s = await resp.Content.ReadAsStringAsync();
            // Expecting a JSON array of simple objects; this is a naive parser for demo
            try
            {
                var docs = JsonSerializer.Deserialize<List<ExternalPropertyDto>>(s);
                var results = new List<Property>();
                if (docs != null)
                {
                    foreach (var d in docs)
                    {
                        var p = new Property
                        {
                            Id = Guid.NewGuid(),
                            Address = d.Address,
                            AreaSqFt = d.AreaSqFt,
                            YearBuilt = d.YearBuilt,
                            Bedrooms = d.Bedrooms,
                            Bathrooms = d.Bathrooms,
                            ListedPrice = d.ListedPrice,
                            Description = d.Description
                        };

                        await _repo.SaveAsync(p);
                        results.Add(p);
                    }
                }

                return results;
            }
            catch
            {
                return Array.Empty<Property>();
            }
        }

        private class ExternalPropertyDto
        {
            public string Address { get; set; }
            public int YearBuilt { get; set; }
            public decimal AreaSqFt { get; set; }
            public int Bedrooms { get; set; }
            public int Bathrooms { get; set; }
            public decimal? ListedPrice { get; set; }
            public string Description { get; set; }
        }
    }
}
