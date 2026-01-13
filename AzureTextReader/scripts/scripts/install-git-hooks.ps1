$hookSource = "githooks\pre-push"
$hookDest = ".git\hooks\pre-push"
if (Test-Path $hookSource) {
    Copy-Item $hookSource $hookDest -Force
    icacls $hookDest /grant Everyone:RX | Out-Null
    Write-Host "Git hooks installed to $hookDest"
} else {
    Write-Host "Hook source $hookSource not found"
}
