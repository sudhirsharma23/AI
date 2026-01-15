using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;

namespace RealEstate.AI.Infrastructure.Services
{
    public class DataIngestionService : IDataIngestionService
    {
        private readonly IEnumerable<IPropertyDataSource> _sources;
        private readonly IPropertyRepository _repository;
        private readonly ExternalConnectorService _connector;

        public DataIngestionService(IEnumerable<IPropertyDataSource> sources, IPropertyRepository repository, ExternalConnectorService connector = null)
        {
            _sources = sources;
            _repository = repository;
            _connector = connector;
        }

        public async Task<IEnumerable<Property>> IngestAllAsync()
        {
            var all = new List<Property>();
            foreach (var s in _sources)
            {
                var props = await s.GetPropertiesAsync();
                foreach (var p in props)
                {
                    await _repository.SaveAsync(p);
                    all.Add(p);
                }
            }

            return all;
        }

        public async Task<Property> IngestPropertyAsync(Property p)
        {
            await _repository.SaveAsync(p);
            return p;
        }

        public async Task<IEnumerable<Property>> IngestFromFileAsync(Stream fileStream, string contentType)
        {
            // kept for backwards compatibility - call the report version and return saved items
            var report = await IngestFromFileWithReportAsync(fileStream, contentType);
            return report.Saved;
        }

        public async Task<IngestionResult> IngestFromFileWithReportAsync(Stream fileStream, string contentType)
        {
            var report = new IngestionResult();

            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(fileStream);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            try
                            {
                                var p = new Property
                                {
                                    Id = Guid.NewGuid(),
                                    Address = el.GetProperty("address").GetString(),
                                    AreaSqFt = el.GetProperty("areaSqFt").GetDecimal(),
                                    YearBuilt = el.GetProperty("yearBuilt").GetInt32(),
                                    Bedrooms = el.GetProperty("bedrooms").GetInt32(),
                                    Bathrooms = el.GetProperty("bathrooms").GetInt32(),
                                    ListedPrice = el.TryGetProperty("listedPrice", out var lp) ? lp.GetDecimal() : null,
                                    Description = el.TryGetProperty("description", out var d) ? d.GetString() : null
                                };

                                await _repository.SaveAsync(p);
                                report.Saved.Add(p);
                            }
                            catch (Exception ex)
                            {
                                report.Errors.Add($"Row parse error: {ex.Message}");
                            }
                        }
                    }

