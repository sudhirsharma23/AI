using System;
using System.Collections.Generic;

namespace Oasis.DeedProcessor.BusinessEntities.Ocr
{
    public class OcrResult
    {
        public string ImageUrl { get; set; } = string.Empty;
        public string Markdown { get; set; } = string.Empty;
        public string PlainText { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Engine { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
    }
}
