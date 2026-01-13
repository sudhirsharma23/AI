param(
  [Parameter(Mandatory=$true)][string]$Input,
  [Parameter(Mandatory=$true)][string]$Output,
  [int]$Width,
  [int]$Height
)

# PowerShell script to convert SVG to PNG using available tools: Inkscape or ImageMagick
if (!(Test-Path $Input)) { Write-Error "Input file not found: $Input"; exit 1 }

$ink = Get-Command inkscape -ErrorAction SilentlyContinue
if ($ink) {
  Write-Host "Using Inkscape to render PNG..."
  $args = @()
  if ($Width) { $args += "--export-width=$Width" }
  if ($Height) { $args += "--export-height=$Height" }
  $args += "--export-type=png"
  $args += "--export-filename=$Output"
  $args += $Input
  & inkscape @args
  Write-Host "Wrote $Output"
  exit 0
}

$magick = Get-Command magick -ErrorAction SilentlyContinue
if ($magick) {
  Write-Host "Using ImageMagick to render PNG..."
  if ($Width -or $Height) {
    $size = "${Width}x${Height}"
    magick convert -background none $Input -resize $size $Output
  } else {
    magick convert -background none $Input $Output
  }
  Write-Host "Wrote $Output"
  exit 0
}

Write-Error "No suitable renderer found. Install Inkscape or ImageMagick."; exit 2
