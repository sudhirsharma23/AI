using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ImageTextExtractor.Configuration;
using ImageTextExtractor.Services;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ImageTextExtractorApp
{
    /// <summary>
    /// Azure Container Instance version - processes single TIF file from blob storage
    /// </summary>
    internal static class Program
    {
     private const string OutputDirectory = "/app/OutputFiles";
        private static BlobServiceClient _blobServiceClient;
        private static string _jobId;
    private static string _blobName;

        private static async Task Main(string[] args)
    {
            try
{
                Console.WriteLine("=== ImageTextExtractor Azure Container Version ===");
 Console.WriteLine($"Started at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

    // Read environment variables
          _jobId = Environment.GetEnvironmentVariable("JOB_ID") 
    ?? throw new InvalidOperationException("JOB_ID environment variable not set");
      
    _blobName = Environment.GetEnvironmentVariable("BLOB_NAME") 
?? throw new InvalidOperationException("BLOB_NAME environment variable not set");
    
var storageConnectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING") 
           ?? throw new InvalidOperationException("STORAGE_CONNECTION_STRING not set");

         Console.WriteLine($"Job ID: {_jobId}");
      Console.WriteLine($"Processing blob: {_blobName}");

            // Initialize blob service client
     _blobServiceClient = new BlobServiceClient(storageConnectionString);

       // Create output directory
                Directory.CreateDirectory(OutputDirectory);

     // Update job status to "Processing"
      await UpdateJobStatus("Processing", "Starting image processing");

          // Run processing
       using var memoryCache = new MemoryCache(new MemoryCacheOptions());
    await RunAsync(memoryCache);

      // Update job status to "Completed"
                await UpdateJobStatus("Completed", "Processing completed successfully");

                Console.WriteLine("=== Processing Completed Successfully ===");
           Environment.Exit(0); // Success
            }
            catch (Exception ex)
       {
        Console.WriteLine($"FATAL ERROR: {ex.Message}");
 Console.WriteLine($"Stack trace: {ex.StackTrace}");
             
     await UpdateJobStatus("Failed", ex.Message);
 
                Environment.Exit(1); // Failure
    }
        }

        private static async Task RunAsync(IMemoryCache memoryCache)
        {
          // Load configuration
            Console.WriteLine("Loading Azure AI configuration...");
 var config = AzureAIConfig.Load();
       config.Validate();

            var endpoint = config.Endpoint + "/contentunderstanding/analyzers/prebuilt-documentAnalyzer:analyze?api-version=2025-05-01-preview";
    var subscriptionKey = config.SubscriptionKey;

            // Download TIF file from blob storage
Console.WriteLine($"Downloading TIF file: {_blobName}");
     var imageUrl = await DownloadBlobToTempUrl(_blobName);

    if (string.IsNullOrEmpty(imageUrl))
      {
                throw new InvalidOperationException($"Failed to download blob: {_blobName}");
            }

         var imageUrls = new[] { imageUrl };

       using var client = new HttpClient();
          client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

// Extract OCR data
            var combinedMarkdown = new StringBuilder();
      foreach (var url in imageUrls)
     {
     Console.WriteLine($"Extracting OCR data from: {url}");
     var markdown = await ExtractOcrFromImage(client, endpoint, subscriptionKey, url);

         if (!string.IsNullOrEmpty(markdown))
    {
           combinedMarkdown.AppendLine($"### Document: {_blobName}");
            combinedMarkdown.AppendLine(markdown);
    combinedMarkdown.AppendLine("\n---\n");
     }
            }

       if (combinedMarkdown.Length == 0)
         {
                throw new InvalidOperationException("No OCR data extracted from document");
}

// Save combined OCR results to blob storage
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
 await SaveOcrResultsToBlob(combinedMarkdown.ToString(), timestamp);

            // Process with ChatCompletion
  await ProcessWithChatCompletion(memoryCache, config, subscriptionKey, combinedMarkdown.ToString(), timestamp);

            // Upload results to blob storage
   await UploadResultsToBlob(timestamp);

      // Move processed file to archive
            await MoveProcessedBlobToArchive(_blobName);

        Console.WriteLine("All processing steps completed successfully");
        }

        private static async Task<string> DownloadBlobToTempUrl(string blobName)
        {
  try
            {
     var containerClient = _blobServiceClient.GetBlobContainerClient("input");
    var blobClient = containerClient.GetBlobClient($"processing/{blobName}");

       // Generate SAS URL for the blob (valid for 1 hour)
   var sasUri = blobClient.GenerateSasUri(
  Azure.Storage.Sas.BlobSasPermissions.Read,
        DateTimeOffset.UtcNow.AddHours(1));

       return sasUri.ToString();
            }
            catch (Exception ex)
   {
    Console.WriteLine($"Error downloading blob: {ex.Message}");
              throw;
 }
        }

    private static async Task SaveOcrResultsToBlob(string ocrText, string timestamp)
     {
            try
          {
        var containerClient = _blobServiceClient.GetBlobContainerClient("output");
      await containerClient.CreateIfNotExistsAsync();

     var blobName = $"ocr/{_jobId}_ocr_{timestamp}.md";
            var blobClient = containerClient.GetBlobClient(blobName);

        var content = Encoding.UTF8.GetBytes(ocrText);
           await blobClient.UploadAsync(new BinaryData(content), overwrite: true);

       Console.WriteLine($"Saved OCR results to blob: {blobName}");
            }
     catch (Exception ex)
       {
                Console.WriteLine($"Error saving OCR results: {ex.Message}");
    throw;
    }
        }

        private static async Task UploadResultsToBlob(string timestamp)
        {
    try
          {
                var containerClient = _blobServiceClient.GetBlobContainerClient("output");
       await containerClient.CreateIfNotExistsAsync();

    // Upload all JSON files from OutputFiles directory
           var outputFiles = Directory.GetFiles(OutputDirectory, "*.json");
       
     foreach (var filePath in outputFiles)
          {
         var fileName = Path.GetFileName(filePath);
   var blobName = $"{_jobId}/{fileName}";
   var blobClient = containerClient.GetBlobClient(blobName);

     using var fileStream = File.OpenRead(filePath);
              await blobClient.UploadAsync(fileStream, overwrite: true);

             Console.WriteLine($"Uploaded result to blob: {blobName}");

             // Also upload summary reports if they exist
   var mdFiles = Directory.GetFiles(OutputDirectory, "*.md");
       foreach (var mdFile in mdFiles)
    {
     var mdFileName = Path.GetFileName(mdFile);
    var mdBlobName = $"{_jobId}/{mdFileName}";
  var mdBlobClient = containerClient.GetBlobClient(mdBlobName);

  using var mdStream = File.OpenRead(mdFile);
        await mdBlobClient.UploadAsync(mdStream, overwrite: true);

 Console.WriteLine($"Uploaded report to blob: {mdBlobName}");
              }
  }
            }
            catch (Exception ex)
        {
       Console.WriteLine($"Error uploading results: {ex.Message}");
            throw;
       }
        }

     private static async Task MoveProcessedBlobToArchive(string blobName)
        {
            try
   {
   var containerClient = _blobServiceClient.GetBlobContainerClient("input");
  
var sourceBlobClient = containerClient.GetBlobClient($"processing/{blobName}");
          var destBlobClient = containerClient.GetBlobClient($"../processed/archive/{DateTime.UtcNow:yyyy/MM/dd}/{blobName}");

       // Copy to archive
    await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

                // Wait for copy to complete
        var properties = await destBlobClient.GetPropertiesAsync();
                while (properties.Value.CopyStatus == CopyStatus.Pending)
                {
      await Task.Delay(100);
         properties = await destBlobClient.GetPropertiesAsync();
      }

    // Delete source
    await sourceBlobClient.DeleteIfExistsAsync();

     Console.WriteLine($"Moved processed file to archive: {destBlobClient.Name}");
}
     catch (Exception ex)
      {
        Console.WriteLine($"Error moving blob to archive: {ex.Message}");
          // Don't throw - this is not critical
     }
      }

        private static async Task UpdateJobStatus(string status, string message)
        {
       try
    {
        // Update job status in Table Storage or Cosmos DB
  Console.WriteLine($"Job Status Update: {status} - {message}");
       
      // TODO: Implement actual status update to Azure Table Storage or Cosmos DB
       // This will allow monitoring and tracking of job progress
   }
            catch (Exception ex)
{
           Console.WriteLine($"Warning: Could not update job status: {ex.Message}");
      // Don't throw - status update failure shouldn't stop processing
          }
        }

 // Include all existing methods from your Program.cs:
   // - ExtractOcrFromImage
  // - ProcessWithChatCompletion
        // - ProcessVersion1
 // - ProcessVersion2
        // - SaveFinalCleanedJson
     // - etc.
        
        // ... (rest of your existing Program.cs code here)
    }
}
