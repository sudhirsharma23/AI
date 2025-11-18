# Production Process Overview

**Document Processing Pipeline with AWS Textract and Bedrock**

> **Last Updated:** January 2025  
> **Version:** 2.0 (Dual-Version Extraction)  
> **Environment:** AWS Lambda + S3 + Textract + Bedrock

---

## Table of Contents

1. [System Architecture](#system-architecture)
2. [Process Flow](#process-flow)
3. [Key Components](#key-components)
4. [Processing Stages](#processing-stages)
5. [Quick Links to Detailed Guides](#quick-links-to-detailed-guides)

---

## System Architecture

```
???????????????????????????????????????????????????????????????????
?     PRODUCTION PIPELINE       ?
???????????????????????????????????????????????????????????????????
?    ?
?  [1] UPLOAD        ?  [2] TEXTRACT OCR  ?  [3] BEDROCK AI ?
?  Files to S3        Extract Text    Extract Data         ?
?  Date-based folders    Forms, Tables        V1: Schema-based   ?
?           Layout       V2: Dynamic    ?
?   ?
?  [4] CACHING       ?  [5] OUTPUT        ?  [6] VALIDATION    ?
?  Textract Cache        JSON Files          Schema Extensions     ?
?  Prompt Cache   Text Files          Summaries            ?
?            Reports   ?
???????????????????????????????????????????????????????????????????
```

---

## Process Flow

### High-Level Workflow

1. **Upload Documents** ? Files uploaded to S3 in date-based structure
2. **OCR Processing** ? Textract extracts text, forms, and tables
3. **AI Extraction** ? Bedrock extracts structured data (V1 + V2)
4. **Result Storage** ? JSON and reports saved to output directory
5. **Validation** ? Schema extensions and summaries generated

### Data Flow Diagram

```
????????????????
?   S3 Bucket  ?
? uploads/date ?
????????????????
    ?
       ?
????????????????      Cache Hit?     ????????????????
?   Textract   ? ??????????????????? ? Cache Layer  ?
?   Service    ?      Cache Miss     ? (Disk-based) ?
????????????????????????????????
       ?
       ?
????????????????
? Textract OCR ?
?   Results    ?
? - Raw Text   ?
? - Forms   ?
? - Tables     ?
????????????????
    ?
       ???????????????????????????????
     ?        ?             ?
????????????    ????????????  ????????????
? V1: With ?    ? V2: Pure ?  ?  Prompt  ?
? Schema + ?    ? Dynamic  ?  ?  Cache   ?
? Rules    ?    ? No Schema?  ? (Memory) ?
????????????    ????????????  ????????????
     ?               ?
     ?    ?
????????????    ????????????
?  Bedrock ?    ?  Bedrock ?
?  Nova/   ?    ?  Nova/   ?
?  Qwen    ?    ?  Qwen    ?
????????????    ????????????
     ?      ?
     ?   ?
????????????    ????????????
? Schema   ?    ? Dynamic  ?
? Based    ?    ? Full     ?
? JSON     ?    ? JSON     ?
????????????    ????????????
     ?      ?
     ?????????????????
             ?
    ???????????????????
    ? Output Files    ?
    ? + Extensions    ?
    ? + Summaries     ?
    ???????????????????
```

---

## Key Components

### 1. **TextractUploader**
- **Purpose:** Upload files to S3 in structured date-based folders
- **Input:** TIF/PDF files from `UploadFiles/` directory
- **Output:** Files in S3 at `uploads/{date}/{filename}/`
- **Guide:** [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md)

### 2. **TextractOcrService**
- **Purpose:** Extract text and structure from documents using AWS Textract
- **Features:** Forms, tables, layout detection, caching
- **Output:** TextractResponse with raw text, form fields, table data
- **Guide:** [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md)

### 3. **BedrockService**
- **Purpose:** AI-powered data extraction using AWS Bedrock LLMs
- **Models:** Nova Lite, Qwen 3, Claude 3, Titan
- **Features:** Prompt caching, model abstraction, region routing
- **Guide:** [PRODUCTION_BEDROCK_AI.md](PRODUCTION_BEDROCK_AI.md)

### 4. **SchemaMapperService**
- **Purpose:** Dual-version extraction (schema-based + dynamic)
- **V1:** Uses predefined schema, rules, and examples
- **V2:** Pure AI-driven dynamic extraction without constraints
- **Guide:** [PRODUCTION_SCHEMA_MAPPER.md](PRODUCTION_SCHEMA_MAPPER.md)

### 5. **PromptService**
- **Purpose:** Manage prompt templates, rules, and examples
- **Features:** Template versioning, caching, composition
- **Guide:** [PRODUCTION_PROMPT_MANAGEMENT.md](PRODUCTION_PROMPT_MANAGEMENT.md)

### 6. **Caching Layer**
- **Textract Cache:** Disk-based caching of OCR results
- **Prompt Cache:** Memory-based caching of Bedrock responses
- **Guide:** [PRODUCTION_CACHING_STRATEGY.md](PRODUCTION_CACHING_STRATEGY.md)

---

## Processing Stages

### Stage 1: File Upload
**Responsibility:** Get documents into S3 with proper structure

```
Local File ? TextractUploader ? S3 Bucket
  file.tif uploads/2025-01-20/file/file.tif
```

**Key Points:**
- Date-based folder structure: `uploads/{YYYY-MM-DD}/{filename}/`
- Supports TIF, PDF, PNG, JPG
- Maintains filename in path for organization

---

### Stage 2: OCR Extraction
**Responsibility:** Extract all text and structure from documents

```
S3 File ? Textract Service ? TextractResponse
                 - RawText
                 - FormFields
  - TableData
    - Pages
```

**Key Points:**
- Async job processing (SNS notifications)
- Extracts: TEXT, FORMS, TABLES, LAYOUT
- Caches results to avoid re-processing
- Wait/retry mechanism for job completion

---

### Stage 3: AI Data Extraction (Dual Version)
**Responsibility:** Convert OCR results into structured data

#### Version 1: Schema-Based
```
TextractResponse ? PromptService ? Bedrock ? Structured JSON
    + Schema      (Follows schema)
              + Rules
          + Examples
```

#### Version 2: Dynamic
```
TextractResponse ? PromptService ? Bedrock ? Dynamic JSON
          (No schema)     (AI-discovered structure)
```

**Key Points:**
- Both versions run simultaneously
- V1 ensures consistency with schema
- V2 discovers additional fields
- Compare outputs to improve schema

---

### Stage 4: Result Storage
**Responsibility:** Save all outputs and analysis reports

**Output Files:**
```
CachedFiles_OutputFiles/
??? {filename}_textract_combined_{timestamp}.txt
??? {filename}_v1_schema_{timestamp}.json
??? {filename}_v2_dynamic_{timestamp}.json
??? {filename}_schema_extensions_{timestamp}.md
??? {filename}_v2_extraction_summary_{timestamp}.md
```

---

### Stage 5: Analysis & Validation
**Responsibility:** Generate reports comparing V1 and V2

**Reports Generated:**
1. **Schema Extensions Report (V1)**
   - Fields extracted that don't exist in base schema
   - Recommendations for schema updates

2. **V2 Extraction Summary**
   - Total fields discovered
   - Section breakdown
   - Key metrics extracted

---

## Quick Links to Detailed Guides

### Process Guides
- **[Upload Process](PRODUCTION_UPLOAD_PROCESS.md)** - How to upload files to S3
- **[Textract OCR](PRODUCTION_TEXTRACT_OCR.md)** - OCR extraction details
- **[Bedrock AI](PRODUCTION_BEDROCK_AI.md)** - AI model usage and configuration
- **[Schema Mapping](PRODUCTION_SCHEMA_MAPPER.md)** - Dual-version extraction
- **[Prompt Management](PRODUCTION_PROMPT_MANAGEMENT.md)** - Template system
- **[Caching Strategy](PRODUCTION_CACHING_STRATEGY.md)** - Performance optimization

### Configuration Guides
- **[AWS Configuration](PRODUCTION_AWS_CONFIG.md)** - IAM roles, SNS, S3 setup
- **[Model Selection](PRODUCTION_MODEL_SELECTION.md)** - Choosing the right LLM
- **[Environment Setup](PRODUCTION_ENVIRONMENT_SETUP.md)** - Lambda, dependencies

### Troubleshooting
- **[Common Issues](PRODUCTION_TROUBLESHOOTING.md)** - Error resolution
- **[Performance Tuning](PRODUCTION_PERFORMANCE.md)** - Optimization tips
- **[Cost Management](PRODUCTION_COST_MANAGEMENT.md)** - AWS cost control

### Reference
- **[API Reference](PRODUCTION_API_REFERENCE.md)** - Method signatures
- **[Data Models](PRODUCTION_DATA_MODELS.md)** - Object structures
- **[Output Formats](PRODUCTION_OUTPUT_FORMATS.md)** - File specifications

---

## Process Metrics

### Typical Processing Times
| Stage | Time | Cached |
|-------|------|--------|
| S3 Upload | 1-5 sec | N/A |
| Textract OCR | 2-5 min | < 1 sec |
| Bedrock V1 | 30-60 sec | < 1 sec |
| Bedrock V2 | 30-60 sec | < 1 sec |
| **Total** | **3-7 min** | **< 5 sec** |

### Cost Breakdown (per document)
| Service | Cost |
|---------|------|
| S3 Storage | $0.001 |
| Textract | $0.015 - $0.065 |
| Bedrock V1 | $0.005 - $0.015 |
| Bedrock V2 | $0.005 - $0.015 |
| **Total** | **$0.026 - $0.096** |

---

## Success Criteria

? **Document Successfully Processed When:**
1. File uploaded to S3 with correct structure
2. Textract completes with SUCCEEDED status
3. Both V1 and V2 extractions complete successfully
4. All output files generated
5. Schema extensions and summaries created
6. Results cached for future runs

---

## Next Steps

1. **New to the system?** Start with [PRODUCTION_UPLOAD_PROCESS.md](PRODUCTION_UPLOAD_PROCESS.md)
2. **Need to configure AWS?** See [PRODUCTION_AWS_CONFIG.md](PRODUCTION_AWS_CONFIG.md)
3. **Want to optimize?** Check [PRODUCTION_PERFORMANCE.md](PRODUCTION_PERFORMANCE.md)
4. **Having issues?** Visit [PRODUCTION_TROUBLESHOOTING.md](PRODUCTION_TROUBLESHOOTING.md)

---

## Support & Maintenance

**Contact:** Development Team  
**Documentation Version:** 2.0  
**Last Review:** January 2025
