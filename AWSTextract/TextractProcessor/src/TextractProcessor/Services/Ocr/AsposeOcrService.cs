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
                    // Use Aspose.OCR for image files
                    var ocrInput = new OcrInput(InputType.SingleImage);
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
                    var licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _ocrConfig.AsposeLicensePath);

                    if (!File.Exists(licensePath))
                    {
                        Console.WriteLine($"⚠ Aspose license file not found at: {licensePath}");
                        Console.WriteLine("  Aspose will run in evaluation mode with limitations.");
                        return;
                    }

                    // Set license for Aspose.OCR
                    var ocrLicense = new Aspose.OCR.License();
                    ocrLicense.SetLicense(licensePath);

                    // Set license for Aspose.PDF
                    var pdfLicense = new Aspose.Pdf.License();
                    pdfLicense.SetLicense(licensePath);

                    _licenseSet = true;
                    Console.WriteLine($"✓ Aspose license loaded successfully from: {Path.GetFileName(licensePath)}");
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
