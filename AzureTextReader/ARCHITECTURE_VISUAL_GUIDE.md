# ImageTextExtractor - Azure Architecture Diagram

## High-Level Architecture

```mermaid
graph TB
    subgraph "File Upload & Trigger"
        A[User/App Upload TIF File]
    end
    
    subgraph "Azure Blob Storage"
B1[input/pending/]
        B2[input/processing/]
        B3[input/failed/]
   B4[processed/archive/]
    B5[output/v1-schema/]
        B6[output/v2-dynamic/]
        B7[output/ocr/]
    end
    
    subgraph "Event Processing"
     C[Azure Event Grid<br/>System Topic]
   D[Azure Function<br/>ProcessTifTrigger<br/><br/>1. Validate event<br/>2. Move file<br/>3. Create metadata<br/>4. Trigger container]
    end
    
    subgraph "Worker Processing"
        E[Azure Container Instance<br/>Container Group: imageextractor-jobId<br/>CPU: 1 core, Memory: 1.5 GB<br/>Restart: Never]
    end
    
    subgraph "Processing Pipeline"
        F1[Download TIF from Blob]
      F2[Azure Content Understanding<br/>OCR]
  F3[Extract Markdown Text]
     F4[Azure OpenAI GPT-4]
        F5[Generate JSON Results]
        F6[Upload Results]
        F7[Move to Archive]
        F8[Update Job Status]
    end
    
    subgraph "AI Services"
        G1[Azure Content Understanding<br/>Prebuilt Analyzer<br/>- OCR Extraction<br/>- Layout Analysis<br/>- Table Detection]
        G2[Azure OpenAI<br/>GPT-4o-mini<br/>- V1: Schema-based<br/>- V2: Dynamic]
    end
    
    subgraph "Data Storage"
        H1[Blob Storage<br/>JSON Files]
        H2[Azure SQL Database<br/>OR<br/>Azure Cosmos DB]
    end
    
    subgraph "Monitoring"
        I1[Application Insights]
        I2[Log Analytics Workspace]
        I3[Power BI Dashboards]
    end
    
    A -->|HTTPS Upload| B1
B1 -->|Blob Created Event| C
    C -->|Event Subscription| D
    D -->|Container API Call| E

    E --> F1
    F1 --> F2
    F2 --> F3
    F3 --> F4
    F4 --> F5
    F5 --> F6
    F6 --> F7
    F7 --> F8
    
    F2 -.->|Uses| G1
    F4 -.->|Uses| G2
    
    F6 --> H1
    F8 --> H2
 
    E -.->|Telemetry| I1
    I1 --> I2
    H2 --> I3
    H1 --> I3
    
    style A fill:#e1f5ff
    style C fill:#fff4e1
    style D fill:#fff4e1
    style E fill:#f0e1ff
    style G1 fill:#e1ffe1
    style G2 fill:#e1ffe1
    style H1 fill:#ffe1e1
    style H2 fill:#ffe1e1
  style I1 fill:#ffffe1
```

### Simplified Text Flow

```
User/App
   |
   | (Upload TIF via HTTPS)
   v
Azure Blob Storage (input/pending/)
   |
   | (Blob Created Event)
   v
Azure Event Grid (System Topic)
   |
   | (Event Subscription)
   v
Azure Function (ProcessTifTrigger)
|
   | 1. Validate event
   | 2. Move file to processing/
   | 3. Create job metadata
   | 4. Trigger container
   v
Azure Container Instance
   |
   | Container Group: imageextractor-{jobId}
   | Resources: 1 core CPU, 1.5 GB RAM
   | Restart Policy: Never
   v
Processing Pipeline:
   |
+-> Step 1: Download TIF from Blob Storage
   |
   +-> Step 2: Send to Azure Content Understanding API (OCR)
   |
   +-> Step 3: Extract markdown text from OCR results
   |
   +-> Step 4: Process with Azure OpenAI GPT-4
   | |
   |      +-> V1: Schema-based extraction
   |      +-> V2: Dynamic extraction
   |
   +-> Step 5: Generate JSON results
   |      |
   |+-> final_output_*_v1_schema.json
   |      +-> final_output_*_v2_dynamic.json
   |
   +-> Step 6: Upload results to Blob Storage
   |
   +-> Step 7: Move source file to archive
   |
   +-> Step 8: Update job status
   |
   v
Results Available:
   |
   +-> Blob Storage (JSON files)
   +-> Database (Azure SQL / Cosmos DB)
```

