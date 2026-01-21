<#
Set environment variables for Staging (PowerShell - session)
Usage: Run on staging host or in a staging shell session before starting the app.
#>

Write-Host "Setting Staging environment variables for this PowerShell session..."

$env:ASPNETCORE_ENVIRONMENT = "Staging"
$env:AZURE_AI_ENDPOINT = "https://<STAGE_RESOURCE>.cognitiveservices.azure.com/"
$env:AZURE_AI_KEY = "<REPLACE_WITH_STAGE_KEY>"
$env:REDIS_CONNECTION_STRING = "<REPLACE_WITH_REDIS_CONNECTION_STRING>"
$env:SERVICE_BUS_CONNECTION = "<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

Write-Host "Environment variables set for this session. Start the app as appropriate (systemd, service, container, etc)."