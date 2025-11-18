# Production Documentation Index

**Complete Reference for AWS Document Processing Pipeline**

> **System:** TextractProcessor + Bedrock AI  
> **Version:** 2.0 (Dual-Version Extraction)  
> **Last Updated:** January 2025

---

## ?? Documentation Library

### ?? Start Here

**New to the system?** Start with the overview:
- **[PRODUCTION_PROCESS_OVERVIEW.md](PRODUCTION_PROCESS_OVERVIEW.md)** - System architecture and workflow

---

## ?? Process Guides

### Step-by-Step Production Workflows

1. **[PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md)**
   - Upload files to S3
   - Date-based folder structure
   - TextractUploader usage
   - **Time to read:** 10 minutes

2. **[PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md)**
   - OCR extraction with AWS Textract
   - Forms, tables, and text extraction
   - Async job management
   - Caching strategy
   - **Time to read:** 15 minutes

3. **[PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md)**
 - AI-powered data extraction
   - Model selection (Nova, Qwen, Claude, Titan)
   - Prompt architecture
   - Token management
   - **Time to read:** 15 minutes

4. **[PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md)**
   - Dual-version extraction (V1 + V2)
   - Schema-based vs dynamic extraction
   - Analysis reports
   - Output file structure
   - **Time to read:** 15 minutes

5. **[PRODUCTION_PROMPT_MANAGEMENT.md](PRODUCTION_PROMPT_MANAGEMENT.md)** *(Coming Soon)*
   - Prompt template system
   - Rules and examples
   - Versioning strategy
   - **Time to read:** 10 minutes

6. **[PRODUCTION_CACHING_STRATEGY.md](PRODUCTION_CACHING_STRATEGY.md)** *(Coming Soon)*
   - Textract disk cache
   - Bedrock memory cache
   - Performance optimization
   - **Time to read:** 10 minutes

---

## ?? Configuration Guides

### AWS and System Setup

1. **[PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md)**
   - Complete AWS infrastructure setup
   - S3, Textract, Bedrock, SNS, IAM, Lambda
   - IAM roles and policies
   - Security best practices
   - Cost estimates
   - **Time to read:** 20 minutes
   - **?? Critical for first-time setup**

2. **[PRODUCTION_MODEL_SELECTION.md](PRODUCTION_MODEL_SELECTION.md)** *(Coming Soon)*
   - Choosing the right Bedrock model
   - Performance vs cost tradeoffs
- Model comparison matrix
   - **Time to read:** 10 minutes

3. **[PRODUCTION_ENVIRONMENT_SETUP.md](PRODUCTION_ENVIRONMENT_SETUP.md)** *(Coming Soon)*
   - Lambda deployment
   - Local development environment
   - Dependency management
   - **Time to read:** 15 minutes

---

## ?? Troubleshooting & Maintenance

### Problem Resolution

1. **[PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md)**
   - Common issues and solutions
   - Upload, Textract, Bedrock, Lambda issues
   - Performance problems
- Cost issues
   - Debug tools
   - **Time to read:** 20 minutes
   - **?? Bookmark this for emergencies**

2. **[PRODUCTION_PERFORMANCE.md](PRODUCTION_PERFORMANCE.md)** *(Coming Soon)*
   - Performance tuning
   - Optimization techniques
   - Benchmarking
   - **Time to read:** 15 minutes

3. **[PRODUCTION_COST_MANAGEMENT.md](PRODUCTION_COST_MANAGEMENT.md)** *(Coming Soon)*
   - Cost monitoring
   - Optimization strategies
   - Billing alerts
   - **Time to read:** 10 minutes

---

## ?? Reference Materials

### Technical Documentation

1. **[PRODUCTION_API_REFERENCE.md](PRODUCTION_API_REFERENCE.md)** *(Coming Soon)*
   - Method signatures
   - Class documentation
   - Code examples

2. **[PRODUCTION_DATA_MODELS.md](PRODUCTION_DATA_MODELS.md)** *(Coming Soon)*
   - Object structures
   - JSON schemas
   - Type definitions

3. **[PRODUCTION_OUTPUT_FORMATS.md](PRODUCTION_OUTPUT_FORMATS.md)** *(Coming Soon)*
   - Output file specifications
   - V1 vs V2 JSON formats
   - Report templates

---

## ?? Quick Start Guide

### Get Up and Running in 30 Minutes

1. **Prerequisites** (5 min)
   - [ ] AWS account with credits
   - [ ] .NET 8 SDK installed
   - [ ] AWS CLI configured
   - [ ] Visual Studio or VS Code

2. **AWS Setup** (15 min)
   - [ ] Create S3 bucket
 - [ ] Set up IAM roles
   - [ ] Create SNS topic
   - [ ] Request Bedrock model access
   - **Guide:** [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md)

