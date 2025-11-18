# Production Upload Process

**Upload Files to S3 for Document Processing**

> **Process:** TextractUploader  
> **Purpose:** Upload documents to S3 in date-based folder structure  
> **Last Updated:** January 2025

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Folder Structure](#folder-structure)
4. [Step-by-Step Process](#step-by-step-process)
5. [Configuration](#configuration)
6. [Verification](#verification)
7. [Troubleshooting](#troubleshooting)

---

## Overview

### What This Process Does

The upload process prepares and uploads documents to Amazon S3 in a structured format that enables:
- **Date-based organization** - Files grouped by upload date
- **Document isolation** - Each document in its own folder
- **Easy retrieval** - Predictable S3 key patterns
- **Batch processing** - Multiple documents uploaded at once

### Upload Flow

```
???????????????????????????????????????????????????????????????
?  LOCAL FILES  ?  TEXTRACT UPLOADER  ?  S3 BUCKET   ?
???????????????????????????????????????????????????????????????
?         ?
?  UploadFiles/           Program.cs        s3://bucket/       ?
?    file.tif       ?     ????????????  ?  uploads/           ?
?    doc.pdf        ?  Upload  ?      2025-01-20/       ?
?    scan.jpg ?  Logic   ?      file/         ?
?          ????????????          file.tif       ?
?doc/   ?
?        doc.pdf     ?
???????????????????????????????????????????????????????????????
```

---

## Prerequisites

### Required Components

? **AWS Account with:**
- S3 bucket created (e.g., `testbucket-sudhir-bsi1`)
- IAM user/role with S3 write permissions
- AWS credentials configured

? **Development Environment:**
- .NET 9 SDK installed
- Visual Studio or VS Code
- TextractUploader project

? **Files to Upload:**
- Supported formats: TIF, TIFF, PDF, PNG, JPG, JPEG
- Located in `UploadFiles/` directory

### AWS Permissions Required

```json
{
  "Version": "2012-10-17",
  "Statement": [
 {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
      "s3:PutObjectAcl",
     "s3:GetObject",
        "s3:ListBucket"
      ],
      "Resource": [
      "arn:aws:s3:::your-bucket-name/*",
 "arn:aws:s3:::your-bucket-name"
      ]
    }
  ]
}
```

---

## Folder Structure

### Local Project Structure

```
TextractUploader/
??? UploadFiles/       ? YOUR FILES GO HERE
?   ??? 2025000065660.tif
?   ??? 2025000065660-1.tif
? ??? invoice.pdf
?   ??? README.md
??? Program.cs        ? Upload logic
??? TextractUploader.csproj
??? bin/
    ??? Debug/
        ??? net9.0/
       ??? UploadFiles/   ? Copied during build
       ??? TextractUploader.exe
```

### S3 Bucket Structure (After Upload)

```
s3://testbucket-sudhir-bsi1/
??? uploads/
    ??? 2025-01-20/           ? Date folder (YYYY-MM-DD)
 ??? 2025000065660/ ? Document folder
        ? ??? 2025000065660.tif
 ??? 2025000065660-1/
        ?   ??? 2025000065660-1.tif
     ??? invoice/
   ??? invoice.pdf
```

### Path Pattern

```
s3://{bucket}/uploads/{YYYY-MM-DD}/{filename-without-ext}/{filename}
```

**Examples:**
- `s3://bucket/uploads/2025-01-20/contract/contract.pdf`
- `s3://bucket/uploads/2025-01-20/deed-001/deed-001.tif`
- `s3://bucket/uploads/2025-01-21/invoice/invoice.tif`

---

## Step-by-Step Process

### Step 1: Prepare Files

1. **Navigate to project directory:**
   ```powershell
   cd E:\Sudhir\GitRepo\AWSTextract\TextractUploader
   ```

2. **Create UploadFiles folder** (if doesn't exist):
   ```powershell
   mkdir UploadFiles
   ```

3. **Copy files to UploadFiles:**
   ```powershell
   copy "C:\Path\To\Your\Files\*.tif" UploadFiles\
   copy "C:\Path\To\Your\Files\*.pdf" UploadFiles\
   ```

4. **Verify files:**
   ```powershell
 dir UploadFiles
   ```

   Expected output:
   ```
   2025000065660.tif
   2025000065660-1.tif
   invoice.pdf
   ```

---

### Step 2: Configure AWS Credentials

#### Option A: AWS CLI Configuration
```powershell
aws configure
```

Enter:
- AWS Access Key ID
- AWS Secret Access Key
- Default region (e.g., `us-east-1`)
- Output format: `json`

#### Option B: Environment Variables
```powershell
$env:AWS_ACCESS_KEY_ID="your-access-key"
$env:AWS_SECRET_ACCESS_KEY="your-secret-key"
$env:AWS_REGION="us-east-1"
```

#### Option C: AWS Profile
```powershell
$env:AWS_PROFILE="your-profile-name"
```

---

### Step 3: Configure Upload Settings

Edit `Program.cs` to set your bucket and date:

```csharp
// Configure S3 bucket
private const string BucketName = "testbucket-sudhir-bsi1";

// Configure upload date (automatically uses today's date)
private static readonly string UploadDate = DateTime.Now.ToString("yyyy-MM-dd");
```

Or specify a custom date:
```csharp
private const string UploadDate = "2025-01-20";
```

---

### Step 4: Run Upload

#### Using Visual Studio
1. Open `TextractUploader.csproj` in Visual Studio
2. Press **F5** or click **Run**
3. Monitor console output

#### Using Command Line
```powershell
cd TextractUploader
dotnet run
```

#### Using Built Executable
```powershell
cd TextractUploader\bin\Debug\net9.0
.\TextractUploader.exe
```

---

### Step 5: Monitor Upload Progress

**Expected Console Output:**

```
=== Starting S3 Upload ===

Project directory: E:\Sudhir\GitRepo\AWSTextract\TextractUploader
Reading files from: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles

Upload date folder: 2025-01-20

Uploading 2025000065660.tif...
  Local path: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles\2025000065660.tif
  S3 Path: uploads/2025-01-20/2025000065660/2025000065660.tif
  ? SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065660/2025000065660.tif

Uploading 2025000065660-1.tif...
  Local path: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles\2025000065660-1.tif
  S3 Path: uploads/2025-01-20/2025000065660-1/2025000065660-1.tif
  ? SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065660-1/2025000065660-1.tif

Uploading invoice.pdf...
  Local path: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles\invoice.pdf
  S3 Path: uploads/2025-01-20/invoice/invoice.pdf
  ? SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/invoice/invoice.pdf

=== Upload Complete ===

Uploaded 3 files to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/
```

---

## Configuration

### Upload Configuration Options

```csharp
// In Program.cs

// 1. Bucket Name
private const string BucketName = "testbucket-sudhir-bsi1";

// 2. Upload Date (auto or manual)
private static readonly string UploadDate = DateTime.Now.ToString("yyyy-MM-dd"); // Auto
// OR
private const string UploadDate = "2025-01-20"; // Manual

// 3. Supported File Extensions
private static readonly string[] SupportedExtensions = 
{
    ".tif", ".tiff", ".pdf", ".png", ".jpg", ".jpeg"
};

// 4. Files Directory
private const string FilesDirectory = "UploadFiles";
```

### Batch Configuration

To upload specific files only:

```csharp
// Define specific files to upload
private static readonly string[] FilesToUpload = new[]
{
    "document1.tif",
    "document2.pdf",
    "scan001.tif"
};

// Or use pattern matching
var filesToUpload = Directory.GetFiles(uploadFilesPath, "*.tif")
    .Concat(Directory.GetFiles(uploadFilesPath, "*.pdf"))
    .ToArray();
```

---

## Verification

### Verify Upload in AWS Console

1. **Navigate to S3:**
   ```
   AWS Console ? S3 ? Your Bucket ? uploads/ ? {date}/
   ```

2. **Check folder structure:**
   ```
   uploads/
   ??? 2025-01-20/
    ??? 2025000065660/
       ?   ??? 2025000065660.tif (? size, modified date)
   ??? invoice/
           ??? invoice.pdf (? size, modified date)
   ```

3. **Verify file properties:**
   - Click on file
   - Check: Size, Type, Last Modified, Storage Class

### Verify Using AWS CLI

```powershell
# List uploaded files
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/ --recursive

# Expected output:
# 2025-01-20 10:30:00   2458624 uploads/2025-01-20/2025000065660/2025000065660.tif
# 2025-01-20 10:30:15   1847293 uploads/2025-01-20/invoice/invoice.pdf
```

### Verify Using Code

```csharp
// Check if file exists in S3
var client = new AmazonS3Client();
var request = new GetObjectMetadataRequest
{
    BucketName = "testbucket-sudhir-bsi1",
    Key = "uploads/2025-01-20/2025000065660/2025000065660.tif"
};

try
{
    var response = await client.GetObjectMetadataAsync(request);
    Console.WriteLine($"File exists! Size: {response.ContentLength} bytes");
}
catch (AmazonS3Exception ex)
{
    Console.WriteLine($"File not found: {ex.Message}");
}
```

---

## Troubleshooting

### Issue: "Folder 'UploadFiles' not found"

**Cause:** UploadFiles directory doesn't exist

**Solution:**
```powershell
cd TextractUploader
mkdir UploadFiles
```

---

### Issue: "Access Denied" Error

**Cause:** AWS credentials not configured or insufficient permissions

**Solutions:**

1. **Check credentials:**
   ```powershell
   aws sts get-caller-identity
   ```

2. **Verify permissions:**
   - Ensure IAM user has `s3:PutObject` permission
   - Check bucket policy allows uploads

3. **Test S3 access:**
   ```powershell
   aws s3 ls s3://testbucket-sudhir-bsi1/
```

---

### Issue: Files Not Uploading

**Cause:** File type not supported or files not in correct location

**Solutions:**

1. **Check file extensions:**
   ```csharp
   // Supported: .tif, .tiff, .pdf, .png, .jpg, .jpeg
   ```

2. **Verify file location:**
   ```powershell
   dir UploadFiles
   ```

3. **Check file size:**
   - Textract supports up to 512 MB
   - S3 supports up to 5 GB per PUT operation

---

### Issue: Upload Slow

**Cause:** Large files or network issues

**Solutions:**

1. **Enable multi-part upload** (for files > 100 MB):
   ```csharp
   var transferUtility = new TransferUtility(s3Client);
   await transferUtility.UploadAsync(filePath, bucketName, key);
   ```

2. **Use parallel uploads:**
   ```csharp
   var tasks = files.Select(f => UploadFileAsync(f));
await Task.WhenAll(tasks);
   ```

3. **Check network connection:**
   ```powershell
   Test-NetConnection s3.amazonaws.com -Port 443
   ```

---

### Issue: Incorrect Date Folder

**Cause:** Date format or timezone issue

**Solutions:**

1. **Use UTC time:**
   ```csharp
   private static readonly string UploadDate = 
       DateTime.UtcNow.ToString("yyyy-MM-dd");
   ```

2. **Set specific date:**
   ```csharp
   private const string UploadDate = "2025-01-20";
   ```

3. **Use local time:**
   ```csharp
   private static readonly string UploadDate = 
       DateTime.Now.ToString("yyyy-MM-dd");
   ```

---

## Best Practices

### 1. Organize by Date
? Use consistent date format: `YYYY-MM-DD`  
? Group related documents by upload batch  
? Keep upload date separate from document date

### 2. File Naming
? Use descriptive filenames: `contract_001.tif`  
? Avoid special characters: `/ \ : * ? " < > |`  
? Keep names under 100 characters

### 3. Pre-Upload Validation
```csharp
// Validate before upload
if (new FileInfo(filePath).Length > 512 * 1024 * 1024)
{
    Console.WriteLine("? File too large for Textract (max 512 MB)");
    return;
}

if (!SupportedExtensions.Contains(Path.GetExtension(filePath).ToLower()))
{
    Console.WriteLine("? Unsupported file type");
    return;
}
```

### 4. Error Handling
```csharp
try
{
    await UploadFileAsync(file);
    Console.WriteLine($"? SUCCESS: {file}");
}
catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
{
    Console.WriteLine($"? ACCESS DENIED: Check permissions");
}
catch (Exception ex)
{
    Console.WriteLine($"? ERROR: {ex.Message}");
    // Log for retry
}
```

### 5. Upload Verification
```csharp
// Verify upload completed
var metadata = await s3Client.GetObjectMetadataAsync(bucketName, key);
if (metadata.ContentLength == new FileInfo(localPath).Length)
{
    Console.WriteLine("? Upload verified");
}
```

---

## Next Steps

After successful upload:

1. **Process with Textract** ? [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md)
2. **Monitor processing** ? Check AWS Lambda logs
3. **Retrieve results** ? Check `CachedFiles_OutputFiles/` directory

---

## Quick Reference

### Upload Command
```powershell
cd TextractUploader
dotnet run
```

### S3 Path Pattern
```
s3://{bucket}/uploads/{YYYY-MM-DD}/{filename-no-ext}/{filename}
```

### Supported File Types
```
.tif, .tiff, .pdf, .png, .jpg, .jpeg
```

### AWS CLI Commands
```powershell
# List uploads for today
aws s3 ls s3://bucket/uploads/$(Get-Date -Format "yyyy-MM-dd")/

# Copy file to S3 manually
aws s3 cp file.tif s3://bucket/uploads/2025-01-20/file/file.tif

# Download from S3
aws s3 cp s3://bucket/uploads/2025-01-20/file/file.tif .
```

---

**Ready to process?** See [PRODUCTION_TEXTRACT_OCR.md](PRODUCTION_TEXTRACT_OCR.md) for the next step.
