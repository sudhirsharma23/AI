namespace TextractProcessor.Models
{
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string MappedFilePath { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
        public decimal TotalCost { get; set; }
    }
}