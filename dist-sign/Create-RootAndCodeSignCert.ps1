<#!
.SYNOPSIS
  创建一套“根 CA + 代码签名子证书”并导出/安装，用于 MSIX 自签测试，解决包分析器 0x800B0109。
.DESCRIPTION
  1. 生成根 CA (CA=true, 可签发) 放 CurrentUser\Root
  2. 生成代码签名证书 (CodeSigning) 由根 CA 签发 放 CurrentUser\My
  3. 导出 PFX (子证书+私钥)、子证书 CER、根 CA CER
  4. 可选安装根 CA 到 LocalMachine\Root (需要管理员)
  5. 输出签名命令示例
.PARAMETER RootSubject
  根 CA 主题 (Subject)
.PARAMETER CodeSignSubject
  子证书主题 (用于签名)
.PARAMETER ValidYearsRoot
  根 CA 有效期（年）
.PARAMETER ValidYearsCode
  子证书有效期（年）
.PARAMETER OutputDir
  输出目录
.PARAMETER ExportPfxName
  子证书 PFX 文件名
.PARAMETER AutoInstallMachineRoot
  是否尝试安装根 CA 到 LocalMachine\Root（需管理员，否则跳过）
.EXAMPLE
  pwsh .\dist-sign\Create-RootAndCodeSignCert.ps1 -AutoInstallMachineRoot
#>
param(
  [string]$RootSubject = 'CN=DuplicateFileFinder Dev Test Root',
  [string]$CodeSignSubject = 'CN=DuplicateFileFinder Dev',
  [int]$ValidYearsRoot = 5,
  [int]$ValidYearsCode = 3,
  [string]$OutputDir = './dist-sign',
  [string]$ExportPfxName = 'CodeSign_FromRoot.pfx',
  [switch]$AutoInstallMachineRoot,
  [switch]$KeepRootInMy,  # 如果指定，则不从 CurrentUser\My 移除根证书
  [switch]$CodeSignFromStoreConfig, # 从 store-identity.json 读取 Publisher 作为代码签名证书 Subject
  [string]$StoreConfigPath = './dist-sign/store-identity.json'
)

if ($CodeSignFromStoreConfig) {
  Write-Host '== 从 store-identity.json 读取 Publisher 以生成匹配商店的代码签名证书 ==' -ForegroundColor Cyan
  if (-not (Test-Path $StoreConfigPath)) { throw "找不到配置文件: $StoreConfigPath" }
  try {
    $storeJson = Get-Content -Raw -Path $StoreConfigPath | ConvertFrom-Json
  } catch { throw "解析 $StoreConfigPath 失败: $_" }
  if (-not $storeJson.Publisher) { throw 'store-identity.json 缺少 Publisher 字段' }
  # 使用商店 Publisher 覆盖 CodeSignSubject
  $CodeSignSubject = $storeJson.Publisher
  Write-Host "使用商店 Publisher 作为代码签名证书主题: $CodeSignSubject" -ForegroundColor Yellow
}

Write-Host '== 创建根 CA 证书 ==' -ForegroundColor Cyan
if (!(Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }

$existingRootInRoot = Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Subject -eq $RootSubject }
if ($existingRootInRoot) {
  Write-Warning '当前用户 Root 存储中已存在同名根证书（不会删除，将继续生成新的用于签发）。'
}

