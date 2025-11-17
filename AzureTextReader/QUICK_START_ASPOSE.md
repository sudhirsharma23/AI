# Quick Start - Switch to Aspose OCR in 2 Minutes

## ? Super Fast Setup

### Step 1: Get License File (30 seconds)
1. Obtain `Aspose.Total.NET.lic` from Aspose
2. Copy to: `E:\Sudhir\GitRepo\AzureTextReader\src\`

### Step 2: Switch Engine (10 seconds)

**Method A: Environment Variable** ? Fastest
```powershell
$env:OCR_ENGINE = "Aspose"
```

**Method B: User Secrets** ? Recommended
```powershell
cd E:\Sudhir\GitRepo\AzureTextReader\src
dotnet user-secrets set "Ocr:Engine" "Aspose"
```

**Method C: Edit appsettings.json**
```json
{
  "Ocr": {
    "Engine": "Aspose"
  }
}
```

### Step 3: Run (5 seconds)
```powershell
cd E:\Sudhir\GitRepo\AzureTextReader\src
dotnet run
```

## ? Verify It's Working

You should see:
```
? Aspose license loaded successfully from: Aspose.Total.NET.lic
? Using OCR Engine: Aspose.Total OCR
? Aspose OCR processing: https://...
? Success! Extracted 5180 characters in 1.52s
```

## ?? Switch Back to Azure

```powershell
$env:OCR_ENGINE = "Azure"
dotnet run
```

You should see:
```
? Using OCR Engine: Azure Document Analyzer
? Azure OCR processing: https://...
? Success! Extracted 5234 characters in 3.45s
```

## ?? That's It!

- ? Zero code changes
- ? Switch engines instantly
- ? Everything else works the same

---

## ?? Troubleshooting

### ?? "Aspose license file not found"
```powershell
# Verify file exists
ls E:\Sudhir\GitRepo\AzureTextReader\src\Aspose.Total.NET.lic

# Rebuild project
dotnet build

# Verify copied to output
ls E:\Sudhir\GitRepo\AzureTextReader\src\bin\Debug\net9.0\Aspose.Total.NET.lic
```

### ?? "Azure AI configuration not found" (when using Azure)
```powershell
# Set Azure credentials
dotnet user-secrets set "AzureAI:Endpoint" "https://your-endpoint.cognitiveservices.azure.com"
dotnet user-secrets set "AzureAI:SubscriptionKey" "your-key"
```

### ?? "Evaluation mode" warning
- Your license file is missing or invalid
- Obtain valid license from: https://purchase.aspose.com/buy
- Evaluation mode has limitations (watermarks, page limits)

---

## ?? Complete Configuration Options

```powershell
# Engine Selection
$env:OCR_ENGINE = "Aspose"  # or "Azure"

# Aspose Settings
$env:ASPOSE_LICENSE_PATH = "Aspose.Total.NET.lic"

# Caching (optional)
$env:OCR_ENABLE_CACHING = "true"   # or "false"
$env:OCR_CACHE_DURATION_DAYS = "30"
```

---

## ?? Full Documentation

For complete details, see:
- **`OCR_CONFIGURATION_GUIDE.md`** - Complete user guide
- **`ASPOSE_IMPLEMENTATION_SUMMARY.md`** - Technical implementation details

---

**That's it! You're ready to use Aspose OCR! ??**

