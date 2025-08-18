<#!
.SYNOPSIS
  生成用于本地调试/侧载 MSIX 包的自签名代码签名证书，并导出 PFX / CER。
.DESCRIPTION
  1. 在当前用户个人证书存储(My)中创建一个可导出的代码签名证书
  2. 导出 PFX (含私钥) 和 CER (公钥)
  3. 将 CER 安装到 TrustedPeople 以便本机信任 (可选)
  4. 输出指纹、有效期及下一步签名命令示例
.NOTES
  如果你要提交到 Microsoft Store，商店会重新签名；自签名证书只用于本地测试或侧载。
#>
param(
  [string]$PublisherSubject = 'CN=DuplicateFileFinder Dev',
  [string]$PfxPath = './dist-sign/DevTestCert.pfx',
  [string]$CerPath = './dist-sign/DevTestCert.cer',
  [int]$ValidYears = 3
)

Write-Host '== 生成自签名代码签名证书 ==' -ForegroundColor Cyan
if (!(Test-Path (Split-Path $PfxPath -Parent))) { New-Item -ItemType Directory -Force -Path (Split-Path $PfxPath -Parent) | Out-Null }

$securePwd = Read-Host '请输入用于导出 PFX 的密码(不会显示)' -AsSecureString

$cert = New-SelfSignedCertificate `
  -Type CodeSigningCert `
  -Subject $PublisherSubject `
  -CertStoreLocation Cert:\CurrentUser\My `
  -KeyExportPolicy Exportable `
  -KeyAlgorithm RSA -KeyLength 2048 `
  -HashAlgorithm SHA256 `
  -NotAfter (Get-Date).AddYears($ValidYears)

if (!$cert) { throw '证书创建失败' }

# 导出 PFX
Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $securePwd | Out-Null
# 导出 CER (公钥)
Export-Certificate -Cert $cert -FilePath $CerPath | Out-Null

Write-Host "PFX: $PfxPath" -ForegroundColor Green
Write-Host "CER: $CerPath" -ForegroundColor Green
Write-Host "指纹(Thumbprint): $($cert.Thumbprint)"
Write-Host "有效期: $(Get-Date) -- $($cert.NotAfter)"

# 可选：导入到 TrustedPeople (便于当前用户信任侧载包)
$importResult = Import-Certificate -FilePath $CerPath -CertStoreLocation Cert:\CurrentUser\TrustedPeople -ErrorAction SilentlyContinue
if ($importResult) { Write-Host '已导入到 CurrentUser/TrustedPeople' -ForegroundColor Yellow }

Write-Host '\n== 例：签名生成的 MSIX ==' -ForegroundColor Cyan
Write-Host 'signtool sign /fd SHA256 /f dist-sign/DevTestCert.pfx /p <你的密码> /tr http://timestamp.digicert.com /td SHA256 .\MyPackage.msix'

Write-Host '\n== 例：制作 MSIX (MakeAppx) ==' -ForegroundColor Cyan
Write-Host 'makeappx pack /d publish\\win10-x64 /p DuplicateFileFinder_1.0.1.0_x64.msix'

Write-Host '\n然后使用上面的 signtool 命令签名。' -ForegroundColor Cyan
