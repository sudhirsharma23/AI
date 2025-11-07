using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.Textract;
using Amazon.BedrockRuntime;
using Amazon.Textract.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using System.IO;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using TextractProcessor.Services;
using TextractProcessor.Models;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TextractProcessor
{
    public class Function
    {
        private const string LOG_GROUP = "/aws/lambda/textract-processor";
        private readonly string _functionName;

        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonTextract _textractClient;
        private readonly SchemaMapperService _schemaMapper;
        private readonly IMemoryCache _cache;
        private readonly TextractCacheService _textractCache;

        private const string BucketName = "testbucket-sudhir-bsi1";
        private const string TextractRoleArn = "arn:aws:iam::912532823432:role/accesstextract-role";
        private const string SnsTopicArn = "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo";
        
        // Configure the upload date and file names to process
  // Format: uploads/<date>/<filename>/<actualfile>
     private const string UploadDate = "2025-01-20"; // Change this to match your upload date
        private static readonly string[] DocumentKeys = new[]
        {
            $"uploads/{UploadDate}/2025000065659/2025000065659.tif",
            $"uploads/{UploadDate}/2025000065659-1/2025000065659-1.tif"
        };
        
        private const int MaxRetries = 60;
        private const int RetryInterval = 5000;
        private const string OutputDirectory = "CachedFiles_OutputFiles";

        public Function()
        {
            _functionName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") ?? "textract-processor";
            _s3Client = new AmazonS3Client();
            _textractClient = new AmazonTextractClient();
            _cache = new MemoryCache(new MemoryCacheOptions());
            _textractCache = new TextractCacheService(OutputDirectory);

            var modelConfig = BedrockModelConfig.Qwen3;

            // NovaLite is currently only available in us-west-2 region
            // Automatically route to the correct region based on model selection
            var bedrockClient = CreateBedrockClientForModel(modelConfig);

            var bedrockService = new BedrockService(bedrockClient, _cache, modelConfig);

            //var bedrockClient = new AmazonBedrockRuntimeClient();
            // You can easily switch models by changing the configuration here
            //var bedrockService = new BedrockService(bedrockClient, _cache, modelConfig);

            _schemaMapper = new SchemaMapperService(bedrockService, _cache, OutputDirectory);

            Directory.CreateDirectory(OutputDirectory);
        }

        /// <summary>
        /// Helper method to construct S3 key with date-based folder structure
        /// Format: uploads/<date>/<filename>/<actualfile>
        /// </summary>
        private static string BuildDocumentKey(string date, string fileName)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            return $"uploads/{date}/{fileNameWithoutExt}/{fileName}";
        }

        /// <summary>
        /// Get document keys for a specific date
        /// </summary>
        private static string[] GetDocumentKeysForDate(string date, params string[] fileNames)
        {
            return fileNames.Select(fileName => BuildDocumentKey(date, fileName)).ToArray();
        }

        private IAmazonBedrockRuntime CreateBedrockClientForModel(BedrockModelConfig modelConfig)
        {
            // Determine the appropriate region based on the model
            var region = modelConfig.ModelId switch
            {
                // NovaLite models are currently only available in us-west-2
                var id when id.Contains("nova-lite") => Amazon.RegionEndpoint.USWest1,

                // Nova models (non-lite) are available in us-east-1 and us-west-2
                var id when id.Contains("nova") => Amazon.RegionEndpoint.USWest1,

                // Qwen models are available in us-west-2
                var id when id.Contains("qwen") => Amazon.RegionEndpoint.USWest2,

                // Claude and Titan are widely available, prefer us-east-1
                _ => Amazon.RegionEndpoint.USEast1
            };

            return new AmazonBedrockRuntimeClient(region);
        }

        private void LogMetric(ILambdaContext context, string metricName, double value, Dictionary<string, string> dimensions = null)
        {
            var logEntry = new
            {
                MetricName = metricName,
                Value = value,
                Unit = "Count",
                Dimensions = dimensions ?? new Dictionary<string, string>(),
                Timestamp = DateTime.UtcNow,
                RequestId = context?.AwsRequestId
            };

            context?.Logger.LogLine($"METRIC#{JsonSerializer.Serialize(logEntry)}");
        }

        private void LogEvent(ILambdaContext context, string message, string level = "INFO", Dictionary<string, object> additionalData = null)
        {
            var logEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                RequestId = context?.AwsRequestId,
                FunctionName = _functionName,
                Message = message,
                AdditionalData = additionalData
            };

            context?.Logger.LogLine(JsonSerializer.Serialize(logEntry));
        }

        private float GetFloatValue(float? nullableValue)
        {
            return nullableValue ?? 0f;
        }

        public async Task<ProcessingResult> FunctionHandler(ILambdaContext context)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                var textractResponses = new List<SimplifiedTextractResponse>();
                var allResponses = new List<TextractResponse>();

                // Process each document
                foreach (var documentKey in DocumentKeys)
                {
                    LogEvent(context, $"Starting document processing for {documentKey}", additionalData: new Dictionary<string, object>
  {
  { "DocumentKey", documentKey },
         { "Bucket", BucketName }
            });

                    // Try to get cached Textract response
                    var textractResponse = await _textractCache.GetCachedResponse(documentKey);

                    if (textractResponse == null)
                    {
                        LogEvent(context, $"Cache miss - processing {documentKey} with Textract");
                        textractResponse = await ProcessTextract(documentKey, context);

                        if (textractResponse.JobStatus != "SUCCEEDED")
                        {
                            LogEvent(context, $"Textract processing failed for {documentKey}", "ERROR", new Dictionary<string, object>
      {
   { "Error", textractResponse.ErrorMessage }
      });
                            return new ProcessingResult
                            {
                                Success = false,
                                Error = $"Failed to process {documentKey}: {textractResponse.ErrorMessage}"
                            };
                        }

                        LogMetric(context, "TextractProcessingTime", (DateTime.UtcNow - startTime).TotalMilliseconds);
                        await _textractCache.CacheTextractResponse(documentKey, textractResponse);
                    }
                    else
                    {
                        LogEvent(context, $"Using cached Textract response for {documentKey}");
                        LogMetric(context, "CacheHit", 1);
                    }

                    allResponses.Add(textractResponse);

                    // Create simplified response
                    var simplifiedResponse = new SimplifiedTextractResponse
                    {
                        RawText = textractResponse.RawText,
                        FormFields = textractResponse.FormData
                       .SelectMany(dict => dict)
                   .GroupBy(kvp => kvp.Key)
            .ToDictionary(
                     g => g.Key,
                     g => string.Join(" | ", g.Select(x => x.Value).Distinct())
                      ),
                        TableData = ExtractTableText(textractResponse.TableData)
                    };

                    textractResponses.Add(simplifiedResponse);
                }

                // Process merged responses with schema mapper
                LogEvent(context, "Processing merged documents with Bedrock", additionalData: new Dictionary<string, object>
        {
            { "DocumentCount", textractResponses.Count },
         { "TotalFormFields", textractResponses.Sum(r => r.FormFields.Count) },
       { "TotalTables", textractResponses.Sum(r => r.TableData.Count) }
     });

                var result = await _schemaMapper.ProcessAndMapSchema(
           textractResponses,
          string.Empty,
          "merged_documents"
          );

                var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                LogMetric(context, "TotalProcessingTime", processingTime);
                LogMetric(context, "InputTokens", result.InputTokens);
                LogMetric(context, "OutputTokens", result.OutputTokens);

                LogEvent(context, "Processing completed", additionalData: new Dictionary<string, object>
      {
      { "ProcessingTimeMs", processingTime },
            { "InputTokens", result.InputTokens },
            { "OutputTokens", result.OutputTokens },
            { "Cost", result.TotalCost },
  { "OutputFile", result.MappedFilePath }
        });

                return result;
            }
            catch (Exception e)
            {
                LogEvent(context, "Processing failed", "ERROR", new Dictionary<string, object>
        {
     { "Error", e.Message },
   { "StackTrace", e.StackTrace }
   });

                return new ProcessingResult
                {
                    Success = false,
                    Error = e.Message
                };
            }
        }

        private List<List<string>> ExtractTableText(List<Dictionary<string, object>> tableDictionaries)
        {
            var tableTexts = new List<List<string>>();

            foreach (var tableDict in tableDictionaries)
            {
                if (tableDict.TryGetValue("Cells", out var cellsObj) &&
      cellsObj is List<Dictionary<string, string>> cells)
                {
                    var tableRows = cells
              .GroupBy(c => int.Parse(c["RowIndex"]))
                 .OrderBy(g => g.Key)
                  .Select(g => g.OrderBy(c => int.Parse(c["ColumnIndex"]))
                 .Select(c => c["Text"])
                   .ToList())
             .ToList();

                    tableTexts.Add(tableRows.SelectMany(row => row).ToList());
                }
            }

            return tableTexts;
        }

        private async Task<TextractResponse> ProcessTextract(string documentKey, ILambdaContext context)
        {
            try
            {
                context.Logger.LogInformation($"Processing file: {documentKey} from bucket: {BucketName}");

                var startRequest = new StartDocumentAnalysisRequest
                {
                    DocumentLocation = new DocumentLocation
                    {
                        S3Object = new S3Object
                        {
                            Bucket = BucketName,
                            Name = documentKey
                        }
                    },
                    FeatureTypes = new List<string> { "TABLES", "FORMS", "LAYOUT" },
                    NotificationChannel = new NotificationChannel
                    {
                        RoleArn = TextractRoleArn,
                        SNSTopicArn = SnsTopicArn
                    }
                };

                var startResponse = await _textractClient.StartDocumentAnalysisAsync(startRequest);
                var jobId = startResponse.JobId;
                context.Logger.LogInformation($"Started Textract analysis job with ID: {jobId}");

                var result = await WaitForJobCompletion(jobId, context);
                if (result == null)
                {
                    return new TextractResponse { JobId = jobId, JobStatus = "FAILED" };
                }

                var response = new TextractResponse
                {
                    JobId = jobId,
                    JobStatus = result.JobStatus,
                    FormData = new List<Dictionary<string, string>>(),
                    TableData = new List<Dictionary<string, object>>(),
                    Pages = new List<PageInfo>(),
                    RawText = string.Empty
                };

                var rawTextBuilder = new StringBuilder();
                var currentPage = CreateNewPage(1);

                string nextToken = null;
                do
                {
                    var getResultsRequest = new GetDocumentAnalysisRequest
                    {
                        JobId = jobId,
                        NextToken = nextToken
                    };

                    var analysisResult = await _textractClient.GetDocumentAnalysisAsync(getResultsRequest);
                    nextToken = analysisResult.NextToken;

                    ProcessBlocks(analysisResult.Blocks, response, ref currentPage, rawTextBuilder);
                    ProcessTables(analysisResult.Blocks, response.TableData);

                } while (nextToken != null);

                if (currentPage.Lines.Any())
                {
                    response.Pages.Add(currentPage);
                }

                response.RawText = rawTextBuilder.ToString();

                //SaveResults(response, documentKey, context);

                return response;
            }
            catch (Exception e)
            {
                context.Logger.LogError($"Error processing file {documentKey} from bucket {BucketName}: {e.Message}");
                return new TextractResponse
                {
                    JobStatus = "ERROR",
                    ErrorMessage = e.Message
                };
            }
        }

        private async Task<GetDocumentAnalysisResponse> WaitForJobCompletion(string jobId, ILambdaContext context)
        {
            for (int i = 0; i < MaxRetries; i++)
            {
                var getResultsRequest = new GetDocumentAnalysisRequest { JobId = jobId };
                var result = await _textractClient.GetDocumentAnalysisAsync(getResultsRequest);

                if (result.JobStatus == "SUCCEEDED")
                {
                    return result;
                }
                else if (result.JobStatus == "FAILED")
                {
                    context.Logger.LogError($"Textract job failed: {result.StatusMessage}");
                    return null;
                }

                await Task.Delay(RetryInterval);
            }

            context.Logger.LogError("Job did not complete within the maximum retry attempts");
            return null;
        }

        private PageInfo CreateNewPage(int pageNumber)
        {
            return new PageInfo
            {
                PageNumber = pageNumber,
                Lines = new List<LineInfo>(),
                Words = new List<WordInfo>(),
                Dimensions = new Dictionary<string, float>()
            };
        }

        private void ProcessBlocks(List<Block> blocks, TextractResponse response, ref PageInfo currentPage, StringBuilder rawTextBuilder)
        {
            foreach (var block in blocks)
            {
                switch (block.BlockType)
                {
                    case "KEY_VALUE_SET" when block.EntityTypes.Contains("KEY"):
                        var keyValuePair = ExtractKeyValuePair(block, blocks);
                        if (keyValuePair != null)
                        {
                            response.FormData.Add(keyValuePair);
                        }
                        break;

                    case "PAGE":
                        if (currentPage.Lines.Any())
                        {
                            response.Pages.Add(currentPage);
                        }
                        currentPage = CreateNewPage(block.Page ?? 1);
                        currentPage.Dimensions["Width"] = GetFloatValue(block.Geometry?.BoundingBox?.Width);
                        currentPage.Dimensions["Height"] = GetFloatValue(block.Geometry?.BoundingBox?.Height);
                        break;

                    case "LINE":
                        ProcessLine(block, currentPage, rawTextBuilder);
                        break;

                    case "WORD":
                        ProcessWord(block, currentPage);
                        break;
                }
            }
        }

        private void ProcessLine(Block block, PageInfo currentPage, StringBuilder rawTextBuilder)
        {
            var lineInfo = new LineInfo
            {
                Text = block.Text,
                Confidence = block.Confidence ?? 0,
                Geometry = CreateGeometryDictionary(block.Geometry?.BoundingBox)
            };
            currentPage.Lines.Add(lineInfo);
            rawTextBuilder.AppendLine(block.Text);
        }

        private void ProcessWord(Block block, PageInfo currentPage)
        {
            var wordInfo = new WordInfo
            {
                Text = block.Text,
                Confidence = block.Confidence ?? 0,
                Geometry = CreateGeometryDictionary(block.Geometry?.BoundingBox)
            };
            currentPage.Words.Add(wordInfo);
        }

        private Dictionary<string, float> CreateGeometryDictionary(BoundingBox boundingBox)
        {
            return new Dictionary<string, float>
        {
   { "Left", GetFloatValue(boundingBox?.Left) },
      { "Top", GetFloatValue(boundingBox?.Top) },
    { "Width", GetFloatValue(boundingBox?.Width) },
    { "Height", GetFloatValue(boundingBox?.Height) }
        };
        }

        private Dictionary<string, string> ExtractKeyValuePair(Block keyBlock, List<Block> blocks)
        {
            try
            {
                var keyText = string.Join(" ", keyBlock.Relationships
                 .FirstOrDefault(r => r.Type == "CHILD")?
                    .Ids
                         .Select(id => blocks.First(b => b.Id == id).Text) ?? Array.Empty<string>());

                var valueBlock = blocks.FirstOrDefault(b =>
             keyBlock.Relationships.Any(r => r.Type == "VALUE" && r.Ids.Contains(b.Id)));

                if (valueBlock != null)
                {
                    var valueText = string.Join(" ", valueBlock.Relationships
                           .FirstOrDefault(r => r.Type == "CHILD")?
                        .Ids
                         .Select(id => blocks.First(b => b.Id == id).Text) ?? Array.Empty<string>());

                    return new Dictionary<string, string> { { keyText.Trim(), valueText.Trim() } };
                }
            }
            catch (Exception)
            {
                // Skip malformed key-value pairs
            }
            return null;
        }

        private void ProcessTables(List<Block> blocks, List<Dictionary<string, object>> tableData)
        {
            var tables = blocks.Where(b => b.BlockType == "TABLE");

            foreach (var table in tables)
            {
                var tableCells = blocks.Where(b =>
         b.BlockType == "CELL" &&
             b.Relationships?.Any(r => r.Type == "CHILD") == true &&
          b.Id.StartsWith(table.Id)).ToList();

                var rowCount = tableCells.Max(c => c.RowIndex);
                var columnCount = tableCells.Max(c => c.ColumnIndex);

                var tableDict = new Dictionary<string, object>
    {
     { "TableId", table.Id },
 { "Rows", rowCount },
             { "Columns", columnCount },
    { "Cells", new List<Dictionary<string, string>>() }
            };

                foreach (var cell in tableCells)
                {
                    var cellText = string.Join(" ", cell.Relationships
                    .FirstOrDefault(r => r.Type == "CHILD")?
                 .Ids
                   .Select(id => blocks.First(b => b.Id == id).Text) ?? Array.Empty<string>());

                    ((List<Dictionary<string, string>>)tableDict["Cells"]).Add(new Dictionary<string, string>
            {
 { "RowIndex", cell.RowIndex.ToString() },
              { "ColumnIndex", cell.ColumnIndex.ToString() },
 { "Text", cellText.Trim() }
        });
                }

                tableData.Add(tableDict);
            }
        }

        //private void SaveResults(TextractResponse response, string documentKey, ILambdaContext context)
        //{
        //    try
        //    {
        //        context.Logger.LogInformation($"Analysis completed. Found {response.FormData.Count} form fields, {response.TableData.Count} tables, and {response.Pages.Count} pages.");

        //        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        //        var baseFileName = Path.GetFileNameWithoutExtension(documentKey.Split('/').Last());

        //        var jsonFileName = Path.Combine(OutputDirectory, $"{baseFileName}_{timestamp}.json");
        //        var jsonContent = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        //        File.WriteAllText(jsonFileName, jsonContent);
        //        context.Logger.LogInformation($"Saved JSON results to: {jsonFileName}");

        //        var textFileName = Path.Combine(OutputDirectory, $"{baseFileName}_{timestamp}.txt");
        //        SaveEnhancedTextFormat(response, textFileName, documentKey);
        //        context.Logger.LogInformation($"Saved readable results to: {textFileName}");
        //    }
        //    catch (Exception e)
        //    {
        //        context.Logger.LogError($"Error saving results for {documentKey}: {e.Message}");
        //    }
        //}

        //private void SaveEnhancedTextFormat(TextractResponse response, string filePath, string documentKey)
        //{
        //    var sb = new StringBuilder();
        //    sb.AppendLine("Textract Analysis Results");
        //    sb.AppendLine("=======================");
        //    sb.AppendLine($"Document: {documentKey}");
        //    sb.AppendLine($"Job ID: {response.JobId}");
        //    sb.AppendLine($"Status: {response.JobStatus}");
        //    sb.AppendLine();

        //    // Document Statistics
        //    sb.AppendLine("Document Statistics");
        //    sb.AppendLine("------------------");
        //    sb.AppendLine($"Total Pages: {response.Pages.Count}");
        //    sb.AppendLine($"Total Lines: {response.Pages.Sum(p => p.Lines.Count)}");
        //    sb.AppendLine($"Total Words: {response.Pages.Sum(p => p.Words.Count)}");
        //    sb.AppendLine($"Form Fields: {response.FormData.Count}");
        //    sb.AppendLine();

        //    // Raw Text Content
        //    sb.AppendLine("Raw Text Content");
        //    sb.AppendLine("---------------");
        //    sb.AppendLine(response.RawText);
        //    sb.AppendLine();

        //    // Form Fields
        //    if (response.FormData?.Any() == true)
        //    {
        //        sb.AppendLine("Form Fields");
        //        sb.AppendLine("-----------");
        //        foreach (var field in response.FormData)
        //        {
        //            foreach (var kvp in field)
        //            {
        //                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
        //            }
        //        }
        //        sb.AppendLine();
        //    }

        //    if (!string.IsNullOrEmpty(response.ErrorMessage))
        //    {
        //        sb.AppendLine("Errors");
        //        sb.AppendLine("------");
        //        sb.AppendLine(response.ErrorMessage);
        //    }

        //    File.WriteAllText(filePath, sb.ToString());
        //}
        // testing first git checkin
    }
}