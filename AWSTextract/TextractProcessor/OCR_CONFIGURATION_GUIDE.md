# TextractProcessor - OCR Configuration Guide

## Overview

The TextractProcessor Lambda function now supports **dual OCR engines**:
- **AWS Textract** (default) - Cloud-based OCR service
- **Aspose.Total** - On-premise OCR library

You can easily switch between engines using environment variables or configuration without any code changes.

---

## Quick Start

### Switch to Aspose OCR

**Set Lambda Environment Variable:**
```
OCR_ENGINE = Aspose
```

### Switch to AWS Textract (default)

```
OCR_ENGINE = Textract
```

---

## Configuration Options

### Environment Variables

| Variable | Description | Default | Values |
|----------|-------------|---------|--------|
| `OCR_ENGINE` | OCR engine to use | `Textract` | `Textract` or `Aspose` |
| `ASPOSE_LICENSE_PATH` | Path to Aspose license file | `Aspose.Total.NET.lic` | File path |
| `OCR_ENABLE_CACHING` | Enable caching of OCR results | `true` | `true` or `false` |
| `OCR_CACHE_DURATION_DAYS` | Cache duration in days | `30` | Integer > 0 |

---

## AWS Textract vs Aspose Comparison

| Feature | AWS Textract | Aspose.Total |
|---------|--------------|--------------|
| **Processing Location** | Cloud (AWS) | On-premise (Lambda) |
| **Latency** | ~4-6 seconds | ~1-2 seconds |
| **Cost Model** | Pay per page | One-time license |
| **Network Required** | Yes | No |
| **Data Privacy** | Data leaves Lambda | Data stays in Lambda |
| **Accuracy** | Very High | Very High |
| **Table Detection** | ? Excellent | ? Requires additional code |
| **Form Fields** | ? Excellent | ? Requires additional code |
| **Multi-page Support** | ? Native | ? Native |
| **File Types** | TIF, PDF, PNG, JPG | TIF, PDF, PNG, JPG, BMP |

---

## Cost Analysis

### AWS Textract Pricing
- **Price**: $1.50 per 1,000 pages
- **Best for**: Low to medium volume (<200K pages/year)
- **No upfront cost**

**Example:**
- 10,000 pages/month = $15/month
- 100,000 pages/month = $150/month
- 200,000 pages/month = $300/month

### Aspose.Total Pricing
- **Price**: ~$2,999/year (one-time license)
- **Best for**: High volume (>200K pages/year)
- **Cost**: ~$250/month amortized

**Break-even Point:** ~200,000 pages/year (16,667 pages/month)

---

## Configuration Examples

### Lambda Environment Variables

**Using AWS Console:**
1. Go to Lambda Console
2. Select your `TextractProcessor` function
3. Click **Configuration** ? **Environment variables**
4. Add/Edit:
   - `OCR_ENGINE` = `Aspose` (or `Textract`)
   - `ASPOSE_LICENSE_PATH` = `Aspose.Total.NET.lic`
   - `OCR_ENABLE_CACHING` = `true`
   - `OCR_CACHE_DURATION_DAYS` = `30`

**Using AWS CLI:**
```bash
aws lambda update-function-configuration \
--function-name TextractProcessor \
  --environment Variables="{OCR_ENGINE=Aspose,ASPOSE_LICENSE_PATH=Aspose.Total.NET.lic,OCR_ENABLE_CACHING=true,OCR_CACHE_DURATION_DAYS=30}"
```

**Using CloudFormation/SAM:**
```yaml
Resources:
  TextractProcessorFunction:
    Type: AWS::Lambda::Function
    Properties:
      Environment:
        Variables:
          OCR_ENGINE: Aspose
      ASPOSE_LICENSE_PATH: Aspose.Total.NET.lic
    OCR_ENABLE_CACHING: true
          OCR_CACHE_DURATION_DAYS: 30
```

---

## Setting Up Aspose License

### Step 1: Obtain License
1. Purchase Aspose.Total license from https://purchase.aspose.com/
2. Download license file (`Aspose.Total.NET.lic`)

