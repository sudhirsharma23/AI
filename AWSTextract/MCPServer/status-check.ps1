# Simple Textract Status Checker

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Textract Status Check" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# 1. Check AWS Credentials
Write-Host "1. Checking AWS Credentials..." -ForegroundColor Yellow
try {
    $identity = aws sts get-caller-identity 2>&1
    if ($LASTEXITCODE -eq 0) {
        $id = $identity | ConvertFrom-Json
        Write-Host "   ? AWS Credentials: Valid" -ForegroundColor Green
        Write-Host "     Account: $($id.Account)" -ForegroundColor Gray
      Write-Host "     User: $($id.Arn.Split('/')[-1])" -ForegroundColor Gray
    } else {
        Write-Host "   ? AWS Credentials: Not Configured" -ForegroundColor Red
        Write-Host "     Run: aws configure" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ? AWS CLI not found or error" -ForegroundColor Red
}

Write-Host ""

# 2. Check Textract Service Access
Write-Host "2. Checking Textract Service Access..." -ForegroundColor Yellow
try {
    $result = aws textract detect-document-text --document-location S3Object={Bucket=testbucket,Name=test.txt} 2>&1
    if ($result -like "*ResourceNotFoundException*" -or $result -like "*InvalidS3ObjectException*") {
 Write-Host "   ? Textract Service: Accessible" -ForegroundColor Green
        Write-Host " (Expected error - test file doesn't exist)" -ForegroundColor Gray
    } elseif ($result -like "*AccessDenied*") {
        Write-Host "   ? Textract Service: Access Denied" -ForegroundColor Red
        Write-Host "     Check IAM permissions" -ForegroundColor Yellow
    } elseif ($result -like "*credentials*") {
      Write-Host "   ? Textract Service: Credential Error" -ForegroundColor Red
    } else {
        Write-Host "   ? Textract Service: Status Unknown" -ForegroundColor Yellow
    }
} catch {
    Write-Host "   ? Error checking Textract" -ForegroundColor Red
}

Write-Host ""

# 3. Check Output Directory
Write-Host "3. Checking TextractProcessor Output..." -ForegroundColor Yellow
$outputDir = "E:\Sudhir\GitRepo\AWSTextract\TextractProcessor\src\TextractProcessor\CachedFiles_OutputFiles"

if (Test-Path $outputDir) {
    $jsonFiles = Get-ChildItem -Path $outputDir -Filter "*.json" -ErrorAction SilentlyContinue
    $txtFiles = Get-ChildItem -Path $outputDir -Filter "*.txt" -ErrorAction SilentlyContinue
    
    Write-Host "   ? Output Directory: Exists" -ForegroundColor Green
    Write-Host "     Path: $outputDir" -ForegroundColor Gray
    Write-Host "     JSON files: $($jsonFiles.Count)" -ForegroundColor Cyan
    Write-Host "     TXT files: $($txtFiles.Count)" -ForegroundColor Cyan
    
    if ($jsonFiles.Count -gt 0) {
        Write-Host "`n     Recent files:" -ForegroundColor White
        $jsonFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
  $age = (Get-Date) - $_.LastWriteTime
    $ageStr = if ($age.TotalHours -lt 1) { 
              "$([int]$age.TotalMinutes) minutes ago" 
  } elseif ($age.TotalDays -lt 1) {
  "$([int]$age.TotalHours) hours ago"
            } else {
       "$([int]$age.TotalDays) days ago"
         }
            Write-Host "       • $($_.Name) ($ageStr)" -ForegroundColor Gray
        }
    } else {
    Write-Host "     No processed files yet" -ForegroundColor Yellow
    }
} else {
    Write-Host "   ? Output Directory: Not Found" -ForegroundColor Yellow
    Write-Host "     (Normal if TextractProcessor hasn't run yet)" -ForegroundColor Gray
}

Write-Host ""

# 4. Check MCP Server
Write-Host "4. Checking MCP Server..." -ForegroundColor Yellow
$mcpProject = "E:\Sudhir\GitRepo\AWSTextract\MCPServer\MCPServer.csproj"
if (Test-Path $mcpProject) {
    Write-Host "   ? MCP Server: Project Found" -ForegroundColor Green
    
    # Try to build to verify
    $buildOutput = dotnet build $mcpProject --nologo -v quiet 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ? MCP Server: Builds Successfully" -ForegroundColor Green
    } else {
        Write-Host "   ? MCP Server: Build Failed" -ForegroundColor Red
    }
} else {
    Write-Host "   ? MCP Server: Not Found" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "AWS Region:     " -NoNewline -ForegroundColor White
Write-Host "$env:AWS_REGION" -ForegroundColor Cyan
Write-Host ""

Write-Host "Available Actions:" -ForegroundColor Cyan
Write-Host "  • Test MCP Server:     dotnet run" -ForegroundColor White
Write-Host "  • Check in Copilot:    @aws-textract-processor status" -ForegroundColor White
Write-Host "  • Process documents:   Run TextractProcessor" -ForegroundColor White
Write-Host "  • List S3 files:       @aws-textract-processor list files" -ForegroundColor White
Write-Host ""
