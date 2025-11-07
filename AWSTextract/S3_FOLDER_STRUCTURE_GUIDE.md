# S3 Folder Structure Documentation

## Overview
The system uses a date-based folder structure for organizing documents in S3.

## Folder Structure
```
s3://bucket-name/
  uploads/
    2025-01-20/
      2025000065659/
    2025000065659.tif
    2025000065659-1/
        2025000065659-1.tif
    2025-01-21/
      invoice-001/
        invoice-001.pdf
      receipt-002/
     receipt-002.tif
```

**Format**: `uploads/<date>/<filename-without-ext>/<actual-filename>`

## Benefits
1. **Organized by Date** - Easy to find documents by upload date
2. **Grouped by Filename** - Related files stay together
3. **Prevents Conflicts** - Same filename can exist on different dates
4. **Clean Structure** - Logical hierarchy for document management

---

## TextractUploader Usage

### Automatic Date-Based Upload

The uploader automatically creates the folder structure using today's date:

```csharp
// Files to upload
private static readonly string[] FilesToUpload = new[]
{
    "2025000065659.tif",
    "2025000065659-1.tif"
};

// Automatically uploads to: uploads/2025-01-20/2025000065659/2025000065659.tif
// Today's date is used automatically
```

### Custom Upload Logic

To upload with a specific date:

```csharp
var uploadDate = "2025-01-15"; // Custom date
var fileName = "invoice.tif";
var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
var key = $"uploads/{uploadDate}/{fileNameWithoutExt}/{fileName}";

var request = new PutObjectRequest
{
    BucketName = "your-bucket",
    Key = key,
    FilePath = localFilePath
};

await s3Client.PutObjectAsync(request);
```

---

## TextractProcessor Usage

### Option 1: Hardcoded Date and Files

```csharp
private const string UploadDate = "2025-01-20";
private static readonly string[] DocumentKeys = new[]
{
    $"uploads/{UploadDate}/2025000065659/2025000065659.tif",
    $"uploads/{UploadDate}/2025000065659-1/2025000065659-1.tif"
};
```

### Option 2: Using Helper Methods

```csharp
// Get keys for specific date
var date = "2025-01-20";
var fileNames = new[] { "2025000065659.tif", "2025000065659-1.tif" };
var documentKeys = GetDocumentKeysForDate(date, fileNames);

// Helper method (add to Function.cs)
private static string[] GetDocumentKeysForDate(string date, params string[] fileNames)
{
    return fileNames.Select(fileName => 
    {
   var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        return $"uploads/{date}/{fileNameWithoutExt}/{fileName}";
 }).ToArray();
}
```

### Option 3: Using S3DocumentConfig Class

```csharp
// For today's date
var config = S3DocumentConfig.ForToday(
    "testbucket-sudhir-bsi1",
    "2025000065659.tif",
    "2025000065659-1.tif"
);

var documentKeys = config.GetAllS3Keys();
// Returns:
// ["uploads/2025-01-20/2025000065659/2025000065659.tif",
//  "uploads/2025-01-20/2025000065659-1/2025000065659-1.tif"]

// For specific date
var config2 = S3DocumentConfig.ForDate(
    "testbucket-sudhir-bsi1",
    "2025-01-15",
    "invoice.tif"
);

var keys = config2.GetAllS3Keys();
// Returns: ["uploads/2025-01-15/invoice/invoice.tif"]
```

### Option 4: Dynamic from Lambda Event

```csharp
public async Task<ProcessingResult> FunctionHandler(S3Event s3Event, ILambdaContext context)
{
    var documentKeys = new List<string>();
    
    foreach (var record in s3Event.Records)
  {
    var bucketName = record.S3.Bucket.Name;
        var s3Key = record.S3.Object.Key;
        documentKeys.Add(s3Key);
    }
    
    // Process the documents from S3 event
    // The keys already have the full path: uploads/2025-01-20/filename/file.tif
}
```

---

## Example Scenarios

### Scenario 1: Daily Batch Processing

