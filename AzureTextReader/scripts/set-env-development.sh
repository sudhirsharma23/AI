#!/usr/bin/env bash
# Set environment variables for Development (bash session)
# Usage: source scripts/set-env-development.sh

export ASPNETCORE_ENVIRONMENT=Development
export AZURE_AI_ENDPOINT="https://sudhir-ai-test.cognitiveservices.azure.com/"
export AZURE_AI_KEY="<REPLACE_WITH_DEVELOPMENT_KEY>"
export REDIS_CONNECTION_STRING="<REPLACE_WITH_REDIS_CONNECTION_STRING>"
export SERVICE_BUS_CONNECTION="<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

echo "Development environment variables set for this shell. Run: dotnet run --project src"