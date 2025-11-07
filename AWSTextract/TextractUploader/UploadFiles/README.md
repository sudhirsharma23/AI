# UploadFiles Folder

## Purpose
This folder contains the files that will be uploaded to S3 by the TextractUploader application.

## Location
Place your `.tif`, `.jpg`, `.pdf`, or other document files in this folder.

**Project structure:**
```
TextractUploader/
  UploadFiles/   <- Place your files here
 2025000065660.tif
    2025000065660-1.tif
    invoice.pdf
    document.jpg
  Program.cs
  TextractUploader.csproj
  bin/
  obj/
```

## How It Works

1. **Files are automatically discovered** - The application automatically finds and uploads ALL files in the `UploadFiles/` folder.

2. **No configuration needed** - Just place files in this folder and run the application. No need to update code!

3. **Supported file types** - The application filters files by extension:
   - `.tif`, `.tiff` - TIFF images
   - `.jpg`, `.jpeg` - JPEG images
   - `.png` - PNG images
   - `.pdf` - PDF documents
   - `.gif` - GIF images
   - `.bmp` - Bitmap images

4. **Automatic content type detection** - Content type is automatically set based on file extension.

## Adding New Files

**Simply:**
1. Copy your file to the `UploadFiles/` folder
2. Run the application
3. Done!

**No code changes needed!** The application will automatically:
- Discover all files in the folder
- Filter by supported extensions
- Upload each file to S3 with correct content type
- Show progress for each file

## Example Output

When you run the application:
```
=== TextractUploader - Automatic File Discovery ===

=== Starting S3 Upload ===

Project directory: E:\Sudhir\GitRepo\AWSTextract\TextractUploader
Reading files from: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles

Found 4 file(s) to upload:
  - 2025000065660.tif
  - 2025000065660-1.tif
  - invoice.pdf
  - document.jpg

Upload date folder: 2025-01-20

Uploading 2025000065660.tif...
Local path: E:\...\UploadFiles\2025000065660.tif
  S3 Path: uploads/2025-01-20/2025000065660/2025000065660.tif
  Content Type: image/tiff
SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065660/2025000065660.tif

Uploading invoice.pdf...
  Local path: E:\...\UploadFiles\invoice.pdf
  S3 Path: uploads/2025-01-20/invoice/invoice.pdf
  Content Type: application/pdf
  SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/invoice/invoice.pdf

Upload completed!
  Total files: 4
  Successful: 4
  Failed: 0
```

## Supported File Types

| Extension | Content Type | Description |
|-----------|-------------|-------------|
| .tif, .tiff | image/tiff | TIFF images |
| .jpg, .jpeg | image/jpeg | JPEG images |
| .png | image/png | PNG images |
| .pdf | application/pdf | PDF documents |
| .gif | image/gif | GIF images |
| .bmp | image/bmp | Bitmap images |

## Troubleshooting

### Warning: No files found in UploadFiles
**Solution**: 
1. Make sure files are directly in `UploadFiles/` folder (not in subfolders)
2. Check file extensions match supported types
3. Verify files have proper extensions (not hidden)

### Files not uploading
**Solution**:
1. Check AWS credentials are configured
2. Verify S3 bucket name is correct
3. Ensure you have write permissions to the bucket
4. Check file names don't contain special characters

### Wrong content type
**Solution**: The content type is automatically detected by file extension. If needed, you can add more extensions to the `GetContentType()` method in `Program.cs`.

## Benefits

? **Automatic Discovery** - No hardcoded filenames
? **Bulk Upload** - Upload multiple files at once
? **Flexible** - Add files anytime, no code changes
? **Type Safe** - Only uploads supported file types
? **Progress Tracking** - Shows status for each file
? **Error Handling** - Continues uploading even if one file fails
