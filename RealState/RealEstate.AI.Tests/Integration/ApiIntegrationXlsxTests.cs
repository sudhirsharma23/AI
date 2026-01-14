using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using RealEstate.AI.WebApi;
using RealEstate.AI.WebApi.DTOs;
using Xunit;

namespace RealEstate.AI.Tests.Integration
{
    public class ApiIntegrationXlsxTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ApiIntegrationXlsxTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task UploadXlsx_ReturnsReport()
        {
            using var ms = new MemoryStream();
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Sheet1");
                ws.Cell(1, 1).Value = "address";
                ws.Cell(1, 2).Value = "areaSqFt";
                ws.Cell(1, 3).Value = "yearBuilt";
                ws.Cell(1, 4).Value = "bedrooms";
                ws.Cell(1, 5).Value = "bathrooms";
                ws.Cell(1, 6).Value = "listedPrice";
                ws.Cell(1, 7).Value = "description";

                ws.Cell(2, 1).Value = "100 Excel Ave";
                ws.Cell(2, 2).Value = 1400;
                ws.Cell(2, 3).Value = 2000;
                ws.Cell(2, 4).Value = 3;
                ws.Cell(2, 5).Value = 2;
                ws.Cell(2, 6).Value = 250000;
                ws.Cell(2, 7).Value = "Nice property";

                wb.SaveAs(ms);
            }

            ms.Position = 0;
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(ms), "file", "props.xlsx");

            var r = await _client.PostAsync("/api/properties/ingest/upload", content);
            r.EnsureSuccessStatusCode();

            var report = await r.Content.ReadFromJsonAsync<IngestResponseDto>();
            Assert.NotNull(report);
            Assert.Empty(report.Errors);
            Assert.Single(report.Saved);
        }
    }
}