---

## Detailed Component Breakdown

### 1. Event-Driven Flow

```mermaid
flowchart LR
    A[File Upload] --> B[Blob Created Event]
    B --> C[Event Grid]
    C --> D[Function Trigger]
 D --> E[Container Instance]
    
    style A fill:#e1f5ff
    style B fill:#fff4e1
    style C fill:#fff4e1
    style D fill:#f0e1ff
    style E fill:#f0e1ff
```

```
File Upload ? Blob Created Event ? Event Grid ? Function Trigger ? Container Instance
```

### 2. Processing States

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Processing: Event Triggered
    Processing --> Completed: Success
    Processing --> Failed: Error
    Failed --> Processing: Retry
    Failed --> ManualReview: Max Retries Exceeded
    Completed --> [*]
    ManualReview --> [*]
    
    note right of Pending
  Location: input/pending/
  end note
    
    note right of Processing
        Location: input/processing/
    end note
    
    note right of Completed
        Location: processed/archive/
    end note
    
    note right of Failed
  Location: input/failed/
    end note
```

**State Locations:**

| State | Location | Description |
|-------|----------|-------------|
| **Pending** | `input/pending/` | File uploaded, waiting for processing |
| **Processing** | `input/processing/` | Currently being processed |
| **Completed** | `processed/archive/` | Successfully processed and archived |
| **Failed** | `input/failed/` | Processing failed, awaiting retry or manual review |

### 3. Data Flow Timeline

```mermaid
gantt
    title TIF File Processing Timeline
    dateFormat ss
    axisFormat %S sec
    
    section Upload
    Blob Upload   :a1, 00, 3s
    
    section Trigger
    Event Grid Trigger    :a2, 03, 2s
    Function Execution    :a3, 05, 5s
    
    section Worker
    Container Start       :a4, 10, 15s
    OCR Processing        :a5, 25, 25s
    OpenAI Processing     :a6, 50, 15s
    
  section Storage
 Save Results        :a7, 65, 4s
    Archive File          :a8, 69, 2s
```

**Processing Time Breakdown:**

```
TIF File ? OCR API ? Markdown ? OpenAI ? JSON ? Storage ? Database
(0s)      (5-10s)   (15-20s)   (10-30s)  (1-2s)  (1-2s)    (1s)

Total Time: ~30-60 seconds per TIF file
Average: ~45 seconds
```

---

## Scalability Patterns

### Horizontal Scaling

```mermaid
graph TB
    subgraph "Multiple Files Uploaded Simultaneously"
        F1[File 1] --> E1[Event 1] --> Fn1[Function 1] --> C1[Container 1]
        F2[File 2] --> E2[Event 2] --> Fn2[Function 2] --> C2[Container 2]
    F3[File 3] --> E3[Event 3] --> Fn3[Function 3] --> C3[Container 3]
        F4[File N] --> E4[Event N] --> Fn4[Function N] --> C4[Container N]
    end
    
    style F1 fill:#e1f5ff
    style F2 fill:#e1f5ff
    style F3 fill:#e1f5ff
    style F4 fill:#e1f5ff
```

**Concurrency Limits:**
- **Function App**: 200 concurrent executions
- **Container Instances**: Subscription limit (default: 100)
- **Event Grid**: 5,000 events/second

### Batch Processing Options

**Option 1: Event Grid (Current Implementation)**
```
Upload all files to pending/
   |
   +-> Event Grid triggers one function per file
   |
   +-> Parallel processing (limited by subscription)
   |
   v
Results
```

**Option 2: Azure Batch (For Very Large Volumes >10,000 files)**
```
Upload all files
   |
   +-> Single trigger creates batch job
   |
   +-> Batch processes multiple files per node
   |
   v
Cost-effective at scale
```

---

## Error Handling Flow

```mermaid
flowchart TD
    A[Upload TIF] --> B{Upload Success?}
    B -->|Yes| C[Event Triggered]
    B -->|No| D[Retry 3 times]
    D -->|All Failed| E[Alert Admin]
  D -->|Last Success| C
    
