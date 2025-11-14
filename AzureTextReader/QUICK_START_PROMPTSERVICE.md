# Quick Start Guide - PromptService Integration

## ? Integration Complete!

The PromptService is now **active** in your ImageTextExtractor project.

## ?? Run Your Application

```powershell
cd E:\Sudhir\GitRepo\AzureTextReader\src
dotnet run
```

## ?? What to Look For

### Console Output
You should see these new messages:
```
Building prompt from templates...
Prompt built successfully (System: ~5000 chars, User: ~2000 chars)
```

### Output Files
Check `OutputFiles\` directory for:
- `combined_ocr_results_XXXXXX.md` - OCR data
- `final_output_XXXXXX.json` - Extracted JSON
- `schema_extensions_XXXXXX.md` - Schema extensions report

## ?? How to Modify Prompts

### Edit the Main Prompt
**File**: `..\AzureTextReader\src\Prompts\SystemPrompts\deed_extraction_v1.txt`

```
1. Open the file in any text editor
2. Make your changes
3. Save
4. Run the application (NO BUILD NEEDED!)
```

**Note**: Prompts are cached for 24 hours. To force reload, restart the application.

### Edit Rules
**Files**:
- `Prompts\Rules\percentage_calculation.md` - Owner percentage rules
- `Prompts\Rules\name_parsing.md` - Name extraction rules
- `Prompts\Rules\date_format.md` - Date formatting rules

**Same process**: Edit, save, restart application.

### Edit Examples
**Files**:
- `Prompts\Examples\default\single_owner.json`
- `Prompts\Examples\default\two_owners.json`
- `Prompts\Examples\default\three_owners.json`

**Format**:
```json
{
  "title": "Example Title",
  "input": "Source text",
  "expectedOutput": { /* JSON structure */ },
  "explanation": "Why this matters"
}
```

## ?? Testing

### Quick Test
```powershell
# Build and run
cd src
dotnet build
dotnet run
```

### Compare Output
1. Save previous `final_output` file
2. Run application with new prompts
3. Compare outputs:
   - Same structure? ?
   - Better accuracy? ?
   - Correct percentages? ?

## ?? Troubleshooting

### Issue: "Prompt template not found"
**Solution**: Check that files exist:
```powershell
dir Prompts\SystemPrompts\deed_extraction_v1.txt
dir Prompts\Examples\default\*.json
dir Prompts\Rules\*.md
```

### Issue: "Examples directory not found"
**Solution**: Create the directory structure:
```powershell
mkdir Prompts\Examples\default -Force
```

### Issue: No output or errors
**Solution**: Check console for error messages. Common causes:
- Missing schema file
- Invalid Azure credentials
- Network connectivity

## ?? Verify Integration

### Checklist
- [ ] Build successful (`dotnet build`)
- [ ] Application runs without errors
- [ ] Console shows "Building prompt from templates..."
- [ ] Console shows "Prompt built successfully..."
- [ ] Output files generated
- [ ] JSON structure looks correct

## ?? Next Steps

### 1. Baseline Test
Run with existing documents and save the output as baseline.

### 2. Experiment
Try modifying prompts:
- Adjust wording in `deed_extraction_v1.txt`
- Add more examples
- Refine rules

### 3. Compare
Run again and compare with baseline. Better results? Keep changes!

### 4. Iterate
Continuously improve prompts based on real-world results.

## ?? Need Help?

### Documentation
- **Full Details**: `PROMPTSERVICE_INTEGRATION_COMPLETE.md`
- **Cleanup Info**: `UNUSED_CODE_CLEANUP_REPORT.md`
- **Analysis**: `ANALYSIS_SUMMARY.md`

### Quick Commands
```powershell
# View file structure
tree Prompts /F

# Test build
dotnet build

# Run application
dotnet run

# Check logs
type OutputFiles\*.md | more
```

## ?? Pro Tips

### Tip 1: Version Your Prompts
Create copies for experimentation:
- `deed_extraction_v1.txt` (current)
- `deed_extraction_v2.txt` (experimental)
- `deed_extraction_v3.txt` (testing)

Switch versions by changing:
```csharp
TemplateType = "deed_extraction",
Version = "v2"  // Change this
```

### Tip 2: Create Example Sets
Organize examples by scenario:
- `Examples\default\` - General cases
- `Examples\complex\` - Multi-owner cases
- `Examples\corporations\` - Business entities
- `Examples\trusts\` - Trust transfers

Switch sets by changing:
```csharp
ExampleSet = "complex"// Change this
```

### Tip 3: Cache Management
Prompts are cached. To force reload:
1. Restart the application, OR
2. Clear cache (modify PromptService if needed)

### Tip 4: Git Tracking
Track prompt changes:
```bash
git add Prompts/
git commit -m "Update percentage calculation rules"
```

## ? Success Indicators

### Green Flags ?
- Application runs without errors
- Prompts load from files
- Output JSON is generated
- Schema extensions detected

### Red Flags ?
- Build errors
- Missing file errors
- Empty output
- Application crashes

## ?? You're Ready!

The PromptService is integrated and working. Now you can:
- ? Modify prompts without touching code
- ? Version control your prompts
- ? A/B test different approaches
- ? Iterate quickly on improvements

**Happy prompt engineering! ??**

---

**Quick Reference**: Keep this file handy for daily use.  
**Full Documentation**: See `PROMPTSERVICE_INTEGRATION_COMPLETE.md`  
**Questions**: Check `UNUSED_CODE_CLEANUP_REPORT.md` for architecture details.
