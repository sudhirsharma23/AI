# TextractUploader - UploadFiles Folder Setup

## What Changed?

### BEFORE:
Files were read from the `bin` folder (current directory):
```
bin/Debug/net9.0/
  2025000065660.tif
  2025000065660-1.tif
  TextractUploader.exe
```

### AFTER:
Files are read from the `UploadFiles` folder in the project directory:
```
TextractUploader/
  UploadFiles/
    2025000065660.tif
    2025000065660-1.tif
    README.md
  Program.cs
  TextractUploader.csproj
```

## Setup Steps

### Step 1: Create UploadFiles Folder

In your project directory, create the `UploadFiles` folder if it doesn't exist:

**Windows:**
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\TextractUploader
mkdir UploadFiles
```

**Linux/Mac:**
```bash
cd /path/to/TextractUploader
mkdir UploadFiles
```

### Step 2: Move Your Files

Move your `.tif` files to the `UploadFiles` folder:

**Windows:**
```powershell
move *.tif UploadFiles\
```

**Linux/Mac:**
```bash
mv *.tif UploadFiles/
```

Or manually copy files to:
```
E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles\
```

### Step 3: Verify Files

Check that your files are in the right location:
```
TextractUploader/
  UploadFiles/
    ? 2025000065660.tif
    ? 2025000065660-1.tif
```

### Step 4: Run the Application

```sh
cd TextractUploader
dotnet run
```

## How It Works

The program now:
1. **Finds the project directory** - Searches for `TextractUploader.csproj`
2. **Reads from UploadFiles** - Looks for files in `{ProjectDir}/UploadFiles/`
3. **Uploads to S3** - Uploads to `s3://bucket/uploads/<date>/<filename>/`

## Example Output

```
=== Starting S3 Upload ===

Project directory: E:\Sudhir\GitRepo\AWSTextract\TextractUploader
Reading files from: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles

Upload date folder: 2025-01-20

Uploading 2025000065660.tif...
  Local path: E:\Sudhir\GitRepo\AWSTextract\TextractUploader\UploadFiles\2025000065660.tif
  S3 Path: uploads/2025-01-20/2025000065660/2025000065660.tif
  SUCCESS: Uploaded to s3://testbucket-sudhir-bsi1/uploads/2025-01-20/2025000065660/2025000065660.tif
```

## Benefits

? **Organized** - Files separate from build output
? **Version Control** - Can add UploadFiles to .gitignore
? **Clean** - No clutter in bin folder
? **Easy to Manage** - All upload files in one place
? **Portable** - Works from any directory

## Folder Structure

```
E:\Sudhir\GitRepo\AWSTextract\
  TextractUploader/
    UploadFiles/  <- Your files go here
      2025000065660.tif
      2025000065660-1.tif
      invoice.pdf
      README.md
    Program.cs
    TextractUploader.csproj
    bin/
      Debug/
        net9.0/
       UploadFiles/     <- Files copied here during build
            2025000065660.tif
      2025000065660-1.tif
          TextractUploader.exe
```

## Troubleshooting

### Error: Folder 'UploadFiles' not found

**Problem:** The UploadFiles folder doesn't exist

**Solution:**
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\TextractUploader
mkdir UploadFiles
```

### Error: File not found in UploadFiles

**Problem:** File is not in the UploadFiles folder

**Solution:**
1. Check file location:
   ```powershell
   dir UploadFiles
 ```
2. Copy file to UploadFiles:
   ```powershell
   copy "2025000065660.tif" UploadFiles\
 ```

### Program reads from wrong folder

**Problem:** Project directory detection failed

**Solution:** The program looks for `TextractUploader.csproj`. Make sure you run from:
```
E:\Sudhir\GitRepo\AWSTextract\TextractUploader\
```

## Adding to .gitignore

To prevent uploading files to Git, add to `.gitignore`:

```
# Ignore uploaded files (but keep the folder structure)
TextractUploader/UploadFiles/*
!TextractUploader/UploadFiles/README.md
!TextractUploader/UploadFiles/.gitkeep
```

This will:
- ? Ignore all files in UploadFiles
- ? Keep the README.md
- ? Keep the .gitkeep file (to track empty folder)

## Next Steps

1. ? Create `UploadFiles` folder
2. ? Move your `.tif` files to `UploadFiles/`
3. ? Run `dotnet run`
4. ? Verify files upload to S3 with correct date structure

Your files will now be uploaded to:
```
s3://testbucket-sudhir-bsi1/
  uploads/
    2025-01-20/
 2025000065660/
      2025000065660.tif
      2025000065660-1/
     2025000065660-1.tif
```
