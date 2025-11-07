using System.Collections.Generic;

namespace TextractProcessor.Models
{
    public class TextractResponse
    {
        public string JobId { get; set; }
        public string JobStatus { get; set; }
        public List<Dictionary<string, string>> FormData { get; set; }
        public List<Dictionary<string, object>> TableData { get; set; }
        public List<PageInfo> Pages { get; set; }
        public string RawText { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class PageInfo
    {
        public int PageNumber { get; set; }
        public List<LineInfo> Lines { get; set; }
        public List<WordInfo> Words { get; set; }
        public Dictionary<string, float> Dimensions { get; set; }
    }

    public class LineInfo
    {
        public string Text { get; set; }
        public Dictionary<string, float> Geometry { get; set; }
        public float Confidence { get; set; }
    }

    public class WordInfo
    {
        public string Text { get; set; }
        public Dictionary<string, float> Geometry { get; set; }
        public float Confidence { get; set; }
    }
}