### Step 2: Add License to Deployment Package

**Option A: Include in Project**
1. Copy `Aspose.Total.NET.lic` to project root
2. Already configured in `.csproj`:
   ```xml
   <None Update="Aspose.Total.NET.lic">
     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
   </None>
   ```

**Option B: Layer (Recommended for Lambda)**
1. Create Lambda Layer with license file
2. Attach layer to function
3. Set `ASPOSE_LICENSE_PATH` environment variable

### Step 3: Verify License

Check Lambda logs after first invocation:
```
? Aspose license loaded successfully from: Aspose.Total.NET.lic
? Creating Aspose.Total OCR Service
```

Without license (evaluation mode):
```
? Aspose license file not found at: Aspose.Total.NET.lic
  Aspose will run in evaluation mode with limitations.
```

---

## Usage Scenarios

### Scenario 1: Low Volume (< 50K pages/year)
**Recommendation:** AWS Textract

```
OCR_ENGINE = Textract
```

**Why:** Lower cost, no upfront investment

---

### Scenario 2: High Volume (> 200K pages/year)
**Recommendation:** Aspose.Total

```
OCR_ENGINE = Aspose
```

**Why:** Cost-effective at scale, faster processing

---

### Scenario 3: Compliance/Privacy Requirements
**Recommendation:** Aspose.Total

```
OCR_ENGINE = Aspose
```

**Why:** All processing stays within Lambda, no external API calls

---

### Scenario 4: A/B Testing
**Recommendation:** Test both engines

Process with both and compare results. The OcrServiceFactory supports creating specific engines:

```csharp
var factory = new OcrServiceFactory(...);
var textractService = factory.CreateOcrService("Textract");
var asposeService = factory.CreateOcrService("Aspose");

var result1 = await textractService.ExtractTextFromS3Async(...);
var result2 = await asposeService.ExtractTextFromFileAsync(...);

// Compare results
Console.WriteLine($"Textract: {result1.ProcessingTime.TotalSeconds}s");
Console.WriteLine($"Aspose: {result2.ProcessingTime.TotalSeconds}s");
```

---

## Caching

Both OCR engines support intelligent caching to reduce processing time and costs.

### How it Works

1. **First Request**: Document processed, result cached
2. **Subsequent Requests**: Cached result returned instantly
3. **Cache Duration**: Configurable (default: 30 days)

### Cache Keys

- **Textract**: Based on S3 document key
- **Aspose**: Based on file path hash

### Cache Statistics

Check Lambda logs:
```
? Textract Cache HIT for: document.tif
? Aspose OCR processing file: document.tif
? Cached Aspose OCR result for: document.tif
```

### Disable Caching

```
OCR_ENABLE_CACHING = false
```

---

## Architecture

### Current Flow (with OCR abstraction)

```
S3 Upload (TIF file)
    ?
Lambda Trigger
    ?
OcrConfig (Environment Variable)
    ?
OcrServiceFactory
    ?
???????????????????
? Textract Service?  ? Selected by config
? Aspose Service  ?
???????????????????
    ?
OcrResult (unified format)
    ?
BedrockService (LLM Processing)
    ?
Schema Mapping
    ?
Save to S3
```

### Code Integration

**Before (hardcoded Textract):**
```csharp
// Direct Textract calls scattered in code
var textractResponse = await ProcessTextract(documentKey, context);
```

**After (configurable):**
```csharp
// Load configuration
var ocrConfig = OcrConfig.Load();
ocrConfig.Validate();

// Create factory
var factory = new OcrServiceFactory(ocrConfig, ...);

// Get configured service
var ocrService = factory.CreateOcrService();

// Process document
var result = await ocrService.ExtractTextFromS3Async(bucketName, documentKey);

// Same downstream processing regardless of engine!
```

---

## Monitoring

### CloudWatch Metrics

**Filter Pattern for OCR Engine Usage:**
```
"Creating AWS Textract OCR Service"
"Creating Aspose.Total OCR Service"
```

