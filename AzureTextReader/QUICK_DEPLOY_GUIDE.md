# Azure ImageTextExtractor - Quick Start Deployment Guide

## ?? Fast Track: Get to Production in 1 Day

### Prerequisites (10 minutes)
```bash
# 1. Install Azure CLI
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# 2. Install Docker
sudo apt-get update && sudo apt-get install -y docker.io

# 3. Login to Azure
az login

# 4. Set your subscription
az account set --subscription "Your-Subscription-Name"

# 5. Install Azure Functions Core Tools
npm install -g azure-functions-core-tools@4
```

---

## ?? Deployment Checklist

### Phase 1: Infrastructure Setup (30 minutes)

#### Create Resource Group
```bash
az group create \
  --name rg-imageextractor-prod \
  --location eastus
```

#### Create Storage Account
```bash
az storage account create \
  --name imageextractorstorage \
  --resource-group rg-imageextractor-prod \
  --location eastus \
  --sku Standard_LRS \
  --kind StorageV2

# Get connection string
az storage account show-connection-string \
  --name imageextractorstorage \
  --resource-group rg-imageextractor-prod \
  --query connectionString -o tsv
# Save this! You'll need it later
```

#### Create Blob Containers
```bash
CONN_STRING="<your-connection-string-from-above>"

# Input containers
az storage container create --name input --connection-string "$CONN_STRING"
az storage blob directory create --container-name input --directory-path pending --connection-string "$CONN_STRING"
az storage blob directory create --container-name input --directory-path processing --connection-string "$CONN_STRING"
az storage blob directory create --container-name input --directory-path failed --connection-string "$CONN_STRING"

# Processed container
az storage container create --name processed --connection-string "$CONN_STRING"

# Output container
az storage container create --name output --connection-string "$CONN_STRING"
```

#### Create Azure Container Registry
```bash
az acr create \
  --name imageextractoracr \
  --resource-group rg-imageextractor-prod \
  --sku Standard \
  --admin-enabled true

# Get ACR credentials
az acr credential show --name imageextractoracr
# Save username and password!
```

#### Create Key Vault
```bash
az keyvault create \
  --name imageextractor-kv \
  --resource-group rg-imageextractor-prod \
  --location eastus

# Store Azure OpenAI secrets
az keyvault secret set \
  --vault-name imageextractor-kv \
  --name "AzureOpenAIEndpoint" \
  --value "https://your-openai.openai.azure.com"

az keyvault secret set \
  --vault-name imageextractor-kv \
  --name "AzureOpenAIKey" \
  --value "your-openai-api-key"
```

---

### Phase 2: Build and Deploy Container (30 minutes)

#### Build Docker Image
```bash
cd E:\Sudhir\GitRepo\AzureTextReader

# Build image
docker build -t imageextractor:latest -f Dockerfile .

# Tag for ACR
docker tag imageextractor:latest imageextractoracr.azurecr.io/imageextractor:latest

# Login to ACR
az acr login --name imageextractoracr

# Push to ACR
docker push imageextractoracr.azurecr.io/imageextractor:latest
```

#### Test Container Locally (Optional)
```bash
docker run -it --rm \
  -e JOB_ID="test-001" \
  -e BLOB_NAME="test.tif" \
  -e STORAGE_CONNECTION_STRING="your-connection-string" \
  -e AZURE_OPENAI_ENDPOINT="your-endpoint" \
  -e AZURE_OPENAI_KEY="your-key" \
  imageextractor:latest
```

---

### Phase 3: Deploy Function App (30 minutes)

#### Create Function App
```bash
# Create App Service Plan
az functionapp plan create \
  --name imageextractor-plan \
  --resource-group rg-imageextractor-prod \
  --location eastus \
  --sku Y1 \
  --is-linux

# Create Function App
az functionapp create \
  --name imageextractor-func \
  --storage-account imageextractorstorage \
  --plan imageextractor-plan \
  --resource-group rg-imageextractor-prod \
  --runtime dotnet-isolated \
  --functions-version 4
```

#### Configure Function App Settings
```bash
az functionapp config appsettings set \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod \
  --settings \
    STORAGE_CONNECTION_STRING="your-connection-string" \
    AZURE_SUBSCRIPTION_ID="your-subscription-id" \
    RESOURCE_GROUP_NAME="rg-imageextractor-prod" \
  ACR_NAME="imageextractoracr" \
    CONTAINER_IMAGE="imageextractoracr.azurecr.io/imageextractor:latest"
```

#### Deploy Function Code
```bash
cd Azure.Functions
func azure functionapp publish imageextractor-func
```

---

### Phase 4: Configure Event Grid (15 minutes)

