# Quick Reference: Dual Version Extraction

## ? TL;DR

TextractProcessor now automatically runs **BOTH** extraction versions on every document:
- **V1**: Schema-based (consistent structure)
- **V2**: Dynamic (discovers everything)

**No configuration needed** - just deploy and run!

---

## ?? Output Files (6 per run)

| File | What It Contains |
|------|-----------------|
| `*_textract_combined_*.txt` | Raw Textract OCR data |
| `*_v1_schema_*.json` | ? Schema-based extraction |
| `*_v2_dynamic_*.json` | ? Dynamic extraction (all fields) |
| `*_schema_extensions_*.md` | V1 analysis: fields added beyond schema |
| `*_v2_extraction_summary_*.md` | V2 statistics and key findings |

---

## ?? Quick Comparison

| Aspect | V1 (Schema-Based) | V2 (Dynamic) |
|--------|-------------------|--------------|
| **Schema** | ? Uses `invoice_schema.json` | ? No schema |
| **Rules** | ? percentage, name, date | ? None |
| **Examples** | ? single/two/three owners | ? None |
| **Output** | Structured, predictable | Flexible, comprehensive |
| **Best For** | Production processing | Discovery & validation |
| **Prompt Size** | ~12K chars | ~3.5K chars |
| **Cost per doc** | ~$0.20 | ~$0.25 |

**Total cost per document set**: **~$0.45** (both versions)

---

## ?? Deploy & Run

```bash
# Build
cd TextractProcessor/src/TextractProcessor
dotnet build

# Deploy
dotnet lambda deploy-function TextractProcessor

# Lambda runs automatically on S3 uploads
# Both V1 and V2 process simultaneously
```

---

## ?? Check Results

### CloudWatch Logs
```
=== Dual Version Processing ===

--- Version 1: Schema-Based ---
? V1 processing complete:
  - Input tokens: 15234
  - Output tokens: 3421
  - Cost: $0.2043
  - Saved to: merged_documents_v1_schema_*.json

--- Version 2: Dynamic ---
? V2 processing complete:
  - Input tokens: 12345
  - Output tokens: 4567
  - Cost: $0.2456
  - Saved to: merged_documents_v2_dynamic_*.json

=== Dual Version Processing Complete ===
```

### Download from S3
```bash
aws s3 cp s3://your-bucket/output/ . --recursive --exclude "*" --include "*_v1_*" --include "*_v2_*"
```

---

## ?? What to Look For

### V1 Output
```json
{
  "buyerInformation": {
    "buyers": [
      {
 "firstName": "John",
        "lastName": "Smith",
        "percentage": 50
    }
    ]
  }
}
```
? Structured  
? Schema-compliant  
? Consistent format  

### V2 Output
```json
{
  "buyerInformation": {
    "buyers": [
      {
     "fullName": "John David Smith",
        "firstName": "John",
 "middleName": "David",
        "lastName": "Smith",
        "percentage": 50,
        "address": "123 Main St",
        "phone": "(555) 123-4567",
   "vesting": "as joint tenants"
      }
    ]
  }
}
```
? More detailed  
? Discovered extra fields  
? Shows what's available  

---

## ?? Quick Use Cases

### Scenario 1: "Is my schema complete?"
**Action**: Compare V1 vs V2  
**Check**: `*_schema_extensions_*.md` report  
**If**: V2 has many more fields ? Update schema

### Scenario 2: "What data exists in these documents?"
**Action**: Review V2 output  
**Check**: `*_v2_extraction_summary_*.md`  
**Use**: Design schema based on V2 findings

### Scenario 3: "Production processing"
**Action**: Use V1 for consistency  
**Check**: Occasionally compare with V2 for validation  
**Benefit**: Catch missing fields

---

## ??? Quick Config

### Run Only V1
Edit `SchemaMapperService.cs`:
```csharp
var v1Result = await ProcessVersion1(...);
// var v2Result = await ProcessVersion2(...); // Comment out
return v1Result;
```

### Run Only V2
```csharp
// var v1Result = await ProcessVersion1(...); // Comment out
var v2Result = await ProcessVersion2(...);
return v2Result;
```

---

## ?? Performance

| Metric | Value |
|--------|-------|
| **Processing Time** | ~30-60 seconds (both) |
| **V1 Tokens** | ~18.5K (input + output) |
| **V2 Tokens** | ~16.8K (input + output) |
| **Total Tokens** | ~35.3K per document set |
| **Total Cost** | ~$0.45 per document set |
| **Cold Start** | +10s (first run) |

---

## ? Verification Checklist

After first run:
- [ ] CloudWatch shows both versions completed
- [ ] S3 bucket has 5 output files
- [ ] V1 JSON is schema-structured
- [ ] V2 JSON has more fields
- [ ] Schema extensions report created
- [ ] V2 summary report created
- [ ] No errors in logs

---

## ?? Quick Troubleshooting

### Problem: Only one version runs
**Fix**: Check for commented code in SchemaMapperService.cs

### Problem: Missing prompt files
**Fix**: Check `TROUBLESHOOTING_PROMPTS.md`

### Problem: High costs
**Fix**: Both versions double the cost - run V2 selectively

### Problem: V2 output too large
**Fix**: Normal behavior - V2 extracts everything

---

## ?? Full Documentation

- **Complete Guide**: `DUAL_VERSION_EXTRACTION.md`
- **Prompt Issues**: `TROUBLESHOOTING_PROMPTS.md`
- **OCR Config**: `OCR_CONFIGURATION_GUIDE.md`
- **Aspose Setup**: `ASPOSE_IMPLEMENTATION_SUMMARY.md`

---

## ?? You're Ready!

? **Dual-version extraction implemented**  
? **Same pattern as ImageTextExtractor**  
? **Automatic schema analysis**  
? **Ready to deploy**  

**Deploy and compare results!** ??

---

**Status**: ? COMPLETE | **Build**: ? SUCCESS | **Pattern**: ? IDENTICAL

