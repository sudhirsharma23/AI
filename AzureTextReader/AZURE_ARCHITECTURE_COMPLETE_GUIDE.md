# Azure Production Architecture - Complete Implementation Guide

## 3. Event-Driven Implementation

### 3.1 Event Flow

```
1. User uploads TIF file ? Azure Blob Storage (input/pending/)
2. Blob Created event ? Azure Event Grid
3. Event Grid triggers ? Azure Function
4. Azure Function:
   - Moves file to input/processing/
   - Creates job metadata
   - Triggers Container Instance
5. Container Instance:
   - Downloads TIF from blob
   - Processes with OCR + AI
   - Uploads results to output/
   - Moves source to processed/archive/
6. Results stored in:
   - Blob Storage (JSON files)
   - Azure SQL/Cosmos DB (structured data)
7. Notifications sent (optional):
   - Azure Service Bus
   - Logic Apps
   - Power Automate
```

### 3.2 Event Grid Configuration

**ARM Template**:
```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.EventGrid/systemTopics",
      "apiVersion": "2021-12-01",
      "name": "imageextractor-storage-events",
      "location": "eastus",
      "properties": {
        "source": "[resourceId('Microsoft.Storage/storageAccounts', 'imageextractorstorage')]",
     "topicType": "Microsoft.Storage.StorageAccounts"
      }
    },
    {
      "type": "Microsoft.EventGrid/systemTopics/eventSubscriptions",
      "apiVersion": "2021-12-01",
  "name": "imageextractor-storage-events/tif-file-subscription",
      "dependsOn": [
        "[resourceId('Microsoft.EventGrid/systemTopics', 'imageextractor-storage-events')]"
      ],
      "properties": {
        "destination": {
       "endpointType": "AzureFunction",
        "properties": {
            "resourceId": "[resourceId('Microsoft.Web/sites/functions', 'imageextractor-func', 'ProcessTifTrigger')]"
     }
        },
        "filter": {
        "includedEventTypes": [
   "Microsoft.Storage.BlobCreated"
          ],
          "subjectBeginsWith": "/blobServices/default/containers/input/blobs/pending/",
       "subjectEndsWith": ".tif",
    "enableAdvancedFilteringOnArrays": true
        },
    "eventDeliverySchema": "EventGridSchema",
        "retryPolicy": {
    "maxDeliveryAttempts": 30,
          "eventTimeToLiveInMinutes": 1440
   }
      }
    }
  ]
}
```

### 3.3 Dead Letter Queue & Retry Logic

**Implement poison message handling**:

```csharp
public class RetryHandler
{
 private const int MaxRetries = 3;
    
    public async Task<bool> ProcessWithRetry(
   Func<Task> operation, 
  string jobId)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
      {
      try
  {
                await operation();
                return true;
 }
        catch (Exception ex)
   {
    if (attempt == MaxRetries)
     {
          // Move to dead letter queue
     await MoveToDeadLetterQueue(jobId, ex);
      throw;
   }
        
           // Exponential backoff
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }
      
        return false;
    }
    
    private async Task MoveToDeadLetterQueue(string jobId, Exception ex)
    {
    // Send to Azure Service Bus Dead Letter Queue
     // Or move to storage dead letter container
    }
}
```

---

## 4. Storage Strategy

### 4.1 Blob Storage Lifecycle Management

**Configure automatic tiering and cleanup**:

```json
{
  "rules": [
    {
      "enabled": true,
  "name": "Move-Processed-To-Cool",
"type": "Lifecycle",
      "definition": {
        "actions": {
          "baseBlob": {
 "tierToCool": {
              "daysAfterModificationGreaterThan": 30
},
            "tierToArchive": {
   "daysAfterModificationGreaterThan": 90
      },
            "delete": {
              "daysAfterModificationGreaterThan": 365
            }
          }
        },
        "filters": {
   "blobTypes": ["blockBlob"],
     "prefixMatch": ["processed/archive/"]
        }
      }
    },
    {
      "enabled": true,
      "name": "Delete-Failed-After-Retention",
 "type": "Lifecycle",
    "definition": {
        "actions": {
        "baseBlob": {
        "delete": {
 "daysAfterModificationGreaterThan": 90
        }
          }
        },
 "filters": {
    "prefixMatch": ["input/failed/"]
        }
      }
    }
  ]
}
```

### 4.2 Database Storage Options

