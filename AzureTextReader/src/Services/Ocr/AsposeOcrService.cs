using Aspose.OCR;
using Aspose.Pdf;
using Aspose.Pdf.Text;
using AzureTextReader.Configuration;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Reflection;
using System.Collections;
using AzureTextReader.Services;
using System.Text.Json;

namespace AzureTextReader.Services.Ocr
{
    /// <summary>
    /// Aspose.Total OCR Service using Aspose.OCR and Aspose.PDF
    /// Adds multi-page TIFF support, handwritten recognition attempts via reflection,
    /// and optional AzureOcr fallback when results are insufficient.
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

            SetAsposeLicense();
            _ocrEngine = new AsposeOcr();
        }

        public string GetEngineName() => "Aspose.Total OCR";

        public async Task<OcrResult> ExtractTextAsync(string imageUrl, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Aspose_OCR_{ComputeHash(imageUrl)}";

            try
            {
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cached))
                    return cached;

                var temp = await DownloadImageToTempAsync(imageUrl);
                try
                {
                    var res = await ExtractTextFromFileAsync(temp, cacheKey);
                    res.ImageUrl = imageUrl;
                    return res;
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
            catch (Exception ex)
            {
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
                if (_ocrConfig.EnableCaching && _cache.TryGetValue<OcrResult>(cacheKey, out var cached))
                    return cached;

                if (!File.Exists(filePath)) throw new FileNotFoundException(filePath);

                string extractedText = string.Empty;
                string markdown = string.Empty;

                var ext = Path.GetExtension(filePath).ToLowerInvariant();

                if (ext == ".pdf")
                {
                    extractedText = await ExtractTextFromPdfAsync(filePath);
                    markdown = ConvertToMarkdown(extractedText);
                }
                else if (new[] { ".tif", ".tiff", ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Contains(ext))
                {
                    var hw = TryRecognizeHandwritten(filePath);
                    if (!string.IsNullOrEmpty(hw))
                    {
                        extractedText = hw;
                        markdown = ConvertToMarkdown(hw);
                    }
                    else
                    {
                        if (ext == ".tif" || ext == ".tiff")
                        {
                            var ocrInput = new OcrInput(Aspose.OCR.InputType.TIFF);
                            ocrInput.Add(filePath);
                            var ocrResults = await Task.Run(() => _ocrEngine.Recognize(ocrInput));
                            var sb = new StringBuilder();
                            if (ocrResults != null && ocrResults.Count > 0)
                            {
                                for (int i = 0; i < ocrResults.Count; i++)
                                {
                                    sb.AppendLine($"--- Page {i + 1} ---");
                                    sb.AppendLine(ocrResults[i]?.RecognitionText ?? string.Empty);
                                    sb.AppendLine();
                                }
                            }
                            extractedText = sb.ToString();
                            markdown = ConvertToMarkdown(extractedText);
                        }
                        else
                        {
                            var ocrInput = new OcrInput(Aspose.OCR.InputType.SingleImage);
                            ocrInput.Add(filePath);
                            var ocrResults = await Task.Run(() => _ocrEngine.Recognize(ocrInput));
                            if (ocrResults != null && ocrResults.Count > 0)
                            {
                                extractedText = ocrResults[0].RecognitionText;
                                markdown = ConvertToMarkdown(extractedText);
                            }
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException($"Unsupported file type: {ext}");
                }

                var result = new OcrResult
                {
                    ImageUrl = Path.GetFileName(filePath),
                    Markdown = markdown,
                    PlainText = extractedText,
                    Success = !string.IsNullOrEmpty(extractedText),
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime,
                    Metadata = new Dictionary<string, object>
                     {
                     { "FileType", ext },
                     { "FileSize", new FileInfo(filePath).Length },
                     { "TextLength", extractedText?.Length ??0 }
                     }
                };

                if (_ocrConfig.EnableCaching && result.Success)
                {
                    var opts = new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(_ocrConfig.CacheDurationDays) };
                    _cache.Set(cacheKey, result, opts);
                }

                return result;
            }
            catch (Exception ex)
            {
                return new OcrResult
                {
                    ImageUrl = Path.GetFileName(filePath),
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] fileBytes, string cacheKey = null)
        {
            var temp = Path.GetTempFileName() + Path.GetExtension(".png");
            await File.WriteAllBytesAsync(temp, fileBytes);
            try
            {
                var r = await ExtractTextFromFileAsync(temp, cacheKey);
                r.ImageUrl = Path.GetFileName(temp);
                return r;
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
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
                        Console.WriteLine($"Aspose license not found at {licensePath}");
                        return;
                    }

                    var ocrLicense = new Aspose.OCR.License();
                    ocrLicense.SetLicense(licensePath);
                    var pdfLicense = new Aspose.Pdf.License();
                    pdfLicense.SetLicense(licensePath);
                    _licenseSet = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to set Aspose license: {ex.Message}");
                }
            }
        }

        private string TryRecognizeHandwritten(string filePath)
        {
            try
            {
                var asm = typeof(AsposeOcr).Assembly;
                var recType = asm.GetType("Aspose.OCR.RecognitionEngine") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "RecognitionEngine");
                if (recType == null) return null;

                var instance = Activator.CreateInstance(recType);
                if (instance == null) return null;

                var methods = recType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                var hwMethod = methods.FirstOrDefault(m => m.Name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0);
                if (hwMethod != null)
                {
                    var parameters = hwMethod.GetParameters();
                    object result = null;
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        result = hwMethod.Invoke(instance, new object[] { filePath });
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType.Name.IndexOf("OcrInput", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var ocrInputType = asm.GetType("Aspose.OCR.OcrInput");
                        if (ocrInputType != null)
                        {
                            var ocrInput = Activator.CreateInstance(ocrInputType, new object[] { Aspose.OCR.InputType.SingleImage });
                            var add = ocrInputType.GetMethod("Add", new[] { typeof(string) });
                            add?.Invoke(ocrInput, new object[] { filePath });
                            result = hwMethod.Invoke(instance, new object[] { ocrInput });
                        }
                    }

                    if (result != null)
                    {
                        var textProp = result.GetType().GetProperty("RecognitionText") ?? result.GetType().GetProperty("Text");
                        if (textProp != null) return textProp.GetValue(result)?.ToString();
                        if (result is IEnumerable enumerable)
                        {
                            var sb = new StringBuilder();
                            foreach (var item in enumerable)
                            {
                                var p = item?.GetType().GetProperty("RecognitionText") ?? item?.GetType().GetProperty("Text");
                                if (p != null) sb.AppendLine(p.GetValue(item)?.ToString());
                                else if (item != null) sb.AppendLine(item.ToString());
                            }
                            var agg = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(agg)) return agg;
                        }
                        var s = result.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }

                var recognizeMethod = methods.FirstOrDefault(m => m.Name.Equals("Recognize", StringComparison.OrdinalIgnoreCase));
                if (recognizeMethod != null)
                {
                    var parameters = recognizeMethod.GetParameters();
                    object result = null;
                    if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string))
                    {
                        result = recognizeMethod.Invoke(instance, new object[] { filePath });
                    }
                    else if (parameters.Length == 1 && parameters[0].ParameterType.Name.IndexOf("OcrInput", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var ocrInputType = asm.GetType("Aspose.OCR.OcrInput");
                        if (ocrInputType != null)
                        {
                            var ocrInput = Activator.CreateInstance(ocrInputType, new object[] { Aspose.OCR.InputType.SingleImage });
                            var add = ocrInputType.GetMethod("Add", new[] { typeof(string) });
                            add?.Invoke(ocrInput, new object[] { filePath });
                            result = recognizeMethod.Invoke(instance, new object[] { ocrInput });
                        }
                    }

                    if (result != null)
                    {
                        var textProp = result.GetType().GetProperty("RecognitionText") ?? result.GetType().GetProperty("Text");
                        if (textProp != null) return textProp.GetValue(result)?.ToString();
                        if (result is IEnumerable enumerable)
                        {
                            var sb = new StringBuilder();
                            foreach (var item in enumerable)
                            {
                                var p = item?.GetType().GetProperty("RecognitionText") ?? item?.GetType().GetProperty("Text");
                                if (p != null) sb.AppendLine(p.GetValue(item)?.ToString());
                                else if (item != null) sb.AppendLine(item.ToString());
                            }
                            var agg = sb.ToString();
                            if (!string.IsNullOrWhiteSpace(agg)) return agg;
                        }
                        var s = result.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) return s;
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Aspose handwritten recognition attempt failed: {ex.Message}");
            }

            return null;
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

        /// <summary>
        /// Version 3: Grant Deed-focused extraction using `document_extraction_v3.txt` system prompt.
        /// This does NOT change existing OCR behavior; it is an additional flow that can be invoked by callers.
        /// </summary>
        public async Task<string> ExtractGrantDeedJsonV3Async(IMemoryCache memoryCache, AzureAIConfig azureConfig, string ocrMarkdown, string cacheKey = null)
        {
            if (memoryCache == null) throw new ArgumentNullException(nameof(memoryCache));
            if (azureConfig == null) throw new ArgumentNullException(nameof(azureConfig));
            ocrMarkdown ??= string.Empty;

            cacheKey ??= $"GrantDeed_V3_{ComputeHash(ocrMarkdown)}";
            if (memoryCache.TryGetValue(cacheKey, out var cachedObj) && cachedObj is string cachedJson)
            {
                return cachedJson;
            }

            var promptService = new PromptService(memoryCache);
            var systemPrompt = await promptService.LoadSystemPromptAsync("document_extraction", "v3");

            var messages = new List<object>
            {
                new { Role = "system", Content = systemPrompt },
                new { Role = "user", Content = ocrMarkdown }
            };

            var credential = new LocalApiKeyCredential(azureConfig.SubscriptionKey);
            var azureClient = new LocalAzureOpenAIClient(new Uri(azureConfig.Endpoint), credential);
            var chatClient = azureClient.GetChatClient(AzureTextReader.Models.AzureOpenAIModelConfig.GPT4oMini.DeploymentName);

            var options = new LocalChatCompletionOptions
            {
                Temperature = 0.0f,
                MaxOutputTokenCount = 2048,
                TopP = 1.0f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f
            };

            var completion = chatClient.CompleteChat(messages, options);
            var completionJson = JsonSerializer.Serialize(completion, new JsonSerializerOptions { WriteIndented = true });

            memoryCache.Set(cacheKey, completionJson, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7),
                SlidingExpiration = TimeSpan.FromHours(24)
            });

            return completionJson;
        }
    }
}
