using Aspose.OCR;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using AzureTextReader.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Text;

namespace AzureTextReader.Services.Ocr
{
    /// <summary>
    /// Aspose.Total OCR Service using Aspose.OCR and Aspose.PDF
    /// </summary>
    public class AsposeOcrService : IOcrService
    {
        private readonly OcrConfig _ocrConfig;
        private readonly IMemoryCache _cache;
        private readonly AsposeOcr _ocrEngine;
        private readonly HttpClient _httpClient;
        private static bool _licenseSet = false;
        private static readonly object _licenseLock = new();

        public AsposeOcrService(OcrConfig ocrConfig, IMemoryCache cache, HttpClient httpClient)
        {
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            // Set Aspose license (only once)
            SetAsposeLicense();

            // Initialize Aspose OCR engine
            _ocrEngine = new AsposeOcr();
        }

        public string GetEngineName() => "Aspose.Total OCR";

        public async Task<OcrResult> ExtractTextAsync(string imageUrl, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Aspose_OCR_{ComputeHash(imageUrl)}";

            try
            {
                // Check cache first
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
                {
                    Console.WriteLine($"? Aspose OCR Cache HIT for: {imageUrl}");
                    return cachedResult;
                }

                Console.WriteLine($"? Aspose OCR processing: {imageUrl}");

                // Download image to temp file
                var tempFile = await DownloadImageToTempAsync(imageUrl);

                try
                {
                    var ocrResult = await ExtractTextFromFileAsync(tempFile, cacheKey);
                    ocrResult.ImageUrl = imageUrl;
                    return ocrResult;
                }
                finally
                {
                    // Clean up temp file
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Aspose OCR Error: {ex.Message}");
                return new OcrResult
                {
                    ImageUrl = imageUrl,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
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
                    Console.WriteLine($"? Aspose OCR Cache HIT for file: {Path.GetFileName(filePath)}");
                    return cachedResult;
                }

                Console.WriteLine($"? Aspose OCR processing file: {Path.GetFileName(filePath)}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }

                string extractedText;
                string markdown;

                // Determine file type and process accordingly
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    // Use Aspose.PDF for PDF files
                    extractedText = await ExtractTextFromPdfAsync(filePath);
                    markdown = ConvertToMarkdown(extractedText);
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

                    markdown = ConvertToMarkdown(extractedText);
                }
                else
                {
                    throw new NotSupportedException($"File type not supported: {extension}");
                }

                var result = new OcrResult
                {
                    ImageUrl = filePath,
                    Markdown = markdown,
                    PlainText = extractedText,
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
                    Console.WriteLine($"? Cached Aspose OCR result for: {Path.GetFileName(filePath)}");
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Aspose OCR Error: {ex.Message}");
                return new OcrResult
                {
                    ImageUrl = filePath,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Aspose_OCR_Bytes_{ComputeHash(Convert.ToBase64String(imageBytes))}";

            try
            {
                // Check cache first
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cachedResult))
                {
                    Console.WriteLine($"? Aspose OCR Cache HIT for byte array");
                    return cachedResult;
                }

                Console.WriteLine($"? Aspose OCR processing byte array ({imageBytes.Length} bytes)");

                // Save to temp file
                var tempFile = Path.GetTempFileName() + ".png";
                await File.WriteAllBytesAsync(tempFile, imageBytes);

                try
                {
                    return await ExtractTextFromFileAsync(tempFile, cacheKey);
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
                Console.WriteLine($"? Aspose OCR Error: {ex.Message}");
                return new OcrResult
                {
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
                        Console.WriteLine($"? Aspose license file not found at: {licensePath}");
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
                    Console.WriteLine($"? Aspose license loaded successfully from: {Path.GetFileName(licensePath)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Warning: Could not set Aspose license: {ex.Message}");
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

        private string ConvertToMarkdown(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;

            // Simple conversion - add markdown formatting
            var lines = plainText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                // Detect potential headers (all caps, short lines)
                if (trimmed.Length < 50 && trimmed == trimmed.ToUpperInvariant() && trimmed.Any(char.IsLetter))
                {
                    sb.AppendLine($"## {trimmed}");
                }
                else
                {
                    sb.AppendLine(trimmed);
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private async Task<string> DownloadImageToTempAsync(string imageUrl)
        {
            var response = await _httpClient.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();

            var extension = Path.GetExtension(new Uri(imageUrl).LocalPath);
            if (string.IsNullOrEmpty(extension))
            {
                extension = ".png"; // Default
            }

            var tempFile = Path.GetTempFileName() + extension;
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(tempFile, imageBytes);

            return tempFile;
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
