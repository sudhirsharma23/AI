# VS Code MCP Integration Test Script

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VS Code MCP Server Diagnostic Tool" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$mcpDir = "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
$allPassed = $true

# Test 1: Check .NET
Write-Host "[1/8] Checking .NET installation..." -ForegroundColor Yellow
try {
$dotnetVersion = dotnet --version
 Write-Host " ? .NET version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "   ? .NET not found or not in PATH" -ForegroundColor Red
    Write-Host "     Install from: https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    $allPassed = $false
}
Write-Host ""

# Test 2: Check Project Builds
Write-Host "[2/8] Checking MCP Server builds..." -ForegroundColor Yellow
cd $mcpDir
$buildOutput = dotnet build --nologo -v quiet 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "   ? Build successful" -ForegroundColor Green
} else {
    Write-Host "   ? Build failed" -ForegroundColor Red
    Write-Host "     Run: dotnet build" -ForegroundColor Yellow
    $allPassed = $false
}
Write-Host ""

# Test 3: Check Interactive Mode
Write-Host "[3/8] Testing interactive mode..." -ForegroundColor Yellow
$job = Start-Job -ScriptBlock {
    Set-Location "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
    dotnet run 2>&1 | Select-String -Pattern "Registered 4 tools"
}
Start-Sleep -Seconds 5
$result = Receive-Job $job
Stop-Job $job
Remove-Job $job

if ($result) {
    Write-Host "   ? Interactive mode works" -ForegroundColor Green
} else {
  Write-Host " ? Interactive mode test inconclusive" -ForegroundColor Yellow
}
Write-Host ""

# Test 4: Check MCP Mode
Write-Host "[4/8] Testing MCP server mode..." -ForegroundColor Yellow

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = "run -- --mcp"
$psi.WorkingDirectory = $mcpDir
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$null = $process.Start()

Start-Sleep -Milliseconds 500

$testRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
$process.StandardInput.WriteLine($testRequest)
$process.StandardInput.Close()

Start-Sleep -Seconds 2

if (!$process.HasExited) {
    $process.Kill()
}

$output = $process.StandardOutput.ReadToEnd()
$errors = $process.StandardError.ReadToEnd()
$process.Dispose()

if ($output -like "*protocolVersion*") {
    Write-Host "   ? MCP server mode works" -ForegroundColor Green
    Write-Host "     Response contains protocol version" -ForegroundColor Gray
} else {
    Write-Host "   ? MCP server mode failed" -ForegroundColor Red
    Write-Host "     Output: $($output.Substring(0, [Math]::Min(100, $output.Length)))" -ForegroundColor Gray
    $allPassed = $false
}
Write-Host ""

# Test 5: Check VS Code Settings
Write-Host "[5/8] Checking VS Code configuration..." -ForegroundColor Yellow
$vsCodeSettings = "$env:APPDATA\Code\User\settings.json"
if (Test-Path $vsCodeSettings) {
    $settings = Get-Content $vsCodeSettings -Raw
    if ($settings -like "*aws-textract-processor*") {
        Write-Host "   ? MCP server configuration found" -ForegroundColor Green
        
        if ($settings -like "*github.copilot.chat.mcpServers*") {
   Write-Host "  Settings key: github.copilot.chat.mcpServers" -ForegroundColor Gray
  }
        
        if ($settings -like "*E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer*" -or 
            $settings -like "*E:/Sudhir/GitRepo/AWSTextract/MCPServer*") {
            Write-Host "     Path: Correctly configured" -ForegroundColor Gray
        } else {
  Write-Host "     ? Path might be incorrect" -ForegroundColor Yellow
        }
        
        if ($settings -like "*--mcp*") {
        Write-Host "     Arguments: --mcp flag present" -ForegroundColor Gray
        } else {
 Write-Host "     ? --mcp flag might be missing" -ForegroundColor Yellow
        }
    } else {
        Write-Host "   ? MCP server configuration NOT found" -ForegroundColor Red
        Write-Host "     Add configuration from vscode-settings-snippet.json" -ForegroundColor Yellow
$allPassed = $false
    }
} else {
    Write-Host "   ? VS Code settings file not found" -ForegroundColor Yellow
    Write-Host "     Path: $vsCodeSettings" -ForegroundColor Gray
}
Write-Host ""

# Test 6: Check GitHub Copilot Extension
Write-Host "[6/8] Checking GitHub Copilot..." -ForegroundColor Yellow
$vsCodeExtensions = code --list-extensions 2>&1
if ($vsCodeExtensions -like "*github.copilot*") {
 Write-Host "   ? GitHub Copilot extension installed" -ForegroundColor Green
    $copilotVersion = $vsCodeExtensions | Select-String "github.copilot"
    Write-Host "     Extension: $copilotVersion" -ForegroundColor Gray
} else {
 Write-Host " ? GitHub Copilot extension not detected" -ForegroundColor Yellow
    Write-Host "     Install from VS Code Extensions marketplace" -ForegroundColor Yellow
}
Write-Host ""

