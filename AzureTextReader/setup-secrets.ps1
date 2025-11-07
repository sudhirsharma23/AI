# Azure Text Reader - Quick Setup Script
# This script helps you set up Azure AI credentials securely

Write-Host "================================" -ForegroundColor Cyan
Write-Host "Azure Text Reader - Secure Setup" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "This script will help you configure Azure AI Services credentials securely." -ForegroundColor Yellow
Write-Host "Your keys will NEVER be committed to Git." -ForegroundColor Green
Write-Host ""

# Check if we're in the correct directory
$projectPath = "AzureTextReader/src"
if (-not (Test-Path $projectPath)) {
    Write-Host "ERROR: Please run this script from the repository root directory" -ForegroundColor Red
    exit 1
}

Write-Host "Choose your configuration method:" -ForegroundColor Cyan
Write-Host "1. User Secrets (Recommended for local development)" -ForegroundColor White
Write-Host "2. Environment Variables (For this PowerShell session)" -ForegroundColor White
Write-Host "3. Environment Variables (Permanent - User level)" -ForegroundColor White
Write-Host ""

$choice = Read-Host "Enter your choice (1-3)"

# Get Azure credentials
Write-Host ""
Write-Host "Enter your Azure AI Services credentials:" -ForegroundColor Cyan
Write-Host "(You can find these in Azure Portal ? Your AI Service ? Keys and Endpoint)" -ForegroundColor Yellow
Write-Host ""

$endpoint = Read-Host "Azure AI Endpoint (e.g., https://your-resource.cognitiveservices.azure.com/)"
$key = Read-Host "Azure AI Subscription Key" -AsSecureString
$keyPlainText = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($key))

# Validate inputs
if ([string]::IsNullOrWhiteSpace($endpoint) -or [string]::IsNullOrWhiteSpace($keyPlainText)) {
    Write-Host "ERROR: Endpoint and Key are required!" -ForegroundColor Red
    exit 1
}

if (-not $endpoint.StartsWith("https://")) {
    Write-Host "ERROR: Endpoint must start with https://" -ForegroundColor Red
    exit 1
}

Write-Host ""

switch ($choice) {
    "1" {
        # User Secrets
   Write-Host "Setting up User Secrets..." -ForegroundColor Cyan
        
 Push-Location $projectPath
        try {
            # Initialize user secrets if not already done
         dotnet user-secrets init
            
            # Set the secrets
  dotnet user-secrets set "AzureAI:Endpoint" $endpoint
            dotnet user-secrets set "AzureAI:SubscriptionKey" $keyPlainText
            
   Write-Host ""
            Write-Host "? User Secrets configured successfully!" -ForegroundColor Green
 Write-Host ""
    Write-Host "Your secrets are stored at:" -ForegroundColor Yellow
            Write-Host "  Windows: %APPDATA%\Microsoft\UserSecrets\azure-text-reader-secrets\secrets.json" -ForegroundColor Gray
            Write-Host "  They are NOT in your source code and will NOT be committed to Git" -ForegroundColor Gray
            Write-Host ""
   Write-Host "To view your secrets:" -ForegroundColor Cyan
   Write-Host "  dotnet user-secrets list --project $projectPath" -ForegroundColor White
        }
      finally {
          Pop-Location
      }
    }
    "2" {
        # Session Environment Variables
    Write-Host "Setting environment variables for this session..." -ForegroundColor Cyan

        $env:AZURE_AI_ENDPOINT = $endpoint
  $env:AZURE_AI_KEY = $keyPlainText
        
      Write-Host ""
        Write-Host "? Environment variables set for this PowerShell session!" -ForegroundColor Green
        Write-Host ""
  Write-Host "NOTE: These variables will be lost when you close this window." -ForegroundColor Yellow
        Write-Host "To make them permanent, choose option 3." -ForegroundColor Yellow
    }
    "3" {
      # Permanent Environment Variables
    Write-Host "Setting permanent environment variables (User level)..." -ForegroundColor Cyan
     
  [Environment]::SetEnvironmentVariable("AZURE_AI_ENDPOINT", $endpoint, "User")
        [Environment]::SetEnvironmentVariable("AZURE_AI_KEY", $keyPlainText, "User")
        
      # Also set for current session
        $env:AZURE_AI_ENDPOINT = $endpoint
      $env:AZURE_AI_KEY = $keyPlainText
        
        Write-Host ""
        Write-Host "? Permanent environment variables set!" -ForegroundColor Green
        Write-Host ""
        Write-Host "NOTE: You may need to restart your terminal/IDE for changes to take effect." -ForegroundColor Yellow
    }
    default {
    Write-Host "Invalid choice. Exiting." -ForegroundColor Red
exit 1
    }
}

Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "1. Run your application: dotnet run --project $projectPath" -ForegroundColor White
Write-Host "2. Your Azure credentials are now secure and NOT in source code" -ForegroundColor White
Write-Host "3. You can safely commit your code to Git" -ForegroundColor White
Write-Host ""
Write-Host "Security reminders:" -ForegroundColor Yellow
Write-Host "- NEVER commit Azure keys to Git" -ForegroundColor Red
Write-Host "- Keep appsettings.Development.json in .gitignore" -ForegroundColor Red
Write-Host "- Rotate your keys regularly in Azure Portal" -ForegroundColor Red
Write-Host ""
Write-Host "For more information, see: AzureTextReader/SECURITY_SETUP.md" -ForegroundColor Cyan
