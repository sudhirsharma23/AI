# Script to remove launchSettings.json from git tracking across all repositories
# This preserves local files while removing them from version control

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Removing launchSettings.json from Git Tracking" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Define repository paths
$repositories = @(
    @{
        name = "AzureTextReader"
        path = "E:\Sudhir\AI\AI\AzureTextReader\"
        files = @("src\Properties\launchSettings.json")
    },
    @{
        name = "RealEstate"
        path = "E:\Sudhir\AI\AI\RealState\"
        files = @("RealEstate.AI.WebApi\Properties\launchSettings.json")
    },
    @{
        name = "AWSTextract"
        path = "E:\Sudhir\AI\AI\AWSTextract\"
        files = @(
            "TextractProcessor\src\TextractProcessor\Properties\launchSettings.json",
            "TextractProcessor\src\TextractProcessor - Copy\Properties\launchSettings.json",
            "TextractProcessor\src\TextractProcessor - Copy (2)\Properties\launchSettings.json",
            "Upload\Properties\launchSettings.json"
        )
    }
)

foreach ($repo in $repositories) {
    Write-Host "Processing repository: $($repo.name)" -ForegroundColor Yellow
    Write-Host "Path: $($repo.path)" -ForegroundColor Gray
    
    if (Test-Path $repo.path) {
        Push-Location $repo.path
        
        foreach ($file in $repo.files) {
            $fullPath = Join-Path $repo.path $file
            
            if (Test-Path $fullPath) {
                Write-Host "  ? Removing from git: $file" -ForegroundColor Green
                git rm --cached $file 2>$null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "    Successfully removed" -ForegroundColor Green
                } else {
                    Write-Host "    File not tracked or already removed" -ForegroundColor Gray
                }
            } else {
                Write-Host "  ? File not found: $file" -ForegroundColor Yellow
            }
        }
        
        # Check if there are changes to commit
        $status = git status --porcelain
        if ($status) {
            Write-Host "  ?? Staged changes found" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "  Changes in $($repo.name):" -ForegroundColor Cyan
            $status | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
        }
        
        Pop-Location
    } else {
        Write-Host "  ? Repository path not found" -ForegroundColor Red
    }
    
    Write-Host ""
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "For each repository, run:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  cd '<repository-path>'" -ForegroundColor White
Write-Host "  git status                          # View changes" -ForegroundColor White
Write-Host "  git commit -m 'Remove launchSettings.json from version control'" -ForegroundColor White
Write-Host "  git push" -ForegroundColor White
Write-Host ""
Write-Host "This will:" -ForegroundColor Green
Write-Host "  • Remove launchSettings.json from git tracking" -ForegroundColor Green
Write-Host "  • Keep your local files with sensitive config intact" -ForegroundColor Green
Write-Host "  • Future changes to these files will be ignored" -ForegroundColor Green
Write-Host ""
