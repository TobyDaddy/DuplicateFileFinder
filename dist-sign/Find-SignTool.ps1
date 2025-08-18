<#!
.SYNOPSIS
  查找本机安装的最新版本 signtool.exe 并输出其完整路径。
.DESCRIPTION
  1. 遍历常见 Windows SDK 安装目录: C:\Program Files (x86)\Windows Kits\10\bin
  2. 优先选择最高版本号子目录下的 x64\signtool.exe (存在则输出路径)
  3. 如未找到, 给出安装 Windows 10/11 SDK 的提示下载链接。
.EXAMPLE
  pwsh .\dist-sign\Find-SignTool.ps1
#>

$base = 'C:\Program Files (x86)\Windows Kits\10\bin'
if (-not (Test-Path $base)) {
  Write-Warning "未找到 $base。请安装 Windows 10/11 SDK (含 Windows App Certification Kit / Signing Tools)。"
  Write-Host  '下载: https://developer.microsoft.com/en-us/windows/downloads/sdk/'
  exit 1
}

# 取版本子目录 (例如 10.0.22621.0)
$versions = Get-ChildItem -Path $base -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^10\.0\.' } | Sort-Object Name -Descending
if (-not $versions) {
  Write-Warning "未在 $base 下发现版本目录。请重新安装 / 修复 Windows SDK。"
  exit 1
}

foreach ($v in $versions) {
  $candidate = Join-Path $v.FullName 'x64\signtool.exe'
  if (Test-Path $candidate) {
    Write-Host "找到 signtool: $candidate" -ForegroundColor Green
    # 输出纯路径 (便于外部脚本捕获)
    $candidate
    exit 0
  }
}

Write-Warning "未找到 signtool.exe。请在安装 Windows SDK 时勾选 'Windows App Certification Kit' / '签名工具' 组件。"
exit 2
