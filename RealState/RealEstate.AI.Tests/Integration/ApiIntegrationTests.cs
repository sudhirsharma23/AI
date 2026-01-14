using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using RealEstate.AI.WebApi;
using RealEstate.AI.WebApi.DTOs;
using Xunit;

namespace RealEstate.AI.Tests.Integration
{
    public class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ApiIntegrationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task GetRoot_ReturnsOk()
        {
            var r = await _client.GetAsync("/");
            r.EnsureSuccessStatusCode();
            var s = await r.Content.ReadAsStringAsync();
            Assert.Contains("RealEstate.AI Web API", s);
        }

        [Fact]
        public async Task IngestUpload_ReturnsReport()
        {
            var csv = "address,areaSqFt,yearBuilt,bedrooms,bathrooms,listedPrice,description\n" +
                      "1 A St,1000,1990,2,1,100000,Desc\n";

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(csv, Encoding.UTF8), "file", "upload.csv");

            var r = await _client.PostAsync("/api/properties/ingest/upload", content);
            r.EnsureSuccessStatusCode();

            var report = await r.Content.ReadFromJsonAsync<IngestResponseDto>();
            Assert.NotNull(report);
            Assert.Empty(report.Errors);
            Assert.Single(report.Saved);
        }
    }
}
