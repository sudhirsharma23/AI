# Azure Text Reader - Secure Configuration Guide

## ?? SECURITY NOTICE

**NEVER commit Azure AI Services keys to Git!**

This project uses secure configuration management to protect your Azure credentials.

---

## Setup Methods (Choose One)

### Method 1: User Secrets (Recommended for Development)

**Best for**: Local development, keeps secrets out of source code

```sh
# Navigate to project directory
cd AzureTextReader/src

# Initialize user secrets
dotnet user-secrets init

# Set your Azure AI credentials
dotnet user-secrets set "AzureAI:Endpoint" "https://your-resource.cognitiveservices.azure.com/"
dotnet user-secrets set "AzureAI:SubscriptionKey" "your-subscription-key-here"

# Optional: Set Redis connection (if using)
dotnet user-secrets set "Redis:ConnectionString" "your-redis-connection"
```

**Verify secrets are set:**
```sh
dotnet user-secrets list
```

**Where are secrets stored?**
- Windows: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- Linux/Mac: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

**Benefits:**
- ? Secrets never in source code
- ? Not committed to Git
- ? Different per developer
- ? Easy to manage

---

### Method 2: Environment Variables

**Best for**: Production, CI/CD pipelines, Docker

**Windows (PowerShell):**
```powershell
$env:AZURE_AI_ENDPOINT = "https://your-resource.cognitiveservices.azure.com/"
$env:AZURE_AI_KEY = "your-subscription-key-here"

# Permanent (requires restart):
[Environment]::SetEnvironmentVariable("AZURE_AI_ENDPOINT", "https://your-resource.cognitiveservices.azure.com/", "User")
[Environment]::SetEnvironmentVariable("AZURE_AI_KEY", "your-key", "User")
```

**Linux/Mac (bash):**
```bash
export AZURE_AI_ENDPOINT="https://your-resource.cognitiveservices.azure.com/"
export AZURE_AI_KEY="your-subscription-key-here"

# Add to ~/.bashrc or ~/.zshrc for persistence
echo 'export AZURE_AI_ENDPOINT="https://your-resource.cognitiveservices.azure.com/"' >> ~/.bashrc
echo 'export AZURE_AI_KEY="your-key"' >> ~/.bashrc
```

**Docker:**
```yaml
# docker-compose.yml
services:
  app:
    environment:
      - AZURE_AI_ENDPOINT=https://your-resource.cognitiveservices.azure.com/
      - AZURE_AI_KEY=your-key
```

---

### Method 3: appsettings.Development.json (Local Only)

**Best for**: Quick local testing

1. Create `appsettings.Development.json` (already in .gitignore):

```json
{
  "AzureAI": {
    "Endpoint": "https://your-resource.cognitiveservices.azure.com/",
    "SubscriptionKey": "your-actual-key-here"
  }
}
```

2. **IMPORTANT**: This file is in `.gitignore` and will NOT be committed

---

## Getting Your Azure Credentials

### From Azure Portal:

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your **Azure AI Services** resource
3. Go to **Keys and Endpoint** (left sidebar)
4. Copy:
   - **Endpoint**: `https://your-resource.cognitiveservices.azure.com/`
   - **Key 1** or **Key 2**: Your subscription key

---

## Usage in Code

The application automatically loads configuration in this priority:

1. **Environment Variables** (highest priority)
2. **User Secrets**
3. **appsettings.Development.json**
4. **appsettings.json** (default/template only)

### Example Usage:

```csharp
using ImageTextExtractor.Configuration;

// Load configuration
var config = AzureAIConfig.Load();
config.Validate();

// Use in your code
var client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(config.SubscriptionKey))
{
    Endpoint = config.Endpoint
};
```

---

## Verification

Run this to verify configuration is loaded:

```sh
cd AzureTextReader/src
dotnet run
```

You should see:
```
? Loaded Azure AI configuration from environment variables
? Azure AI Configuration validated: https://your-resource.cognitiveservices.azure.com/
```

---

## Troubleshooting

### Error: "Azure AI configuration not found"

**Solution**: Configuration not set. Use one of the methods above.

### Error: "Azure AI Endpoint must be a valid HTTPS URL"

**Solution**: Endpoint must start with `https://`

### Keys showing in Git status

**Solution**: 
1. Make sure `.gitignore` is updated
2. Remove files from Git tracking:
```sh
git rm --cached AzureTextReader/src/Program*.cs
git rm --cached appsettings.*.json
```

### Already committed keys by mistake?

**URGENT - Rotate your keys immediately:**
1. Go to Azure Portal ? Your AI Service ? Keys and Endpoint
2. Click **Regenerate Key 1** and **Regenerate Key 2**
3. Update your local configuration with new keys
4. Remove keys from Git history (optional, but recommended):
```sh
# This rewrites history - coordinate with your team!
git filter-branch --force --index-filter \
  'git rm --cached --ignore-unmatch AzureTextReader/src/Program*.cs' \
  --prune-empty --tag-name-filter cat -- --all
```

---

## Production Deployment

### Azure App Service

Set application settings in Azure Portal:
```
AZURE_AI_ENDPOINT = https://your-resource.cognitiveservices.azure.com/
AZURE_AI_KEY = your-key
```

### AWS Lambda

Set environment variables in Lambda configuration

### Docker

Pass environment variables:
```sh
docker run -e AZURE_AI_ENDPOINT=... -e AZURE_AI_KEY=... your-image
```

---

## Best Practices

? **DO:**
- Use User Secrets for local development
- Use Environment Variables for production
- Rotate keys regularly
- Use separate keys for dev/staging/production
- Store production keys in Azure Key Vault

? **DON'T:**
- Hardcode keys in source code
- Commit keys to Git
- Share keys in Slack/Teams/Email
- Use production keys in development
- Log full subscription keys

---

## Quick Reference

### Check configuration is working:
```sh
dotnet run --project AzureTextReader/src
```

### List user secrets:
```sh
dotnet user-secrets list --project AzureTextReader/src
```

### Remove a secret:
```sh
dotnet user-secrets remove "AzureAI:SubscriptionKey" --project AzureTextReader/src
```

### Clear all secrets:
```sh
dotnet user-secrets clear --project AzureTextReader/src
```

---

## Need Help?

- **Azure AI Services Documentation**: https://learn.microsoft.com/azure/cognitive-services/
- **User Secrets Guide**: https://learn.microsoft.com/aspnet/core/security/app-secrets
- **Environment Variables**: https://learn.microsoft.com/dotnet/core/tools/dotnet-environment-variables

---

**Remember: Never commit secrets to Git! ??**
