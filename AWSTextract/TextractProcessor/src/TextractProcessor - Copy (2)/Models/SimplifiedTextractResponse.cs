using System.Collections.Generic;

namespace TextractProcessor.Models
{
    public class SimplifiedTextractResponse
 {
  public string RawText { get; set; }
   public Dictionary<string, string> FormFields { get; set; }
        public List<List<string>> TableData { get; set; }

        public SimplifiedTextractResponse()
        {
   RawText = string.Empty;
          FormFields = new Dictionary<string, string>();
            TableData = new List<List<string>>();
    }
    }
}