# 不能直接创建到 Root，会触发 “A new certificate can only be installed into MY store.”
# 先创建到 CurrentUser\My，然后导出 CER 再导入到 Root。
# 注意：为避免后续 signtool 误把根证书当成代码签名证书，这里不再赋予 DigitalSignature 给根证书。
$root = New-SelfSignedCertificate `
  -Type Custom `
  -Subject $RootSubject `
  -KeyAlgorithm RSA -KeyLength 2048 `
  -HashAlgorithm SHA256 `
  -KeyUsage CertSign,CRLSign `
  -TextExtension @('2.5.29.19={text}CA=TRUE&pathlength=1') `
  -CertStoreLocation Cert:\CurrentUser\My `
  -NotAfter (Get-Date).AddYears($ValidYearsRoot) `
  -KeyExportPolicy Exportable

if (!$root) { throw '根 CA 创建失败 (New-SelfSignedCertificate 返回空)' }

# 将根证书导出再导入到 CurrentUser\Root (如果 Root 中不存在同指纹)
$tmpRootCer = Join-Path $OutputDir 'RootCA_temp.cer'
Export-Certificate -Cert $root -FilePath $tmpRootCer | Out-Null
$alreadySameThumb = Get-ChildItem Cert:\CurrentUser\Root | Where-Object { $_.Thumbprint -eq $root.Thumbprint }
if (-not $alreadySameThumb) {
  Import-Certificate -FilePath $tmpRootCer -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
  Write-Host '已导入根证书到 CurrentUser\\Root' -ForegroundColor Green
} else {
  Write-Host '当前用户 Root 已包含同指纹根证书，跳过导入。' -ForegroundColor Yellow
}
Remove-Item $tmpRootCer -ErrorAction SilentlyContinue

Write-Host '== 创建代码签名子证书 ==' -ForegroundColor Cyan
Write-Host "代码签名主题: $CodeSignSubject" -ForegroundColor Yellow
$codeCert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject $CodeSignSubject `
  -Signer $root `
  -KeyAlgorithm RSA -KeyLength 2048 `
  -HashAlgorithm SHA256 `
  -KeyExportPolicy Exportable `
  -CertStoreLocation Cert:\CurrentUser\My `
  -NotAfter (Get-Date).AddYears($ValidYearsCode)

if (!$codeCert) { throw '代码签名证书创建失败' }

# 避免与 PowerShell 内置变量名称混淆，使用 $pfxPassword
$pfxPassword = Read-Host '请输入用于导出 PFX 的密码(不会显示)' -AsSecureString
$pfxPath = Join-Path $OutputDir $ExportPfxName
$cerCodePath = Join-Path $OutputDir 'CodeSign_FromRoot.cer'
$cerRootPath = Join-Path $OutputDir 'RootCA.cer'

Export-PfxCertificate -Cert $codeCert -FilePath $pfxPath -Password $pfxPassword | Out-Null
Export-Certificate -Cert $codeCert -FilePath $cerCodePath | Out-Null
Export-Certificate -Cert $root -FilePath $cerRootPath | Out-Null

# 如果不保留根证书在个人存储则删除，避免 signtool 看到两个可选证书
if (-not $KeepRootInMy) {
  $rootInMy = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $root.Thumbprint }
  if ($rootInMy) {
    try {
      Remove-Item $rootInMy.PSPath -Force
      Write-Host '已从 CurrentUser\\My 移除根证书（仍保留在 Root 用于信任链）。' -ForegroundColor Yellow
    } catch { Write-Warning "从 My 移除根证书失败: $_" }
  }
}

Write-Host "PFX: $pfxPath" -ForegroundColor Green
Write-Host "子证书 CER: $cerCodePath" -ForegroundColor Green
Write-Host "根证书 CER: $cerRootPath" -ForegroundColor Green
Write-Host "根指纹: $($root.Thumbprint)" -ForegroundColor Yellow
Write-Host "子指纹: $($codeCert.Thumbprint)" -ForegroundColor Yellow

if ($AutoInstallMachineRoot) {
  Write-Host '== 尝试安装根证书到 LocalMachine\\Root ==' -ForegroundColor Cyan
  try {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
    if (-not $isAdmin) {
      Write-Warning '当前非管理员，会跳过 LocalMachine 根安装。可手动右键以管理员运行后再执行。'
    } else {
      Import-Certificate -FilePath $cerRootPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
      Write-Host '已安装到 LocalMachine 根存储。' -ForegroundColor Green
    }
  }
  catch { Write-Warning "安装根证书失败: $_" }
}

Write-Host '\n== 签名示例 ==' -ForegroundColor Cyan
Write-Host "signtool sign /fd SHA256 /f $pfxPath /p <你的密码> /tr http://timestamp.digicert.com /td SHA256 .\YourApp.msix"

Write-Host '\n== 验证示例 ==' -ForegroundColor Cyan
Write-Host 'signtool verify /pa /v .\YourApp.msix'

Write-Host '\n提示: 如果包分析器仍提示 0x800B0109, 确认根证书是否在 受信任的根证书颁发机构 (当前用户或本机) 中, 以及 MSIX 重新签名后未被修改。' -ForegroundColor Magenta
