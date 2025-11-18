# Production Aspose Configuration for TextractProcessor

This document explains how to configure and deploy Aspose.Total OCR in production for the `TextractProcessor` project. Follow these steps to enable Aspose OCR in Lambda and ensure the license and runtime settings are applied correctly.

---

##1. Overview

Goal: run `AsposeOcrService` in production Lambda (or other compute) so the project uses Aspose instead of AWS Textract.

Key items:
- Place the Aspose license in the deployment package.
- Set environment variables to select Aspose and configure license path and caching.
- Ensure Lambda has sufficient memory/timeout for OCR processing.
- Use CI/CD to include the license file and publish the Lambda.

---

##2. Prerequisites

- Aspose.Total NuGet package is already referenced in `TextractProcessor.csproj`.
- Valid Aspose license file (commonly `Aspose.Total.NET.lic` or `Aspose.Total.lic`).
- AWS account with IAM role for the Lambda that has S3 read permissions.
- `TextractProcessor` project builds successfully locally.

---

##3. License placement (required)

1. Place the Aspose license file in the project root (next to the `.csproj`):
 - Example: `TextractProcessor/src/TextractProcessor/Aspose.Total.NET.lic`
2. The project file already contains entries that copy `Aspose.Total.NET.lic` to the output directory on build:
 - Confirm in `TextractProcessor.csproj` there is a `<None Update="Aspose.Total.NET.lic">` with `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`.
3. When packaging the Lambda, the license will be included in the published output. If you store the license elsewhere, set `ASPOSE_LICENSE_PATH` accordingly.

---

##4. Required environment variables (Lambda configuration)

Set the following Lambda environment variables (Console, CloudFormation, SAM, or CLI):

- `OCR_ENGINE=Aspose` — selects Aspose OCR at runtime.
- `ASPOSE_LICENSE_PATH=Aspose.Total.NET.lic` — relative path from the application base directory to the license file. Change if you place the license under a subfolder.
- `OCR_ENABLE_CACHING=true` — enable in-memory caching for OCR results.
- `OCR_CACHE_DURATION_DAYS=30` — cache TTL.

Example CLI update:

`aws lambda update-function-configuration --function-name textract-processor --environment Variables='{"OCR_ENGINE":"Aspose","ASPOSE_LICENSE_PATH":"Aspose.Total.NET.lic","OCR_ENABLE_CACHING":"true"}'`

---

##5. Lambda runtime settings

For reliable Aspose processing set:
- Memory: start at `1024 MB` and increase if you see out-of-memory issues for large multi-page documents.
- Timeout: set to `900` seconds (15 minutes) for large jobs.
- Architecture: keep `x86_64` unless you need `arm64` and have validated the Aspose native dependencies.

Adjust in console or via IaC.

---

##6. IAM permissions

Lambda role must include at minimum:
- `s3:GetObject`, `s3:ListBucket` for the input bucket(s).
- `s3:PutObject` if the function writes output JSON back to S3.
- CloudWatch Logs permissions (`logs:CreateLogGroup`, `logs:CreateLogStream`, `logs:PutLogEvents`).

Do not grant `*` permissions — scope to the bucket ARNs.

---

##7. Packaging & Deployment

Options:

- Manual (local):
1. Ensure license file is in project and included in the csproj.
2. `dotnet publish -c Release -r linux-x64 --self-contained false` (or use your CI command).
3. Create deployment ZIP from publish folder and upload to Lambda or use `dotnet lambda deploy-function`.

- CI/CD: ensure pipeline copies the license into the build artifact before zipping/publishing.

Example `dotnet lambda deploy-function` usage (ensure AWS credentials are configured):

`dotnet lambda deploy-function textract-processor --function-role arn:aws:iam::123456789012:role/lambda-textract-processor-role --region us-east-1`

If using containers, include the license in the image and set `ASPOSE_LICENSE_PATH` to the location inside the image.

---

##8. Testing & Verification

1. Deploy Lambda with `OCR_ENGINE=Aspose`.
2. Upload a test `.tif` to S3.
3. Invoke the function (or upload to trigger S3 event) and check CloudWatch logs:
 - Expect logs mentioning `Aspose license loaded successfully` (if license present) or a warning about evaluation mode.
 - Expect `Aspose OCR processing file: <file>` and returned JSON output written to `CachedFiles_OutputFiles` or S3.
4. If no license message appears, verify `ASPOSE_LICENSE_PATH` path and that the license file exists in the Lambda package.

---

##9. Monitoring & Troubleshooting

- Monitor CloudWatch Logs for errors and memory usage.
- Use X-Ray for tracing (optional).
- Common issues:
 - "License file not found": verify `ASPOSE_LICENSE_PATH` and confirm file in deployment package.
 - OutOfMemory errors: increase Lambda memory.
 - Long processing times: consider buffering, increasing timeout, or using larger memory.

---

##10. CI/CD recommendations

- GitHub Actions: simple to integrate and works well with AWS. Create workflow to restore, build, publish and deploy using `aws-actions/configure-aws-credentials` and `aws lambda update-function-code` / `dotnet lambda deploy-function`.
- AWS CodePipeline/CodeBuild: use if you want native AWS integration and tighter IAM controls.
- Azure DevOps: also possible, but adds cross-cloud complexity; prefer GitHub Actions or AWS native for simplicity.

Checklist for CI jobs:
- Checkout code
- Restore NuGet packages
- Build and publish (include license file)
- Zip artifacts and upload to S3 (or use direct Lambda update)
- Run integration tests (optional)
- Deploy to Lambda

---

##11. File references in repository

- `TextractProcessor/src/TextractProcessor/Aspose.Total.NET.lic` — recommended location for the license (project already copies it to output).
- `TextractProcessor/src/TextractProcessor/TextractProcessor.csproj` — confirm `<None Update="Aspose.Total.NET.lic">` exists.

---

If you want, I can add a GitHub Actions workflow that builds, packages and deploys the Lambda including the license. Tell me your preferred deployment method and I will add the pipeline file.
