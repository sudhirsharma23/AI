using TextractProcessor.Models;

namespace TextractProcessor.Services.Ocr
{
    /// <summary>
    /// Common OCR result structure - abstraction for both Textract and Aspose
    /// </summary>
    public class OcrResult
    {
        public string DocumentKey { get; set; }
        public string RawText { get; set; }
        public Dictionary<string, string> FormFields { get; set; } = new();
        public List<List<string>> TableData { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string Engine { get; set; }
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// OCR Service abstraction - implemented by both Textract and Aspose services
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
        /// Extract text from S3 document
        /// </summary>
        Task<OcrResult> ExtractTextFromS3Async(string bucketName, string documentKey, string cacheKey = null);

        /// <summary>
        /// Extract text from local file path
        /// </summary>
        Task<OcrResult> ExtractTextFromFileAsync(string filePath, string cacheKey = null);

        /// <summary>
        /// Extract text from byte array
        /// </summary>
        Task<OcrResult> ExtractTextFromBytesAsync(byte[] fileBytes, string fileName, string cacheKey = null);

        /// <summary>
        /// Get the OCR engine name
        /// </summary>
        string GetEngineName();
    }
}
