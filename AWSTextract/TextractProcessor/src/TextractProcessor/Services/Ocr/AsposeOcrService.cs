using Aspose.OCR;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using TextractProcessor.Configuration;
using TextractProcessor.Services.Ocr;

namespace TextractProcessor.Services.Ocr
{
    /// <summary>
    /// Aspose.Total OCR Service using Aspose.OCR and Aspose.PDF
    /// </summary>
    public class AsposeOcrService : IOcrService
    {
        private readonly IMemoryCache _cache;
        private readonly OcrConfig _ocrConfig;
        private readonly AsposeOcr _ocrEngine;
        private static bool _licenseSet = false;
        private static readonly object _licenseLock = new();

        public AsposeOcrService(IMemoryCache cache, OcrConfig ocrConfig)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));

            // Set Aspose license (only once)
            SetAsposeLicense();

            // Initialize Aspose OCR engine
            _ocrEngine = new AsposeOcr();
        }

        public string GetEngineName() => "Aspose.Total OCR";

        public async Task<OcrResult> ExtractTextFromS3Async(string bucketName, string documentKey, string cacheKey = null)
        {
            throw new NotImplementedException("Aspose service works with local files. Download from S3 first and use ExtractTextFromFileAsync.");
        }

        public async Task<OcrResult> ExtractTextFromFileAsync(string filePath, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Aspose_OCR_File_{ComputeHash(filePath)}";

            try
            {
                // Check cache first
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
                {
                    Console.WriteLine($"✓ Aspose OCR Cache HIT for file: {Path.GetFileName(filePath)}");
                    return cachedResult;
                }

                Console.WriteLine($"⚙ Aspose OCR processing file: {Path.GetFileName(filePath)}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                string extractedText;
                var formFields = new Dictionary<string, string>();
                var tableData = new List<List<string>>();
                int? resultMetadataPageCount = null; // Track page count from OCR results

                // Determine file type and process accordingly
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    // Use Aspose.PDF for PDF files
                    extractedText = await ExtractTextFromPdfAsync(filePath);
                    // Note: Form fields and tables would require additional Aspose.PDF processing
                }
                else if (new[] { ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(extension))
                {
                    // Handle multi-page TIFFs explicitly
                    if (extension == ".tif" || extension == ".tiff")
                    {
                        // Use TIFF input type to process multi-page TIFF files
                        var ocrInput = new OcrInput(Aspose.OCR.InputType.TIFF);
                        ocrInput.Add(filePath);

                        var ocrResults = await Task.Run(() => _ocrEngine.Recognize(ocrInput));

                        var sb = new StringBuilder();
                        if (ocrResults != null && ocrResults.Count > 0)
                        {
                            for (int i = 0; i < ocrResults.Count; i++)
                            {
                                var pageText = ocrResults[i]?.RecognitionText ?? string.Empty;
                                sb.AppendLine($"--- Page {i + 1} ---");
                                sb.AppendLine(pageText);
                                sb.AppendLine();
                            }

                            // Record page count based on OCR results
                            resultMetadataPageCount = ocrResults.Count;
                        }

                        extractedText = sb.ToString();

                        // tableData remains empty unless further parsing is implemented
                    }
                    else
                    {
                        // Use Aspose.OCR for single-image files
                        var ocrInput = new OcrInput(Aspose.OCR.InputType.SingleImage);
                        ocrInput.Add(filePath);

                        var ocrResults = await Task.Run(() => _ocrEngine.Recognize(ocrInput));

                        if (ocrResults != null && ocrResults.Count > 0)
                        {
                            extractedText = ocrResults[0].RecognitionText;
                        }
                        else
                        {
                            extractedText = string.Empty;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"File type not supported: {extension}");
                }

                var result = new OcrResult
                {
                    DocumentKey = Path.GetFileName(filePath),
                    RawText = extractedText,
                    FormFields = formFields,
                    TableData = tableData,
                    Success = !string.IsNullOrEmpty(extractedText),
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
           {
          { "FileType", extension },
   { "FileSize", new FileInfo(filePath).Length },
            { "TextLength", extractedText?.Length ?? 0 }
          }
                };

                // If multi-page TIFF add page count to metadata (set earlier from OCR results)
                if ((extension == ".tif" || extension == ".tiff") && !string.IsNullOrEmpty(extractedText))
                {
                    if (resultMetadataPageCount.HasValue)
                    {
                        result.Metadata["PageCount"] = resultMetadataPageCount.Value;
                    }
                }

                // Cache the result
                if (_ocrConfig.EnableCaching && result.Success)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_ocrConfig.CacheDurationDays),
                        SlidingExpiration = TimeSpan.FromDays(Math.Min(7, _ocrConfig.CacheDurationDays))
                    };
                    _cache.Set(cacheKey, result, cacheOptions);
                    Console.WriteLine($"✓ Cached Aspose OCR result for: {Path.GetFileName(filePath)}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Aspose OCR Error: {ex.Message}");
                return new OcrResult
                {
                    DocumentKey = Path.GetFileName(filePath),
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] fileBytes, string fileName, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Aspose_OCR_Bytes_{ComputeHash(Convert.ToBase64String(fileBytes))}";

            try
            {
                // Check cache first
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
                {
                    Console.WriteLine($"✓ Aspose OCR Cache HIT for byte array: {fileName}");
                    return cachedResult;
                }

                Console.WriteLine($"⚙ Aspose OCR processing byte array ({fileBytes.Length} bytes): {fileName}");

                // Save to temp file
                var extension = Path.GetExtension(fileName);
                var tempFile = Path.GetTempFileName() + extension;
                await File.WriteAllBytesAsync(tempFile, fileBytes);

                try
                {
                    var result = await ExtractTextFromFileAsync(tempFile, cacheKey);
                    result.DocumentKey = fileName;
                    return result;
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Aspose OCR Error: {ex.Message}");
                return new OcrResult
                {
                    DocumentKey = fileName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        private void SetAsposeLicense()
        {
            if (_licenseSet) return;

            lock (_licenseLock)
            {
                if (_licenseSet) return;

                try
                {
                    // Build candidate license file names
                    var configuredPath = _ocrConfig.AsposeLicensePath;
                    var candidates = new List<string>();

                    if (!string.IsNullOrWhiteSpace(configuredPath))
                    {
                        // If absolute path was provided, try it directly
                        if (Path.IsPathRooted(configuredPath))
                        {
                            candidates.Add(configuredPath);
                        }
                        else
                        {
                            // Try common base directories
                            var baseDirs = new[]
 {
 AppDomain.CurrentDomain.BaseDirectory,
 AppContext.BaseDirectory,
 Directory.GetCurrentDirectory()
 };

                            foreach (var baseDir in baseDirs.Where(d => !string.IsNullOrWhiteSpace(d)))
                            {
                                candidates.Add(Path.Combine(baseDir, configuredPath));
                            }
                        }
                    }

                    // Also try default filenames in common locations
                    var defaultNames = new[] { "Aspose.Total.NET.lic", "Aspose.Total.lic" };
                    var searchDirs = new[]
 {
 AppDomain.CurrentDomain.BaseDirectory,
 AppContext.BaseDirectory,
 Directory.GetCurrentDirectory(),
 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Licenses")
 };

                    foreach (var name in defaultNames)
                    {
                        foreach (var dir in searchDirs.Where(d => !string.IsNullOrWhiteSpace(d)))
                        {
                            candidates.Add(Path.Combine(dir, name));
                        }
                    }

                    // Deduplicate candidates preserving order
                    candidates = candidates.Where(c => !string.IsNullOrWhiteSpace(c)).Select(Path.GetFullPath).Distinct().ToList();

                    Console.WriteLine("Aspose license search candidates:");
                    foreach (var c in candidates)
                    {
                        Console.WriteLine($" - {c}");
                    }

                    string found = null;

                    foreach (var candidate in candidates)
                    {
                        try
                        {
                            if (File.Exists(candidate))
                            {
                                // Try to set license using path first
                                Console.WriteLine($"Attempting to set Aspose license from: {candidate}");

                                // Aspose OCR license
                                try
                                {
                                    var ocrLicense = new Aspose.OCR.License();
                                    ocrLicense.SetLicense(candidate);
                                }
                                catch (Exception ocrEx)
                                {
                                    Console.WriteLine($"Aspose.OCR.SetLicense failed for {candidate}: {ocrEx.Message}");
                                }

                                // Aspose.PDF license
                                try
                                {
                                    var pdfLicense = new Aspose.Pdf.License();
                                    pdfLicense.SetLicense(candidate);
                                }
                                catch (Exception pdfEx)
                                {
                                    Console.WriteLine($"Aspose.PDF.SetLicense failed for {candidate}: {pdfEx.Message}");
                                }

                                // If no exception thrown above set found
                                found = candidate;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error while checking license candidate {candidate}: {ex.Message}");
                        }
                    }

                    // As a fallback, attempt to load license from embedded resource names
                    if (found == null)
                    {
                        var asm = System.Reflection.Assembly.GetExecutingAssembly();
                        var resources = asm.GetManifestResourceNames();
                        Console.WriteLine("Embedded resources: " + string.Join(", ", resources));

                        foreach (var name in resources)
                        {
                            if (name.EndsWith("Aspose.Total.NET.lic", StringComparison.OrdinalIgnoreCase) ||
                                name.EndsWith("Aspose.Total.lic", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine($"Attempting to set Aspose license from embedded resource: {name}")
;
                                try
                                {
                                    using var stream = asm.GetManifestResourceStream(name);
                                    if (stream != null)
                                    {
                                        var ocrLicense = new Aspose.OCR.License();
                                        ocrLicense.SetLicense(stream);

                                        stream.Position = 0; // reset for next reader

                                        var pdfLicense = new Aspose.Pdf.License();
                                        pdfLicense.SetLicense(stream);

                                        found = name;
                                        break;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to set license from embedded resource {name}: {ex.Message}");
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(found))
                    {
                        _licenseSet = true;
                        Console.WriteLine($"✓ Aspose license loaded successfully from: {Path.GetFileName(found)}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Aspose license file not found. Attempted {candidates.Count} locations.");
                        Console.WriteLine("Aspose will run in evaluation mode with limitations.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠ Warning: Could not set Aspose license: {ex.Message}");
                    Console.WriteLine("  Aspose will run in evaluation mode with limitations.");
                }
            }
        }

        private async Task<string> ExtractTextFromPdfAsync(string pdfPath)
        {
            return await Task.Run(() =>
              {
                  using var pdfDocument = new Document(pdfPath);
                  var textAbsorber = new TextAbsorber();

                  var sb = new StringBuilder();

                  foreach (Page page in pdfDocument.Pages)
                  {
                      page.Accept(textAbsorber);
                      sb.AppendLine(textAbsorber.Text);
                      sb.AppendLine(); // Add page separator
                  }

                  return sb.ToString();
              });
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
