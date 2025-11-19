# AzureTextReader — Test Cases

This document lists the current unit tests and recommended test cases for the project. It includes how to run tests locally and suggestions for additional integration and E2E tests.

## Current Unit Tests

### FileMonitorUtilsTests
- Location: `test/AzureTextReader.Tests/FileMonitorUtilsTests.cs`
- Purpose: Verify file stability, hashing, and state index helpers used by the file-monitoring pipeline.
- Test cases:
 - `ComputeFileHashAsync_ReturnsDifferentHashAfterChange`
 - Description: Compute SHA256 hash of a file, change the file, compute again and assert hashes differ.
 - Expected: The two hashes are not equal.
 - `IsFileStable_DetectsStability`
 - Description: Verify `IsFileStable` returns true when the file's last seen time and last write are older than the stability threshold.
 - Expected: Returns true for stable conditions.
 - `LoadProcessedHashes_ReturnsEmptyForMissing`
 - Description: Load processed hashes from a temporary state folder that does not contain an index file.
 - Expected: Returns an empty (but non-null) set.

## How to run unit tests

- Restore packages and run tests (from repository root):

```
dotnet restore
dotnet test test/AzureTextReader.Tests/AzureTextReader.Tests.csproj
```

- Run a single test by fully qualified name (xUnit filter):

```
dotnet test --filter FullyQualifiedName~FileMonitorUtilsTests
```

## Integration / E2E Test Suggestions

These are recommended tests to add (not yet implemented):

1. `FileMonitor_ProcessFile_E2E`
 - Drop a small sample image/PDF into `incoming/` while the app host runs (in test harness).
 - Wait/poll for output JSON in `processed/` and assert expected fields exist.
 - Assertions: output file exists; processed hash recorded in `state/processed_hashes.json`; JSON contains `PlainText` and non-empty `Markdown`.

2. `Api_Process_Endpoint`
 - Start the app host (in-memory or container) and POST `{"imageUrl":"<url>"}` to `/api/process`.
 - Poll incoming folder (or `/api/status`) until the job is processed.
 - Assertions: accepted response, output file produced.

3. `Idempotency_DuplicateFiles`
 - Place the same file twice into `incoming/`.
 - Ensure only one processed JSON is produced and second file is deduplicated.

4. `ServiceBus_Queue_Processing`
 - With `SERVICE_BUS_CONNECTION` configured for a test namespace, verify monitor enqueues messages and ServiceBusProcessor consumers process messages and produce outputs.

5. `Failure_Retry_And_FailedFolder`
 - Simulate OCR failures (invalid file or temporarily break OCR service) and verify retry behavior then movement to `failed/` after max retries.

## Test Infrastructure / CI Notes

- Tests should run on CI with .NET9 SDK available.
- For integration tests that require Aspose or Azure OCR keys, use environment variables or GitHub Actions secrets (do not commit keys to repo).
- To run Service Bus integration tests, provision a short-lived Service Bus namespace and set `SERVICE_BUS_CONNECTION` via CI secrets.
- Use an isolated temporary directory for integration tests and clean up artifacts after each test run.

## Recommended Additional Unit Tests

- `FileMonitor_BackoffLogic` — validate exponential backoff calculation.
- `FileMonitor_GetUniquePath` — ensure unique naming avoids collisions.
- `FileMonitor_ScanAndEnqueue_DetectsPartialWrites` — simulate partial write behavior.
- `OcrService_FailureResponse` — mock `IOcrService` to simulate failures and ensure the monitor moves files to `failed/`.

## Contact / Maintenance

- Keep `test/AzureTextReader.Tests` in sync with code changes, update tests when behavior or configuration changes.
- For any environment-specific tests (Aspose license, Azure credentials), add gating so unit tests do not fail when secrets are missing.

