# Production Troubleshooting Guide

**Common Issues and Solutions for Document Processing Pipeline**

> **Purpose:** Diagnose and resolve production issues  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [Upload Issues](#upload-issues)
3. [Textract Issues](#textract-issues)
4. [Bedrock Issues](#bedrock-issues)
5. [Lambda Issues](#lambda-issues)
6. [Caching Issues](#caching-issues)
7. [Output Issues](#output-issues)
8. [Performance Issues](#performance-issues)
9. [Cost Issues](#cost-issues)
10. [Debug Tools](#debug-tools)

---

## Quick Diagnostics

### System Health Checklist

Run this checklist first to identify the problem area:

```
? S3: Can you list files in bucket?
  aws s3 ls s3://testbucket-sudhir-bsi1/uploads/

? Textract: Can you start a job?
  aws textract start-document-analysis ...

? Bedrock: Can you invoke a model?
  aws bedrock-runtime invoke-model ...

? Lambda: Can you invoke the function?
  aws lambda invoke ...

? Permissions: Are IAM roles correctly configured?
  aws iam get-role --role-name accesstextract-role

? Logs: Any errors in CloudWatch?
  aws logs tail /aws/lambda/textract-processor
```

### Error Message Decoder

| Error Pattern | Service | Section |
|---------------|---------|---------|
| `Access Denied` | S3/IAM | [IAM permissions](#issue-access-denied-errors) |
| `InvalidS3ObjectException` | Textract | [Textract issues](#issue-textract-job-fails-immediately) |
| `Model not found` | Bedrock | [Bedrock issues](#issue-model-not-found) |
| `Task timed out` | Lambda | [Lambda issues](#issue-lambda-timeout) |
| `File not found` | Local/S3 | [Upload issues](#issue-files-not-uploading) |
| `JSON parse error` | Bedrock | [Output issues](#issue-invalid-json-output) |
| `Cache miss` | Caching | [Caching issues](#issue-cache-not-working) |

---

## Upload Issues

### Issue: Files Not Uploading

**Symptoms:**
```
Error: Could not upload file to S3
AmazonS3Exception: Access Denied
```

**Diagnosis:**
```powershell
# Check if bucket exists
aws s3 ls s3://testbucket-sudhir-bsi1/

# Check AWS credentials
aws sts get-caller-identity

# Try manual upload
aws s3 cp test.tif s3://testbucket-sudhir-bsi1/uploads/test/test.tif
```

**Solutions:**

1. **Verify AWS credentials:**
   ```powershell
   aws configure list
   ```

2. **Check IAM permissions:**
   ```json
   {
     "Effect": "Allow",
     "Action": ["s3:PutObject", "s3:ListBucket"],
     "Resource": [
       "arn:aws:s3:::testbucket-sudhir-bsi1",
       "arn:aws:s3:::testbucket-sudhir-bsi1/*"
     ]
   }
   ```

3. **Verify bucket name:**
   ```csharp
   // In Program.cs or Function.cs
   private const string BucketName = "testbucket-sudhir-bsi1";
   ```

4. **Check file size:**
   - S3 limit: 5 GB per PUT
   - Textract limit: 512 MB

---

### Issue: UploadFiles Folder Not Found

**Symptoms:**
```
Error: Folder 'UploadFiles' not found
Could not find path: E:\...\TextractUploader\UploadFiles
```

**Solutions:**

1. **Create folder:**
   ```powershell
   cd E:\Sudhir\GitRepo\AWSTextract\TextractUploader
   mkdir UploadFiles
   ```

2. **Verify files:**
   ```powershell
   dir UploadFiles
   ```

3. **Check project path:**
   ```csharp
   // Uploader looks for TextractUploader.csproj
   var projectDir = FindProjectDirectory();
   ```

---

### Issue: Incorrect S3 Path Structure

**Symptoms:**
Files uploaded to wrong location:
```
? s3://bucket/2025000065660.tif
? s3://bucket/uploads/2025-01-20/2025000065660/2025000065660.tif
```

**Solutions:**

1. **Verify upload code:**
   ```csharp
   var s3Key = $"uploads/{uploadDate}/{fileNameWithoutExt}/{fileName}";
   ```

2. **Check date format:**
   ```csharp
   private static readonly string UploadDate = DateTime.Now.ToString("yyyy-MM-dd");
   ```

3. **Manually move files:**
   ```bash
   aws s3 mv s3://bucket/file.tif s3://bucket/uploads/2025-01-20/file/file.tif
   ```

---

## Textract Issues

### Issue: Textract Job Fails Immediately

**Symptoms:**
```
Job failed: InvalidS3ObjectException
Could not retrieve S3 object
```

**Diagnosis:**
```bash
# Check if file exists
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/deed/deed.tif

# Check file metadata
aws s3api head-object \
  --bucket testbucket-sudhir-bsi1 \
  --key uploads/2025-01-20/deed/deed.tif
```

**Solutions:**

1. **Verify file path:**
   ```csharp
   // Exact S3 key must match
   var documentKey = "uploads/2025-01-20/deed/deed.tif";
   ```

2. **Check file format:**
   - Supported: TIF, TIFF, PDF, PNG, JPG, JPEG
   - Not supported: DOCX, TXT, etc.

3. **Verify file size:**
   ```bash
   # File must be < 512 MB
   aws s3api head-object --bucket bucket --key key \
     --query 'ContentLength' --output text
   ```

4. **Check Textract role permissions:**
   ```json
   {
     "Effect": "Allow",
     "Action": "s3:GetObject",
     "Resource": "arn:aws:s3:::testbucket-sudhir-bsi1/*"
   }
   ```

---

### Issue: Textract Job Timeout

**Symptoms:**
```
Job did not complete within the maximum retry attempts
Waited 5 minutes, job still IN_PROGRESS
```

**Diagnosis:**
```bash
# Check job status manually
aws textract get-document-analysis --job-id abc123...
```

**Solutions:**

1. **Increase retry limits:**
   ```csharp
   private const int MaxRetries = 120; // 10 minutes instead of 5
   private const int RetryInterval = 5000; // 5 seconds
   ```

2. **Check document complexity:**
   - Large multi-page documents (> 10 pages) take longer
   - Complex tables slow processing

3. **Use SNS notifications** instead of polling:
   ```csharp
   NotificationChannel = new NotificationChannel
   {
  RoleArn = TextractRoleArn,
     SNSTopicArn = SnsTopicArn
   }
   ```

4. **Split large documents:**
   ```csharp
   // Process in batches of 5 pages
   var chunks = SplitPdf(largePdf, pagesPerChunk: 5);
```

---

### Issue: Missing Form Fields

**Symptoms:**
```
Textract extracted text but no form fields found
FormData.Count == 0
```

**Diagnosis:**
```json
// Check raw Textract response
{
  "Blocks": [
    {"BlockType": "LINE", "Text": "Property Address:"},
    {"BlockType": "LINE", "Text": "123 Main St"}
  ]
}
// No KEY_VALUE_SET blocks found
```

**Solutions:**

1. **Document has no formal form fields:**
   - Use raw text extraction
   - Let Bedrock AI extract from text

2. **Improve OCR quality:**
   - Increase image resolution (min 150 DPI)
   - Enhance contrast
   - Remove noise

3. **Try Textract Queries:**
   ```csharp
   Queries = new List<Query>
   {
       new Query { Text = "What is the property address?" }
   }
   ```

---

## Bedrock Issues

### Issue: Model Not Found

**Symptoms:**
```
ValidationException: The provided model identifier is invalid
Model amazon.nova-lite-v1:0 not found in region us-east-1
```

**Solutions:**

1. **Check model availability in region:**
   ```bash
   aws bedrock list-foundation-models --region us-west-2
   ```

2. **Request model access:**
   - AWS Console ? Bedrock ? Model access
   - Click "Request access" for desired models

3. **Use correct region:**
   ```csharp
   // Nova Lite requires us-west-1 or us-west-2
   var region = Amazon.RegionEndpoint.USWest2;
   var bedrockClient = new AmazonBedrockRuntimeClient(region);
   ```

4. **Switch to available model:**
   ```csharp
   // Use Qwen 3 (us-west-2) or Claude 3 (us-east-1)
   var modelConfig = BedrockModelConfig.Qwen3;
   ```

---

### Issue: Bedrock Returns Malformed JSON

**Symptoms:**
```
JsonException: Could not extract valid JSON from response
Response contains explanation text or markdown
```

**Example Bad Response:**
```
Here's the extracted data:

```json
{
  "property": "123 Main St"
}
```

Let me explain...
```

**Solutions:**

1. **Check raw response files:**
   ```
   CachedFiles_OutputFiles/raw_response_{timestamp}.txt
   CachedFiles_OutputFiles/completion_text_{timestamp}.txt
   ```

2. **Improve prompt:**
   ```
   "Return ONLY valid JSON. No markdown. No explanations. No code fences."
   ```

3. **Use JsonExtractor:**
   ```csharp
   // Already implemented in BedrockService
   var cleanJson = JsonExtractor.NormalizeJson(responseText);
   ```

4. **Lower temperature:**
   ```csharp
   Temperature = 0.0f  // Fully deterministic
   ```

---

### Issue: Bedrock Slow Responses

**Symptoms:**
```
Bedrock request taking > 60 seconds
Processing timeout warnings
```

**Diagnosis:**
```csharp
// Check token counts
Console.WriteLine($"Input tokens: {inputTokens}");
Console.WriteLine($"Output tokens: {outputTokens}");
```

**Solutions:**

1. **Reduce input size:**
   ```csharp
   // Summarize or truncate
   if (combinedData.Length > 30000)
   {
       combinedData = combinedData.Substring(0, 30000);
   }
   ```

2. **Lower max_tokens:**
   ```csharp
   MaxTokens = 2048  // Instead of 4096
   ```

3. **Switch to faster model:**
   ```csharp
   var modelConfig = BedrockModelConfig.NovaLite; // Fastest
   ```

4. **Enable streaming (if supported):**
   ```csharp
 // Use InvokeModelWithResponseStream
   ```

---

### Issue: High Bedrock Costs

**Symptoms:**
```
AWS bill higher than expected
Bedrock charges: $100+ for 100 documents
```

**Diagnosis:**
```powershell
# Check CloudWatch metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Bedrock \
  --metric-name TokensUsed \
  --start-time 2025-01-01T00:00:00Z \
  --end-time 2025-01-31T23:59:59Z \
  --period 86400 \
  --statistics Sum
```

**Solutions:**

1. **Enable caching:**
   ```csharp
   // Already enabled in BedrockService
   _cache.Set(promptCacheKey, response, TimeSpan.FromMinutes(60));
   ```

2. **Monitor token usage:**
   ```csharp
   Console.WriteLine($"Cost: ${CalculateCost(inputTokens, outputTokens):F4}");
   ```

3. **Use cheaper model:**
   ```csharp
   var modelConfig = BedrockModelConfig.NovaLite; // $0.0004 input vs $0.0008
   ```

4. **Reduce prompt size:**
   - Remove unnecessary examples
   - Simplify rules
   - Truncate long documents

5. **Batch processing:**
   ```csharp
   // Process multiple docs in single prompt
   ```

---

## Lambda Issues

### Issue: Lambda Timeout

**Symptoms:**
```
Task timed out after 3.00 seconds
Function execution time exceeded configured timeout
```

**Solutions:**

1. **Increase timeout:**
   ```bash
   aws lambda update-function-configuration \
 --function-name textract-processor \
  --timeout 900  # 15 minutes
   ```

2. **Increase memory:**
   ```bash
   aws lambda update-function-configuration \
     --function-name textract-processor \
     --memory-size 1024  # More memory = faster CPU
   ```

3. **Optimize processing:**
   - Enable caching (Textract + Bedrock)
   - Reduce max retries for Textract
   - Process fewer documents per invocation

---

### Issue: Lambda Out of Memory

**Symptoms:**
```
Runtime exited with error: signal: killed
Process out of memory
```

**Solutions:**

1. **Increase memory:**
   ```bash
   aws lambda update-function-configuration \
     --function-name textract-processor \
     --memory-size 2048  # Or higher
   ```

2. **Reduce memory usage:**
   ```csharp
   // Don't hold all results in memory
   await File.WriteAllTextAsync(path, json);
   json = null; // Release memory
   ```

3. **Process in batches:**
   ```csharp
   // Instead of processing all documents at once
   foreach (var doc in documents)
   {
     await ProcessDocument(doc);
   GC.Collect(); // Force garbage collection
   }
   ```

---

### Issue: Lambda Cold Start

**Symptoms:**
```
First invocation takes 10+ seconds
Subsequent invocations are fast
```

**Solutions:**

1. **Use provisioned concurrency:**
   ```bash
   aws lambda put-provisioned-concurrency-config \
     --function-name textract-processor \
     --provisioned-concurrent-executions 1
   ```

2. **Optimize package size:**
   - Remove unused dependencies
   - Use Lambda layers for large libraries

3. **Keep Lambda warm:**
   ```bash
   # Schedule regular invocations
   aws events put-rule \
     --name keep-warm \
     --schedule-expression "rate(5 minutes)"
   ```

---

### Issue: Lambda Permission Errors

**Symptoms:**
```
User: arn:aws:sts::123:assumed-role/lambda-role/function
is not authorized to perform: bedrock:InvokeModel
```

**Solutions:**

1. **Check execution role:**
   ```bash
   aws lambda get-function-configuration \
     --function-name textract-processor \
   --query 'Role'
   ```

2. **Verify role permissions:**
   ```bash
   aws iam get-role-policy \
     --role-name lambda-textract-processor-role \
     --policy-name LambdaTextractProcessorPolicy
   ```

3. **Add missing permissions:**
   ```json
   {
     "Effect": "Allow",
     "Action": "bedrock:InvokeModel",
     "Resource": "arn:aws:bedrock:*::foundation-model/*"
   }
   ```

---

## Caching Issues

### Issue: Cache Not Working

**Symptoms:**
```
Cache miss every time
Processing taking full time even for duplicate requests
```

**Diagnosis:**
```csharp
Console.WriteLine($"Cache key: {promptCacheKey}");
Console.WriteLine($"Cache hit: {_cache.TryGetValue(promptCacheKey, out var cached)}");
```

**Solutions:**

1. **Verify cache enabled:**
   ```csharp
   // In BedrockService
   _cache.Set(promptCacheKey, response, TimeSpan.FromMinutes(60));
   ```

2. **Check cache duration:**
   ```csharp
   private const int CACHE_DURATION_MINUTES = 60;
   ```

3. **Verify cache key calculation:**
```csharp
   // Cache key includes: data + system prompt + user prompt + model ID
   var cacheInput = $"{sourceData}|{systemPrompt}|{userPrompt}|{modelConfig.ModelId}";
   ```

4. **Lambda restarts clear cache:**
   - Memory cache is cleared on Lambda cold start
   - Consider using ElastiCache or DynamoDB for persistent cache

---

### Issue: Textract Cache Not Found

**Symptoms:**
```
Cache miss: Processing document with Textract again
Cache file exists but not loaded
```

**Solutions:**

1. **Check cache directory:**
   ```csharp
   var cacheDir = "CachedFiles_OutputFiles";
   Console.WriteLine($"Cache directory: {cacheDir}");
   Console.WriteLine($"Files: {string.Join(", ", Directory.GetFiles(cacheDir))}");
   ```

2. **Verify file naming:**
 ```
   Expected: uploads_2025-01-20_deed_deed.tif_textract.json
   Actual: ?
   ```

3. **Check Lambda /tmp storage:**
   ```bash
   # Lambda /tmp has 512 MB limit
   # Cache files may be evicted if space runs out
   ```

4. **Use S3 for persistent cache:**
   ```csharp
   // Save to S3 instead of local disk
   await _s3Client.PutObjectAsync(new PutObjectRequest
   {
       BucketName = "cache-bucket",
     Key = $"textract-cache/{cacheKey}.json",
       Body = jsonStream
   });
   ```

---

## Output Issues

### Issue: Invalid JSON Output

**Symptoms:**
```
JsonException: The JSON value could not be converted
Duplicate keys in JSON object
```

**Diagnosis:**
```json
// Check output file
{
  "buyer": "John",
  "buyer": "Jane"  // Duplicate key
}
```

**Solutions:**

1. **Check raw Bedrock response:**
   ```
   CachedFiles_OutputFiles/raw_response_{timestamp}.txt
   ```

2. **Use JSON validation:**
   ```csharp
   try
   {
       using var doc = JsonDocument.Parse(jsonString);
     // Valid JSON
   }
   catch (JsonException ex)
   {
  Console.WriteLine($"Invalid JSON: {ex.Message}");
   }
   ```

3. **Handle duplicate keys:**
   ```csharp
   // SchemaMapperService.ValidateAndCleanJson()
   private void HandleDuplicateKeysInArrays(JToken token)
   {
       // Merges duplicate keys into arrays
   }
   ```

---

### Issue: Output Files Not Generated

**Symptoms:**
```
Processing completes but no JSON files created
Output directory empty
```

**Diagnosis:**
```powershell
# Check directory
dir CachedFiles_OutputFiles

# Check permissions
icacls CachedFiles_OutputFiles
```

**Solutions:**

1. **Create output directory:**
   ```csharp
   Directory.CreateDirectory(OutputDirectory);
   ```

2. **Check Lambda /tmp permissions:**
   ```csharp
   var outputPath = "/tmp/CachedFiles_OutputFiles";
 Directory.CreateDirectory(outputPath);
   ```

3. **Verify file write operations:**
   ```csharp
   try
   {
       await File.WriteAllTextAsync(filePath, json);
       Console.WriteLine($"? Saved: {filePath}");
   }
   catch (Exception ex)
   {
       Console.WriteLine($"? Error saving: {ex.Message}");
   }
   ```

---

## Performance Issues

### Issue: Slow Processing

**Symptoms:**
```
Total processing time: 10+ minutes per document
Expected: 3-5 minutes
```

**Diagnosis:**
```csharp
// Add timing measurements
var sw = Stopwatch.StartNew();
// ... operation ...
Console.WriteLine($"Operation took: {sw.ElapsedMilliseconds}ms");
```

**Solutions:**

1. **Enable caching:**
   - Textract: Disk-based cache
   - Bedrock: Memory cache

2. **Parallel processing:**
   ```csharp
   var tasks = documents.Select(doc => ProcessDocumentAsync(doc));
   await Task.WhenAll(tasks);
   ```

3. **Optimize Bedrock:**
   - Use faster model (Nova Lite)
   - Reduce token counts
   - Lower temperature

4. **Optimize Textract:**
   - Reduce polling interval
   - Use SNS notifications instead of polling

---

## Cost Issues

### Issue: Unexpected High Costs

**Symptoms:**
```
AWS bill much higher than estimated
$500+ per month for 100 docs/day (expected $60)
```

**Diagnosis:**
```bash
# Check AWS Cost Explorer
# Navigate to: AWS Console ? Billing ? Cost Explorer

# Check service breakdown:
# - S3: Storage + requests
# - Textract: Pages processed
# - Bedrock: Tokens used
# - Lambda: Invocations + duration
```

**Solutions:**

1. **Enable caching everywhere:**
   ```csharp
   // Textract cache: Saves $0.045/page
   // Bedrock cache: Saves $0.003-0.006/request
   ```

2. **Monitor token usage:**
   ```csharp
   LogMetric(context, "InputTokens", inputTokens);
   LogMetric(context, "OutputTokens", outputTokens);
   LogMetric(context, "Cost", CalculateCost(inputTokens, outputTokens));
   ```

3. **Use cheaper models:**
   ```csharp
   // Nova Lite: $0.0004 input (cheapest)
   // vs Claude 3: $0.00025 input (but more accurate)
   ```

4. **Set up billing alerts:**
```bash
   aws cloudwatch put-metric-alarm \
 --alarm-name high-textract-costs \
   --metric-name EstimatedCharges \
     --threshold 100 \
     --comparison-operator GreaterThanThreshold
   ```

---

## Debug Tools

### CloudWatch Logs

```bash
# Tail Lambda logs
aws logs tail /aws/lambda/textract-processor --follow

# Filter errors only
aws logs tail /aws/lambda/textract-processor --follow --filter-pattern "ERROR"

# Get specific request
aws logs filter-log-events \
  --log-group-name /aws/lambda/textract-processor \
  --filter-pattern "RequestId: abc123"
```

### Local Testing

```csharp
// Test Lambda locally
var context = new TestLambdaContext();
var function = new Function();
var result = await function.FunctionHandler(context);
```

### Enable Debug Logging

```csharp
// In Function.cs
private void LogDebug(ILambdaContext context, string message)
{
    if (Environment.GetEnvironmentVariable("DEBUG") == "true")
    {
        context?.Logger.LogLine($"[DEBUG] {message}");
 }
}
```

### AWS X-Ray Tracing

```bash
# Enable X-Ray for Lambda
aws lambda update-function-configuration \
  --function-name textract-processor \
  --tracing-config Mode=Active
```

### Test Individual Components

```csharp
// Test S3 upload
await _s3Client.PutObjectAsync(new PutObjectRequest
{
    BucketName = "testbucket",
    Key = "test.txt",
    ContentBody = "test"
});

// Test Textract
var jobId = await StartTextractJob(bucket, key);
Console.WriteLine($"Job ID: {jobId}");

// Test Bedrock
var (response, _, _) = await _bedrockService.ProcessTextractResults(
    "Test input",
    "Extract data",
    "Process this text"
);
Console.WriteLine($"Response: {response}");
```

---

## Emergency Procedures

### If Everything Fails

1. **Check AWS Service Health:**
   ```
   https://status.aws.amazon.com/
   ```

2. **Review recent changes:**
   - Code deployments
   - Configuration changes
   - IAM policy modifications

3. **Roll back to last known good state:**
   ```bash
   git revert HEAD
   # Redeploy
   ```

4. **Contact AWS Support:**
   - Technical support case
   - Include: logs, error messages, request IDs

---

## Quick Reference

### Common Commands

```bash
# Check S3
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/

# Check Textract job
aws textract get-document-analysis --job-id <job-id>

# Check Bedrock model
aws bedrock list-foundation-models --region us-west-2

# Check Lambda logs
aws logs tail /aws/lambda/textract-processor --follow

# Test Lambda
aws lambda invoke --function-name textract-processor output.json
```

### Support Resources

- **AWS Documentation:** https://docs.aws.amazon.com/
- **Textract Docs:** https://docs.aws.amazon.com/textract/
- **Bedrock Docs:** https://docs.aws.amazon.com/bedrock/
- **AWS Support:** https://console.aws.amazon.com/support/

---

**Still having issues?** Contact the development team with:
1. Error messages (full stack trace)
2. CloudWatch logs (request ID)
3. Steps to reproduce
4. Expected vs actual behavior
