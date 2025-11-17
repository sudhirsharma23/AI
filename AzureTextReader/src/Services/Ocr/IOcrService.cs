namespace ImageTextExtractor.Services.Ocr
{
    /// <summary>
    /// Common OCR result structure - abstraction for both Azure and Aspose
    /// </summary>
    public class OcrResult
    {
        public string ImageUrl { get; set; }
    public string Markdown { get; set; }
        public string PlainText { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
   public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Engine { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// OCR Service abstraction - implemented by both Azure and Aspose services
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
  /// Extract text from image URL
      /// </summary>
        Task<OcrResult> ExtractTextAsync(string imageUrl, string cacheKey = null);

        /// <summary>
 /// Extract text from local file path
        /// </summary>
        Task<OcrResult> ExtractTextFromFileAsync(string filePath, string cacheKey = null);

        /// <summary>
    /// Extract text from byte array
        /// </summary>
        Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string cacheKey = null);

        /// <summary>
        /// Get the OCR engine name
        /// </summary>
        string GetEngineName();
    }
}