C --> F{Function Success?}
    F -->|Yes| G[Container Run]
    F -->|No| H[Move to failed/<br/>Send Alert<br/>Log Error]
    
    G --> I{Processing Success?}
    I -->|Yes| J[Archive File<br/>Upload Results<br/>Complete]
 I -->|No| K[Move to failed/<br/>Update Status<br/>Retry Queue]
 
    style A fill:#e1f5ff
 style J fill:#e1ffe1
    style E fill:#ffe1e1
    style H fill:#ffe1e1
    style K fill:#ffe1e1
```

---

## Cost Breakdown (Monthly)

### Monthly Usage: 300 TIF files (10/day)

| Service | Usage | Monthly Cost (USD) | % of Total |
|---------|-------|-------------------|------------|
| **Azure Blob Storage** | 100 GB Hot + 50 GB Cool + 30K operations | $4.70 | 5% |
| **Azure Functions** | 300 executions, 1 GB-second | $0.00 | 0% (Free tier) |
| **Container Instances** | 300 runs × 2 min × 1.5 GB RAM + 1 vCPU | $65.25 | 67% |
| **Azure OpenAI** | 300K input + 150K output tokens | $0.14 | <1% |
| **Content Understanding API** | 300 pages OCR | $15.00 | 15% |
| **Azure Event Grid** | 300 operations | $0.60 | <1% |
| **Application Insights** | 2 GB data ingestion | $5.75 | 6% |
| **Azure SQL Basic** | Database + 10 GB storage | $6.15 | 6% |
| **TOTAL** | | **$97.59** | **100%** |

**Cost Per TIF File Processed: $0.33**

#### Cost Breakdown Details

```
Azure Blob Storage
?? Hot tier (100 GB)    $2.30
?? Cool tier (50 GB archived)     $1.00
?? Transactions (30K operations)  $0.50
?? Data transfer (10 GB egress)   $0.90
   Subtotal: $4.70

Azure Functions (Consumption Plan)
?? Executions (300 function calls)  $0.00 (Free tier)
?? Execution time (300 × 5 sec)     $0.00 (Free tier)
?? Memory (1 GB-seconds)      $0.00 (Free tier)
   Subtotal: $0.00

Azure Container Instances
?? 300 runs × 2 minutes × $0.0000125/sec  $45.00
?? Memory (1.5 GB × 300 × 2 min)       $9.00
?? vCPU (1 core × 300 × 2 min)      $11.25
Subtotal: $65.25

Azure OpenAI (GPT-4o-mini)
?? Input tokens (300K × $0.15/1M)$0.045
?? Output tokens (150K × $0.60/1M)  $0.090
?? Content Understanding API (300 pages)   $15.00
   Subtotal: $15.14

Azure Event Grid
?? Operations (300 events)                 $0.60
   Subtotal: $0.60

Application Insights
?? Data ingestion (2 GB)       $5.75
?? Data retention (90 days)       $0.00 (Free)
?? Synthetic monitoring          $0.00
   Subtotal: $5.75

Database (Azure SQL Basic)
?? Database instance   $4.99
?? Storage (10 GB)     $1.16
?? Backup storage  $0.00 (Free)
   Subtotal: $6.15
```

---

## Performance Characteristics

### Single TIF File Processing Time

```mermaid
gantt
    title Processing Performance Timeline
    dateFormat ss
    axisFormat %S sec
    
  section Upload
Blob upload  :a1, 00, 3s
    
    section Trigger
    Event Grid trigger  :a2, 03, 2s
    Function execution :a3, 05, 5s
    
    section Worker
    Container start         :a4, 10, 15s
    OCR processing      :a5, 25, 25s
    OpenAI processing       :a6, 50, 15s
    
  section Storage
 Save results            :a7, 65, 4s
    Archive file            :a8, 69, 2s
```

| Stage | Time | Notes |
|-------|------|-------|
| 1. Blob upload | 1-3 seconds | Depends on file size and network |
| 2. Event Grid trigger | 1-2 seconds | Near real-time event processing |
| 3. Function execution | 2-5 seconds | Move file, create metadata |
| 4. Container start | 10-15 seconds | Cold start penalty |
| 5. OCR processing | 15-30 seconds | Azure Content Understanding |
| 6. OpenAI processing | 10-20 seconds | GPT-4o-mini for extraction |
| 7. Save results | 2-5 seconds | Upload JSON to blob |
| 8. Archive file | 1-2 seconds | Move to archive folder |
| **TOTAL** | **42-82 seconds** | **Average: ~60 seconds** |

---

## Deployment Environments

### Environment Strategy

```mermaid
graph LR
    subgraph Development
     D1[rg-imageextractor-dev<br/>Cost: ~$20/month]
  end
    
    subgraph Staging
        S1[rg-imageextractor-staging<br/>Cost: ~$50/month]
    end
    
    subgraph Production
        P1[rg-imageextractor-prod<br/>High Availability<br/>Cost: ~$100/month]
    end
    
  D1 -->|Promote| S1
    S1 -->|Promote| P1
    
    style D1 fill:#e1f5ff
    style S1 fill:#fff4e1
    style P1 fill:#e1ffe1