#### Option 1: Azure SQL Database (Recommended for structured queries)

**Schema**:
```sql
CREATE TABLE ProcessingJobs (
    JobId UNIQUEIDENTIFIER PRIMARY KEY,
    BlobName NVARCHAR(500) NOT NULL,
    BlobUrl NVARCHAR(2000) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- Queued, Processing, Completed, Failed
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    StartedAt DATETIME2 NULL,
    CompletedAt DATETIME2 NULL,
    ErrorMessage NVARCHAR(MAX) NULL,
    ResultV1BlobUrl NVARCHAR(2000) NULL,
    ResultV2BlobUrl NVARCHAR(2000) NULL,
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
    RecordingDate DATE NULL,
    JsonData NVARCHAR(MAX) NULL, -- Full JSON result
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE Buyers (
    BuyerId UNIQUEIDENTIFIER PRIMARY KEY,
    ResultId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES ExtractionResults(ResultId),
    SequenceNumber INT NOT NULL,
    IsPrimary BIT NOT NULL,
    FullName NVARCHAR(200) NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    OwnershipPercentage DECIMAL(5,2) NULL,
    AddressLine1 NVARCHAR(200) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(2) NULL,
    ZipCode NVARCHAR(10) NULL
);

CREATE TABLE Sellers (
    SellerId UNIQUEIDENTIFIER PRIMARY KEY,
    ResultId UNIQUEIDENTIFIER FOREIGN KEY REFERENCES ExtractionResults(ResultId),
    SequenceNumber INT NOT NULL,
    IsPrimary BIT NOT NULL,
    FullName NVARCHAR(200) NULL,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    OwnershipPercentage DECIMAL(5,2) NULL,
    DateAcquired DATE NULL,
    AddressLine1 NVARCHAR(200) NULL,
    City NVARCHAR(100) NULL,
    State NVARCHAR(2) NULL,
    ZipCode NVARCHAR(10) NULL
);

-- Indexes for performance
CREATE INDEX IX_ProcessingJobs_Status ON ProcessingJobs(Status);
CREATE INDEX IX_ProcessingJobs_CreatedAt ON ProcessingJobs(CreatedAt);
CREATE INDEX IX_ExtractionResults_JobId ON ExtractionResults(JobId);
CREATE INDEX IX_Buyers_ResultId ON Buyers(ResultId);
CREATE INDEX IX_Sellers_ResultId ON Sellers(ResultId);
```

**C# Data Access Layer**:
```csharp
public class ResultsRepository
{
    private readonly string _connectionString;
    
    public ResultsRepository(string connectionString)
    {
  _connectionString = connectionString;
    }
    
    public async Task SaveExtractionResultAsync(
     Guid jobId, 
     string version, 
        string jsonData)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
    
        var resultId = Guid.NewGuid();
        
        // Parse JSON to extract key fields
        var json = JsonDocument.Parse(jsonData);
        var buyerCount = json.RootElement
    .GetProperty("buyerInformation")
            .GetProperty("totalBuyers")
      .GetInt32();
        
     // Insert into ExtractionResults
      await connection.ExecuteAsync(@"
            INSERT INTO ExtractionResults 
            (ResultId, JobId, Version, BuyerCount, JsonData)
        VALUES (@ResultId, @JobId, @Version, @BuyerCount, @JsonData)",
         new { ResultId = resultId, JobId = jobId, Version = version, 
      BuyerCount = buyerCount, JsonData = jsonData });
     
      // Extract and insert buyers
        var buyers = json.RootElement
            .GetProperty("buyerInformation")
            .GetProperty("buyers")
            .EnumerateArray();
        
        foreach (var buyer in buyers)
 {
         await connection.ExecuteAsync(@"
           INSERT INTO Buyers 
       (BuyerId, ResultId, SequenceNumber, FullName, OwnershipPercentage)
                VALUES (@BuyerId, @ResultId, @SequenceNumber, @FullName, @OwnershipPercentage)",
           new {
          BuyerId = Guid.NewGuid(),
            ResultId = resultId,
         SequenceNumber = buyer.GetProperty("sequenceNumber").GetInt32(),
       FullName = buyer.GetProperty("fullName").GetString(),
     OwnershipPercentage = buyer.GetProperty("ownershipPercentage").GetDecimal()
    });
   }
    }
}
```

#### Option 2: Azure Cosmos DB (Recommended for document storage)

