#!/usr/bin/env bash
# Set environment variables for Staging (bash session)
# Usage: source scripts/set-env-staging.sh

export ASPNETCORE_ENVIRONMENT=Staging
export AZURE_AI_ENDPOINT="https://<STAGE_RESOURCE>.cognitiveservices.azure.com/"
export AZURE_AI_KEY="<REPLACE_WITH_STAGE_KEY>"
export REDIS_CONNECTION_STRING="<REPLACE_WITH_REDIS_CONNECTION_STRING>"
export SERVICE_BUS_CONNECTION="<REPLACE_WITH_SERVICE_BUS_CONNECTION>"

echo "Staging environment variables set for this shell. Start the app as appropriate."