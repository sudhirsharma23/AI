# Quick Textract Status Check Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Checking Textract Status" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$mcpDir = "E:\Sudhir\GitRepo\AWSTextract\MCPServer"
cd $mcpDir

Write-Host "Building MCP Server..." -ForegroundColor Yellow
dotnet build --nologo -v quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "? Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "? Build successful" -ForegroundColor Green
Write-Host ""

Write-Host "Calling textract_status tool..." -ForegroundColor Yellow
Write-Host ""

# Create a test request for the tool
$testRequest = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"textract_status","arguments":{}}}'

# Start the MCP server
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

# Give it a moment to start
Start-Sleep -Milliseconds 500

# Send the request
$process.StandardInput.WriteLine($testRequest)
$process.StandardInput.Close()

# Wait for response
Start-Sleep -Seconds 2

# Kill the process
if (!$process.HasExited) {
    $process.Kill()
}

# Read output
$output = $process.StandardOutput.ReadToEnd()
$errors = $process.StandardError.ReadToEnd()

$process.Dispose()

# Parse and display the result
Write-Host "Response:" -ForegroundColor Cyan
Write-Host "?????????????????????????????????????????" -ForegroundColor Gray

if ($output) {
    # Try to extract the result JSON
    try {
        $lines = $output -split "`n"
     $jsonLine = $lines | Where-Object { $_ -like '*"result"*' } | Select-Object -First 1
        
        if ($jsonLine) {
            $response = $jsonLine | ConvertFrom-Json
            
            if ($response.result) {
          $result = $response.result
        
             # Check if it's the content structure
        if ($result.content) {
   $content = $result.content[0].text | ConvertFrom-Json
           
 Write-Host "Status:          " -NoNewline -ForegroundColor White
   Write-Host $content.status -ForegroundColor $(if ($content.status -eq "running") { "Green" } else { "Yellow" })
         
       Write-Host "Output Directory:" -NoNewline -ForegroundColor White
          Write-Host " $($content.output_directory)" -ForegroundColor Gray
            
           Write-Host "Directory Exists:" -NoNewline -ForegroundColor White
          Write-Host " $($content.output_directory_exists)" -ForegroundColor $(if ($content.output_directory_exists) { "Green" } else { "Red" })
    
 Write-Host "Processed Files: " -NoNewline -ForegroundColor White
 Write-Host $content.processed_files -ForegroundColor Cyan
 
         Write-Host "Last Check:      " -NoNewline -ForegroundColor White
       Write-Host $content.last_check -ForegroundColor Gray
 
         Write-Host "Message:   " -NoNewline -ForegroundColor White
  Write-Host $content.message -ForegroundColor Yellow
           } else {
      Write-Host ($result | ConvertTo-Json -Depth 10) -ForegroundColor White
          }
       }
        } else {
         Write-Host $output -ForegroundColor White
        }
    } catch {
        Write-Host $output -ForegroundColor White
    }
} else {
  Write-Host "No output received" -ForegroundColor Red
}

Write-Host ""
Write-Host "?????????????????????????????????????????" -ForegroundColor Gray

if ($errors -and $errors.Trim()) {
    Write-Host ""
    Write-Host "Debug Info (from stderr):" -ForegroundColor Yellow
    $errors -split "`n" | Where-Object { $_.Trim() } | ForEach-Object {
  if ($_ -like "*Registered tool*" -or $_ -like "*MCP*") {
            Write-Host "  $_" -ForegroundColor DarkGray
        }
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Status Check Complete" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Also check the actual output directory
$outputDir = "E:\Sudhir\GitRepo\AWSTextract\TextractProcessor\src\TextractProcessor\CachedFiles_OutputFiles"
Write-Host "Additional Information:" -ForegroundColor Cyan
Write-Host ""

if (Test-Path $outputDir) {
    $files = Get-ChildItem -Path $outputDir -Filter "*.json" -ErrorAction SilentlyContinue
  Write-Host "? Output directory exists" -ForegroundColor Green
    Write-Host "  Path: $outputDir" -ForegroundColor Gray
    
    if ($files) {
        Write-Host "  Files found: $($files.Count)" -ForegroundColor Cyan
      Write-Host ""
 Write-Host "  Recent files:" -ForegroundColor White
        $files | Sort-Object LastWriteTime -Descending | Select-Object -First 5 | ForEach-Object {
            Write-Host "    - $($_.Name) ($('{0:yyyy-MM-dd HH:mm:ss}' -f $_.LastWriteTime))" -ForegroundColor Gray
        }
    } else {
        Write-Host "  No processed files found yet" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Output directory does not exist" -ForegroundColor Red
    Write-Host "  Expected: $outputDir" -ForegroundColor Gray
    Write-Host "  (This is normal if no documents have been processed yet)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  - To process documents: Run TextractProcessor" -ForegroundColor White
Write-Host "- To use in Copilot: @aws-textract-processor status" -ForegroundColor White
Write-Host "  - Interactive mode: dotnet run" -ForegroundColor White
Write-Host ""
