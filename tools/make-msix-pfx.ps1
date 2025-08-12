param(
    [string]$Subject = 'CN=Lin Gang',
    [string]$OutDir = 'D:\code\vscode\dist-sign',
    [string]$PfxPassword = 'PfxPassword123!'
)

$ErrorActionPreference = 'Stop'

# 1) 输出目录
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

# 2) 生成自签名证书（用于 MSIX 签名）
$cert = New-SelfSignedCertificate `
    -Type Custom `
    -Subject $Subject `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3') `
    -NotAfter (Get-Date).AddYears(3) `
    -FriendlyName 'MSIX Signing (Dev)'

# 3) 导出 PFX 与 CER
$pfx = Join-Path $OutDir 'LinGang.pfx'
$cer = Join-Path $OutDir 'LinGang.cer'
$sec = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $sec | Out-Null
Export-Certificate   -Cert $cert -FilePath $cer | Out-Null

# 4) 将发布者证书导入 TrustedPeople（便于侧载安装）
Import-Certificate -FilePath $cer -CertStoreLocation 'Cert:\CurrentUser\TrustedPeople' | Out-Null

Write-Host ("PFX: " + $pfx)
Write-Host ("CER: " + $cer)
Write-Host ("Thumbprint: " + $cert.Thumbprint)
Write-Host ("Subject: " + $cert.Subject)
