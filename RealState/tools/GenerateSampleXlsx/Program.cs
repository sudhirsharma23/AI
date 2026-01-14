using System.IO;
using ClosedXML.Excel;

var dir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "samples");
Directory.CreateDirectory(dir);
var path = Path.Combine(dir, "props.xlsx");
using var wb = new XLWorkbook();
var ws = wb.AddWorksheet("Sheet1");
ws.Cell(1, 1).Value = "address";
ws.Cell(1, 2).Value = "areaSqFt";
ws.Cell(1, 3).Value = "yearBuilt";
ws.Cell(1, 4).Value = "bedrooms";
ws.Cell(1, 5).Value = "bathrooms";
ws.Cell(1, 6).Value = "listedPrice";
ws.Cell(1, 7).Value = "description";

ws.Cell(2, 1).Value = "10 Test St";
ws.Cell(2, 2).Value = 1200;
ws.Cell(2, 3).Value = 1999;
ws.Cell(2, 4).Value = 3;
ws.Cell(2, 5).Value = 2;
ws.Cell(2, 6).Value = 250000;
ws.Cell(2, 7).Value = "Good home with updates";

ws.Cell(3, 1).Value = "20 Oak Ave";
ws.Cell(3, 2).Value = 1800;
ws.Cell(3, 3).Value = 2005;
ws.Cell(3, 4).Value = 4;
ws.Cell(3, 5).Value = 3;
ws.Cell(3, 6).Value = 375000;
ws.Cell(3, 7).Value = "Spacious and bright";

ws.Cell(4, 1).Value = "30 Pine Rd";
ws.Cell(4, 2).Value = 900;
ws.Cell(4, 3).Value = 1970;
ws.Cell(4, 4).Value = 2;
ws.Cell(4, 5).Value = 1;
ws.Cell(4, 6).Value = 95000;
ws.Cell(4, 7).Value = "Fixer-upper as-is";

wb.SaveAs(path);
Console.WriteLine($"Wrote {path}");