**Container Configuration**:
```json
{
  "id": "extraction-results",
  "partitionKey": {
    "paths": ["/jobId"],
 "kind": "Hash"
  },
  "indexingPolicy": {
    "indexingMode": "consistent",
    "automatic": true,
    "includedPaths": [
      { "path": "/*" }
    ],
    "excludedPaths": [
      { "path": "/jsonData/*" }
    ]
  }
}
```

**C# Cosmos DB Client**:
```csharp
public class CosmosDbRepository
{
    private readonly Container _container;
    
    public CosmosDbRepository(CosmosClient cosmosClient)
    {
     var database = cosmosClient.GetDatabase("ImageExtractor");
        _container = database.GetContainer("extraction-results");
    }
    
    public async Task SaveResultAsync(string jobId, string jsonData)
    {
        var document = new
        {
            id = Guid.NewGuid().ToString(),
  jobId = jobId,
            version = "V2",
            result = JsonSerializer.Deserialize<object>(jsonData),
            createdAt = DateTime.UtcNow,
    ttl = 31536000 // 1 year TTL
        };
        
        await _container.CreateItemAsync(document, new PartitionKey(jobId));
    }
    
    public async Task<IEnumerable<dynamic>> QueryResultsAsync(string query)
    {
        var queryDefinition = new QueryDefinition(query);
     var iterator = _container.GetItemQueryIterator<dynamic>(queryDefinition);
    
        var results = new List<dynamic>();
        while (iterator.HasMoreResults)
        {
     var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        
  return results;
    }
}
```

---

## 5. CI/CD Pipeline Setup

### 5.1 GitHub Actions Workflow

**.github/workflows/deploy-imageextractor.yml**:
```yaml
name: Deploy ImageTextExtractor to Azure

on:
  push:
    branches: [ main, develop ]
    paths:
      - 'AzureTextReader/**'
  pull_request:
    branches: [ main ]
  workflow_dispatch:

env:
  AZURE_RESOURCE_GROUP: rg-imageextractor-prod
  AZURE_LOCATION: eastus
  ACR_NAME: imageextractoracr
  IMAGE_NAME: imageextractor
  FUNCTION_APP_NAME: imageextractor-func

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
   - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
   with:
  dotnet-version: '9.0.x'

      - name: Restore dependencies
        run: dotnet restore ./AzureTextReader/src/ImageTextExtractor.csproj

      - name: Build
        run: dotnet build ./AzureTextReader/src/ImageTextExtractor.csproj --configuration Release --no-restore

      - name: Run unit tests
        run: dotnet test ./AzureTextReader/tests/**/*.csproj --no-build --verbosity normal

  - name: Run integration tests
      run: dotnet test ./AzureTextReader/tests.integration/**/*.csproj --no-build --verbosity normal
  env:
AZURE_ENDPOINT: ${{ secrets.AZURE_ENDPOINT }}
       AZURE_SUBSCRIPTION_KEY: ${{ secrets.AZURE_SUBSCRIPTION_KEY }}

  build-docker-image:
    needs: build-and-test
    runs-on: ubuntu-latest
 if: github.ref == 'refs/heads/main' || github.ref == 'refs/heads/develop'
    steps:
      - name: Checkout code
    uses: actions/checkout@v4

      - name: Log in to Azure
        uses: azure/login@v1
 with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

    - name: Log in to Azure Container Registry
        run: |
          az acr login --name ${{ env.ACR_NAME }}

      - name: Build and push Docker image
        run: |
    cd AzureTextReader
       docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }} .
          docker build -t ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest .
          docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:${{ github.sha }}
     docker push ${{ env.ACR_NAME }}.azurecr.io/${{ env.IMAGE_NAME }}:latest

  deploy-infrastructure:
    needs: build-docker-image
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
  steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Log in to Azure
        uses: azure/login@v1
     with:
       creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Azure Resources (ARM/Bicep)
        run: |
      az deployment group create \
         --resource-group ${{ env.AZURE_RESOURCE_GROUP }} \
     --template-file ./infrastructure/main.bicep \
         --parameters ./infrastructure/parameters.prod.json \
    --parameters containerImageTag=${{ github.sha }}

  deploy-function-app:
    needs: deploy-infrastructure
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
    - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET 9
uses: actions/setup-dotnet@v4
        with:
        dotnet-version: '9.0.x'

      - name: Build Function App
   run: |
          cd AzureTextReader/Azure.Functions
          dotnet build --configuration Release --output ./output

      - name: Deploy to Azure Functions
        uses: Azure/functions-action@v1
     with:
          app-name: ${{ env.FUNCTION_APP_NAME }}
     package: './AzureTextReader/Azure.Functions/output'
   publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}

  smoke-tests:
    needs: [deploy-infrastructure, deploy-function-app]
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Run smoke tests
        run: |
 # Upload test TIF file
  az storage blob upload \
            --account-name imageextractorstorage \
  --container-name input \
 --name pending/test-${{ github.sha }}.tif \
            --file ./test-data/sample.tif \
     --auth-mode key

          # Wait for processing
          sleep 60

          # Check if output exists
        az storage blob exists \
    --account-name imageextractorstorage \
     --container-name output \
    --name test-${{ github.sha }}/final_output_*_v2_dynamic.json
```

