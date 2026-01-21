<#
Set environment variables for local Development (PowerShell - session)
Usage: Open PowerShell in repo root and run:
  .\scripts\set-env-development.ps1
This sets values for the current session only. Replace placeholders with real values or use dotnet user-secrets where appropriate.
#>

Write-Host "Setting Development environment variables for this PowerShell session..."

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:AZURE_AI_ENDPOINT = "https://sudhir-ai-test.cognitiveservices.azure.com/"
$env:AZURE_AI_KEY = "<REPLACE_WITH_DEVELOPMENT_KEY>"
$env:REDIS_CONNECTION_STRING = "<REPLACE_WITH_REDIS_CONNECTION_STRING>"
$env:SERVICE_BUS_CONNECTION = "<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

Write-Host "Environment variables set for this session. Run 'dotnet run --project src' from the repo root."