**Filter Pattern for Cache Performance:**
```
"Cache HIT"
"Cache MISS"
```

**Custom Metrics to Add:**
```csharp
// Add to your Lambda code
PutMetric("OCREngine", ocrConfig.Engine);
PutMetric("OCRProcessingTime", result.ProcessingTime.TotalMilliseconds);
PutMetric("OCRCacheHit", cacheHit ? 1 : 0);
```

### CloudWatch Logs Insights Queries

**OCR Engine Usage:**
```
fields @timestamp, @message
| filter @message like /Creating.*OCR Service/
| stats count() by @message
```

**Average Processing Time:**
```
fields @timestamp, @message
| filter @message like /processing file/
| parse @message /(?<duration>\d+\.?\d*)s/
| stats avg(duration) as avgTime by bin(5m)
```

---

## Troubleshooting

### Issue 1: Aspose runs in evaluation mode

**Symptoms:**
```
? Aspose license file not found at: Aspose.Total.NET.lic
  Aspose will run in evaluation mode with limitations.
```

**Solutions:**
1. Verify license file is in deployment package
2. Check `ASPOSE_LICENSE_PATH` environment variable
3. Verify file permissions in Lambda
4. Check CloudFormation/SAM template includes license

---

### Issue 2: High Lambda costs with Aspose

**Cause:** Aspose requires more memory than Textract integration

**Solution:**
- Textract: 512 MB sufficient
- Aspose: Recommend 1024-2048 MB

**Update Lambda memory:**
```bash
aws lambda update-function-configuration \
  --function-name TextractProcessor \
  --memory-size 2048
```

---

### Issue 3: Slower performance than expected

**Check:**
1. **Cache enabled?** `OCR_ENABLE_CACHING=true`
2. **Memory sufficient?** Increase Lambda memory
3. **Cold starts?** Consider provisioned concurrency

**Enable provisioned concurrency:**
```bash
aws lambda put-provisioned-concurrency-config \
  --function-name TextractProcessor \
  --provisioned-concurrent-executions 2 \
--qualifier $LATEST
```

---

### Issue 4: "Unknown OCR engine" error

**Symptoms:**
```
Unknown OCR engine: <value>
```

**Solution:**
Verify `OCR_ENGINE` value is exactly:
- `Textract` (case-insensitive)
- `Aspose` (case-insensitive)

---

## Best Practices

### 1. Start with Textract
Begin with Textract for lower upfront costs and easier setup.

### 2. Monitor Costs
Track monthly OCR costs to determine break-even point.

### 3. Cache Aggressively
Enable caching with reasonable duration (30+ days).

### 4. Test Both Engines
Run A/B tests to compare accuracy and performance for your specific documents.

### 5. Use Lambda Layers
Store Aspose license in Lambda Layer for easier management.

### 6. Set Appropriate Memory
- Textract: 512 MB
- Aspose: 2048 MB (for best performance)

### 7. Monitor Logs
Set up CloudWatch alarms for:
- High error rates
- Slow processing times
- Cache miss ratio

---

## Migration Path

### From Textract to Aspose

1. **Test Phase** (Week 1):
   - Deploy with Aspose enabled
   - Run parallel processing
   - Compare results

2. **Gradual Rollout** (Week 2):
   - Switch 10% of traffic
   - Monitor errors and performance
   - Adjust Lambda memory if needed

3. **Full Migration** (Week 3):
   - Switch all traffic to Aspose
   - Monitor for 1 week
   - Decommission Textract config

### From Aspose to Textract

Same process, reverse direction.

---

## Support

For issues:
1. Check CloudWatch Logs
2. Verify environment variables
3. Test with small sample document
4. Review this guide

For Aspose-specific issues:
- Aspose Support: https://forum.aspose.com/
- Aspose Documentation: https://docs.aspose.com/

For AWS Textract issues:
- AWS Support
- AWS Textract Documentation: https://docs.aws.amazon.com/textract/

---

**Configuration Version:** 1.0  
**Last Updated:** 2025-01-29  
**Status:** ? Production Ready

