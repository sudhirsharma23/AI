# Production Textract OCR Process

**Extract Text, Forms, and Tables from Documents using AWS Textract**

> **Service:** TextractOcrService  
> **Purpose:** OCR extraction with caching and async job management  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Overview](#overview)
2. [How Textract Works](#how-textract-works)
3. [OCR Features](#ocr-features)
4. [Processing Flow](#processing-flow)
5. [Configuration](#configuration)
6. [Output Structure](#output-structure)
7. [Caching Strategy](#caching-strategy)
8. [Troubleshooting](#troubleshooting)

---

## Overview

### What Textract Does

AWS Textract analyzes document images and extracts:
- **Raw Text** - All text content in the document
- **Form Fields** - Key-value pairs (labels and values)
- **Tables** - Structured data in rows and columns
- **Layout** - Document structure and geometry

### Service Architecture

```
???????????????????????????????????????????????????????????????????
?   TEXTRACT OCR SERVICE ARCHITECTURE     ?
???????????????????????????????????????????????????????????????????
?      ?
?  [S3 Document] ? [Check Cache] ? [Start Textract Job]   ?
?       ?             Cache Hit ?       ? SNS    ?
?   Return Cached       ?    [Wait for Completion] ?
?   Result   ?       ? Poll Status     ?
?    ?    [Get Results] ?
?     ?       ? Multiple Pages ?
?      ?    [Process Blocks]       ?
?         ? ? Extract ?
?    ?    [Forms + Tables + Text]?
?       ?     ? Save   ?
?       ?  [Cache Results]?
?         ?       ?     ?
?          ?    [Return OcrResult]     ?
???????????????????????????????????????????????????????????????????
```

---

## How Textract Works

### Async Job Processing

Textract uses **asynchronous processing** for document analysis:

1. **Start Job** - Submit document for analysis
2. **Get Job ID** - Receive unique job identifier
3. **Wait for Completion** - Poll status or use SNS notifications
4. **Retrieve Results** - Get analysis results with pagination

### Processing Steps

```
1. StartDocumentAnalysis
   ?? Input: S3 bucket + key
   ?? Features: TABLES, FORMS, LAYOUT
   ?? SNS: Notification channel
   ?? Output: JobId

2. Poll GetDocumentAnalysis
   ?? Input: JobId
   ?? Check: JobStatus (IN_PROGRESS, SUCCEEDED, FAILED)
   ?? Retry: Every 5 seconds, max 60 attempts (5 minutes)
   ?? Output: Analysis response when SUCCEEDED

3. Process Blocks
   ?? PAGE blocks ? Document pages
   ?? LINE blocks ? Text lines
   ?? WORD blocks ? Individual words
   ?? KEY_VALUE_SET ? Form fields
   ?? TABLE blocks ? Table structures
   ?? CELL blocks ? Table cells

4. Build Response
   ?? RawText: All text content
   ?? FormData: Key-value pairs
   ?? TableData: Structured tables
   ?? Pages: Geometry and metadata
```

---

## OCR Features

### 1. Text Extraction

**Raw Text:**
All text content extracted line-by-line from the document.

```csharp
// Output example
response.RawText = @"
GRANT DEED
Property Address: 123 Main St
Grantor: John Doe
Grantee: Jane Smith
Sale Price: $500,000
";
```

---

### 2. Form Field Detection

**Key-Value Pairs:**
Identifies form labels and their corresponding values.

```csharp
// Output example
response.FormData = new List<Dictionary<string, string>>
{
  { { "Property Address:", "123 Main St, Los Angeles, CA 90001" } },
    { { "Grantor:", "John Doe" } },
    { { "Grantee:", "Jane Smith" } },
    { { "Sale Price:", "$500,000.00" } },
    { { "Recording Date:", "01/15/2025" } }
};
```

**How It Works:**
- Textract identifies KEY_VALUE_SET blocks
- KEY block: The form label (e.g., "Property Address:")
- VALUE block: The corresponding value (e.g., "123 Main St")

---

### 3. Table Extraction

**Structured Tables:**
Extracts tables with row/column structure preserved.

```csharp
// Output example
response.TableData = new List<Dictionary<string, object>>
{
    new Dictionary<string, object>
 {
  { "TableId", "table-123" },
        { "Rows", 5 },
        { "Columns", 3 },
        { "Cells", new List<Dictionary<string, string>>
      {
             { { "RowIndex", "1" }, { "ColumnIndex", "1" }, { "Text", "Name" } },
       { { "RowIndex", "1" }, { "ColumnIndex", "2" }, { "Text", "Percentage" } },
       { { "RowIndex", "1" }, { "ColumnIndex", "3" }, { "Text", "Address" } },
  { { "RowIndex", "2" }, { "ColumnIndex", "1" }, { "Text", "John Doe" } },
       { { "RowIndex", "2" }, { "ColumnIndex", "2" }, { "Text", "50%" } },
            { { "RowIndex", "2" }, { "ColumnIndex", "3" }, { "Text", "123 Main St" } }
            }
   }
    }
};
```

---

### 4. Geometry Information

**Bounding Boxes:**
Each text element includes position coordinates.

```csharp
// Word geometry example
{
    "Text": "GRANT DEED",
    "Confidence": 99.8,
    "Geometry": {
      "Left": 0.125,
 "Top": 0.050,
        "Width": 0.250,
        "Height": 0.030
    }
}
```

**Use Cases:**
- Identify document sections by position
- Extract headers/footers
- Validate form alignment

---

## Processing Flow

### End-to-End Flow

```
????????????????????????????????????????????????????????????????
? STEP 1: CHECK CACHE ?
????????????????????????????????????????????????????????????????
? Input: S3 document key?
? Cache: CachedFiles_OutputFiles/{document}_textract.json      ?
? ?
? Cache HIT ? Return cached TextractResponse  ?
? Cache MISS ? Proceed to Step 2   ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 2: START TEXTRACT JOB     ?
????????????????????????????????????????????????????????????????
? Request:  ?
?   - DocumentLocation: S3 bucket + key?
?   - FeatureTypes: [TABLES, FORMS, LAYOUT]?
?   - NotificationChannel: SNS topic ARN ?
? ?
? Response:   ?
?   - JobId: "abc123..."   ?
?   - Status: Job queued      ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 3: WAIT FOR COMPLETION   ?
????????????????????????????????????????????????????????????????
? Poll GetDocumentAnalysis every 5 seconds ?
? Max retries: 60 (5 minutes timeout)      ?
?    ?
? Status: IN_PROGRESS ? Continue polling ?
? Status: SUCCEEDED ? Proceed to Step 4?
? Status: FAILED ? Return error   ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 4: RETRIEVE RESULTS (with pagination)?
????????????????????????????????????????????????????????????????
? GetDocumentAnalysis(JobId, NextToken=null)?
? ?
? Response contains:  ?
?   - Blocks: All detected elements  ?
?   - NextToken: For pagination ?
?    ?
? If NextToken exists ? Fetch next page  ?
? Repeat until NextToken = null  ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 5: PROCESS BLOCKS ?
????????????????????????????????????????????????????????????????
? Iterate through all blocks:  ?
?   ?
? PAGE blocks:      ?
?   - Create new page with dimensions?
?     ?
? LINE blocks:   ?
?   - Append to RawText  ?
?   - Store line info with geometry   ?
?  ?
? KEY_VALUE_SET blocks:   ?
?   - Extract key text?
?   - Find corresponding VALUE block?
?   - Store as form field?
?  ?
? TABLE blocks:       ?
?   - Identify table structure ?
?   - Extract all CELL blocks  ?
?   - Build table with rows/columns?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 6: BUILD TEXTRACT RESPONSE ?
????????????????????????????????????????????????????????????????
? TextractResponse:   ?
?   - JobId: Original job ID  ?
? - JobStatus: SUCCEEDED?
?   - RawText: Combined text from all pages ?
?   - FormData: List of key-value dictionaries?
?   - TableData: List of table dictionaries   ?
?   - Pages: Page-by-page details ?
????????????????????????????????????????????????????????????????

????????????????????????????????????????????????????????????????
? STEP 7: CACHE RESULTS   ?
????????????????????????????????????????????????????????????????
? Save to: CachedFiles_OutputFiles/{document}_textract.json?
? Future requests for same document return cached result   ?
????????????????????????????????????????????????????????????????
```

---

## Configuration

### AWS Resources Required

```csharp
// Function.cs configuration
private const string BucketName = "testbucket-sudhir-bsi1";
private const string TextractRoleArn = "arn:aws:iam::912532823432:role/accesstextract-role";
private const string SnsTopicArn = "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo";
```

### IAM Role Permissions

The Textract role needs:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
 "Action": [
        "textract:StartDocumentAnalysis",
        "textract:GetDocumentAnalysis"
 ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
   "Action": [
        "s3:GetObject"
      ],
      "Resource": "arn:aws:s3:::testbucket-sudhir-bsi1/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "sns:Publish"
    ],
      "Resource": "arn:aws:sns:us-east-1:912532823432:sns-topic-textract.fifo"
  }
  ]
}
```

### OCR Configuration

```csharp
// OcrConfig.cs
public class OcrConfig
{
    public bool EnableCaching { get; set; } = true;
    public string CacheDirectory { get; set; } = "CachedFiles_OutputFiles";
    public int MaxRetries { get; set; } = 60;
    public int RetryIntervalMs { get; set; } = 5000;
    public List<string> Features { get; set; } = new() { "TABLES", "FORMS", "LAYOUT" };
}
```

### Feature Types

| Feature | Description | Use Case |
|---------|-------------|----------|
| **TABLES** | Extract table structure | Structured data extraction |
| **FORMS** | Detect key-value pairs | Form field extraction |
| **LAYOUT** | Analyze document layout | Section detection |
| **SIGNATURES** | Detect signatures | Signature verification |
| **QUERIES** | Ask specific questions | Targeted extraction |

---

## Output Structure

### TextractResponse Object

```csharp
public class TextractResponse
{
    // Job information
    public string JobId { get; set; }
    public string JobStatus { get; set; } // SUCCEEDED, FAILED, IN_PROGRESS
    
    // Extracted data
    public string RawText { get; set; }
    public List<Dictionary<string, string>> FormData { get; set; }
    public List<Dictionary<string, object>> TableData { get; set; }
  public List<PageInfo> Pages { get; set; }
    
    // Error handling
    public string ErrorMessage { get; set; }
}
```

### OcrResult Object (Returned to caller)

```csharp
public class OcrResult
{
    public string DocumentKey { get; set; }
    public string RawText { get; set; }
    public Dictionary<string, string> FormFields { get; set; }
    public List<List<string>> TableData { get; set; }
    public bool Success { get; set; }
    public string Engine { get; set; } = "AWS Textract";
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public string ErrorMessage { get; set; }
}
```

### Example Output

```json
{
  "documentKey": "uploads/2025-01-20/deed/deed.tif",
  "rawText": "GRANT DEED\nProperty Address: 123 Main St...",
  "formFields": {
    "Property Address:": "123 Main St, Los Angeles, CA 90001",
    "Grantor:": "John Doe",
    "Grantee:": "Jane Smith",
    "Sale Price:": "$500,000.00"
  },
  "tableData": [
["Name", "Percentage", "Address"],
    ["John Doe", "50%", "123 Main St"],
    ["Jane Smith", "50%", "456 Oak Ave"]
  ],
  "success": true,
  "engine": "AWS Textract",
  "processingTime": "00:02:34.567",
  "metadata": {
    "jobId": "abc123def456",
    "pages": 3,
    "formFieldCount": 15,
    "tableCount": 2
  }
}
```

---

## Caching Strategy

### Why Cache Textract Results?

? **Cost Savings** - Textract charges per page  
? **Speed** - Cached results return in < 1 second  
? **Reliability** - Avoid re-processing if Textract fails  
? **Development** - Test AI extraction without re-running OCR

### Cache Implementation

```csharp
// TextractCacheService.cs
public class TextractCacheService
{
    private readonly string _outputDirectory;
    
    // Cache file naming: {documentKey}_textract.json
    public async Task CacheTextractResponse(string documentKey, TextractResponse response)
    {
        var cacheKey = ComputeCacheKey(documentKey);
    var cachePath = Path.Combine(_outputDirectory, $"{cacheKey}_textract.json");
   
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions 
      { 
        WriteIndented = true 
        });
        
        await File.WriteAllTextAsync(cachePath, json);
    }
    
    public async Task<TextractResponse?> GetCachedResponse(string documentKey)
    {
        var cacheKey = ComputeCacheKey(documentKey);
        var cachePath = Path.Combine(_outputDirectory, $"{cacheKey}_textract.json");
        
        if (!File.Exists(cachePath))
      return null;
            
        var json = await File.ReadAllTextAsync(cachePath);
      return JsonSerializer.Deserialize<TextractResponse>(json);
    }
    
    private string ComputeCacheKey(string documentKey)
    {
      // Use document key as cache identifier
        return documentKey.Replace("/", "_").Replace("\\", "_");
}
}
```

### Cache Behavior

```
First Request:
  S3 Document ? Textract ? Cache ? Return Result
  Time: 2-5 minutes

Subsequent Requests:
  Cache ? Return Result
  Time: < 1 second
```

### Cache Location

```
CachedFiles_OutputFiles/
??? uploads_2025-01-20_deed_deed.tif_textract.json
??? uploads_2025-01-20_contract_contract.pdf_textract.json
??? uploads_2025-01-20_invoice_invoice.tif_textract.json
```

### Cache Invalidation

To force re-processing, delete the cached file:

```powershell
# Delete specific cache
Remove-Item "CachedFiles_OutputFiles\uploads_2025-01-20_deed_deed.tif_textract.json"

# Clear all Textract cache
Remove-Item "CachedFiles_OutputFiles\*_textract.json"
```

---

## Troubleshooting

### Issue: Textract Job Times Out

**Symptoms:**
```
Job did not complete within the maximum retry attempts
```

**Causes:**
- Large multi-page document
- Textract service overloaded
- Complex document structure

**Solutions:**

1. **Increase retry settings:**
   ```csharp
   private const int MaxRetries = 120; // 10 minutes
   private const int RetryInterval = 5000; // 5 seconds
   ```

2. **Check job status manually:**
   ```powershell
   aws textract get-document-analysis --job-id abc123...
   ```

3. **Use SNS notifications** instead of polling (already configured)

---

### Issue: "Access Denied" from Textract

**Cause:** IAM permissions not configured

**Solution:**

1. **Verify Textract role:**
   ```powershell
   aws iam get-role --role-name accesstextract-role
   ```

2. **Check trust relationship:**
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
  "Effect": "Allow",
         "Principal": {
   "Service": "textract.amazonaws.com"
         },
         "Action": "sts:AssumeRole"
       }
  ]
   }
   ```

3. **Verify S3 permissions:**
   - Role must have `s3:GetObject` on bucket

---

### Issue: Missing Form Fields

**Cause:** Textract couldn't detect key-value pairs

**Solutions:**

1. **Check document quality:**
   - Resolution should be at least 150 DPI
   - Clear, readable text
   - Structured form layout

2. **Try different feature types:**
   ```csharp
   FeatureTypes = new List<string> { "TABLES", "FORMS", "LAYOUT", "SIGNATURES" }
   ```

3. **Use Textract Queries** for specific fields:
   ```csharp
   Queries = new List<Query>
   {
       new Query { Text = "What is the property address?" },
       new Query { Text = "Who is the grantor?" }
   }
   ```

---

### Issue: Tables Not Extracted Correctly

**Cause:** Complex table structure or merged cells

**Solutions:**

1. **Check table structure** in Textract console
2. **Validate row/column counts:**
   ```csharp
   foreach (var table in tableData)
   {
       Console.WriteLine($"Table: {table["Rows"]}x{table["Columns"]}");
   }
   ```

3. **Handle merged cells:**
   ```csharp
   // Check for RowSpan and ColumnSpan in cell blocks
   ```

---

### Issue: Low Confidence Scores

**Symptoms:**
```
{
  "Text": "John Doe",
  "Confidence": 45.3
}
```

**Causes:**
- Poor image quality
- Handwritten text
- Faded or damaged documents

**Solutions:**

1. **Pre-process images:**
   - Increase contrast
   - Remove noise
   - Enhance resolution

2. **Filter low-confidence results:**
   ```csharp
   var reliableWords = words.Where(w => w.Confidence > 80);
   ```

3. **Use manual review** for critical data

---

## Performance Metrics

### Typical Processing Times

| Document Type | Pages | Processing Time |
|---------------|-------|-----------------|
| Single-page form | 1 | 30-60 seconds |
| Multi-page deed | 3-5 | 2-3 minutes |
| Large contract | 10-20 | 4-6 minutes |
| Book/manual | 50+ | 15-30 minutes |

### Cost Breakdown

**Textract Pricing (us-east-1):**
- Document Analysis: $0.015 per page
- Forms/Tables: $0.050 per page (included in analysis)

**Example:**
- 3-page deed = $0.045
- 10-page contract = $0.150
- 100 docs/day = $4.50/day = $135/month

### Optimization Tips

1. **Cache aggressively** - Save 100% on re-processing
2. **Batch process** - Group related documents
3. **Choose features wisely** - Only request needed features
4. **Pre-filter documents** - Skip blank or duplicate pages

---

## Next Steps

After Textract extraction:

1. **Process with Bedrock** ? [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md)
2. **Schema mapping** ? [PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md)
3. **Review outputs** ? Check `CachedFiles_OutputFiles/` directory

---

## Quick Reference

### Start Textract Job
```csharp
var response = await _textractClient.StartDocumentAnalysisAsync(new StartDocumentAnalysisRequest
{
    DocumentLocation = new DocumentLocation
    {
        S3Object = new S3Object { Bucket = bucket, Name = key }
    },
    FeatureTypes = new List<string> { "TABLES", "FORMS", "LAYOUT" },
    NotificationChannel = new NotificationChannel
    {
        RoleArn = roleArn,
        SNSTopicArn = snsArn
    }
});
```

### Get Results
```csharp
var result = await _textractClient.GetDocumentAnalysisAsync(new GetDocumentAnalysisRequest
{
    JobId = jobId,
    NextToken = nextToken
});
```

### Check Cache
```csharp
var cached = await _textractCache.GetCachedResponse(documentKey);
if (cached != null)
{
    // Use cached result
}
```

---

**Ready for AI extraction?** See [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md) for the next step.
