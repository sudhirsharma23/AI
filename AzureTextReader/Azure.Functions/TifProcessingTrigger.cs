using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager;
using Azure.Identity;
using System.Text.Json;

namespace ImageTextExtractor.Azure.Functions
{
    public class TifProcessingTrigger
    {
        private readonly ILogger<TifProcessingTrigger> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ArmClient _armClient;

        public TifProcessingTrigger(
      ILogger<TifProcessingTrigger> logger,
  BlobServiceClient blobServiceClient)
     {
        _logger = logger;
      _blobServiceClient = blobServiceClient;
          _armClient = new ArmClient(new DefaultAzureCredential());
        }

[FunctionName("ProcessTifTrigger")]
        public async Task Run(
      [EventGridTrigger] EventGridEvent eventGridEvent,
  ILogger log)
        {
  try
      {
              log.LogInformation($"Event received: {eventGridEvent.EventType}");
        log.LogInformation($"Subject: {eventGridEvent.Subject}");

// Parse event data
   var blobCreatedData = JsonSerializer.Deserialize<BlobCreatedEventData>(
         eventGridEvent.Data.ToString());

  var blobUrl = blobCreatedData.Url;
      var blobName = ExtractBlobName(blobUrl);

      log.LogInformation($"Processing blob: {blobName}");

    // Move file to processing folder
 await MoveBlobToProcessing(blobName);

 // Create job metadata
      var jobId = Guid.NewGuid().ToString();
                var jobMetadata = new ProcessingJob
                {
   JobId = jobId,
  BlobName = blobName,
 BlobUrl = blobUrl,
          Status = "Queued",
CreatedAt = DateTime.UtcNow,
      TriggerSource = "EventGrid"
   };

       // Store job metadata in Table Storage or Cosmos DB
         await StoreJobMetadata(jobMetadata);

           // Trigger Container Instance to process the file
            await TriggerContainerInstance(jobId, blobName);

  log.LogInformation($"Successfully triggered processing for job: {jobId}");
    }
    catch (Exception ex)
            {
         log.LogError(ex, "Error processing event grid trigger");
     
    // Move file to failed folder for retry
 var blobName = ExtractBlobName(eventGridEvent.Subject);
      await MoveBlobToFailed(blobName, ex.Message);
                
      throw;
       }
        }

   private async Task MoveBlobToProcessing(string blobName)
        {
 var sourceContainer = _blobServiceClient.GetBlobContainerClient("input");
            var destContainer = _blobServiceClient.GetBlobContainerClient("input");

      var sourcePath = $"pending/{blobName}";
            var destPath = $"processing/{blobName}";

         var sourceBlobClient = sourceContainer.GetBlobClient(sourcePath);
            var destBlobClient = destContainer.GetBlobClient(destPath);

      // Copy blob
await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

 // Wait for copy to complete
            var properties = await destBlobClient.GetPropertiesAsync();
       while (properties.Value.CopyStatus == Azure.Storage.Blobs.Models.CopyStatus.Pending)
        {
   await Task.Delay(100);
       properties = await destBlobClient.GetPropertiesAsync();
        }

            // Delete source blob
          await sourceBlobClient.DeleteIfExistsAsync();
        }

        private async Task MoveBlobToFailed(string blobName, string errorMessage)
        {
    var sourceContainer = _blobServiceClient.GetBlobContainerClient("input");
       var destContainer = _blobServiceClient.GetBlobContainerClient("input");

  var sourcePath = $"processing/{blobName}";
    var destPath = $"failed/{blobName}";

            var sourceBlobClient = sourceContainer.GetBlobClient(sourcePath);
 var destBlobClient = destContainer.GetBlobClient(destPath);

            // Add error metadata
 var metadata = new Dictionary<string, string>
            {
    ["ErrorMessage"] = errorMessage,
     ["FailedAt"] = DateTime.UtcNow.ToString("o")
     };

            await destBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
      await destBlobClient.SetMetadataAsync(metadata);
        await sourceBlobClient.DeleteIfExistsAsync();
        }

  private async Task TriggerContainerInstance(string jobId, string blobName)
        {
        var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
          var resourceGroupName = Environment.GetEnvironmentVariable("RESOURCE_GROUP_NAME");
            var containerGroupName = $"imageextractor-{jobId}";

            var containerGroupData = new ContainerInstanceContainerGroupData(
    Azure.Core.AzureLocation.EastUS,
        new[]
      {
   new ContainerInstanceContainer(
         "imageextractor",
   "youracr.azurecr.io/imageextractor:latest",
                  new ContainerResourceRequirements(
    new ContainerResourceRequestsContent(1.5, 1) // 1.5 GB RAM, 1 CPU
         )
   )
     {
          EnvironmentVariables =
   {
            new ContainerEnvironmentVariable("JOB_ID") { Value = jobId },
       new ContainerEnvironmentVariable("BLOB_NAME") { Value = blobName },
         new ContainerEnvironmentVariable("STORAGE_CONNECTION_STRING") 
    { 
       SecureValue = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING") 
      }
  }
       }
         },
              Azure.ResourceManager.ContainerInstance.Models.ContainerInstanceOperatingSystemType.Linux
            )
          {
      RestartPolicy = Azure.ResourceManager.ContainerInstance.Models.ContainerGroupRestartPolicy.Never
            };

    var subscription = _armClient.GetSubscriptionResource(
       new Azure.Core.ResourceIdentifier($"/subscriptions/{subscriptionId}"));

      var resourceGroup = (await subscription.GetResourceGroupAsync(resourceGroupName)).Value;
 
            await resourceGroup.GetContainerGroups().CreateOrUpdateAsync(
         Azure.WaitUntil.Started,
            containerGroupName,
  containerGroupData);

 _logger.LogInformation($"Container instance '{containerGroupName}' triggered for job {jobId}");
    }

   private async Task StoreJobMetadata(ProcessingJob job)
        {
        // Store in Azure Table Storage or Cosmos DB
       var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
      // Implementation depends on your choice (Table Storage or Cosmos DB)
            
            _logger.LogInformation($"Stored job metadata for job: {job.JobId}");
   }

        private string ExtractBlobName(string urlOrSubject)
        {
            // Extract blob name from URL or Event Grid subject
            var parts = urlOrSubject.Split('/');
            return parts[^1];
      }
    }

    public class ProcessingJob
    {
        public string JobId { get; set; }
 public string BlobName { get; set; }
        public string BlobUrl { get; set; }
        public string Status { get; set; }
 public DateTime CreatedAt { get; set; }
        public string TriggerSource { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
 public string ErrorMessage { get; set; }
    }

    public class BlobCreatedEventData
    {
        public string Url { get; set; }
        public string Api { get; set; }
  public string ContentType { get; set; }
        public long ContentLength { get; set; }
        public string BlobType { get; set; }
    }
}
