# Git Commit Helper - Security Fixes

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Git Commit Helper - Security Fixes" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

cd E:\Sudhir\GitRepo

Write-Host "Step 1: Check current status..." -ForegroundColor Yellow
git status

Write-Host ""
Write-Host "Step 2: Remove problematic files from staging (if any)..." -ForegroundColor Yellow
git restore --staged AzureTextReader/src/Program*.cs 2>$null

Write-Host ""
Write-Host "Step 3: Add security-related files..." -ForegroundColor Yellow

# Add the gitignore changes
git add .gitignore
Write-Host "  ? Added .gitignore" -ForegroundColor Green

# Add all AzureTextReader security files
git add AzureTextReader/SECURITY_SETUP.md
git add AzureTextReader/QUICK_FIX_SUMMARY.md
git add AzureTextReader/setup-secrets.ps1
git add AzureTextReader/src/appsettings.json
git add AzureTextReader/src/Configuration/
git add AzureTextReader/src/ImageTextExtractor.csproj
Write-Host "  ? Added AzureTextReader security files" -ForegroundColor Green

# Add MCPServer and other changes
git add MCPServer/
Write-Host "  ? Added MCPServer" -ForegroundColor Green

git add TextractUploader/
Write-Host "  ? Added TextractUploader changes" -ForegroundColor Green

Write-Host ""
Write-Host "Step 4: Review what will be committed..." -ForegroundColor Yellow
Write-Host ""
git status --short

Write-Host ""
Write-Host "Files to be committed:" -ForegroundColor Cyan
git diff --cached --name-only

Write-Host ""
$confirm = Read-Host "Do you want to commit these changes? (y/n)"

if ($confirm -eq 'y' -or $confirm -eq 'Y') {
    Write-Host ""
    Write-Host "Step 5: Committing changes..." -ForegroundColor Yellow
    
    git commit -m "Security: Implement secure Azure AI configuration + Add MCP Server

Security Improvements:
- Implement secure configuration management for Azure AI Services
- Add support for environment variables and user secrets
- Update .gitignore to exclude sensitive files
- Add interactive setup script (setup-secrets.ps1)
- Add comprehensive security documentation

New Features:
- Add MCP (Model Context Protocol) Server project for learning
- Update TextractUploader with automatic file discovery
- Improve folder structure and documentation

Files Changed:
- .gitignore: Added security-critical exclusions
- AzureTextReader: Complete security overhaul
- MCPServer: New learning project
- TextractUploader: Enhanced functionality

All Azure AI keys are now loaded from secure sources (env vars/user secrets).
No secrets are committed to the repository."

    Write-Host ""
    Write-Host "? Changes committed successfully!" -ForegroundColor Green
    Write-Host ""
 
    Write-Host "Step 6: Push to GitHub..." -ForegroundColor Yellow
    $push = Read-Host "Do you want to push now? (y/n)"
    
    if ($push -eq 'y' -or $push -eq 'Y') {
      Write-Host ""
        Write-Host "Pushing to origin/main..." -ForegroundColor Yellow
  git push
        
        if ($LASTEXITCODE -eq 0) {
         Write-Host ""
       Write-Host "=====================================" -ForegroundColor Green
            Write-Host "? Successfully pushed to GitHub!" -ForegroundColor Green
       Write-Host "=====================================" -ForegroundColor Green
            Write-Host ""
     Write-Host "Your code is now secure and committed!" -ForegroundColor Cyan
 } else {
        Write-Host ""
            Write-Host "? Push failed! Check the error above." -ForegroundColor Red
        Write-Host ""
            Write-Host "If GitHub is still blocking due to secrets:" -ForegroundColor Yellow
            Write-Host "1. Go to Azure Portal and regenerate your keys" -ForegroundColor White
      Write-Host "2. Run: git push" -ForegroundColor White
        }
    } else {
  Write-Host ""
        Write-Host "Changes committed but not pushed." -ForegroundColor Yellow
        Write-Host "Run 'git push' when you're ready." -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "Commit cancelled. No changes made." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Done!" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