### 5.2 Azure DevOps Pipeline

**azure-pipelines.yml**:
```yaml
trigger:
  branches:
    include:
      - main
      - develop
  paths:
    include:
      - AzureTextReader/**

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'Azure-Prod-Subscription'
  resourceGroupName: 'rg-imageextractor-prod'
  acrName: 'imageextractoracr'
  imageName: 'imageextractor'

stages:
  - stage: Build
    displayName: 'Build and Test'
    jobs:
      - job: Build
displayName: 'Build Application'
        pool:
        vmImage: 'ubuntu-latest'
      steps:
   - task: UseDotNet@2
            displayName: 'Use .NET 9'
          inputs:
   packageType: 'sdk'
   version: '9.0.x'

- task: DotNetCoreCLI@2
            displayName: 'Restore NuGet packages'
     inputs:
     command: 'restore'
  projects: '**/ImageTextExtractor.csproj'

          - task: DotNetCoreCLI@2
 displayName: 'Build'
            inputs:
    command: 'build'
   projects: '**/ImageTextExtractor.csproj'
         arguments: '--configuration $(buildConfiguration)'

          - task: DotNetCoreCLI@2
       displayName: 'Run Tests'
       inputs:
  command: 'test'
              projects: '**/*Tests.csproj'
              arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage"'

          - task: PublishCodeCoverageResults@1
            displayName: 'Publish Code Coverage'
   inputs:
        codeCoverageTool: 'Cobertura'
 summaryFileLocation: '$(Agent.TempDirectory)/**/*coverage.cobertura.xml'

  - stage: BuildDockerImage
    displayName: 'Build Docker Image'
    dependsOn: Build
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - job: Docker
      displayName: 'Build and Push Docker Image'
   pool:
 vmImage: 'ubuntu-latest'
   steps:
          - task: Docker@2
       displayName: 'Build Docker Image'
     inputs:
     containerRegistry: '$(acrName)'
              repository: '$(imageName)'
              command: 'build'
          Dockerfile: 'AzureTextReader/Dockerfile'
      tags: |
  $(Build.BuildId)
       latest

          - task: Docker@2
            displayName: 'Push Docker Image'
            inputs:
 containerRegistry: '$(acrName)'
              repository: '$(imageName)'
       command: 'push'
          tags: |
    $(Build.BuildId)
            latest

  - stage: Deploy
    displayName: 'Deploy to Azure'
    dependsOn: BuildDockerImage
    condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
    jobs:
      - deployment: DeployInfrastructure
        displayName: 'Deploy Azure Resources'
     environment: 'production'
        pool:
          vmImage: 'ubuntu-latest'
        strategy:
       runOnce:
        deploy:
       steps:
          - task: AzureCLI@2
    displayName: 'Deploy ARM Template'
    inputs:
           azureSubscription: '$(azureSubscription)'
           scriptType: 'bash'
      scriptLocation: 'inlineScript'
             inlineScript: |
         az deployment group create \
          --resource-group $(resourceGroupName) \
       --template-file infrastructure/main.bicep \
     --parameters containerImageTag=$(Build.BuildId)

    - deployment: DeployFunctionApp
        displayName: 'Deploy Function App'
        dependsOn: DeployInfrastructure
     environment: 'production'
        pool:
          vmImage: 'ubuntu-latest'
        strategy:
    runOnce:
            deploy:
      steps:
     - task: AzureFunctionApp@1
     displayName: 'Deploy Azure Function'
              inputs:
      azureSubscription: '$(azureSubscription)'
      appType: 'functionApp'
           appName: 'imageextractor-func'
  package: '$(Pipeline.Workspace)/drop/functions'
```