                    return report;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"JSON parse error: {ex.Message}");
                    return report;
                }
            }

            // CSV handling - expect header row
            if (contentType.Contains("csv", StringComparison.OrdinalIgnoreCase) || contentType.Contains("comma-separated", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var sr = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: false);
                    string header = await sr.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        report.Errors.Add("Empty CSV file or missing header");
                        return report;
                    }

                    var cols = header.Split(',').Select(c => c.Trim().ToLowerInvariant()).ToArray();
                    int rowIndex = 1;
                    while (!sr.EndOfStream)
                    {
                        rowIndex++;
                        var line = await sr.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = line.Split(',');
                        if (parts.Length != cols.Length)
                        {
                            report.Errors.Add($"Row {rowIndex}: column count mismatch");
                            continue;
                        }

                        try
                        {
                            var dict = new Dictionary<string, string>();
                            for (int i = 0; i < cols.Length; i++) dict[cols[i]] = parts[i].Trim();

                            // basic validation
                            if (!dict.TryGetValue("address", out var address) || string.IsNullOrWhiteSpace(address))
                            {
                                report.Errors.Add($"Row {rowIndex}: missing required field 'address'");
                                continue;
                            }

                            if (!dict.TryGetValue("areasqft", out var areaStr) || !decimal.TryParse(areaStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var area))
                            {
                                report.Errors.Add($"Row {rowIndex}: invalid areaSqFt");
                                continue;
                            }

                            if (!dict.TryGetValue("yearbuilt", out var yearStr) || !int.TryParse(yearStr, out var year))
                            {
                                report.Errors.Add($"Row {rowIndex}: invalid yearBuilt");
                                continue;
                            }

                            var p = new Property
                            {
                                Id = Guid.NewGuid(),
                                Address = address,
                                AreaSqFt = area,
                                YearBuilt = year,
                                Bedrooms = dict.TryGetValue("bedrooms", out var b) && int.TryParse(b, out var bi) ? bi : 0,
                                Bathrooms = dict.TryGetValue("bathrooms", out var ba) && int.TryParse(ba, out var bai) ? bai : 0,
                                ListedPrice = dict.TryGetValue("listedprice", out var lp) && decimal.TryParse(lp, NumberStyles.Currency, CultureInfo.InvariantCulture, out var lpd) ? lpd : null,
                                Description = dict.TryGetValue("description", out var desc) ? desc : null
                            };

                            await _repository.SaveAsync(p);
                            report.Saved.Add(p);
                        }
                        catch (Exception ex)
                        {
                            report.Errors.Add($"Row {rowIndex}: parse error {ex.Message}");
                        }
                    }

                    return report;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"CSV read error: {ex.Message}");
                    return report;
                }
            }

            // Excel (XLSX) support using ClosedXML
            if (contentType.Contains("sheet", StringComparison.OrdinalIgnoreCase) || contentType.Contains("excel", StringComparison.OrdinalIgnoreCase) || contentType.Contains("spreadsheet", StringComparison.OrdinalIgnoreCase) || contentType.EndsWith("xlsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Need to copy to a MemoryStream because ClosedXML reads from seekable stream
                    using var ms = new MemoryStream();
                    await fileStream.CopyToAsync(ms);
                    ms.Position = 0;

                    using var workbook = new XLWorkbook(ms);
                    var ws = workbook.Worksheets.First();
                    var headerRow = ws.Row(1);
                    var headers = new List<string>();
                    foreach (var cell in headerRow.CellsUsed()) headers.Add(cell.GetString().Trim().ToLowerInvariant());

                    var lastRow = ws.LastRowUsed().RowNumber();
                    for (int r = 2; r <= lastRow; r++)
                    {
                        try
                        {
                            var row = ws.Row(r);
                            var dict = new Dictionary<string, string>();
                            for (int c = 1; c <= headers.Count; c++)
                            {
                                var h = headers[c - 1];
                                var cell = row.Cell(c);
                                dict[h] = cell.GetString();
                            }

                            // basic validation
                            if (!dict.TryGetValue("address", out var address) || string.IsNullOrWhiteSpace(address))
                            {
                                report.Errors.Add($"Row {r}: missing required field 'address'");
                                continue;
                            }

                            if (!dict.TryGetValue("areasqft", out var areaStr) || !decimal.TryParse(areaStr, NumberStyles.Number, CultureInfo.InvariantCulture, out var area))
                            {
                                report.Errors.Add($"Row {r}: invalid areaSqFt");
                                continue;
                            }

                            if (!dict.TryGetValue("yearbuilt", out var yearStr) || !int.TryParse(yearStr, out var year))
                            {
                                report.Errors.Add($"Row {r}: invalid yearBuilt");
                                continue;
                            }

                            var p = new Property
                            {
                                Id = Guid.NewGuid(),
                                Address = address,
                                AreaSqFt = area,
                                YearBuilt = year,
                                Bedrooms = dict.TryGetValue("bedrooms", out var b) && int.TryParse(b, out var bi) ? bi : 0,
                                Bathrooms = dict.TryGetValue("bathrooms", out var ba) && int.TryParse(ba, out var bai) ? bai : 0,
                                ListedPrice = dict.TryGetValue("listedprice", out var lp) && decimal.TryParse(lp, NumberStyles.Currency, CultureInfo.InvariantCulture, out var lpd) ? lpd : null,
                                Description = dict.TryGetValue("description", out var desc) ? desc : null
                            };

                            await _repository.SaveAsync(p);
                            report.Saved.Add(p);
                        }
                        catch (Exception ex)
                        {
                            report.Errors.Add($"Row {r}: parse error {ex.Message}");
                        }
                    }

                    return report;
                }
                catch (Exception ex)
                {
                    report.Errors.Add($"Excel parse error: {ex.Message}");
                    return report;
                }
            }

            report.Errors.Add("Unsupported content type");
            return report;
        }

        public async Task<IEnumerable<Property>> IngestFromExternalAsync(string connectorId)
        {
            if (_connector == null) return Array.Empty<Property>();
            // connectorId used as URL in this simplistic example
            return await _connector.FetchAndIngestAsync(connectorId);
        }
    }
}
