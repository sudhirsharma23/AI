# Script to remove launchSettings.json from git tracking while keeping local files
# This allows the files to stay in your workspace but be ignored by git

Write-Host "Removing launchSettings.json from git tracking..."

# Remove from git tracking (--cached keeps the local file)
git rm --cached src/Properties/launchSettings.json

# Add the changes to staging
git add .gitignore

Write-Host "Complete! The launchSettings.json file has been removed from git tracking."
Write-Host "Your local file is preserved and future changes will be ignored."
Write-Host ""
Write-Host "You can now commit and push:"
Write-Host "  git commit -m 'Remove launchSettings.json from version control'"
Write-Host "  git push"