---

## 6. Infrastructure as Code (Bicep)

**main.bicep**:
```bicep
@description('Environment name (dev, staging, prod)')
param environment string = 'prod'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Container image tag')
param containerImageTag string = 'latest'

var storageAccountName = 'imageextractor${environment}'
var functionAppName = 'imageextractor-func-${environment}'
var appServicePlanName = 'imageextractor-plan-${environment}'
var acrName = 'imageextractor${environment}acr'
var keyVaultName = 'imageextractor-kv-${environment}'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
  accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
}

// Blob Containers
resource inputContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/input'
  properties: {
    publicAccess: 'None'
  }
}

resource outputContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: '${storageAccount.name}/default/output'
  properties: {
    publicAccess: 'None'
  }
}

// Azure Container Registry
resource acr 'Microsoft.ContainerRegistry/registries@2023-01-01-preview' = {
  name: acrName
  location: location
  sku: {
  name: 'Standard'
  }
  properties: {
    adminUserEnabled: true
  }
}

// App Service Plan for Functions
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

// Azure Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
      {
          name: 'FUNCTIONS_WORKER_RUNTIME'
   value: 'dotnet-isolated'
}
        {
     name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
  }
        {
          name: 'STORAGE_CONNECTION_STRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value}'
   }
        {
    name: 'CONTAINER_IMAGE'
          value: '${acr.name}.azurecr.io/imageextractor:${containerImageTag}'
        }
    ]
    }
  }
}

// Event Grid System Topic
resource eventGridTopic 'Microsoft.EventGrid/systemTopics@2023-06-01-preview' = {
  name: 'imageextractor-storage-events'
  location: location
  properties: {
    source: storageAccount.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

// Event Grid Subscription
resource eventGridSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-06-01-preview' = {
  parent: eventGridTopic
  name: 'tif-file-subscription'
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        resourceId: '${functionApp.id}/functions/ProcessTifTrigger'
 }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
      ]
   subjectBeginsWith: '/blobServices/default/containers/input/blobs/pending/'
      subjectEndsWith: '.tif'
    }
    eventDeliverySchema: 'EventGridSchema'
    retryPolicy: {
  maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}

output storageAccountName string = storageAccount.name
output functionAppName string = functionApp.name
output acrName string = acr.name
```

---

## 7. Monitoring & Observability

### 7.1 Application Insights

**Configure in Program.cs**:
```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

var telemetryConfiguration = TelemetryConfiguration.CreateDefault();
telemetryConfiguration.ConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");

var telemetryClient = new TelemetryClient(telemetryConfiguration);

// Track custom events
telemetryClient.TrackEvent("ProcessingStarted", new Dictionary<string, string>
{
    ["JobId"] = jobId,
  ["BlobName"] = blobName
});

// Track metrics
telemetryClient.TrackMetric("ProcessingTimeMs", processingTime);

// Track dependencies
using (var operation = telemetryClient.StartOperation<DependencyTelemetry>("CallOpenAI"))
{
    // Your OpenAI call here
    operation.Telemetry.Success = true;
}
```

### 7.2 Log Analytics Queries

**Monitor processing status**:
```kusto
ProcessingJobs
| where Status == "Failed"
| summarize FailureCount = count() by bin(CreatedAt, 1h)
| render timechart

ProcessingJobs
| where Status == "Completed"
| extend ProcessingTime = datetime_diff('millisecond', CompletedAt, StartedAt)
| summarize avg(ProcessingTime), percentile(ProcessingTime, 95) by bin(CreatedAt, 1h)
```

### 7.3 Azure Monitor Alerts

**Create alerts for**:
- High failure rate (>10% in 5 minutes)
- Long processing time (>5 minutes)
- Container Instance failures
- Storage account throttling

---

## 8. Security Best Practices

### 8.1 Managed Identity

**Enable Managed Identity**:
```bash
# Enable system-assigned identity for Function App
az functionapp identity assign \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod

# Grant access to storage
az role assignment create \
  --assignee <managed-identity-principal-id> \
  --role "Storage Blob Data Contributor" \
  --scope <storage-account-resource-id>
```

### 8.2 Key Vault Integration

