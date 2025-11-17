using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.Textract;
using Amazon.Textract.Model;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using TextractProcessor.Configuration;
using TextractProcessor.Models;

namespace TextractProcessor.Services.Ocr
{
    /// <summary>
    /// AWS Textract OCR Service - wraps existing Textract functionality
    /// </summary>
    public class TextractOcrService : IOcrService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IAmazonTextract _textractClient;
        private readonly IMemoryCache _cache;
        private readonly OcrConfig _ocrConfig;
        private readonly TextractCacheService _textractCache;
        private readonly string _textractRoleArn;
        private readonly string _snsTopicArn;
        private const int MaxRetries = 60;
        private const int RetryInterval = 5000;

        public TextractOcrService(
       IAmazonS3 s3Client,
       IAmazonTextract textractClient,
            IMemoryCache cache,
        OcrConfig ocrConfig,
               TextractCacheService textractCache,
           string textractRoleArn,
       string snsTopicArn)
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _textractClient = textractClient ?? throw new ArgumentNullException(nameof(textractClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _ocrConfig = ocrConfig ?? throw new ArgumentNullException(nameof(ocrConfig));
            _textractCache = textractCache ?? throw new ArgumentNullException(nameof(textractCache));
            _textractRoleArn = textractRoleArn ?? throw new ArgumentNullException(nameof(textractRoleArn));
            _snsTopicArn = snsTopicArn ?? throw new ArgumentNullException(nameof(snsTopicArn));
        }

        public string GetEngineName() => "AWS Textract";

        public async Task<OcrResult> ExtractTextFromS3Async(string bucketName, string documentKey, string cacheKey = null)
        {
            var startTime = DateTime.UtcNow;
            cacheKey ??= $"Textract_{ComputeHash(documentKey)}";

            try
            {
                // Check cache first
                if (_ocrConfig.EnableCaching)
                {
                    var cachedResponse = await _textractCache.GetCachedResponse(documentKey);
                    if (cachedResponse != null)
                    {
                        Console.WriteLine($"✓ Textract Cache HIT for: {documentKey}");
                        return ConvertToOcrResult(cachedResponse, documentKey, startTime);
                    }
                }

                Console.WriteLine($"⚙ Textract processing: {documentKey}");

                var textractResponse = await ProcessTextract(bucketName, documentKey);

                if (textractResponse.JobStatus != "SUCCEEDED")
                {
                    return new OcrResult
                    {
                        DocumentKey = documentKey,
                        Success = false,
                        ErrorMessage = textractResponse.ErrorMessage,
                        Engine = GetEngineName(),
                        ProcessingTime = DateTime.UtcNow - startTime
                    };
                }

                // Cache the response
                if (_ocrConfig.EnableCaching)
                {
                    await _textractCache.CacheTextractResponse(documentKey, textractResponse);
                }

                return ConvertToOcrResult(textractResponse, documentKey, startTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Textract Error: {ex.Message}");
                return new OcrResult
                {
                    DocumentKey = documentKey,
                    Success = false,
                    ErrorMessage = ex.Message,
                    Engine = GetEngineName(),
                    ProcessingTime = DateTime.UtcNow - startTime
                };
            }
        }

        public async Task<OcrResult> ExtractTextFromFileAsync(string filePath, string cacheKey = null)
        {
            throw new NotImplementedException("Textract service requires S3 bucket. Use ExtractTextFromS3Async instead.");
        }

        public async Task<OcrResult> ExtractTextFromBytesAsync(byte[] fileBytes, string fileName, string cacheKey = null)
        {
            throw new NotImplementedException("Textract service requires S3 bucket. Upload to S3 first and use ExtractTextFromS3Async.");
        }

        private async Task<TextractResponse> ProcessTextract(string bucketName, string documentKey)
        {
            try
            {
                Console.WriteLine($"Processing file: {documentKey} from bucket: {bucketName}");

                var startRequest = new StartDocumentAnalysisRequest
                {
                    DocumentLocation = new DocumentLocation
                    {
                        S3Object = new S3Object
                        {
                            Bucket = bucketName,
                            Name = documentKey
                        }
                    },
                    FeatureTypes = new List<string> { "TABLES", "FORMS", "LAYOUT" },
                    NotificationChannel = new NotificationChannel
                    {
                        RoleArn = _textractRoleArn,
                        SNSTopicArn = _snsTopicArn
                    }
                };

                var startResponse = await _textractClient.StartDocumentAnalysisAsync(startRequest);
                var jobId = startResponse.JobId;
                Console.WriteLine($"Started Textract analysis job with ID: {jobId}");

                var result = await WaitForJobCompletion(jobId);
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

                return response;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error processing file {documentKey} from bucket {bucketName}: {e.Message}");
                return new TextractResponse
                {
                    JobStatus = "ERROR",
                    ErrorMessage = e.Message
                };
            }
        }

        private async Task<GetDocumentAnalysisResponse> WaitForJobCompletion(string jobId)
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
                    Console.WriteLine($"Textract job failed: {result.StatusMessage}");
                    return null;
                }

                await Task.Delay(RetryInterval);
            }

            Console.WriteLine("Job did not complete within the maximum retry attempts");
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

        private OcrResult ConvertToOcrResult(TextractResponse textractResponse, string documentKey, DateTime startTime)
        {
            var formFields = textractResponse.FormData
          .SelectMany(dict => dict)
         .GroupBy(kvp => kvp.Key)
             .ToDictionary(
    g => g.Key,
      g => string.Join(" | ", g.Select(x => x.Value).Distinct())
        );

            var tableData = ExtractTableText(textractResponse.TableData);

            return new OcrResult
            {
                DocumentKey = documentKey,
                RawText = textractResponse.RawText,
                FormFields = formFields,
                TableData = tableData,
                Success = textractResponse.JobStatus == "SUCCEEDED",
                Engine = GetEngineName(),
                ProcessingTime = DateTime.UtcNow - startTime,
                Metadata = new Dictionary<string, object>
             {
    { "JobId", textractResponse.JobId },
                { "Pages", textractResponse.Pages?.Count ?? 0 },
                    { "FormFieldCount", formFields.Count },
             { "TableCount", tableData.Count }
      }
            };
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

        private float GetFloatValue(float? nullableValue)
        {
            return nullableValue ?? 0f;
        }

        private static string ComputeHash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