3. **Upload First Document** (5 min)
   - [ ] Copy file to `UploadFiles/`
   - [ ] Run TextractUploader
   - [ ] Verify file in S3
   - **Guide:** [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md)

4. **Process Document** (5 min)
   - [ ] Deploy Lambda function
   - [ ] Invoke Lambda
   - [ ] Check output files
   - **Guide:** [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md)

---

## ?? Process Flow Diagram

```
???????????????????????????????????????????????????????????????
?   COMPLETE PRODUCTION PIPELINE     ?
???????????????????????????????????????????????????????????????
?  ?
?  Step 1: UPLOAD  ?
?  Local Files ? TextractUploader ? S3 Bucket ?
?    Guide: PRODUCTION_UPLOAD_PROCESS.md  ?
?    ?
?  Step 2: OCR EXTRACTION  ?
?  S3 ? Textract ? TextractResponse  ?
?    Guide: PRODUCTION_TEXTRACT_OCR.md ?
? ?
?  Step 3: AI EXTRACTION?
?    TextractResponse ? Bedrock ? Structured JSON ?
?    Guide: PRODUCTION_BEDROCK_AI.md   ?
?    ?
?  Step 4: SCHEMA MAPPING    ?
?    V1 (Schema-based) + V2 (Dynamic) ? Dual Outputs?
?    Guide: PRODUCTION_SCHEMA_MAPPER.md?
? ?
?  Step 5: ANALYSIS & REPORTS?
?    Compare outputs ? Extension reports ? Summaries  ?
?    ?
?  Step 6: STORAGE  ?
?    Save JSON + TXT + MD files ? CachedFiles_OutputFiles/ ?
???????????????????????????????????????????????????????????????
```

---

## ?? Learning Path

### Recommended Reading Order

**For Developers (First Time):**
1. [PRODUCTION_PROCESS_OVERVIEW.md](PRODUCTION_PROCESS_OVERVIEW.md) - Understand the big picture
2. [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md) - Set up AWS infrastructure
3. [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md) - Upload your first file
4. [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md) - Understand OCR extraction
5. [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md) - Learn AI extraction
6. [PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md) - Understand dual extraction

**For Operations (Maintenance):**
1. [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md) - Problem resolution
2. [PRODUCTION_PERFORMANCE.md](PRODUCTION_PERFORMANCE.md) - Optimization
3. [PRODUCTION_COST_MANAGEMENT.md](PRODUCTION_COST_MANAGEMENT.md) - Cost control

**For Researchers (Schema Design):**
1. [PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md) - V1 vs V2 extraction
2. [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md) - Model selection
3. [PRODUCTION_OUTPUT_FORMATS.md](PRODUCTION_OUTPUT_FORMATS.md) - Output analysis

---

## ?? Find What You Need

### By Task

