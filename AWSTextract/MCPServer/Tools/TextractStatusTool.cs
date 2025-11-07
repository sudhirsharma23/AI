using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MCPServer.Tools
{
    /// <summary>
    /// Tool to check Textract processing status
    /// Demonstrates integrating with existing application
    /// </summary>
    public class TextractStatusTool : IMcpTool
    {
        public string Name => "textract_status";

        public string Description => "Gets the status of Textract document processing";

        public ToolInputSchema InputSchema => new()
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["document_key"] = new()
                {
                    Type = "string",
                    Description = "Optional: S3 document key to check status for"
                }
            },
            Required = new List<string>()
        };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            // Simulate checking Textract status
            var documentKey = parameters.ContainsKey("document_key")
          ? parameters["document_key"].ToString()
        : null;

            // Check if output directory exists (where TextractProcessor saves results)
            var outputDir = Path.Combine(
                  Directory.GetCurrentDirectory(),
                      "..", "..", "..", "TextractProcessor", "src", "TextractProcessor", "CachedFiles_OutputFiles"
                  );

            var exists = Directory.Exists(outputDir);
            var fileCount = exists ? Directory.GetFiles(outputDir, "*.json").Length : 0;

            return Task.FromResult<object>(new
            {
                status = "running",
                output_directory = outputDir,
                output_directory_exists = exists,
                processed_files = fileCount,
                last_check = DateTime.UtcNow,
                document_key = documentKey,
                message = fileCount > 0
              ? $"Found {fileCount} processed document(s)"
                     : "No processed documents found"
            });
        }
    }
}
