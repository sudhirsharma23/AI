# Quick Reference: Date-Based S3 Folder Structure

## What Changed?

### OLD Structure
```
uploads/2025000065659.tif
uploads/2025000065659-1.tif
```

### NEW Structure
```
uploads/2025-01-20/2025000065659/2025000065659.tif
uploads/2025-01-20/2025000065659-1/2025000065659-1.tif
```

**Format**: `uploads/<date>/<filename-without-extension>/<actual-file>`

---

## TextractUploader Changes

**What it does now:**
- Automatically uses today's date (YYYY-MM-DD format)
- Creates folder by filename (without extension)
- Uploads to: `uploads/2025-01-20/2025000065659/2025000065659.tif`

**Output Example:**
```
=== Starting S3 Upload ===

Upload date folder: 2025-01-20

Uploading 2025000065659.tif...
  S3 Path: uploads/2025-01-20/2025000065659/2025000065659.tif
  SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065659/2025000065659.tif

Uploading 2025000065659-1.tif...
  S3 Path: uploads/2025-01-20/2025000065659-1/2025000065659-1.tif
  SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065659-1/2025000065659-1.tif
```

---

## TextractProcessor Changes

### Update the Date in Function.cs

**Change this line** (around line 33):
```csharp
private const string UploadDate = "2025-01-20"; // Change to match your upload date
```

**Document keys now look like:**
```csharp
private static readonly string[] DocumentKeys = new[]
{
    $"uploads/{UploadDate}/2025000065659/2025000065659.tif",
    $"uploads/{UploadDate}/2025000065659-1/2025000065659-1.tif"
};
```

### Or Use Helper Methods

```csharp
// Get keys dynamically
var date = "2025-01-20";
var files = new[] { "2025000065659.tif", "2025000065659-1.tif" };
var keys = GetDocumentKeysForDate(date, files);
```

---

## To Use Right Now

### Step 1: Upload Files
```sh
cd TextractUploader
dotnet run
```
Files will be uploaded to: `uploads/2025-01-20/...` (today's date)

### Step 2: Update Processor Date
Open `Function.cs` and set:
```csharp
private const string UploadDate = "2025-01-20"; // Today's date
```

### Step 3: Deploy Lambda
```sh
cd TextractProcessor
dotnet lambda deploy-function TextractProcessor
```

### Step 4: Test
The Lambda will now read from the correct date folder!

---

## Benefits

? **Organized** - Files grouped by date and filename
? **No Conflicts** - Same filename on different dates won't collide
? **Easy Cleanup** - Delete old date folders easily
? **Searchable** - Find files by date quickly
? **Scalable** - Works for thousands of files per day

---

## Important Notes

1. **Date Format**: Always use `yyyy-MM-dd` (e.g., `2025-01-20`)
2. **Filename Folder**: Uses filename WITHOUT extension (e.g., `2025000065659` not `2025000065659.tif`)
3. **Update Lambda**: Remember to update the `UploadDate` constant or use environment variables
4. **S3 Events**: May need to update S3 event trigger prefix from `uploads/` to `uploads/*/`

---

## Verify Upload Structure

**AWS CLI:**
```sh
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/ --recursive
```

**AWS Console:**
1. Go to S3
2. Open `testbucket-sudhir-bsi1`
3. Navigate: `uploads` ? `2025-01-20` ? `2025000065659` ? `2025000065659.tif`