| Task | Guide |
|------|-------|
| Upload files to S3 | [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md) |
| Extract text from documents | [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md) |
| Extract structured data with AI | [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md) |
| Compare V1 vs V2 outputs | [PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md) |
| Set up AWS services | [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md) |
| Fix errors | [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md) |
| Choose a model | [PRODUCTION_BEDROCK_AI.md#model-selection](PRODUCTION_BEDROCK_AI.md#model-selection) |
| Reduce costs | [PRODUCTION_COST_MANAGEMENT.md](PRODUCTION_COST_MANAGEMENT.md) |
| Improve performance | [PRODUCTION_PERFORMANCE.md](PRODUCTION_PERFORMANCE.md) |

### By Service

| AWS Service | Guide |
|-------------|-------|
| **S3** | [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md) |
| **Textract** | [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md) |
| **Bedrock** | [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md) |
| **Lambda** | [PRODUCTION_ENVIRONMENT_SETUP.md](PRODUCTION_ENVIRONMENT_SETUP.md) |
| **IAM** | [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md) |
| **SNS** | [PRODUCTION_AWS_CONFIG.md#sns-topic-configuration](PRODUCTION_AWS_CONFIG.md#sns-topic-configuration) |

### By Problem Type

| Problem | Guide |
|---------|-------|
| Upload failures | [PRODUCTION_TROUBLESHOOTING.md#upload-issues](PRODUCTION_TROUBLESHOOTING.md#upload-issues) |
| Textract timeouts | [PRODUCTION_TROUBLESHOOTING.md#textract-issues](PRODUCTION_TROUBLESHOOTING.md#textract-issues) |
| Bedrock errors | [PRODUCTION_TROUBLESHOOTING.md#bedrock-issues](PRODUCTION_TROUBLESHOOTING.md#bedrock-issues) |
| Lambda timeouts | [PRODUCTION_TROUBLESHOOTING.md#lambda-issues](PRODUCTION_TROUBLESHOOTING.md#lambda-issues) |
| Cache not working | [PRODUCTION_TROUBLESHOOTING.md#caching-issues](PRODUCTION_TROUBLESHOOTING.md#caching-issues) |
| Invalid JSON output | [PRODUCTION_TROUBLESHOOTING.md#output-issues](PRODUCTION_TROUBLESHOOTING.md#output-issues) |
| High costs | [PRODUCTION_TROUBLESHOOTING.md#cost-issues](PRODUCTION_TROUBLESHOOTING.md#cost-issues) |
| Slow processing | [PRODUCTION_TROUBLESHOOTING.md#performance-issues](PRODUCTION_TROUBLESHOOTING.md#performance-issues) |

---

## ?? System Metrics

### Typical Performance

| Metric | Value | Cached |
|--------|-------|--------|
| Upload time | 1-5 sec | N/A |
| Textract OCR | 2-5 min | < 1 sec |
| Bedrock V1 | 30-60 sec | < 1 sec |
| Bedrock V2 | 30-60 sec | < 1 sec |
| **Total (first run)** | **3-7 min** | - |
| **Total (cached)** | - | **< 5 sec** |

### Typical Costs (per document)

| Service | Cost Range |
|---------|------------|
| S3 storage | $0.001 |
| Textract (3 pages) | $0.045 |
| Bedrock V1 | $0.003-$0.006 |
| Bedrock V2 | $0.003-$0.006 |
| Lambda | $0.001 |
| SNS | $0.001 |
| **Total** | **$0.054-$0.060** |

**Monthly (100 docs/day):**
- **3,000 documents/month: ~$162-$180**
- With caching (50% cached): ~$81-$90

---

## ??? Tools and Utilities

### Command-Line Tools

```bash
# Upload files
cd TextractUploader
dotnet run

# Deploy Lambda
sam build
sam deploy

# Tail logs
aws logs tail /aws/lambda/textract-processor --follow

# Check costs
aws ce get-cost-and-usage \
  --time-period Start=2025-01-01,End=2025-01-31 \
  --granularity MONTHLY \
  --metrics BlendedCost
```

### Visual Studio / VS Code

- **Build:** `Ctrl+Shift+B`
- **Run:** `F5`
- **Debug Lambda:** AWS Toolkit extension

---

## ?? Support

### Getting Help

1. **Documentation:** Check relevant guide first
2. **Troubleshooting:** [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md)
3. **AWS Support:** https://console.aws.amazon.com/support/
4. **Development Team:** [Contact info]

### Reporting Issues

When reporting issues, include:
- [ ] Error messages (full stack trace)
- [ ] CloudWatch logs (request ID)
- [ ] Steps to reproduce
- [ ] Expected vs actual behavior
- [ ] AWS region and service versions
- [ ] Relevant configuration files

---

## ?? Version History

### v2.0 - January 2025 (Current)
- ? Dual-version extraction (V1 + V2)
- ? PromptService with template system
- ? Multi-model support (Nova, Qwen, Claude, Titan)
- ? Comprehensive caching (Textract + Bedrock)
- ? Schema extension analysis
- ? V2 discovery summaries

### v1.0 - December 2024
- Single-version extraction
- Basic schema mapping
- Textract + Bedrock integration

---

## ?? Documentation Maintenance

### Contributing to Documentation

To update documentation:
1. Edit markdown files in `TextractProcessor/` directory
2. Follow existing format and structure
3. Update version history
4. Test all code examples
5. Update index if adding new files

### Documentation Standards

- ? Use clear, concise language
- ? Include code examples
- ? Add diagrams where helpful
- ? Provide troubleshooting steps
- ? Estimate reading time
- ? Cross-reference related guides

---

## ?? Next Steps

Choose your path:

**First-Time Setup:**
? Start with [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md)

**Ready to Process:**
? Go to [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md)

**Having Issues:**
? Check [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md)

**Need to Understand:**
? Read [PRODUCTION_PROCESS_OVERVIEW.md](PRODUCTION_PROCESS_OVERVIEW.md)

---

## ?? Additional Resources

### AWS Documentation
- **Textract:** https://docs.aws.amazon.com/textract/
- **Bedrock:** https://docs.aws.amazon.com/bedrock/
- **Lambda:** https://docs.aws.amazon.com/lambda/
- **S3:** https://docs.aws.amazon.com/s3/

### .NET Resources
- **.NET 8:** https://learn.microsoft.com/en-us/dotnet/
- **AWS SDK for .NET:** https://aws.amazon.com/sdk-for-net/

### Related Documentation
- **DUAL_VERSION_EXTRACTION.md** - Original dual extraction design
- **TROUBLESHOOTING_PROMPTS.md** - Prompt engineering guide
- **QUICK_REFERENCE_DUAL_VERSION.md** - Quick reference card

---

**Documentation Version:** 2.0  
**Last Updated:** January 2025  
**Maintained By:** Development Team
