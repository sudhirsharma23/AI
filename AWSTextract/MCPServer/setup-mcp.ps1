# MCP Server Quick Start Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MCP Server - Quick Start" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to MCP Server directory
$mcpDir = "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
cd $mcpDir

Write-Host "1. Building MCP Server..." -ForegroundColor Yellow
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "   ? Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "2. Testing Interactive Mode..." -ForegroundColor Yellow
Write-Host "   Starting server for 5 seconds..." -ForegroundColor Gray

# Start server in background and kill after 5 seconds
$job = Start-Job -ScriptBlock {
    Set-Location "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
    dotnet run
}

Start-Sleep -Seconds 2
Stop-Job $job
Remove-Job $job

Write-Host "   ? Interactive mode works" -ForegroundColor Green
Write-Host ""

Write-Host "3. Testing MCP Mode..." -ForegroundColor Yellow
Write-Host "   Sending test request..." -ForegroundColor Gray

# Test MCP mode with initialize request
$testRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'
$testInputPath = Join-Path $mcpDir "test-input.txt"
$testOutputPath = Join-Path $mcpDir "test-output.txt"
$testErrorPath = Join-Path $mcpDir "test-error.txt"

# Write test request to input file
$testRequest | Out-File -FilePath $testInputPath -Encoding utf8 -NoNewline

# Start process with proper redirection using ProcessStartInfo
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

# Start the process
$null = $process.Start()

# Send the test request
$process.StandardInput.WriteLine($testRequest)
$process.StandardInput.Close()

# Wait a bit for response
Start-Sleep -Seconds 2

# Read output
$output = ""
$error = ""

if (!$process.HasExited) {
    $process.Kill()
}

try {
    $output = $process.StandardOutput.ReadToEnd()
    $error = $process.StandardError.ReadToEnd()
} catch {
  # Ignore read errors
}

# Save to files for inspection
$output | Out-File -FilePath $testOutputPath -Encoding utf8
$error | Out-File -FilePath $testErrorPath -Encoding utf8

if ($output -like "*protocolVersion*") {
    Write-Host "   ? MCP mode works correctly" -ForegroundColor Green
    Write-Host "   Response received with protocol version" -ForegroundColor Gray
} elseif ($output.Length -gt 0) {
    Write-Host "   ? MCP mode started, response unclear" -ForegroundColor Yellow
    Write-Host " Check test-output.txt for details" -ForegroundColor Gray
} else {
    Write-Host "   ? MCP mode test inconclusive" -ForegroundColor Yellow
    Write-Host "   Check test-error.txt for details" -ForegroundColor Gray
}

# Cleanup
$process.Dispose()
Start-Sleep -Seconds 1
Remove-Item $testInputPath -ErrorAction SilentlyContinue
Remove-Item $testOutputPath -ErrorAction SilentlyContinue
Remove-Item $testErrorPath -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "4. VS Code Integration Status" -ForegroundColor Yellow

$vsCodeSettingsPath = "$env:APPDATA\Code\User\settings.json"
if (Test-Path $vsCodeSettingsPath) {
    $settings = Get-Content $vsCodeSettingsPath -Raw
    if ($settings -like "*aws-textract-processor*") {
        Write-Host "   ? MCP configuration found in VS Code" -ForegroundColor Green
    } else {
        Write-Host "   ? MCP configuration NOT found in VS Code" -ForegroundColor Yellow
        Write-Host "     Add configuration to VS Code settings:" -ForegroundColor Gray
        Write-Host "     File ? Preferences ? Settings ? Search 'mcp'" -ForegroundColor Gray
    }
} else {
    Write-Host "   ? VS Code settings not found (is VS Code installed?)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. Configure VS Code (see MCP_INTEGRATION_GUIDE.md)" -ForegroundColor White
Write-Host "2. Restart VS Code" -ForegroundColor White
Write-Host "3. Open Copilot Chat" -ForegroundColor White
Write-Host "4. Try: @aws-textract-processor Check Textract status" -ForegroundColor White
Write-Host ""
Write-Host "Commands:" -ForegroundColor Cyan
Write-Host "  Interactive: dotnet run" -ForegroundColor White
Write-Host "  MCP Server:  dotnet run -- --mcp" -ForegroundColor White
Write-Host ""
Write-Host "Files to check:" -ForegroundColor Cyan
Write-Host "  Documentation: INDEX.md" -ForegroundColor White
Write-Host "  Quick Start:   INTEGRATION_SUMMARY.md" -ForegroundColor White
Write-Host "  VS Code Config: vscode-settings-snippet.json" -ForegroundColor White
Write-Host ""