**Store secrets in Key Vault**:
```bash
# Create Key Vault
az keyvault create \
  --name imageextractor-kv \
  --resource-group rg-imageextractor-prod \
  --location eastus

# Add secrets
az keyvault secret set \
  --vault-name imageextractor-kv \
  --name "AzureOpenAIKey" \
  --value "<your-key>"

# Reference in Function App
az functionapp config appsettings set \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod \
  --settings "AZURE_OPENAI_KEY=@Microsoft.KeyVault(SecretUri=https://imageextractor-kv.vault.azure.net/secrets/AzureOpenAIKey/)"
```

---

## 9. Cost Estimation

**Monthly costs (approximate)**:

| Service | Usage | Cost (USD) |
|---------|-------|------------|
| Azure Storage (Blob) | 100 GB, 10K transactions/month | $2-5 |
| Azure Functions | 1M executions, 1GB-second | $0-20 (within free tier) |
| Container Instances | 10 runs/day, 5 min each, 1.5 GB RAM | $30-50 |
| Azure OpenAI GPT-4 | 1M input + 500K output tokens | $15-30 |
| Event Grid | 10K operations | $0.60 |
| Application Insights | 5 GB data | $10-15 |
| Azure SQL Database (Basic) | If used | $5 |
| **TOTAL** | | **$60-125/month** |

**Cost Optimization Tips**:
1. Use Azure Container Apps instead of Container Instances (scale-to-zero)
2. Implement caching to reduce OpenAI calls
3. Use cool/archive storage for processed files
4. Set blob lifecycle policies
5. Use reserved capacity for predictable workloads

---

## 10. Step-by-Step Deployment Guide

### Step 1: Prerequisites
```bash
# Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Install Docker
sudo apt-get install docker.io

# Login to Azure
az login

# Set subscription
az account set --subscription "<your-subscription-id>"
```

### Step 2: Create Resource Group
```bash
az group create \
  --name rg-imageextractor-prod \
  --location eastus
```

### Step 3: Deploy Infrastructure
```bash
# Deploy using Bicep
az deployment group create \
  --resource-group rg-imageextractor-prod \
  --template-file infrastructure/main.bicep \
  --parameters environment=prod
```

### Step 4: Build and Push Docker Image
```bash
# Build image
docker build -t imageextractor:latest ./AzureTextReader

# Tag for ACR
docker tag imageextractor:latest imageextractorprodacr.azurecr.io/imageextractor:latest

# Login to ACR
az acr login --name imageextractorprodacr

# Push image
docker push imageextractorprodacr.azurecr.io/imageextractor:latest
```

### Step 5: Deploy Function App
```bash
# Publish Function App
cd AzureTextReader/Azure.Functions
func azure functionapp publish imageextractor-func-prod
```

### Step 6: Configure Secrets
```bash
# Store Azure OpenAI credentials
az keyvault secret set \
  --vault-name imageextractor-kv-prod \
  --name "AzureEndpoint" \
  --value "<your-endpoint>"

az keyvault secret set \
--vault-name imageextractor-kv-prod \
  --name "AzureSubscriptionKey" \
  --value "<your-key>"
```

### Step 7: Test End-to-End
```bash
# Upload test file
az storage blob upload \
  --account-name imageextractorprod \
  --container-name input \
  --name pending/test-sample.tif \
  --file ./test-data/sample.tif

# Monitor logs
az monitor activity-log list \
  --resource-group rg-imageextractor-prod \
  --max-events 50

# Check results
az storage blob list \
  --account-name imageextractorprod \
  --container-name output \
  --prefix "test-"
```

---

## Summary Checklist

? **Architecture Design**: Event-driven, serverless, scalable  
? **Storage**: Blob Storage with lifecycle management  
? **Processing**: Container Instances triggered by Event Grid  
? **Database**: Azure SQL or Cosmos DB for results  
? **CI/CD**: GitHub Actions or Azure DevOps pipelines  
? **Monitoring**: Application Insights + Log Analytics  
? **Security**: Managed Identity + Key Vault  
? **Cost**: $60-125/month for moderate usage  

**Next Steps**:
1. Review architecture with your team
2. Set up Azure subscription and resource group
3. Deploy infrastructure using Bicep
4. Configure CI/CD pipeline
5. Test with sample TIF files
6. Monitor and optimize

---

**Questions? Need help with specific steps? Let me know!**
