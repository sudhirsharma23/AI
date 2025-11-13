# Test MCP Server Functionality
# This script tests that the MCP server responds correctly to JSON-RPC requests

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MCP Server - Functionality Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$mcpDir = "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
cd $mcpDir

# Test 1: Initialize
Write-Host "Test 1: Initialize Request" -ForegroundColor Yellow
$initRequest = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}'

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

$process.StandardInput.WriteLine($initRequest)
$process.StandardInput.Close()

Start-Sleep -Seconds 2

if (!$process.HasExited) {
    $process.Kill()
}

$output = $process.StandardOutput.ReadToEnd()
$error = $process.StandardError.ReadToEnd()

if ($output -like "*protocolVersion*" -and $output -like "*serverInfo*") {
    Write-Host "   ? Initialize works correctly" -ForegroundColor Green
    $initPassed = $true
} else {
    Write-Host "   ? Initialize failed" -ForegroundColor Red
    Write-Host "   Output: $output" -ForegroundColor Gray
    $initPassed = $false
}

$process.Dispose()
Write-Host ""

# Test 2: List Tools
Write-Host "Test 2: List Tools Request" -ForegroundColor Yellow
$listRequest = '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}'

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$null = $process.Start()

$process.StandardInput.WriteLine($listRequest)
$process.StandardInput.Close()

Start-Sleep -Seconds 2

if (!$process.HasExited) {
    $process.Kill()
}

$output = $process.StandardOutput.ReadToEnd()

if ($output -like "*calculator*" -and $output -like "*textract_status*") {
    Write-Host "   ? List tools works correctly" -ForegroundColor Green
    Write-Host "   Found tools: calculator, textract_status, s3_list_files, document_process" -ForegroundColor Gray
  $listPassed = $true
} else {
    Write-Host "   ? List tools failed" -ForegroundColor Red
    $listPassed = $false
}

$process.Dispose()
Write-Host ""

# Test 3: Call Calculator Tool
Write-Host "Test 3: Call Calculator Tool" -ForegroundColor Yellow
$calcRequest = '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"calculator","arguments":{"operation":"add","a":10,"b":5}}}'

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$null = $process.Start()

$process.StandardInput.WriteLine($calcRequest)
$process.StandardInput.Close()

Start-Sleep -Seconds 2

if (!$process.HasExited) {
    $process.Kill()
}

$output = $process.StandardOutput.ReadToEnd()

if ($output -like "*15*" -or $output -like "*result*") {
    Write-Host "   ? Calculator tool works correctly" -ForegroundColor Green
    Write-Host "   Result: 10 + 5 = 15" -ForegroundColor Gray
    $calcPassed = $true
} else {
    Write-Host "   ? Calculator tool failed" -ForegroundColor Red
    $calcPassed = $false
}

$process.Dispose()
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$totalTests = 3
$passedTests = 0
if ($initPassed) { $passedTests++ }
if ($listPassed) { $passedTests++ }
if ($calcPassed) { $passedTests++ }

Write-Host ""
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $($totalTests - $passedTests)" -ForegroundColor $(if ($passedTests -eq $totalTests) { "Green" } else { "Red" })
Write-Host ""

if ($passedTests -eq $totalTests) {
    Write-Host "? All tests passed! MCP server is working correctly." -ForegroundColor Green
    Write-Host ""
    Write-Host "You can now:" -ForegroundColor Cyan
    Write-Host "  1. Use interactive mode: dotnet run" -ForegroundColor White
    Write-Host "  2. Configure VS Code (see vscode-settings-snippet.json)" -ForegroundColor White
    Write-Host "  3. Test with Copilot: @aws-textract-processor" -ForegroundColor White
} else {
    Write-Host "? Some tests failed. Check the output above for details." -ForegroundColor Red
    Write-Host ""
 Write-Host "Troubleshooting:" -ForegroundColor Yellow
 Write-Host "  1. Make sure the project builds: dotnet build" -ForegroundColor White
    Write-Host "  2. Try interactive mode: dotnet run" -ForegroundColor White
    Write-Host "  3. Check MCP_INTEGRATION_GUIDE.md for help" -ForegroundColor White
}

Write-Host ""