#### Create Event Grid System Topic
```bash
az eventgrid system-topic create \
  --name imageextractor-storage-events \
  --resource-group rg-imageextractor-prod \
  --location eastus \
  --topic-type Microsoft.Storage.StorageAccounts \
  --source /subscriptions/<your-sub-id>/resourceGroups/rg-imageextractor-prod/providers/Microsoft.Storage/storageAccounts/imageextractorstorage
```

#### Create Event Subscription
```bash
# Get Function resource ID
FUNCTION_ID=$(az functionapp function show \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod \
  --function-name ProcessTifTrigger \
  --query id -o tsv)

# Create subscription
az eventgrid system-topic event-subscription create \
  --name tif-file-subscription \
  --system-topic-name imageextractor-storage-events \
  --resource-group rg-imageextractor-prod \
  --endpoint-type azurefunction \
  --endpoint $FUNCTION_ID \
  --included-event-types Microsoft.Storage.BlobCreated \
  --subject-begins-with /blobServices/default/containers/input/blobs/pending/ \
  --subject-ends-with .tif
```

---

### Phase 5: Setup Database (Optional, 15 minutes)

#### Option A: Azure SQL Database
```bash
# Create SQL Server
az sql server create \
  --name imageextractor-sql \
  --resource-group rg-imageextractor-prod \
  --location eastus \
  --admin-user sqladmin \
  --admin-password 'YourP@ssw0rd123!'

# Create Database
az sql db create \
  --name imageextractor-db \
  --server imageextractor-sql \
  --resource-group rg-imageextractor-prod \
  --service-objective S0

# Allow Azure services to access
az sql server firewall-rule create \
  --server imageextractor-sql \
  --resource-group rg-imageextractor-prod \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Get connection string
az sql db show-connection-string \
  --client ado.net \
  --server imageextractor-sql \
  --name imageextractor-db
```

#### Option B: Azure Cosmos DB
```bash
# Create Cosmos DB account
az cosmosdb create \
  --name imageextractor-cosmos \
  --resource-group rg-imageextractor-prod \
  --locations regionName=eastus

# Create database
az cosmosdb sql database create \
  --account-name imageextractor-cosmos \
  --resource-group rg-imageextractor-prod \
  --name ImageExtractor

# Create container
az cosmosdb sql container create \
  --account-name imageextractor-cosmos \
  --database-name ImageExtractor \
  --resource-group rg-imageextractor-prod \
  --name extraction-results \
  --partition-key-path /jobId \
  --throughput 400

# Get connection string
az cosmosdb keys list \
  --name imageextractor-cosmos \
  --resource-group rg-imageextractor-prod \
  --type connection-strings \
  --query "connectionStrings[0].connectionString" -o tsv
```

---

### Phase 6: Testing (10 minutes)

#### Upload Test File
```bash
# Upload a test TIF file
az storage blob upload \
  --account-name imageextractorstorage \
  --container-name input \
  --name pending/test-001.tif \
  --file ./test-data/sample.tif \
  --connection-string "$CONN_STRING"

# Wait 30-60 seconds for processing

# Check output
az storage blob list \
  --account-name imageextractorstorage \
  --container-name output \
  --prefix "test-001" \
  --connection-string "$CONN_STRING"

# Download results
az storage blob download \
  --account-name imageextractorstorage \
  --container-name output \
  --name "test-001/final_output_*_v2_dynamic.json" \
  --file ./results.json \
  --connection-string "$CONN_STRING"

# View results
cat ./results.json | jq .
```

---

### Phase 7: Monitor and Verify (5 minutes)

#### Check Function Logs
```bash
az monitor activity-log list \
  --resource-group rg-imageextractor-prod \
  --max-events 20

# Stream function logs
func azure functionapp logstream imageextractor-func
```

#### Check Container Logs
```bash
# List container groups
az container list \
  --resource-group rg-imageextractor-prod \
  --output table

# View logs for specific job
az container logs \
  --resource-group rg-imageextractor-prod \
  --name imageextractor-<job-id>
```

---

## ?? Verification Checklist

After deployment, verify:

- [ ] Storage account created with 3 containers
- [ ] ACR created and Docker image pushed
- [ ] Function App deployed and running
- [ ] Event Grid subscription active
- [ ] Key Vault secrets stored
- [ ] Test file uploaded successfully
- [ ] Event Grid triggered function
- [ ] Container Instance created and ran
- [ ] Results uploaded to output container
- [ ] Source file moved to archive

---

## ?? Common Issues & Solutions

### Issue 1: Event Grid not triggering
**Solution:**
```bash
# Check Event Grid subscription status
az eventgrid system-topic event-subscription show \
  --name tif-file-subscription \
  --system-topic-name imageextractor-storage-events \
  --resource-group rg-imageextractor-prod

# Check Function App is running
az functionapp list --resource-group rg-imageextractor-prod --query "[].state"

# Enable diagnostic logs
az monitor diagnostic-settings create \
  --name eventgrid-diagnostics \
  --resource <event-grid-resource-id> \
  --logs '[{"category":"DeliveryFailures","enabled":true}]' \
  --workspace <log-analytics-workspace-id>
```