# Test 7: Check AWS Credentials
Write-Host "[7/8] Checking AWS credentials..." -ForegroundColor Yellow
try {
    $awsIdentity = aws sts get-caller-identity 2>&1 | ConvertFrom-Json
  Write-Host "   ? AWS credentials configured" -ForegroundColor Green
    Write-Host "     Account: $($awsIdentity.Account)" -ForegroundColor Gray
    Write-Host "     User: $($awsIdentity.Arn.Split('/')[-1])" -ForegroundColor Gray
} catch {
    Write-Host "   ? AWS credentials not configured" -ForegroundColor Yellow
  Write-Host "     Some tools may not work without AWS credentials" -ForegroundColor Gray
}
Write-Host ""

# Test 8: Check Project Files
Write-Host "[8/8] Checking project files..." -ForegroundColor Yellow
$requiredFiles = @(
    "MCPServer.csproj",
    "Program.cs",
    "Core\JsonRpcServer.cs",
    "Core\McpServer.cs",
    "Tools\TextractStatusTool.cs",
    "vscode-settings-snippet.json"
)

$missingFiles = @()
foreach ($file in $requiredFiles) {
    if (!(Test-Path (Join-Path $mcpDir $file))) {
      $missingFiles += $file
    }
}

if ($missingFiles.Count -eq 0) {
    Write-Host "   ? All required files present" -ForegroundColor Green
} else {
    Write-Host "   ? Some files missing:" -ForegroundColor Yellow
foreach ($file in $missingFiles) {
   Write-Host "     - $file" -ForegroundColor Gray
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Diagnostic Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if ($allPassed) {
    Write-Host "? All critical tests passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your MCP server should work in VS Code." -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "1. Completely restart VS Code (close all windows)" -ForegroundColor White
    Write-Host "2. Open Copilot Chat (Ctrl+Shift+I)" -ForegroundColor White
    Write-Host "3. Type: @aws-textract-processor status" -ForegroundColor White
    Write-Host ""
    Write-Host "If it still doesn't work, check:" -ForegroundColor Yellow
    Write-Host "- GitHub Copilot extension is up to date" -ForegroundColor White
    Write-Host "- Your Copilot plan supports MCP servers" -ForegroundColor White
    Write-Host "- See VSCODE_TROUBLESHOOTING.md for more help" -ForegroundColor White
} else {
    Write-Host "? Some tests failed" -ForegroundColor Red
    Write-Host ""
  Write-Host "Please fix the issues above before proceeding." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Common fixes:" -ForegroundColor Cyan
    Write-Host "1. Install/update .NET SDK: https://dotnet.microsoft.com/download" -ForegroundColor White
    Write-Host "2. Run: dotnet build" -ForegroundColor White
    Write-Host "3. Add MCP config to VS Code settings" -ForegroundColor White
    Write-Host "4. See VSCODE_TROUBLESHOOTING.md for detailed help" -ForegroundColor White
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Show VS Code settings example
Write-Host "Your VS Code settings.json should contain:" -ForegroundColor Cyan
Write-Host ""
Write-Host '{' -ForegroundColor Gray
Write-Host '  "github.copilot.chat.mcpServers": {' -ForegroundColor Gray
Write-Host '    "aws-textract-processor": {' -ForegroundColor Gray
Write-Host '      "command": "dotnet",' -ForegroundColor Gray
Write-Host '  "args": [' -ForegroundColor Gray
Write-Host '   "run",' -ForegroundColor Gray
Write-Host '        "--project",' -ForegroundColor Gray
Write-Host '        "E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer\\MCPServer.csproj",' -ForegroundColor Gray
Write-Host '        "--",' -ForegroundColor Gray
Write-Host '        "--mcp"' -ForegroundColor Gray
Write-Host '      ]' -ForegroundColor Gray
Write-Host '    }' -ForegroundColor Gray
Write-Host '  }' -ForegroundColor Gray
Write-Host '}' -ForegroundColor Gray
Write-Host ""

Write-Host "Copy this configuration:" -ForegroundColor Cyan
Write-Host "1. Press Ctrl+Shift+P in VS Code" -ForegroundColor White
Write-Host "2. Type: 'Preferences: Open User Settings (JSON)'" -ForegroundColor White
Write-Host "3. Paste the configuration above" -ForegroundColor White
Write-Host "4. Save and restart VS Code" -ForegroundColor White
Write-Host ""
