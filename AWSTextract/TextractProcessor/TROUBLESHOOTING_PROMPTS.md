# Troubleshooting: "Prompt template not found" Error

## Issue
Error message: `"Error":"Schema processing error: Prompt template not found: ..."`

## Root Cause
The PromptService cannot find the `Prompts` directory at runtime.

## Solution (Now Implemented)

The PromptService has been updated with:
1. **Enhanced logging** - Shows exactly where it's looking for files
2. **Multiple path fallbacks** - Tries several common locations
3. **Diagnostic output** - Lists available files when path not found

## How to Debug

### Step 1: Check Console Output
The updated PromptService now logs:
```
Base directory: /var/task/
Checking path: /var/task/Prompts
? Found Prompts directory at: /var/task/Prompts
Loading prompt from: /var/task/Prompts/SystemPrompts/document_extraction_v1.txt
? Loaded prompt: document_extraction_v1 (5234 chars)
```

### Step 2: Verify Files Are Copied
Check that `.csproj` includes:
```xml
<None Update="Prompts\**\*">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</None>
```

### Step 3: Check Deployment Package
For Lambda:
```bash
# Extract deployment package
unzip -l deployment-package.zip | grep Prompts

# Should show:
# Prompts/SystemPrompts/document_extraction_v1.txt
# Prompts/SystemPrompts/document_extraction_v2.txt
# Prompts/Rules/percentage_calculation.md
# Prompts/Rules/name_parsing.md
# Prompts/Rules/date_format.md
# Prompts/Examples/default/single_owner.json
# Prompts/Examples/default/two_owners.json
# Prompts/Examples/default/three_owners.json
```

### Step 4: Manual Override
If automatic detection fails, you can manually specify the path:

```csharp
// In SchemaMapperService constructor:
var promptsDir = "/var/task/Prompts"; // Lambda path
var promptService = new PromptService(cache, promptsDir);
```

## Common Scenarios

### Scenario 1: Running Locally (Visual Studio/Rider)
**Path**: `bin/Debug/net8.0/Prompts/`

The service will automatically find it using:
```csharp
Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Prompts")
```

### Scenario 2: Running in Lambda
**Path**: `/var/task/Prompts/`

The service checks this location automatically.

### Scenario 3: Running Tests
**Path**: `test-project/bin/Debug/net8.0/Prompts/`

May need manual override:
```csharp
var testPromptsPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Prompts");
var promptService = new PromptService(cache, testPromptsPath);
```

## Verification Commands

### Check Local Build
```powershell
# PowerShell
cd TextractProcessor\src\TextractProcessor
dotnet build
ls bin\Debug\net8.0\Prompts

# Should show:
# Examples/
# Rules/
# SystemPrompts/
```

### Check Lambda Package
```bash
# Linux/Mac
cd publish
ls -R Prompts

# Should show all subdirectories and files
```

## Quick Fix

If you're still getting the error, try this immediate fix in `SchemaMapperService.cs`:

```csharp
// Add this at the top of ProcessAndMapSchema method:
var baseDir = AppDomain.CurrentDomain.BaseDirectory;
Console.WriteLine($"Working from: {baseDir}");

var promptsPath = Path.Combine(baseDir, "Prompts");
if (!Directory.Exists(promptsPath))
{
    Console.WriteLine($"ERROR: Prompts directory not found at: {promptsPath}");
    Console.WriteLine("Available directories:");
    foreach (var dir in Directory.GetDirectories(baseDir))
    {
        Console.WriteLine($"  - {dir}");
    }
}

// Then use explicit path:
_promptService = new PromptService(_cache, promptsPath);
```

## Testing the Fix

Run your Lambda function and check CloudWatch Logs for the diagnostic output. You should see:

? **Success Output:**
```
Base directory: /var/task
Checking path: /var/task/Prompts
? Found Prompts directory at: /var/task/Prompts
Loading prompt from: /var/task/Prompts/SystemPrompts/document_extraction_v1.txt
? Loaded prompt: document_extraction_v1 (5234 chars)
  ? Inserted schema
  ? Inserted 3 examples
  ? Inserted rule: percentage_calculation
  ? Inserted rule: name_parsing
  ? Inserted rule: date_format
? Built complete prompt (System: 12450 chars, User: 8923 chars)
```

? **Failure Output (with diagnostics):**
```
Base directory: /var/task
Checking path: /var/task/Prompts
Checking path: /var/task/../../../Prompts
? Prompts directory not found, using default: /var/task/Prompts
Loading prompt from: /var/task/Prompts/SystemPrompts/document_extraction_v1.txt
Available prompts in /var/task/Prompts/SystemPrompts:
  - (empty)
ERROR: Prompt template not found: /var/task/Prompts/SystemPrompts/document_extraction_v1.txt
```

## Resolution Checklist

- [ ] Build successful
- [ ] `.csproj` includes Prompts copy rule
- [ ] Local `bin` folder contains Prompts directory
- [ ] Lambda deployment package includes Prompts directory
- [ ] Console logs show "Found Prompts directory"
- [ ] Console logs show "Loaded prompt" messages
- [ ] No FileNotFoundException errors

## Contact

If issue persists after following this guide:
1. Check CloudWatch Logs for diagnostic output
2. Verify deployment package contents
3. Try manual path override
4. Review Lambda execution role permissions (read access to /var/task)

---

**Status**: ? Fixed with enhanced logging and path detection  
**Version**: 1.1 (2025-01-29)  
**Pattern**: Same as ImageTextExtractor with improved diagnostics