```

| Environment | Resource Group | Storage Account | Function App | Container Registry | Cost/Month |
|-------------|---------------|-----------------|--------------|-------------------|------------|
| **Development** | rg-imageextractor-dev | imageextractordevstore | imageextractor-func-dev | imageextractordevacr | ~$20 |
| **Staging** | rg-imageextractor-staging | imageextractorstagingstore | imageextractor-func-staging | imageextractorstagingacr | ~$50 |
| **Production** | rg-imageextractor-prod | imageextractorstorage | imageextractor-func | imageextractoracr | ~$100 |

---

## Security Layers

```mermaid
graph TB
 subgraph "Layer 1: Network Security"
        N1[Private Endpoints]
        N2[VNet Integration]
        N3[NSG Rules]
        N4[Firewall Rules]
    end
    
    subgraph "Layer 2: Identity & Access"
        I1[Managed Identity<br/>System-assigned]
 I2[RBAC]
      I3[Azure AD Authentication]
        I4[Service Principal<br/>CI/CD]
    end
    
    subgraph "Layer 3: Secrets Management"
        S1[Azure Key Vault]
        S2[Managed Identity<br/>to Key Vault]
    S3[Secrets Rotation Policy]
     S4[Access Policies]
    end
    
    subgraph "Layer 4: Data Protection"
        D1[Encryption at Rest<br/>Storage]
        D2[Encryption in Transit<br/>HTTPS/TLS]
  D3[Blob Versioning]
        D4[Soft Delete<br/>30 days]
        D5[Lifecycle Management]
    end
    
    subgraph "Layer 5: Monitoring & Compliance"
        M1[Azure Policy]
        M2[Security Center]
        M3[Audit Logs]
        M4[Compliance Reports]
 M5[Alert Rules]
    end
    
    style N1 fill:#e1f5ff
    style I1 fill:#fff4e1
    style S1 fill:#f0e1ff
    style D1 fill:#e1ffe1
    style M1 fill:#ffffe1
```

**Security Architecture:**

```
Layer 1: Network Security
?? Private Endpoints (Optional)
?? VNet Integration
?? NSG Rules
?? Firewall Rules

Layer 2: Identity & Access
?? Managed Identity (System-assigned)
?? RBAC (Role-Based Access Control)
?? Azure AD Authentication
?? Service Principal (CI/CD)

Layer 3: Secrets Management
?? Azure Key Vault
?? Managed Identity to Key Vault
?? Secrets rotation policy
?? Access policies

Layer 4: Data Protection
?? Encryption at rest (Storage)
?? Encryption in transit (HTTPS/TLS)
?? Blob versioning
?? Soft delete (30 days)
?? Lifecycle management

Layer 5: Monitoring & Compliance
?? Azure Policy
?? Security Center
?? Audit logs
?? Compliance reports
?? Alert rules
```

---

## Architecture Summary

**This architecture is:**
- ? Event-driven and serverless
- ? Scalable (handles 1000+ files/day)
- ? Cost-effective ($0.33 per file)
- ? Secure (multiple security layers)
- ? Monitored (Application Insights)
- ? Production-ready (99.95% SLA)

**Deployment time**: ~2 hours  
**Maintenance effort**: Low (serverless = less ops)  
**Learning curve**: Medium (Azure PaaS services)

---

## Additional Resources

- **Complete Deployment Guide**: See `AZURE_ARCHITECTURE_GUIDE_FIXED.md`
- **Quick Deploy**: See `QUICK_DEPLOY_GUIDE.md`
- **Documentation Index**: See `START_HERE.md`

**Document Version**: 2.0 (Fixed Rendering)  
**Last Updated**: 2025-01-28  
**Status**: ? All Diagrams Render Correctly
