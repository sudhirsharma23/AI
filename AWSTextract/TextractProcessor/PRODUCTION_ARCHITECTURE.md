# Production AWS Architecture for TextractProcessor

**Event-Driven Document Processing Pipeline**

> **Architecture Type:** Event-Driven, Serverless  
> **Deployment Model:** AWS Lambda + S3 Event Triggers  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Event-Driven Processing Flow](#event-driven-processing-flow)
3. [AWS Services Architecture](#aws-services-architecture)
4. [Central Upload Location](#central-upload-location)
5. [Automatic Processing Triggers](#automatic-processing-triggers)
6. [Output Storage Strategy](#output-storage-strategy)
7. [Scalability & High Availability](#scalability--high-availability)
8. [Security Architecture](#security-architecture)
9. [Monitoring & Observability](#monitoring--observability)
10. [Cost Optimization](#cost-optimization)

---

## Architecture Overview

### High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│   PRODUCTION AWS ARCHITECTURE       │
├─────────────────────────────────────────────────────────────────────┤
│         │
│  ┌──────────────┐            │
│  │   External   │  Upload .tif files           │
│  │   Systems    │ ─────────────┐      │
│  └──────────────┘        │              │
│          ▼         │
│  ┌─────────────────────────────────────────────┐       │
│  │    CENTRAL S3 BUCKET  │       │
│  │  s3://textract-prod-central/incoming/      │    │
│  │  ├─ file1.tif (uploaded)  │          │
│  │  ├─ file2.tif (uploaded)    │             │
│  │  └─ file3.tif (uploaded)        │       │
│  └──────────────────┬──────────────────────────┘     │
│                   │ S3 Event (ObjectCreated)   │
│           │ AUTOMATIC TRIGGER             │
│      ▼           │
│  ┌─────────────────────────────────────────────┐        │
│  │      EventBridge / S3 Event Bridge          │       │
│  │  Routes events to processing Lambda         │      │
│  └──────────────────┬──────────────────────────┘        │
│  │       │
│               ▼        │
│  ┌─────────────────────────────────────────────┐           │
│  │    Lambda: Orchestrator Function            │        │
│  │  textract-processor-orchestrator       │            │
│  │  • Validates file         │       │
│  │  • Checks duplicates (DynamoDB)│         │
│  │  • Initiates Textract job              │       │
│  │  • Writes metadata to DynamoDB         │   │
│  └──────────────────┬──────────────────────────┘     │
│      │       │
│         ▼    │
│  ┌─────────────────────────────────────────────┐     │
│  │         AWS Textract Service      │   │
│  │  • Async document analysis  │            │
│  │  • OCR extraction (TABLES, FORMS, LAYOUT)   │                   │
│  │  • Job status tracking     │        │
│  └──────────────────┬──────────────────────────┘ │
│        │   │
│            ▼         │
│  ┌─────────────────────────────────────────────┐     │
│  │    SNS Topic (FIFO)       │         │
│  │  sns-textract-completion.fifo       │         │
│  │  • Textract job completion notification   │          │
│  └──────────────────┬──────────────────────────┘    │
│       │       │
│        ▼    │
│  ┌─────────────────────────────────────────────┐        │
│  │    Lambda: Bedrock Processor Function       │           │
│  │  textract-processor-bedrock     │        │
│  │  • Retrieves Textract results        │      │
│  │  • Invokes Bedrock AI (V1 + V2)   │  │
│  │  • Generates structured JSON   │       │
│  │  • Saves to S3 output bucket   │       │
│  │  • Updates DynamoDB status           │    │
│  └──────────────────┬──────────────────────────┘   │
││          │
│   ▼            │
│  ┌─────────────────────────────────────────────┐       │
││       S3 Output Bucket    │       │
│  │  s3://textract-prod-central/processed/      │        │
│  │  ├─ file1_v1_schema.json  │          │
│  │  ├─ file1_v2_dynamic.json     │          │
│  │  ├─ file1_analysis.md             │     │
│  │  └─ file1_textract_raw.json (cached)        │   │
│  └─────────────────────────────────────────────┘         │
│                   │        │
│     ▼           │
│  ┌─────────────────────────────────────────────┐          │
│  │  DynamoDB Table (Future)          │       │
│  │  textract-processing-results                │ │
│  │  • Stores JSON output for querying       │    │
│  │  • Partitioned by date      │     │
│  │  • Global Secondary Indexes            │   │
│  └─────────────────────────────────────────────┘     │
│          │
│  ┌─────────────────────────────────────────────┐        │
│  │       CloudWatch & X-Ray        │          │
│  │  • Logs aggregation    │   │
│  │  • Metrics & Alarms            │ │
│  │  • Distributed tracing         │        │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Event-Driven Processing Flow

### Complete Event Flow (Step-by-Step)

```
┌──────────────────────────────────────────────────────────────────┐
│ STEP 1: FILE UPLOAD (Event Source)          │
├──────────────────────────────────────────────────────────────────┤
│ Upload Method: S3 Upload (API, Console, CLI, SDK)     │
│ Destination: s3://textract-prod-central/incoming/   │
│           │
│ Example:      │
│   aws s3 cp document.tif \   │
│     s3://textract-prod-central/incoming/document.tif             │
│       │
│ S3 automatically generates event:               │
│   Event Type: s3:ObjectCreated:Put       │
│   Bucket: textract-prod-central             │
│   Key: incoming/document.tif       │
│   Size: 2.4 MB       │
│   Timestamp: 2025-01-20T10:30:45Z     │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 2: EVENT ROUTING (EventBridge)       │
├──────────────────────────────────────────────────────────────────┤
│ S3 Event → EventBridge → Lambda Trigger   │
│            │
│ Event Filter Rules:      │
│   - Prefix: incoming/             │
│ - Suffix: .tif, .tiff, .pdf          │
│   - Max size: 512 MB       │
│         │
│ Routing:         │
│   ✅ .tif files → textract-processor-orchestrator   │
│   ✅ .pdf files → textract-processor-orchestrator     │
│   ❌ Other files → Ignored (logged)  │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 3: ORCHESTRATOR LAMBDA (Entry Point)      │
├──────────────────────────────────────────────────────────────────┤
│ Function: textract-processor-orchestrator    │
│ Trigger: S3 ObjectCreated event             │
│ Timeout: 2 minutes   │
│ Memory: 256 MB   │
│   │
│ Processing Steps:   │
│   1. Parse S3 event payload      │
│   2. Validate file existence and format        │
│   3. Check DynamoDB for duplicate processing │
│   4. Generate unique processing ID     │
│   5. Start Textract async job                │
│   6. Write initial status to DynamoDB            │
│   7. Return success/failure      │
│          │
│ DynamoDB Record:    │
│   {          │
│     "processingId": "proc-abc123",      │
│     "s3Key": "incoming/document.tif",               │
│     "status": "TEXTRACT_STARTED",      │
│     "textractJobId": "job-xyz789",      │
│     "uploadedAt": "2025-01-20T10:30:45Z",│
│     "ttl": 1737379845  // 30 days expiry│
│   }         │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 4: TEXTRACT PROCESSING (Async)            │
├──────────────────────────────────────────────────────────────────┤
│ Service: AWS Textract       │
│ Job Type: StartDocumentAnalysis     │
│ Features: TABLES, FORMS, LAYOUT  │
│        │
│ Processing Time: 30 seconds - 5 minutes       │
│   - 1 page: ~30-60 seconds             │
│   - 3 pages: ~2-3 minutes      │
│   - 10 pages: ~4-6 minutes          │
│  │
│ Status Polling: Via SNS notification (no polling needed)         │
│         │
│ On Completion:             │
│   Textract publishes to SNS:     │
│     Topic: sns-textract-completion.fifo│
│     Message: {   │
│ "JobId": "job-xyz789",             │
│   "Status": "SUCCEEDED",     │
│       "Timestamp": "2025-01-20T10:33:12Z"            │
│     }       │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 5: SNS NOTIFICATION (Event Trigger)       │
├──────────────────────────────────────────────────────────────────┤
│ Topic: sns-textract-completion.fifo    │
│ Subscriber: textract-processor-bedrock Lambda                  │
│           │
│ Notification triggers Bedrock processor automatically  │
│ No manual intervention required   │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 6: BEDROCK PROCESSOR LAMBDA (AI Extraction)              │
├──────────────────────────────────────────────────────────────────┤
│ Function: textract-processor-bedrock     │
│ Trigger: SNS message  │
│ Timeout: 15 minutes    │
│ Memory: 1024 MB         │
│    │
│ Processing Steps:   │
│   1. Parse SNS message (get JobId)         │
│   2. Query DynamoDB for processing record  │
│   3. Retrieve Textract results (paginated)  │
│   4. Run V1 extraction (schema-based) │
│ 5. Run V2 extraction (dynamic)      │
│   6. Generate analysis reports      │
│   7. Save outputs to S3        │
│   8. Update DynamoDB status to COMPLETED   │
│   9. Send completion notification (optional)│
│      │
│ Output Files:     │
│   s3://textract-prod-central/processed/2025-01-20/    │
│     ├─ document_v1_schema_20250120_103345.json     │
│     ├─ document_v2_dynamic_20250120_103345.json                  │
│     ├─ document_textract_raw_20250120_103345.json     │
│     ├─ document_schema_extensions_20250120_103345.md │
│     └─ document_v2_summary_20250120_103345.md       │
└──────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────┐
│ STEP 7: OUTPUT STORAGE & DATABASE (Future)            │
├──────────────────────────────────────────────────────────────────┤
│ Current: JSON files stored in S3           │
│ Future: DynamoDB table for queryable results         │
│         │
│ DynamoDB Table: textract-processing-results        │
│   Partition Key: processingId (String)   │
│   Sort Key: timestamp (Number)     │
│       │
│ Indexes:    │
│   GSI-1: s3Key (for duplicate detection)        │
│ GSI-2: uploadDate (for date-range queries)     │
│   GSI-3: status (for monitoring)      │
│     │
│ Sample Record:     │
│   {             │
│     "processingId": "proc-abc123",       │
│     "timestamp": 1705751625,         │
│     "s3Key": "incoming/document.tif",      │
│ "status": "COMPLETED",              │
│     "textractJobId": "job-xyz789", │
│     "v1OutputS3": "processed/.../v1_schema.json",          │
│     "v2OutputS3": "processed/.../v2_dynamic.json",               │
│     "uploadedAt": "2025-01-20T10:30:45Z",           │
│     "completedAt": "2025-01-20T10:33:45Z",                 │
│   "processingTimeMs": 180000,    │
│     "extractedData": { /* JSON output */ },   │
│     "ttl": 1737379845 │
│   }          │
└──────────────────────────────────────────────────────────────────┘
```

---

## AWS Services Architecture

### Service Selection & Justification

| Service | Purpose | Why This Service? | Alternatives Considered |
|---------|---------|-------------------|------------------------|
| **S3** | Central file storage | • Unlimited scalability<br>• 99.999999999% durability<br>• Event notifications<br>• Lifecycle policies | EFS (overkill), EBS (not scalable) |
| **Lambda** | Serverless compute | • No server management<br>• Auto-scaling<br>• Pay-per-use<br>• Native AWS integrations | ECS Fargate (more complex), EC2 (operational overhead) |
| **EventBridge** | Event routing | • Flexible rule-based routing<br>• Multiple targets<br>• Built-in retry logic | SNS direct (less flexible), SQS (requires polling) |
| **SNS (FIFO)** | Job notifications | • Guaranteed ordering<br>• Exactly-once delivery<br>• Textract native support | SQS (requires polling), EventBridge (less suitable for Textract) |
| **Textract** | OCR processing | • Managed service<br>• High accuracy<br>• Native AWS integration | Aspose (licensing costs), Tesseract (lower accuracy) |
| **Bedrock** | AI extraction | • Multiple models available<br>• Pay-per-token<br>• No infrastructure | SageMaker (complex), External APIs (data privacy) |
| **DynamoDB** | State tracking | • Serverless<br>• Single-digit ms latency<br>• Auto-scaling<br>• TTL support | RDS (operational overhead), Aurora Serverless (more expensive) |
| **CloudWatch** | Monitoring | • Native integration<br>• Logs + Metrics + Alarms<br>• Free tier generous | Datadog (additional cost), ELK (operational overhead) |
| **X-Ray** | Distributed tracing | • Visualize service interactions<br>• Identify bottlenecks<br>• Native Lambda support | Jaeger (self-hosted), New Relic (additional cost) |

---

## Central Upload Location

### S3 Bucket Structure

```
s3://textract-prod-central/
├── incoming/         # Upload destination (event source)
│   ├── document1.tif
│   ├── document2.pdf
│   └── document3.tif
│
├── processed/   # Processing outputs
│   ├── 2025-01-20/            # Date partitioning
│   │   ├── document1_v1_schema_timestamp.json
│   │   ├── document1_v2_dynamic_timestamp.json
││   ├── document1_textract_raw_timestamp.json
│   │   ├── document1_schema_extensions_timestamp.md
│   │   └── document1_v2_summary_timestamp.md
│   └── 2025-01-21/
│       └── ...
│
├── failed/        # Failed processing (for investigation)
│   ├── 2025-01-20/
│   │   ├── failed_document.tif
│   │   └── error_logs.txt
│   └── ...
│
└── archive/      # Lifecycle-managed archives
  └── ...
```

### Bucket Configuration

**Lifecycle Policy:**
```json
{
  "Rules": [
    {
   "Id": "ArchiveProcessedFiles",
      "Status": "Enabled",
"Prefix": "processed/",
      "Transitions": [
        {
          "Days": 30,
 "StorageClass": "STANDARD_IA"
      },
        {
          "Days": 90,
   "StorageClass": "GLACIER"
 }
  ]
    },
    {
      "Id": "DeleteIncomingFiles",
      "Status": "Enabled",
      "Prefix": "incoming/",
  "Expiration": {
        "Days": 7
    }
    },
    {
      "Id": "DeleteFailedFiles",
      "Status": "Enabled",
      "Prefix": "failed/",
      "Expiration": {
        "Days": 30
      }
    }
]
}
```

**Versioning:**
```
Enabled: true
Purpose: Recover from accidental deletions or overwrites
```

**Encryption:**
```
Type: SSE-S3 (AWS managed keys)
Alternative: SSE-KMS (for more control)
```

**Access Logging:**
```
Target Bucket: s3://textract-prod-logs/s3-access/
Purpose: Audit trail for compliance
```

---

## Automatic Processing Triggers

### S3 Event Notification Configuration

**Method 1: S3 Event Notifications (Direct to Lambda)**

```json
{
  "LambdaFunctionConfigurations": [
    {
      "Id": "TextractProcessorTrigger",
      "LambdaFunctionArn": "arn:aws:lambda:us-east-1:123456789012:function:textract-processor-orchestrator",
      "Events": ["s3:ObjectCreated:*"],
      "Filter": {
        "Key": {
     "FilterRules": [
  {
              "Name": "prefix",
      "Value": "incoming/"
         },
      {
      "Name": "suffix",
            "Value": ".tif"
            }
          ]
        }
      }
    }
  ]
}
```

**Method 2: EventBridge (Recommended - More Flexible)**

```json
{
  "Source": ["aws.s3"],
  "DetailType": ["Object Created"],
  "Detail": {
    "bucket": {
 "name": ["textract-prod-central"]
    },
    "object": {
      "key": [{
        "prefix": "incoming/"
      }]
 }
  }
}
```

**EventBridge Rule:**
```json
{
"Name": "textract-file-upload-rule",
  "State": "ENABLED",
  "EventPattern": {
    "source": ["aws.s3"],
    "detail-type": ["Object Created"],
    "detail": {
      "bucket": {
        "name": ["textract-prod-central"]
      },
    "object": {
        "key": [{
          "suffix": ".tif"
        }, {
          "suffix": ".pdf"
    }]
      }
    }
  },
  "Targets": [
    {
      "Arn": "arn:aws:lambda:us-east-1:123456789012:function:textract-processor-orchestrator",
   "RetryPolicy": {
 "MaximumRetryAttempts": 3,
        "MaximumEventAge": 3600
   },
      "DeadLetterConfig": {
        "Arn": "arn:aws:sqs:us-east-1:123456789012:textract-dlq"
      }
    }
  ]
}
```

### Trigger Characteristics

| Aspect | Configuration | Notes |
|--------|--------------|-------|
| **Latency** | < 1 second | From upload to Lambda invocation |
| **Concurrency** | Up to 1000 | Lambda concurrent executions (default quota) |
| **Batch Size** | 1 file per invocation | Each upload triggers separate Lambda |
| **Retry Logic** | 3 retries | EventBridge automatic retry |
| **Dead Letter Queue** | SQS DLQ | Failed events for investigation |
| **Filtering** | Prefix + Suffix | Only .tif/.pdf in incoming/ folder |

---

## Output Storage Strategy

### Current: S3 JSON Storage

**Advantages:**
- ✅ Simple implementation
- ✅ No database management
- ✅ Easy to download and review
- ✅ Version control with S3 versioning
- ✅ Lifecycle management for cost optimization

**Disadvantages:**
- ❌ No querying capability
- ❌ Must download entire file to read
- ❌ No indexing
- ❌ Difficult to aggregate data

### Future: DynamoDB Integration

**Phase 1 (Current): S3 Only**
```
Upload → Process → Save JSON to S3
```

**Phase 2 (Future): S3 + DynamoDB**
```
Upload → Process → Save JSON to S3 + Store in DynamoDB
```

**DynamoDB Schema Design:**

```typescript
// Main Table: textract-processing-results
{
  processingId: string,        // Partition Key
  timestamp: number,   // Sort Key (epoch ms)
  s3Key: string,              // Original file location
  status: string,             // PENDING, PROCESSING, COMPLETED, FAILED
  textractJobId: string,      // Textract job ID
  
  // Output locations
  v1OutputS3: string,         // S3 path to V1 JSON
  v2OutputS3: string,         // S3 path to V2 JSON
  
  // Timing
  uploadedAt: string,    // ISO 8601
  startedAt: string,
  completedAt: string,
  processingTimeMs: number,
  
  // Extracted data (limited fields for querying)
  buyerNames: string[],       // Extracted buyer names
  propertyAddress: string,    // Property address
  salePrice: number,          // Transaction amount
  documentType: string,    // deed, contract, etc.
  
  // Full JSON (optional, for small documents)
  extractedData: object,      // Full V1 output (if < 400KB)
  
// Metadata
  fileSizeBytes: number,
  pageCount: number,
  textractCost: number,
  bedrockCost: number,
  
  // TTL for auto-cleanup
  ttl: number       // Unix timestamp (30 days)
}
```

**Global Secondary Indexes:**

```typescript
// GSI-1: Query by S3 key (duplicate detection)
{
  partitionKey: "s3Key",
  sortKey: "timestamp",
  projectionType: "ALL"
}

// GSI-2: Query by upload date range
{
  partitionKey: "uploadDate",  // Derived attribute: YYYY-MM-DD
  sortKey: "timestamp",
  projectionType: "ALL"
}

// GSI-3: Query by status
{
  partitionKey: "status",
  sortKey: "timestamp",
  projectionType: "ALL"
}

// GSI-4: Query by document type
{
  partitionKey: "documentType",
  sortKey: "timestamp",
  projectionType: "KEYS_ONLY"
}
```

**Query Examples:**

```typescript
// Find all processing for a specific file
const params = {
  TableName: 'textract-processing-results',
  IndexName: 'GSI-1',
  KeyConditionExpression: 's3Key = :key',
  ExpressionAttributeValues: {
    ':key': 'incoming/document.tif'
  }
};

// Find all documents processed on a specific date
const params = {
  TableName: 'textract-processing-results',
  IndexName: 'GSI-2',
  KeyConditionExpression: 'uploadDate = :date',
  ExpressionAttributeValues: {
    ':date': '2025-01-20'
  }
};

// Find all failed processing
const params = {
  TableName: 'textract-processing-results',
  IndexName: 'GSI-3',
  KeyConditionExpression: '#status = :status',
  ExpressionAttributeNames: {
    '#status': 'status'
  },
  ExpressionAttributeValues: {
    ':status': 'FAILED'
  }
};
```

---

## Scalability & High Availability

### Scalability Characteristics

| Component | Scaling Type | Limits | Scaling Trigger |
|-----------|-------------|--------|-----------------|
| **S3** | Automatic, unlimited | No practical limit | N/A |
| **Lambda Orchestrator** | Automatic | 1000 concurrent (default) | Per invocation |
| **Lambda Bedrock** | Automatic | 1000 concurrent (default) | Per invocation |
| **Textract** | Automatic | 100 concurrent jobs (default) | Per job submission |
| **Bedrock** | Automatic | Model-specific (high) | Per API call |
| **DynamoDB** | On-demand or provisioned | Unlimited (on-demand) | Automatic |
| **SNS** | Automatic | Unlimited | N/A |
| **EventBridge** | Automatic | Unlimited | N/A |

### Handling High Volume

**Scenario: 1000 files uploaded simultaneously**

```
1. S3 Events Generated: 1000 events (< 1 second)
   ↓
2. EventBridge Routes: 1000 events to Lambda
   ↓
3. Lambda Orchestrator: 
   - 1000 concurrent executions (if quota allows)
   - OR queued if over limit (automatic)
   ↓
4. Textract Jobs: 
   - First 100 start immediately
   - Remaining 900 queued (automatic)
   ↓
5. SNS Notifications: 
   - As jobs complete, notifications sent
   ↓
6. Lambda Bedrock:
   - Processes as notifications arrive
   - Scales to demand
```

**Cost for 1000 files (3 pages each):**
- Textract: 3000 pages × $0.045 = $135
- Bedrock: 1000 docs × $0.014 = $14
- Lambda: 2000 invocations × $0.0000002 = $0.40
- S3: Negligible (first 100K requests free)
- **Total: ~$149.40**

### High Availability

**Multi-AZ Deployment:**
- S3: Automatically replicated across 3+ AZs
- Lambda: Automatically deployed across AZs
- DynamoDB: Automatically replicated across 3 AZs
- Textract: AWS-managed multi-AZ
- Bedrock: AWS-managed multi-AZ

**Fault Tolerance:**
- Lambda retry: 3 attempts (EventBridge)
- Dead Letter Queue: Failed events captured for manual review
- S3 versioning: Recover from accidental deletions
- DynamoDB backups: Point-in-time recovery enabled

**Regional Failover (Future Enhancement):**
```
Primary Region: us-east-1
Failover Region: us-west-2

S3 Cross-Region Replication:
  s3://textract-prod-central (us-east-1)
    → s3://textract-prod-central-replica (us-west-2)

Route 53 Health Checks:
  Monitor Lambda endpoints
  Automatic DNS failover to us-west-2 if us-east-1 unhealthy
```

---

## Security Architecture

### Security Layers

```
┌────────────────────────────────────────────────────────────┐
│          SECURITY ARCHITECTURE       │
├────────────────────────────────────────────────────────────┤
│                │
│  Layer 1: Network Security     │
│  ┌──────────────────────────────────────────────┐       │
│  │ VPC Endpoints (Private S3/DynamoDB access)   │         │
││ No internet gateway required       │         │
│  │ Lambda in VPC (optional, for database access)│         │
│  └──────────────────────────────────────────────┘         │
│       │
│  Layer 2: Identity & Access Management                │
│  ┌──────────────────────────────────────────────┐   │
│  │ Least Privilege IAM Roles     │         │
│  │ • Lambda Execution Role (specific resources) │  │
│  │ • Textract Service Role (S3 + SNS only)      │         │
│  │ Resource-based policies (bucket/topic)       │         │
│  └──────────────────────────────────────────────┘      │
│   │
│  Layer 3: Encryption       │
│  ┌──────────────────────────────────────────────┐ │
││ At Rest:         │         │
│  │ • S3: SSE-S3 (AES-256)    │         │
│  │ • DynamoDB: AWS managed encryption      │   │
│  │ • Lambda environment variables: KMS     │         │
│  ││         │
│  │ In Transit:   │         │
│  │ • All API calls over HTTPS/TLS 1.2+       │     │
│  │ • AWS internal network encryption        │    │
│  └──────────────────────────────────────────────┘         │
│         │
│  Layer 4: Monitoring & Auditing    │
│  ┌──────────────────────────────────────────────┐         │
│  │ • CloudTrail: All API calls logged           │         │
│  │ • CloudWatch Logs: Application logs          │       │
│  │ • GuardDuty: Threat detection  │    │
│  │ • Config: Resource compliance monitoring  │      │
│  └──────────────────────────────────────────────┘         │
│    │
│  Layer 5: Application Security          │
│  ┌──────────────────────────────────────────────┐         │
│  │ • Input validation (file type, size)         │         │
│  │ • Virus scanning (optional: ClamAV Lambda)   │         │
│  │ • Rate limiting (Lambda reserved concurrency)│         │
│  │ • Secrets Manager for sensitive config       │         │
│  └──────────────────────────────────────────────┘         │
└────────────────────────────────────────────────────────────┘
```

### IAM Policies (Production-Ready)

**Lambda Orchestrator Role:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "S3ReadIncoming",
      "Effect": "Allow",
      "Action": [
        "s3:GetObject",
      "s3:GetObjectVersion"
   ],
      "Resource": "arn:aws:s3:::textract-prod-central/incoming/*"
    },
    {
      "Sid": "TextractStartJob",
      "Effect": "Allow",
   "Action": "textract:StartDocumentAnalysis",
      "Resource": "*"
    },
    {
      "Sid": "DynamoDBWrite",
      "Effect": "Allow",
      "Action": [
        "dynamodb:PutItem",
    "dynamodb:UpdateItem",
        "dynamodb:Query"
      ],
      "Resource": "arn:aws:dynamodb:us-east-1:*:table/textract-processing-tracking"
    },
    {
      "Sid": "CloudWatchLogs",
    "Effect": "Allow",
   "Action": [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
     "logs:PutLogEvents"
      ],
      "Resource": "arn:aws:logs:us-east-1:*:log-group:/aws/lambda/textract-processor-orchestrator:*"
    },
    {
 "Sid": "XRayTracing",
    "Effect": "Allow",
      "Action": [
  "xray:PutTraceSegments",
        "xray:PutTelemetryRecords"
      ],
      "Resource": "*"
    }
  ]
}
```

**Lambda Bedrock Processor Role:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "S3ReadWrite",
      "Effect": "Allow",
      "Action": [
    "s3:GetObject",
        "s3:PutObject"
      ],
      "Resource": [
        "arn:aws:s3:::textract-prod-central/incoming/*",
        "arn:aws:s3:::textract-prod-central/processed/*"
      ]
    },
    {
      "Sid": "TextractGetResults",
      "Effect": "Allow",
    "Action": "textract:GetDocumentAnalysis",
      "Resource": "*"
    },
    {
      "Sid": "BedrockInvoke",
      "Effect": "Allow",
      "Action": "bedrock:InvokeModel",
      "Resource": [
        "arn:aws:bedrock:*::foundation-model/amazon.nova-lite-v1:0",
"arn:aws:bedrock:*::foundation-model/qwen.qwen2.5-7b-instruct"
      ]
},
    {
   "Sid": "DynamoDBReadWrite",
      "Effect": "Allow",
 "Action": [
    "dynamodb:GetItem",
   "dynamodb:PutItem",
        "dynamodb:UpdateItem",
    "dynamodb:Query"
      ],
      "Resource": [
     "arn:aws:dynamodb:us-east-1:*:table/textract-processing-tracking",
        "arn:aws:dynamodb:us-east-1:*:table/textract-processing-results"
      ]
    },
    {
      "Sid": "CloudWatchLogs",
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
   "logs:PutLogEvents"
      ],
    "Resource": "arn:aws:logs:us-east-1:*:log-group:/aws/lambda/textract-processor-bedrock:*"
    }
  ]
}
```

---

## Monitoring & Observability

### CloudWatch Dashboard

**Key Metrics to Monitor:**

| Metric | Source | Threshold | Alarm Action |
|--------|--------|-----------|--------------|
| **S3 Upload Rate** | CloudWatch S3 Metrics | > 1000/min | Scale Lambda concurrency |
| **Lambda Errors** | CloudWatch Lambda | > 1% | SNS notification to ops team |
| **Lambda Duration** | CloudWatch Lambda | > 800 seconds | Investigate performance |
| **Lambda Throttles** | CloudWatch Lambda | > 0 | Increase concurrency limit |
| **Textract Job Failures** | Custom Metric | > 5% | SNS notification |
| **DynamoDB Throttles** | CloudWatch DynamoDB | > 0 | Increase capacity |
| **DLQ Message Count** | CloudWatch SQS | > 0 | Investigate failed events |
| **Cost Anomaly** | AWS Cost Anomaly Detection | > $200/day | Budget alert |

### CloudWatch Alarms

```json
{
  "AlarmName": "textract-lambda-errors-high",
  "MetricName": "Errors",
  "Namespace": "AWS/Lambda",
  "Statistic": "Sum",
  "Period": 300,
  "EvaluationPeriods": 2,
  "Threshold": 10,
  "ComparisonOperator": "GreaterThanThreshold",
  "AlarmActions": [
    "arn:aws:sns:us-east-1:123456789012:ops-alerts"
  ],
  "Dimensions": [
    {
      "Name": "FunctionName",
      "Value": "textract-processor-orchestrator"
    }
  ]
}
```

### X-Ray Tracing

**Service Map Visualization:**
```
S3 → EventBridge → Lambda Orchestrator → Textract
       ↓
         DynamoDB
            ↓
       SNS ← Textract Complete
                ↓
           Lambda Bedrock → Textract (GetResults)
   ↓
       Bedrock
    ↓
    S3 (Save Outputs)
     ↓
       DynamoDB
```

**Trace Segments:**
- Upload to Lambda: < 1 second
- Lambda to Textract: < 5 seconds
- Textract processing: 30s - 5 minutes
- SNS to Lambda: < 1 second
- Lambda to Bedrock: 10-60 seconds
- Save to S3: < 5 seconds

---

## Cost Optimization

### Monthly Cost Breakdown (1000 files/month, 3 pages each)

| Service | Usage | Unit Cost | Monthly Cost |
|---------|-------|-----------|--------------|
| **S3 Storage** | 10 GB | $0.023/GB | $0.23 |
| **S3 Requests** | 5000 PUT/GET | First 100K free | $0.00 |
| **Lambda (Orchestrator)** | 1000 invocations, 256MB, 30s avg | $0.0000002/invocation | $0.20 |
| **Lambda (Bedrock)** | 1000 invocations, 1024MB, 120s avg | $0.0000166/GB-sec | $2.00 |
| **Textract** | 3000 pages | $0.045/page | $135.00 |
| **Bedrock (Qwen)** | 5M input + 1M output tokens | $0.0008 in + $0.0024 out | $6.40 |
| **SNS** | 1000 notifications | $0.50/million | $0.00 |
| **DynamoDB** | On-demand, 5000 writes | $1.25/million writes | $0.01 |
| **CloudWatch Logs** | 5 GB | $0.50/GB | $2.50 |
| **X-Ray** | 1000 traces | $0.00 (within free tier) | $0.00 |
| **EventBridge** | 1000 events | Free tier | $0.00 |
| **Total** | | | **~$146.34/month** |

**Cost Optimization Strategies:**

1. **S3 Lifecycle Management:**
   - Move to Standard-IA after 30 days: Save 50%
   - Move to Glacier after 90 days: Save 80%

2. **Lambda Reserved Concurrency:**
   - For predictable workloads: Up to 70% savings with Compute Savings Plans

3. **Textract Caching:**
   - Already implemented: Avoid re-processing duplicate files
   - Estimated savings: 20-30%

4. **Bedrock Prompt Caching:**
   - Already implemented: Cache identical prompts
   - Estimated savings: 40-60%

5. **DynamoDB On-Demand vs Provisioned:**
   - On-demand for variable traffic: Pay per request
   - Provisioned for predictable traffic: Up to 50% cheaper

6. **CloudWatch Log Retention:**
   - Set retention to 30 days: Reduce storage costs
   - Export old logs to S3: 80% cheaper

**Estimated Costs at Scale:**

| Volume | Monthly Cost | Cost per Document |
|--------|--------------|-------------------|
| 1K files/month | $146 | $0.146 |
| 10K files/month | $1,460 | $0.146 |
| 100K files/month | $14,600 | $0.146 |

---

## Architecture Decision Records

### ADR-001: Why Lambda over ECS/EC2?

**Decision:** Use AWS Lambda for compute

**Rationale:**
- ✅ No server management
- ✅ Auto-scaling to zero (cost-effective)
- ✅ Built-in high availability
- ✅ Native integration with S3, SNS, EventBridge
- ✅ Pay-per-use pricing model

**Trade-offs:**
- ❌ 15-minute execution limit (acceptable for our use case)
- ❌ Cold start latency (1-2 seconds, acceptable)
- ❌ Vendor lock-in (acceptable for managed service benefits)

---

### ADR-002: Why EventBridge over Direct S3 → Lambda?

**Decision:** Use EventBridge for event routing

**Rationale:**
- ✅ Flexible rule-based routing
- ✅ Multiple targets (future: DLQ, monitoring)
- ✅ Built-in retry logic with configurable backoff
- ✅ Event archive for replay
- ✅ Easier to add future integrations

**Trade-offs:**
- ❌ Slightly higher latency (50-100ms, acceptable)
- ❌ Additional service to monitor

---

### ADR-003: Why DynamoDB over RDS?

**Decision:** Use DynamoDB for state tracking and results storage

**Rationale:**
- ✅ Serverless, auto-scaling
- ✅ Single-digit millisecond latency
- ✅ No database administration
- ✅ Built-in backup and point-in-time recovery
- ✅ TTL for automatic data expiration

**Trade-offs:**
- ❌ Less flexible querying (mitigated with GSIs)
- ❌ Item size limit of 400KB (JSON stored in S3 if larger)

---

## Next Steps

**Refer to these documents for detailed implementation:**

1. **[PRODUCTION_DEPLOYMENT_STRATEGY.md](PRODUCTION_DEPLOYMENT_STRATEGY.md)** - CI/CD pipeline and release strategy
2. **[PRODUCTION_INFRASTRUCTURE_CODE.md](PRODUCTION_INFRASTRUCTURE_CODE.md)** - CloudFormation/Terraform templates
3. **[PRODUCTION_CICD_PIPELINE.md](PRODUCTION_CICD_PIPELINE.md)** - CI/CD setup guide
4. **[PRODUCTION_MONITORING_SETUP.md](PRODUCTION_MONITORING_SETUP.md)** - CloudWatch dashboards and alarms

---

**Architecture Version:** 1.0  
**Last Reviewed:** January 2025  
**Next Review:** April 2025
