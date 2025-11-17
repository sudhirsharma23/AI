# Azure Architecture - Comprehensive Guide (Fixed)

## ?? Table of Contents

1. [Overview](#overview)
2. [Architecture Diagrams](#architecture-diagrams)
3. [Component Details](#component-details)
4. [Deployment Guide](#deployment-guide)
5. [Cost Analysis](#cost-analysis)
6. [Security & Monitoring](#security--monitoring)

---

## Overview

This guide provides the complete Azure production architecture for ImageTextExtractor with **properly formatted diagrams** that render correctly in all Markdown viewers.

### Key Features

- ? Event-driven serverless architecture
- ? Scalable (1000+ files/day)
- ? Cost-effective ($0.33 per file)
- ? Secure (Managed Identity + Key Vault)
- ? Monitored (Application Insights)
- ? Production-ready (99.95% SLA)

---

## Architecture Diagrams

### 1. Complete System Architecture

\`\`\`mermaid
graph TB
    A[User/App Upload TIF] -->|HTTPS| B[Azure Blob Storage]
    B -->|Blob Created Event| C[Azure Event Grid]
    C -->|Trigger| D[Azure Function]
    D -->|Orchestrate| E[Azure Container Instance]
    E -->|OCR| F[Azure Content Understanding]
    E -->|AI| G[Azure OpenAI GPT-4]
    E -->|Save| H[Blob Storage Output]
    E -->|Store| I[Azure SQL/Cosmos DB]
    H --> J[Power BI/Analytics]
    I --> J
    E -.->|Telemetry| K[Application Insights]
\`\`\`

### 2. Processing Flow

\`\`\`
Step 1: User uploads TIF file
    |
      v
Step 2: Azure Blob Storage (input/pending/)
  |
     | Blob Created Event
     v
Step 3: Azure Event Grid
        |
   | Event Subscription
 v
Step 4: Azure Function (ProcessTifTrigger)
        |
        | - Validate event
        | - Move file to processing/
      | - Create job metadata
        | - Trigger container
        v
Step 5: Azure Container Instance
    |
        | - Download TIF from blob
        | - OCR Processing (Azure Content Understanding)
   | - AI Processing (Azure OpenAI GPT-4)
        | - Generate JSON (V1 schema + V2 dynamic)
        | - Upload results to blob
        | - Move source to archive
 | - Update job status
        v
Step 6: Results Available
   |
  +-- Blob Storage (JSON files)
      +-- Database (Structured data)
\`\`\`

### 3. Blob Storage Structure

\`\`\`
imageextractorstorage/
|
+-- input/      (Hot tier)
|   +-- pending/  (New files waiting)
|   +-- processing/       (Currently processing)
|   +-- failed/        (Failed processing)
|
+-- processed/     (Cool tier)
|   +-- archive/ (Successfully processed)
|   +-- 2025/
|           +-- 01/
|   +-- 28/
|
+-- output/         (Hot tier)
|   +-- v1-schema/          (V1 schema-based results)
|   +-- v2-dynamic/         (V2 dynamic results)
|   +-- ocr/              (OCR markdown results)
|
+-- logs/             (Archive tier)
    +-- 2025/
        +-- 01/
            +-- 28/
\`\`\`

### 4. Processing States

\`\`\`mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Processing: Event Triggered
    Processing --> Completed: Success
    Processing --> Failed: Error
    Failed --> Processing: Retry
    Failed --> Manual_Review: Max Retries
    Completed --> [*]
    Manual_Review --> [*]
\`\`\`

| State | Location | Description |
|-------|----------|-------------|
| **Pending** | input/pending/ | File uploaded, awaiting processing |
| **Processing** | input/processing/ | Currently being processed |
| **Completed** | processed/archive/ | Successfully processed and archived |
| **Failed** | input/failed/ | Processing failed, awaiting retry |

---

## Component Details

### Azure Blob Storage

**Purpose**: Store TIF files, processing results, and logs

**Configuration**:
- **Account Type**: StorageV2 (General purpose v2)
- **Replication**: LRS (Local Redundant Storage)
- **Access Tier**: Hot (for active containers)
- **Features Enabled**:
  - Blob versioning
  - Soft delete (30 days)
  - Lifecycle management

**Lifecycle Policy Example**:

\`\`\`json
{
  "rules": [
    {
      "enabled": true,
      "name": "MoveToArchive",
      "type": "Lifecycle",
   "definition": {
   "filters": {
"prefixMatch": ["processed/archive/"],
          "blobTypes": ["blockBlob"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": { "daysAfterModificationGreaterThan": 30 },
  "tierToArchive": { "daysAfterModificationGreaterThan": 90 },
     "delete": { "daysAfterModificationGreaterThan": 365 }
          }
        }
      }
    }
  ]
}
\`\`\`

---

### Azure Event Grid

**Purpose**: Trigger processing when new TIF file uploaded

**Event Configuration**:
- **Event Type**: `Microsoft.Storage.BlobCreated`
- **Subject Filter Begins With**: `/blobServices/default/containers/input/blobs/pending/`
- **Subject Filter Ends With**: `.tif`
- **Retry Policy**: 30 attempts, 24-hour TTL

**Create Event Grid**:

\`\`\`bash
# Create system topic
az eventgrid system-topic create \\
  --name imageextractor-storage-events \\
  --resource-group rg-imageextractor-prod \\
  --location eastus \\
  --topic-type Microsoft.Storage.StorageAccounts \\
  --source /subscriptions/{sub-id}/resourceGroups/rg-imageextractor-prod/providers/Microsoft.Storage/storageAccounts/imageextractorstorage

# Create event subscription
az eventgrid system-topic event-subscription create \\
  --name tif-file-subscription \\
  --system-topic-name imageextractor-storage-events \\
  --resource-group rg-imageextractor-prod \\
  --endpoint-type azurefunction \\
  --endpoint {function-resource-id} \\
  --included-event-types Microsoft.Storage.BlobCreated \\
  --subject-begins-with /blobServices/default/containers/input/blobs/pending/ \\
  --subject-ends-with .tif
\`\`\`

---

### Azure Functions (Orchestrator)

**Purpose**: Coordinate container instance execution

**Specifications**:
- **Runtime**: .NET 9 (Isolated Worker)
- **Plan**: Consumption (pay-per-execution)
- **Version**: Azure Functions v4

**Key Responsibilities**:
1. Validate Event Grid events
2. Move files between blob containers
3. Create job metadata
4. Trigger Azure Container Instance
5. Handle errors and implement retry logic

**Code Location**: `Azure.Functions/TifProcessingTrigger.cs` (already created)

---

### Azure Container Instances (Worker)

**Purpose**: Execute ImageTextExtractor application

**Configuration**:

| Setting | Value | Reason |
|---------|-------|--------|
| CPU | 1 core | Sufficient for single-file processing |
| Memory | 1.5 GB | Handles OCR + AI + result generation |
| Restart Policy | Never | One-time job, no automatic restart |
| OS | Linux | Cost-effective, Docker support |
| Image | imageextractoracr.azurecr.io/imageextractor:latest | Private ACR image |

**Environment Variables**:

\`\`\`bash
JOB_ID="{guid}"
BLOB_NAME="{filename.tif}"
STORAGE_CONNECTION_STRING="{connection-string}"
AZURE_OPENAI_ENDPOINT="{endpoint-url}"
AZURE_OPENAI_KEY="@Microsoft.KeyVault(SecretUri=...)"
\`\`\`

**Container Execution Flow**:

\`\`\`
1. Container starts
2. Read environment variables
3. Download TIF from blob storage (generate SAS URL)
4. Process with Azure Content Understanding (OCR)
5. Process with Azure OpenAI GPT-4 (V1 + V2)
6. Generate JSON results
7. Upload results to output/ container
8. Move source file to processed/archive/
9. Update job status in database
10. Container terminates
\`\`\`

---

### Database Options

#### Option 1: Azure SQL Database

**Recommended for**: Structured queries, reporting, analytics

**Schema**:

\`\`\`sql
CREATE TABLE ProcessingJobs (
    JobId UNIQUEIDENTIFIER PRIMARY KEY,
    BlobName NVARCHAR(500) NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    ProcessingTimeMs INT NULL
);

CREATE TABLE ExtractionResults (
    ResultId UNIQUEIDENTIFIER PRIMARY KEY,
    JobId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES ProcessingJobs(JobId),
    Version VARCHAR(10) NOT NULL, -- 'V1' or 'V2'
    BuyerCount INT NULL,
SellerCount INT NULL,
    PropertyAddress NVARCHAR(500) NULL,
    SalePrice DECIMAL(18,2) NULL,
    JsonData NVARCHAR(MAX) NULL
);

CREATE TABLE Buyers (
    BuyerId UNIQUEIDENTIFIER PRIMARY KEY,
    ResultId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES ExtractionResults(ResultId),
FullName NVARCHAR(200),
    OwnershipPercentage DECIMAL(5,2),
 AddressLine1 NVARCHAR(200),
    City NVARCHAR(100),
    State NVARCHAR(2),
    ZipCode NVARCHAR(10)
);

CREATE TABLE Sellers (
    SellerId UNIQUEIDENTIFIER PRIMARY KEY,
    ResultId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES ExtractionResults(ResultId),
    FullName NVARCHAR(200),
    OwnershipPercentage DECIMAL(5,2),
    DateAcquired DATE,
    AddressLine1 NVARCHAR(200),
    City NVARCHAR(100),
    State NVARCHAR(2),
    ZipCode NVARCHAR(10)
);
\`\`\`

#### Option 2: Azure Cosmos DB

**Recommended for**: Document storage, global distribution, flexible schema

**Container Configuration**:
- **Partition Key**: `/jobId`
- **Throughput**: 400 RU/s (autoscale to 4000 RU/s)
- **Indexing**: Automatic, exclude large JSON data
- **TTL**: 1 year (optional cleanup)

---

## Deployment Guide

### Prerequisites

\`\`\`bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install Docker
sudo apt-get update && sudo apt-get install -y docker.io

# Login to Azure
az login

# Set subscription
az account set --subscription "Your-Subscription-Name"
\`\`\`

### Step-by-Step Deployment

#### Step 1: Create Resource Group

\`\`\`bash
az group create \\
  --name rg-imageextractor-prod \\
  --location eastus
\`\`\`

#### Step 2: Create Storage Account

\`\`\`bash
az storage account create \\
  --name imageextractorstorage \\
  --resource-group rg-imageextractor-prod \\
  --location eastus \\
  --sku Standard_LRS \\
  --kind StorageV2

# Get connection string (save this!)
CONN_STRING=$(az storage account show-connection-string \\
  --name imageextractorstorage \\
  --resource-group rg-imageextractor-prod \\
  --query connectionString -o tsv)

echo "Connection String: $CONN_STRING"
\`\`\`

#### Step 3: Create Blob Containers

\`\`\`bash
# Create containers
az storage container create --name input --connection-string "$CONN_STRING"
az storage container create --name processed --connection-string "$CONN_STRING"
az storage container create --name output --connection-string "$CONN_STRING"
az storage container create --name logs --connection-string "$CONN_STRING"
\`\`\`

#### Step 4: Create Azure Container Registry

\`\`\`bash
az acr create \\
  --name imageextractoracr \\
  --resource-group rg-imageextractor-prod \\
  --sku Standard \\
  --admin-enabled true

# Get ACR credentials
az acr credential show --name imageextractoracr
\`\`\`

#### Step 5: Build and Push Docker Image

\`\`\`bash
# Navigate to project directory
cd E:\\Sudhir\\GitRepo\\AzureTextReader

# Build Docker image
docker build -t imageextractor:latest -f Dockerfile .

# Tag for ACR
docker tag imageextractor:latest imageextractoracr.azurecr.io/imageextractor:latest

# Login to ACR
az acr login --name imageextractoracr

# Push to ACR
docker push imageextractoracr.azurecr.io/imageextractor:latest
\`\`\`

#### Step 6: Create Key Vault

\`\`\`bash
az keyvault create \\
  --name imageextractor-kv \\
  --resource-group rg-imageextractor-prod \\
  --location eastus

# Add secrets
az keyvault secret set \\
  --vault-name imageextractor-kv \\
  --name "AzureOpenAIKey" \\
  --value "<your-openai-key>"

az keyvault secret set \\
  --vault-name imageextractor-kv \\
  --name "AzureOpenAIEndpoint" \\
  --value "https://your-openai.openai.azure.com"
\`\`\`

#### Step 7: Create Function App

\`\`\`bash
# Create consumption plan (or use existing)
az functionapp plan create \\
  --name imageextractor-plan \\
  --resource-group rg-imageextractor-prod \\
  --location eastus \\
  --sku Y1 \\
  --is-linux

# Create Function App
az functionapp create \\
  --name imageextractor-func \\
  --storage-account imageextractorstorage \\
  --plan imageextractor-plan \\
  --resource-group rg-imageextractor-prod \\
  --runtime dotnet-isolated \\
  --functions-version 4
\`\`\`

#### Step 8: Deploy Function Code

\`\`\`bash
cd Azure.Functions
func azure functionapp publish imageextractor-func
\`\`\`

#### Step 9: Configure Event Grid

\`\`\`bash
# Create system topic
az eventgrid system-topic create \\
  --name imageextractor-storage-events \\
  --resource-group rg-imageextractor-prod \\
  --location eastus \\
  --topic-type Microsoft.Storage.StorageAccounts \\
  --source /subscriptions/{sub-id}/resourceGroups/rg-imageextractor-prod/providers/Microsoft.Storage/storageAccounts/imageextractorstorage

# Get Function resource ID
FUNCTION_ID=$(az functionapp function show \\
  --name imageextractor-func \\
  --resource-group rg-imageextractor-prod \\
  --function-name ProcessTifTrigger \\
  --query id -o tsv)

# Create event subscription
az eventgrid system-topic event-subscription create \\
  --name tif-file-subscription \\
  --system-topic-name imageextractor-storage-events \\
  --resource-group rg-imageextractor-prod \\
  --endpoint-type azurefunction \\
  --endpoint $FUNCTION_ID \\
  --included-event-types Microsoft.Storage.BlobCreated \\
  --subject-begins-with /blobServices/default/containers/input/blobs/pending/ \\
  --subject-ends-with .tif
\`\`\`

#### Step 10: Test End-to-End

\`\`\`bash
# Upload test TIF file
az storage blob upload \\
  --account-name imageextractorstorage \\
  --container-name input \\
  --name pending/test-001.tif \\
  --file ./test-data/sample.tif \\
  --connection-string "$CONN_STRING"

# Wait 60 seconds for processing

# Check if output exists
az storage blob list \\
  --account-name imageextractorstorage \\
  --container-name output \\
  --prefix "test-001" \\
  --connection-string "$CONN_STRING"

# Download results
az storage blob download \\
  --account-name imageextractorstorage \\
  --container-name output \\
  --name "test-001/final_output_*_v2_dynamic.json" \\
  --file ./results.json \\
  --connection-string "$CONN_STRING"

# View results
cat ./results.json | jq .
\`\`\`

---

## Cost Analysis

### Monthly Cost Breakdown (300 files @ 10/day)

| Service | Usage | Monthly Cost | % |
|---------|-------|--------------|---|
| Azure Blob Storage | 100 GB Hot + 50 GB Cool + transactions | $4.70 | 5% |
| Azure Functions | 300 executions, 1 GB-second | $0.00 | 0% |
| Container Instances | 300 runs × 2 min × 1.5 GB RAM | $65.25 | 67% |
| Azure OpenAI | 300K input + 150K output tokens | $0.14 | <1% |
| Content Understanding | 300 pages OCR | $15.00 | 15% |
| Event Grid | 300 operations | $0.60 | <1% |
| Application Insights | 2 GB data ingestion | $5.75 | 6% |
| Azure SQL Basic | Database + 10 GB storage | $6.15 | 6% |
| **TOTAL** | | **$97.59** | **100%** |

**Cost per file**: $0.33

### Cost Optimization Strategies

1. **Azure Container Apps** (instead of ACI)
   - Scale-to-zero capability
   - Save: ~$20/month

2. **Cache OpenAI results**
 - Avoid re-processing identical documents
   - Save: ~$7/month

3. **Use Cool/Archive storage**
   - Automatic tiering with lifecycle policies
   - Save: ~$2/month

4. **SQL Reserved Capacity**
   - 1-year reservation = 38% discount
   - Save: ~$2/month

**Optimized monthly cost**: $65-75

---

## Security & Monitoring

### Security Architecture

\`\`\`mermaid
graph TB
    subgraph "Network Security"
    A1[Private Endpoints]
    A2[VNet Integration]
    A3[NSG Rules]
    end
    
    subgraph "Identity & Access"
    B1[Managed Identity]
    B2[RBAC]
    B3[Azure AD]
    end
    
    subgraph "Secrets Management"
    C1[Key Vault]
C2[Secret Rotation]
    C3[Access Policies]
    end
    
    subgraph "Data Protection"
    D1[Encryption at Rest]
  D2[Encryption in Transit]
    D3[Blob Versioning]
    D4[Soft Delete]
    end
    
    subgraph "Compliance & Monitoring"
    E1[Azure Policy]
    E2[Security Center]
    E3[Audit Logs]
    E4[Application Insights]
    end
\`\`\`

### Enable Managed Identity

\`\`\`bash
# Enable for Function App
az functionapp identity assign \\
  --name imageextractor-func \\
  --resource-group rg-imageextractor-prod

# Get principal ID
PRINCIPAL_ID=$(az functionapp identity show \\
  --name imageextractor-func \\
  --resource-group rg-imageextractor-prod \\
  --query principalId -o tsv)

# Grant Storage Blob Data Contributor role
az role assignment create \\
  --assignee $PRINCIPAL_ID \\
  --role "Storage Blob Data Contributor" \\
  --scope /subscriptions/{sub}/resourceGroups/rg-imageextractor-prod/providers/Microsoft.Storage/storageAccounts/imageextractorstorage

# Grant Key Vault access
az keyvault set-policy \\
  --name imageextractor-kv \\
  --object-id $PRINCIPAL_ID \\
  --secret-permissions get list
\`\`\`

### Application Insights Setup

\`\`\`bash
# Create Application Insights
az monitor app-insights component create \\
  --app imageextractor-insights \\
  --location eastus \\
  --resource-group rg-imageextractor-prod \\
--application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \\
  --app imageextractor-insights \\
  --resource-group rg-imageextractor-prod \\
  --query instrumentationKey -o tsv)

# Configure Function App
az functionapp config appsettings set \\
  --name imageextractor-func \\
  --resource-group rg-imageextractor-prod \\
  --settings "APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY"
\`\`\`

### Log Analytics Queries

\`\`\`kusto
// Processing job statistics
ProcessingJobs
| where Status == "Completed"
| summarize 
  Count = count(),
    AvgTime = avg(ProcessingTimeMs),
    P95Time = percentile(ProcessingTimeMs, 95)
  by bin(CreatedAt, 1h)
| render timechart

// Failure rate
ProcessingJobs
| summarize 
    Total = count(),
    Failed = countif(Status == "Failed"),
    FailureRate = (countif(Status == "Failed") * 100.0) / count()
  by bin(CreatedAt, 1h)
| render timechart

// Average processing time by hour
customMetrics
| where name == "ProcessingTimeMs"
| summarize avg(value), percentile(value, 95) by bin(timestamp, 1h)
| render timechart
\`\`\`

---

## CI/CD Pipeline

### GitHub Actions Workflow

Create `.github/workflows/deploy.yml`:

\`\`\`yaml
name: Deploy to Azure

on:
  push:
    branches: [main]
  workflow_dispatch:

env:
  RESOURCE_GROUP: rg-imageextractor-prod
  ACR_NAME: imageextractoracr
  IMAGE_NAME: imageextractor
  FUNCTION_APP: imageextractor-func

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '9.0.x'
    - run: dotnet build ./AzureTextReader/src/ImageTextExtractor.csproj --configuration Release

  docker:
  needs: build
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    - uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    - run: |
   az acr login --name ${{ env.ACR_NAME }}
        docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest -f AzureTextReader/Dockerfile .
        docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest

  deploy:
    needs: docker
    runs-on: ubuntu-latest
    steps:
- uses: Azure/functions-action@v1
  with:
        app-name: ${{ env.FUNCTION_APP }}
        package: './AzureTextReader/Azure.Functions/output'
        publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
\`\`\`

---

## ? Verification Checklist

After deployment, verify:

- [ ] Storage account created
- [ ] Blob containers created (input, processed, output, logs)
- [ ] ACR created and Docker image pushed
- [ ] Key Vault created with secrets
- [ ] Function App deployed and running
- [ ] Event Grid configured
- [ ] Managed Identity enabled
- [ ] Application Insights configured
- [ ] Test file processed successfully
- [ ] Results available in output container
- [ ] Database populated (if configured)

---

## ?? Additional Resources

- [Azure Functions Documentation](https://docs.microsoft.com/azure/azure-functions/)
- [Azure Container Instances](https://docs.microsoft.com/azure/container-instances/)
- [Azure Event Grid](https://docs.microsoft.com/azure/event-grid/)
- [Azure OpenAI Service](https://docs.microsoft.com/azure/cognitive-services/openai/)
- [Azure Pricing Calculator](https://azure.microsoft.com/pricing/calculator/)

---

**Document Version**: 2.0 (Fixed Rendering)  
**Last Updated**: 2025-01-28  
**Status**: ? Production Ready
