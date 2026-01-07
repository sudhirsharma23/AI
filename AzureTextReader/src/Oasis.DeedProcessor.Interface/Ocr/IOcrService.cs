using System.Threading.Tasks;
using Oasis.DeedProcessor.BusinessEntities.Ocr;

namespace Oasis.DeedProcessor.Interface.Ocr
{
    public interface IOcrService
    {
        Task<OcrResult> ExtractTextAsync(string imageUrl, string? cacheKey = null);
        Task<OcrResult> ExtractTextFromFileAsync(string filePath, string? cacheKey = null);
        Task<OcrResult> ExtractTextFromBytesAsync(byte[] imageBytes, string? cacheKey = null);
        string GetEngineName();
    }
}
