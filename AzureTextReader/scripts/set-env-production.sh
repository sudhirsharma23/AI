#!/usr/bin/env bash
# Set environment variables for Production (bash session)
# Usage: source scripts/set-env-production.sh

export ASPNETCORE_ENVIRONMENT=Production
export AZURE_AI_ENDPOINT="https://<PROD_RESOURCE>.cognitiveservices.azure.com/"
export AZURE_AI_KEY="<REPLACE_WITH_PROD_KEY>"
export REDIS_CONNECTION_STRING="<REPLACE_WITH_REDIS_CONNECTION_STRING>"
export SERVICE_BUS_CONNECTION="<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

echo "Production environment variables set for this shell. Start the app as appropriate."