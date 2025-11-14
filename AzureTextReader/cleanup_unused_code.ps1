# ImageTextExtractor - Unused Code Cleanup Script
# Run this script to remove unused PromptService infrastructure
# Date: 2025-01-28

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " ImageTextExtractor - Code Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to project root
$projectRoot = "E:\Sudhir\GitRepo\AzureTextReader"
Set-Location $projectRoot

Write-Host "[1/6] Checking project location..." -ForegroundColor Yellow
if (-Not (Test-Path ".")) {
    Write-Host "ERROR: Project directory not found!" -ForegroundColor Red
    exit 1
}
Write-Host "    ? Project found at: $projectRoot" -ForegroundColor Green
Write-Host ""

# Create archive directory
Write-Host "[2/6] Creating archive directory for documentation..." -ForegroundColor Yellow
$archivePath = ".\docs\archive"
if (-Not (Test-Path $archivePath)) {
    New-Item -ItemType Directory -Force -Path $archivePath | Out-Null
    Write-Host "    ? Created: $archivePath" -ForegroundColor Green
} else {
    Write-Host "    ? Archive directory already exists" -ForegroundColor Green
}
Write-Host ""

# Move planning documents to archive
Write-Host "[3/6] Archiving planning documentation..." -ForegroundColor Yellow

$docsToArchive = @(
    "PROMPT_REFACTORING_PLAN.md",
    "IMPLEMENTATION_SUMMARY.md"
)

foreach ($doc in $docsToArchive) {
    if (Test-Path $doc) {
        Move-Item $doc $archivePath -Force
        Write-Host "    ? Archived: $doc" -ForegroundColor Green
    } else {
        Write-Host "    ? Not found: $doc" -ForegroundColor Yellow
    }
}
Write-Host ""

# Delete unused service
Write-Host "[4/6] Removing unused PromptService..." -ForegroundColor Yellow
$servicePath = ".\src\Services\PromptService.cs"
if (Test-Path $servicePath) {
    Remove-Item $servicePath -Force
    Write-Host "    ? Deleted: $servicePath" -ForegroundColor Green
} else {
    Write-Host "    ? Not found: $servicePath" -ForegroundColor Yellow
}
Write-Host ""

# Delete unused prompt files
Write-Host "[5/6] Removing unused prompt files..." -ForegroundColor Yellow

$promptFilesToDelete = @(
    ".\src\Prompts\SystemPrompts\deed_extraction_v1.txt",
    ".\src\Prompts\Examples\default\single_owner.json",
    ".\src\Prompts\Examples\default\two_owners.json",
    ".\src\Prompts\Examples\default\three_owners.json",
    ".\src\Prompts\Rules\percentage_calculation.md"
)

foreach ($file in $promptFilesToDelete) {
    if (Test-Path $file) {
Remove-Item $file -Force
        Write-Host "    ? Deleted: $file" -ForegroundColor Green
    } else {
        Write-Host "    ? Not found: $file" -ForegroundColor Yellow
    }
}
Write-Host ""

# Remove empty directories
Write-Host "[6/6] Cleaning up empty directories..." -ForegroundColor Yellow

$dirsToRemove = @(
    ".\src\Prompts\SystemPrompts",
    ".\src\Prompts\Examples\default",
    ".\src\Prompts\Examples",
    ".\src\Prompts\Rules",
    ".\src\Prompts",
    ".\src\Services"
)

foreach ($dir in $dirsToRemove) {
    if (Test-Path $dir) {
   # Only remove if directory is empty or contains only .gitkeep
        $items = Get-ChildItem $dir -Force
      if ($items.Count -eq 0 -or ($items.Count -eq 1 -and $items[0].Name -eq ".gitkeep")) {
            Remove-Item $dir -Recurse -Force
            Write-Host "    ? Removed empty directory: $dir" -ForegroundColor Green
        } else {
  Write-Host "    ? Directory not empty, keeping: $dir" -ForegroundColor Yellow
        }
    }
}
Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Cleanup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "? Unused code removed" -ForegroundColor Green
Write-Host "? Documentation archived to: docs\archive\" -ForegroundColor Green
Write-Host ""

# Verify build
Write-Host "Verifying build..." -ForegroundColor Yellow
Set-Location ".\src"
$buildResult = & dotnet build --nologo -v quiet 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Build successful!" -ForegroundColor Green
} else {
    Write-Host "? Build failed! Check errors above." -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

Set-Location $projectRoot
Write-Host ""

# Git status
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Next Steps" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Review changes with: git status" -ForegroundColor White
Write-Host "2. Commit cleanup: git add . && git commit -m 'Remove unused PromptService infrastructure'" -ForegroundColor White
Write-Host "3. Review archived docs in: docs\archive\" -ForegroundColor White
Write-Host ""
Write-Host "Cleanup complete! ?" -ForegroundColor Green
Write-Host ""
