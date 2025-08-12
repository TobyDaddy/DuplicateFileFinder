param(
  [string]$Source,
  [string]$OutDir = 'D:\code\vscode\DuplicateFileFinder\Assets\Icons'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

if (!(Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

function Get-SourceImage {
  param([string]$src,[string]$searchDir)
  if ($src -and (Test-Path $src)) { return $src }
  if ($searchDir -and (Test-Path $searchDir)) {
    $candidates = Get-ChildItem -Path $searchDir -Filter *.png -Recurse -File | Sort-Object Length -Descending
    if ($candidates.Count -gt 0) { return $candidates[0].FullName }
  }
  throw 'No source PNG found. Please pass -Source <path-to-highres-png>.'
}

function Resize-Icon {
  param([string]$src,[int]$size,[string]$dest)
  $img = [System.Drawing.Image]::FromFile($src)
  try {
    $bmp = New-Object System.Drawing.Bitmap $size,$size
    try {
      $g = [System.Drawing.Graphics]::FromImage($bmp)
      $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
      $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $g.Clear([System.Drawing.Color]::Transparent)
      $g.DrawImage($img, 0,0, $size, $size)
      $g.Dispose()
      $destDir = Split-Path $dest -Parent
      if (!(Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
      $bmp.Save($dest, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $bmp.Dispose() }
  } finally { $img.Dispose() }
}

$defaultSearch = 'D:\code\vscode\DuplicateFileFinder\Assets\Icons'
$srcPath = Get-SourceImage -src $Source -searchDir $defaultSearch

$icon300 = Join-Path $OutDir 'app-icon-300.png'
$icon71  = Join-Path $OutDir 'app-icon-71.png'

Resize-Icon -src $srcPath -size 300 -dest $icon300
Resize-Icon -src $srcPath -size 71  -dest $icon71

Write-Output ("Generated: " + $icon300)
Write-Output ("Generated: " + $icon71)
