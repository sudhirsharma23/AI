using System.IO;
using System.Text;
using System.Threading.Tasks;
using RealEstate.AI.Infrastructure.Services;
using RealEstate.AI.Infrastructure.Repositories;
using RealEstate.AI.Core.Interfaces;
using Xunit;

namespace RealEstate.AI.Tests
{
    public class IngestionTests
    {
        [Fact]
        public async Task CsvIngestion_ParsesValidRows()
        {
            var csv = new StringBuilder();
            csv.AppendLine("address,areaSqFt,yearBuilt,bedrooms,bathrooms,listedPrice,description");
            csv.AppendLine("10 Test St,1200,1999,3,2,250000,Good home");

            var repo = new InMemoryPropertyRepository();
            var service = new DataIngestionService(System.Array.Empty<IPropertyDataSource>(), repo);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
            var report = await service.IngestFromFileWithReportAsync(ms, "text/csv");

            Assert.Empty(report.Errors);
            Assert.Single(report.Saved);
        }

        [Fact]
        public async Task CsvIngestion_ReturnsErrorsForInvalidRows()
        {
            var csv = new StringBuilder();
            csv.AppendLine("address,areaSqFt,yearBuilt,bedrooms,bathrooms,listedPrice,description");
            csv.AppendLine(",1200,1999,3,2,250000,Missing address");
            csv.AppendLine("BadRow");

            var repo = new InMemoryPropertyRepository();
            var service = new DataIngestionService(System.Array.Empty<IPropertyDataSource>(), repo);

            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
            var report = await service.IngestFromFileWithReportAsync(ms, "text/csv");

            Assert.NotEmpty(report.Errors);
            Assert.Empty(report.Saved);
        }
    }
}
