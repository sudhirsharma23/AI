<#
Set environment variables for Production (PowerShell - session)
Usage: Run on production host or in a production shell session before starting the app.
#>

Write-Host "Setting Production environment variables for this PowerShell session..."

$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AZURE_AI_ENDPOINT = "https://<PROD_RESOURCE>.cognitiveservices.azure.com/"
$env:AZURE_AI_KEY = "<REPLACE_WITH_PROD_KEY>"
$env:REDIS_CONNECTION_STRING = "<REPLACE_WITH_REDIS_CONNECTION_STRING>"
$env:SERVICE_BUS_CONNECTION = "<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

Write-Host "Environment variables set for this session. Start the app as appropriate (systemd, service, container, etc)."