**Upload** (runs daily at midnight):
```csharp
// TextractUploader automatically uses current date
// Files uploaded to: uploads/2025-01-20/...
```

**Process** (Lambda triggered by S3 event):
```csharp
// Lambda receives S3 event with full key
// Processes: uploads/2025-01-20/2025000065659/2025000065659.tif
```

### Scenario 2: Process Specific Date

**Scenario**: Need to reprocess documents from a specific date

```csharp
var config = S3DocumentConfig.ForDate(
    "testbucket-sudhir-bsi1",
    "2025-01-15", // Specific date
    "2025000065659.tif",
    "2025000065659-1.tif"
);

foreach (var key in config.GetAllS3Keys())
{
    var response = await ProcessTextract(key, context);
    // Process each document
}
```

### Scenario 3: Process Date Range

```csharp
var startDate = new DateTime(2025, 1, 15);
var endDate = new DateTime(2025, 1, 20);

for (var date = startDate; date <= endDate; date = date.AddDays(1))
{
    var config = S3DocumentConfig.ForDateTime(
      "testbucket-sudhir-bsi1",
        date,
        "2025000065659.tif"
    );
    
    var keys = config.GetAllS3Keys();
    // Process documents for this date
}
```

---

## Environment Variables (Optional)

You can make the date configurable via environment variables:

```csharp
// In Function.cs constructor
var uploadDate = Environment.GetEnvironmentVariable("UPLOAD_DATE") 
    ?? DateTime.Now.ToString("yyyy-MM-dd");

private static readonly string[] DocumentKeys = new[]
{
    $"uploads/{uploadDate}/2025000065659/2025000065659.tif",
 $"uploads/{uploadDate}/2025000065659-1/2025000065659-1.tif"
};
```

**Set in AWS Lambda**:
- Key: `UPLOAD_DATE`
- Value: `2025-01-20`

---

## Migration from Old Structure

### Old Structure
```
uploads/
  2025000065659.tif
  2025000065659-1.tif
```

### New Structure
```
uploads/
  2025-01-20/
    2025000065659/
      2025000065659.tif
    2025000065659-1/
      2025000065659-1.tif
```

### Migration Script (PowerShell)

```powershell
# List all files in old location
aws s3 ls s3://testbucket-sudhir-bsi1/uploads/

# Copy to new structure
$date = "2025-01-20"
$files = @("2025000065659.tif", "2025000065659-1.tif")

foreach ($file in $files) {
    $basename = [System.IO.Path]::GetFileNameWithoutExtension($file)
    $oldKey = "uploads/$file"
    $newKey = "uploads/$date/$basename/$file"
    
    aws s3 cp "s3://testbucket-sudhir-bsi1/$oldKey" "s3://testbucket-sudhir-bsi1/$newKey"
    Write-Host "Copied: $oldKey -> $newKey"
}
```

---

## Best Practices

1. **Use Consistent Date Format** - Always use `yyyy-MM-dd` format
2. **Validate Dates** - Ensure date strings are valid before constructing keys
3. **Handle Errors** - Check if S3 keys exist before processing
4. **Log Full Paths** - Always log complete S3 keys for debugging
5. **Use Config Classes** - Leverage `S3DocumentConfig` for maintainability

---

## Troubleshooting

### Files Not Found
```
Error: The specified key does not exist
Key: uploads/2025-01-20/2025000065659/2025000065659.tif
```

**Solutions**:
1. Verify the date matches upload date
2. Check filename spelling and extension
3. Use AWS CLI to list actual keys:
   ```sh
   aws s3 ls s3://testbucket-sudhir-bsi1/uploads/2025-01-20/ --recursive
 ```

### Wrong Date
If processing wrong date's files:
1. Check `UploadDate` constant in Function.cs
2. Verify environment variable `UPLOAD_DATE` if used
3. Confirm upload date in TextractUploader logs

### Lambda Not Triggered
S3 Event triggers may need updating:
1. Go to S3 bucket properties
2. Event notifications
3. Update prefix filter from `uploads/` to `uploads/*/` to match new structure