### Issue 2: Container Instance fails to start
**Solution:**
```bash
# Check Container Registry access
az acr check-health --name imageextractoracr

# Verify Function App has permission
az functionapp identity assign \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod

# Grant ACR pull permission
PRINCIPAL_ID=$(az functionapp identity show \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod \
  --query principalId -o tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "AcrPull" \
  --scope $(az acr show --name imageextractoracr --query id -o tsv)
```

### Issue 3: OpenAI API errors
**Solution:**
```bash
# Verify Key Vault access
az keyvault set-policy \
  --name imageextractor-kv \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list

# Test secret retrieval
az keyvault secret show \
  --vault-name imageextractor-kv \
  --name AzureOpenAIKey \
  --query value -o tsv
```

---

## ?? Monitoring Dashboard

### Application Insights Query
```kusto
// Track processing jobs
customEvents
| where name == "ProcessingStarted" or name == "ProcessingCompleted"
| summarize 
    StartCount = countif(name == "ProcessingStarted"),
    CompleteCount = countif(name == "ProcessingCompleted")
  by bin(timestamp, 1h)
| render timechart

// Average processing time
customMetrics
| where name == "ProcessingTimeMs"
| summarize avg(value), percentile(value, 95) by bin(timestamp, 1h)
| render timechart

// Error rate
traces
| where severityLevel >= 3
| summarize ErrorCount = count() by bin(timestamp, 5m)
| render timechart
```

---

## ?? Cost Management

### Set Budget Alert
```bash
az consumption budget create \
  --budget-name imageextractor-monthly \
  --amount 150 \
  --resource-group rg-imageextractor-prod \
  --time-grain Monthly \
  --time-period start-date=2025-01-01 \
  --notifications \
    '{"enabled":true,"operator":"GreaterThan","threshold":80,"contactEmails":["admin@example.com"]}'
```

### Enable Cost Analysis
```bash
# Tag resources for cost tracking
az tag create \
  --resource-id $(az group show --name rg-imageextractor-prod --query id -o tsv) \
  --tags Project=ImageExtractor Environment=Production
```

---

## ?? Security Hardening

### Enable Managed Identity
```bash
# Function App
az functionapp identity assign \
  --name imageextractor-func \
  --resource-group rg-imageextractor-prod

# Grant Storage permissions
az role assignment create \
  --assignee <managed-identity-principal-id> \
--role "Storage Blob Data Contributor" \
  --scope $(az storage account show --name imageextractorstorage --query id -o tsv)
```

### Enable Private Endpoints (Optional)
```bash
# Create VNet
az network vnet create \
  --name imageextractor-vnet \
  --resource-group rg-imageextractor-prod \
  --address-prefix 10.0.0.0/16 \
  --subnet-name default \
  --subnet-prefix 10.0.0.0/24

# Create private endpoint for storage
az network private-endpoint create \
  --name storage-private-endpoint \
  --resource-group rg-imageextractor-prod \
  --vnet-name imageextractor-vnet \
  --subnet default \
  --private-connection-resource-id $(az storage account show --name imageextractorstorage --query id -o tsv) \
  --group-id blob \
  --connection-name storage-connection
```

---

## ?? CI/CD Quick Setup

### GitHub Secrets to Add
```
AZURE_CREDENTIALS
AZURE_SUBSCRIPTION_ID
AZURE_RESOURCE_GROUP
ACR_NAME
ACR_USERNAME
ACR_PASSWORD
STORAGE_CONNECTION_STRING
FUNCTION_APP_NAME
```

### Trigger First Deployment
```bash
git add .
git commit -m "Deploy ImageTextExtractor to Azure"
git push origin main

# Monitor GitHub Actions
gh run watch
```

---

## ? Final Verification Steps

1. **Upload a real TIF file** to `input/pending/`
2. **Wait 1-2 minutes** for processing
3. **Check output** in `output/` container
4. **Verify JSON results** are correct
5. **Check database** (if configured) for entries
6. **Review logs** in Application Insights
7. **Test error handling** by uploading invalid file
8. **Verify failed file** moved to `input/failed/`

---

## ?? Support & Resources

- **Azure Documentation**: https://docs.microsoft.com/azure
- **Azure Container Instances**: https://docs.microsoft.com/azure/container-instances/
- **Azure Functions**: https://docs.microsoft.com/azure/azure-functions/
- **Event Grid**: https://docs.microsoft.com/azure/event-grid/

---

**Deployment Time**: ~2 hours  
**Monthly Cost**: $60-125  
**Scalability**: Handles 1000+ TIF files/day  
**Reliability**: 99.95% SLA with Azure services  

?? **You're now production-ready!**
