<#!
.SYNOPSIS
  一键：构建 -> 布局 -> (可选更新版本) -> 打包 MSIX -> 用子证书签名 -> 验证。
.DESCRIPTION
  假设你已：
    1. 拥有 AppxManifest (默认 ./dist-sign/AppxManifest.xml)
    2. 已用 Create-RootAndCodeSignCert.ps1 生成 CodeSign_FromRoot.pfx 与 RootCA.cer
    3. Manifest 的 <Identity Publisher=...> 与 代码签名子证书 Subject 匹配 (例如 CN=DuplicateFileFinder Dev)

  步骤：
    - dotnet publish WPF 项目 (自包含可选)
    - 复制输出文件 + 资产文件到临时 Layout 目录
    - 复制/更新 Manifest (可自动写入指定 Version)
    - 使用 makeappx pack 生成 .msix
    - 使用指定 PFX 签名 signtool sign
    - signtool verify 验证结果

.PARAMETER Project
  要发布的 .csproj 路径。
.PARAMETER Manifest
  AppxManifest.xml 路径。
.PARAMETER OutputDir
  输出目录 (用于存放生成的 msix 与中间 Layout)。
.PARAMETER PfxPath
  代码签名证书 PFX 路径 (子证书)。
.PARAMETER Version
  (可选) 指定要写入 Manifest Identity 的版本号 (格式 a.b.c.d)。若省略，保留原 Manifest 值。
.PARAMETER Config
  构建配置 (默认 Release)。
.PARAMETER Runtime
  发布 runtime (默认 win-x64)。
.PARAMETER SelfContained
  开关：是否自包含发布 (默认 true)。
.PARAMETER SignTimestampUrl
  时间戳 URL (默认 http://timestamp.digicert.com)。
.PARAMETER MsixName
  生成的 msix 文件名 (默认 根据项目+版本)。
.PARAMETER SkipBuild
  跳过 dotnet publish (用于只重新打包+签名)。
.PARAMETER Force
  遇到 Publisher 与证书 Subject 不匹配时继续 (默认中止)。
.PARAMETER KeepLayout
  保留 Layout 目录 (默认打包后删除)。
.PARAMETER Password
  直接传入明文密码 (不推荐)，若不提供则交互输入 SecureString。

.EXAMPLE
  pwsh ./dist-sign/Build-Pack-Sign.ps1 -Project ./DuplicateFileFinder/DuplicateFileFinderWPF.csproj -Manifest ./dist-sign/AppxManifest.xml -PfxPath ./dist-sign/CodeSign_FromRoot.pfx -Version 1.0.1.0

.NOTES
  需要安装 Windows SDK (提供 makeappx.exe 与 signtool.exe)。
#>
[CmdletBinding()]
param(
  [string]$Project = './DuplicateFileFinder/DuplicateFileFinderWPF.csproj',
  [string]$Manifest = './dist-sign/AppxManifest.xml',
  [string]$OutputDir = './dist-msix',
  [string]$PfxPath = './dist-sign/CodeSign_FromRoot.pfx',
  [string]$Version,
  [string]$Config = 'Release',
  [string]$Runtime = 'win-x64',
  [switch]$SelfContained = $true,
  [string]$SignTimestampUrl = 'http://timestamp.digicert.com',
  [string]$MsixName,
  [switch]$SkipBuild,
  [switch]$Force,
  [switch]$KeepLayout,
  [string]$Password,
  [string]$Sha1,            # 可显式指定签名使用的证书指纹（替代 /f PFX）
  [switch]$AutoPruneCerts,  # 自动清理 CurrentUser\My 中旧的/根的同主题证书
  [switch]$StoreMode,       # 商店模式：强制 Version 第四段为 0
  [switch]$ApplyStoreIdentity, # 读取 store-identity.json 覆盖 Manifest Identity/PublisherDisplayName
  [string]$StoreIdentityConfig = './dist-sign/store-identity.json'
)

function Write-Section($text) { Write-Host "== $text ==" -ForegroundColor Cyan }
function Fail($msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red; throw $msg }

# --- 前置检查 ---
Write-Section '参数校验'
if (-not (Test-Path $Project)) { Fail "项目不存在: $Project" }
if (-not (Test-Path $Manifest)) { Fail "Manifest 不存在: $Manifest" }
if (-not (Test-Path $PfxPath)) { Fail "PFX 不存在: $PfxPath" }

$publishDir = Join-Path $OutputDir 'publish'
$layoutDir  = Join-Path $OutputDir 'layout'
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null }

# --- 解析 Manifest Publisher & Name ---
[xml]$manifestXml = Get-Content -Path $Manifest -Raw
$ns = @{ f = 'http://schemas.microsoft.com/appx/manifest/foundation/windows10' }
$identityNode = $manifestXml.Package.Identity
$publisher = $identityNode.Publisher
$appName   = $identityNode.Name
$manifestVersion = $identityNode.Version

if ($Version) {
  if ($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$') { Fail 'Version 必须是 a.b.c.d 格式' }
  Write-Host "修改 Manifest 版本: $manifestVersion -> $Version" -ForegroundColor Yellow
  $identityNode.Version = $Version
  $manifestXml.Save((Resolve-Path $Manifest))
  $manifestVersion = $Version
}

# 如果要求应用商店身份覆盖
if ($ApplyStoreIdentity) {
  if (-not (Test-Path $StoreIdentityConfig)) { Fail "未找到 store identity 配置: $StoreIdentityConfig" }
  Write-Section '应用商店 Identity'
  try {
    $storeJson = Get-Content -Raw -Path $StoreIdentityConfig | ConvertFrom-Json
  } catch { Fail "解析 $StoreIdentityConfig 失败: $_" }
  if (-not $storeJson.IdentityName -or -not $storeJson.Publisher -or -not $storeJson.PublisherDisplayName) {
    Fail 'store-identity.json 必须包含 IdentityName, Publisher, PublisherDisplayName (可选 DisplayName)'
  }
  Write-Host "覆盖 Identity.Name => $($storeJson.IdentityName)" -ForegroundColor Yellow
  Write-Host "覆盖 Identity.Publisher => $($storeJson.Publisher)" -ForegroundColor Yellow
  Write-Host "覆盖 PublisherDisplayName => $($storeJson.PublisherDisplayName)" -ForegroundColor Yellow
  $identityNode.Name = $storeJson.IdentityName
  $identityNode.Publisher = $storeJson.Publisher
  $manifestXml.Package.Properties.PublisherDisplayName = $storeJson.PublisherDisplayName
  if ($storeJson.DisplayName) {
    Write-Host "覆盖 Properties.DisplayName => $($storeJson.DisplayName)" -ForegroundColor Yellow
    # 设置 <Properties><DisplayName>
    $displayNode = $manifestXml.SelectSingleNode('/f:Package/f:Properties/f:DisplayName', (New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)))
    # 由于上方未附加 nsMgr, 重新构建并添加命名空间
    $nsMgr = New-Object System.Xml.XmlNamespaceManager($manifestXml.NameTable)
    $nsMgr.AddNamespace('f','http://schemas.microsoft.com/appx/manifest/foundation/windows10')
    $nsMgr.AddNamespace('uap','http://schemas.microsoft.com/appx/manifest/uap/windows10')
    $displayNode = $manifestXml.SelectSingleNode('/f:Package/f:Properties/f:DisplayName', $nsMgr)
    if ($displayNode) { $displayNode.InnerText = $storeJson.DisplayName }
    # 同步 uap:VisualElements 的 DisplayName
    $visualNode = $manifestXml.SelectSingleNode('/f:Package/f:Applications/f:Application/uap:VisualElements', $nsMgr)
    if ($visualNode) { $visualNode.SetAttribute('DisplayName', $storeJson.DisplayName) }
  }
  $manifestXml.Save((Resolve-Path $Manifest))
  $publisher = $identityNode.Publisher
}

# StoreMode 版本校验：第四段必须为 0
if ($StoreMode) {
  $segments = $manifestVersion.Split('.')
  if ($segments.Length -ne 4 -or $segments[3] -ne '0') {
    Fail "StoreMode: 版本 $manifestVersion 不符合要求 (第四段必须为 0)"
  } else {
    Write-Host "StoreMode 校验通过 (Version=$manifestVersion)" -ForegroundColor Green
  }
}

# --- 读取证书 Subject ---
Write-Host "Manifest Publisher: $publisher" -ForegroundColor Green
if ($Sha1) {
  Write-Host "使用指定 Sha1: $Sha1 (将使用 /sha1)" -ForegroundColor Green
  # 尝试从存储中检索以便显示 Subject
  $matchCert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq ($Sha1 -replace '[^A-Fa-f0-9]','') | Select-Object -First 1
  if ($matchCert) {
    Write-Host "匹配证书 Subject: $($matchCert.Subject)" -ForegroundColor Green
    if ($publisher -ne $matchCert.Subject -and -not $Force) {
      Fail 'Publisher 与指定 Sha1 证书 Subject 不一致'
    }
  } else {
    Write-Warning '未在 CurrentUser\\My 找到指定 Sha1 证书（仍尝试签名，如果系统其他存储有该证书可能成功）'
  }
} else {
  try {
    $pfxCert = Get-PfxCertificate -FilePath $PfxPath
  } catch { Fail "读取 PFX 失败: $_" }
  $certSubject = $pfxCert.Subject
  Write-Host "PFX Subject      : $certSubject" -ForegroundColor Green
  if ($publisher -ne $certSubject) {
    $msg = 'Publisher 与 PFX Subject 不一致，将导致包分析器报错。'
    if (-not $Force) { Fail $msg }
    Write-Warning $msg
  }
}

# 自动清理证书（仅当未指定 Sha1 或 Sha1 存在）
if ($AutoPruneCerts) {
  Write-Section '自动清理证书'
  $targetThumb = if ($Sha1) { ($Sha1 -replace '[^A-Fa-f0-9]','').ToUpper() } else { $pfxCert.Thumbprint.ToUpper() }
  $all = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -like '*DuplicateFileFinder Dev*'
  foreach ($c in $all) {
    if ($c.Thumbprint.ToUpper() -ne $targetThumb) {
      Write-Host "移除额外证书: $($c.Thumbprint) $($c.Subject)" -ForegroundColor Yellow
      try { Remove-Item $c.PSPath -Force } catch { Write-Warning "移除失败: $_" }
    }
  }
  # 移除根证书若误存于 My
  $rootInMy = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq 'CN=DuplicateFileFinder Dev Test Root'
  foreach ($r in $rootInMy) {
    Write-Host "移除 My 中根证书: $($r.Thumbprint)" -ForegroundColor Yellow
    try { Remove-Item $r.PSPath -Force } catch { Write-Warning "移除根失败: $_" }
  }
}

if (-not $MsixName) {
  $baseVersion = $manifestVersion
  $MsixName = "${appName}_${baseVersion}_x64.msix"
}
$msixPath = Join-Path $OutputDir $MsixName

# --- 构建发布 ---
if (-not $SkipBuild) {
  Write-Section 'dotnet publish'
  if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
  $scArg = $SelfContained.IsPresent ? '--self-contained true' : '--self-contained false'
  $publishCmd = "dotnet publish `"$Project`" -c $Config -r $Runtime $scArg -p:PublishSingleFile=false -p:IncludeAllContentForSelfExtract=true -o `"$publishDir`""
  Write-Host $publishCmd -ForegroundColor DarkGray
  $pub = Invoke-Expression $publishCmd
  if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish 失败' }
}
else {
  Write-Section '跳过构建 (SkipBuild)'
  if (-not (Test-Path $publishDir)) { Fail '跳过构建但 publish 输出目录不存在' }
}

# --- 准备 Layout ---
Write-Section '准备 Layout'
if (Test-Path $layoutDir) { Remove-Item -Recurse -Force $layoutDir }
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null

# 复制发布输出
Copy-Item -Path (Join-Path $publishDir '*') -Destination $layoutDir -Recurse -Force

# 复制 Icon/Assets (如果已经在发布输出里，可忽略；此处尝试从项目资源目录推测)
# 收集可能的资产目录；单独调用 Join-Path 避免数组作为 AdditionalChildPath 触发错误
$projDir = Split-Path (Resolve-Path $Project) -Parent
$assetsCandidatesRaw = @(
  (Join-Path $projDir 'Assets'),
  (Join-Path $projDir 'assets')
)
$assetsCandidates = $assetsCandidatesRaw | Where-Object { Test-Path $_ }
foreach ($ac in $assetsCandidates) {
  Write-Host "复制资产: $ac" -ForegroundColor DarkCyan
  Copy-Item -Path (Join-Path $ac '*') -Destination (Join-Path $layoutDir 'Assets') -Recurse -Force
}

# 复制 Manifest
Copy-Item -Path $Manifest -Destination (Join-Path $layoutDir 'AppxManifest.xml') -Force

# --- 查找 makeappx & signtool ---
Write-Section '定位工具 (makeappx, signtool)'
function Find-InSDK([string]$exe){
  $pf       = [Environment]::GetEnvironmentVariable('ProgramFiles')
  $pf86     = [Environment]::GetEnvironmentVariable('ProgramFiles(x86)')
  $sdkRoots = @(
    (Join-Path $pf   'Windows Kits\10\bin'),
    (Join-Path $pf86 'Windows Kits\10\bin')
  ) | Where-Object { $_ -and (Test-Path $_) }
  Write-Host "搜索 SDK 根: $($sdkRoots -join '; ')" -ForegroundColor DarkGray
  foreach ($root in $sdkRoots) {
    # 首先枚举第一层版本目录
    Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | ForEach-Object {
      $verDir = $_.FullName
      # 直接在版本目录尝试 (有的工具直接放这里)
      $candidate = Join-Path $verDir $exe
      if (Test-Path $candidate) { return $candidate }
      # 再尝试 x64 子目录
      $candidate64 = Join-Path (Join-Path $verDir 'x64') $exe
      if (Test-Path $candidate64) { return $candidate64 }
    }
    # 兜底递归（限制深度 4 层防止过慢）
    $found = Get-ChildItem -Path $root -Recurse -ErrorAction SilentlyContinue -Include $exe | Select-Object -First 1
    if ($found) { return $found.FullName }
  }
  return $null
}

$makeappx = Get-Command makeappx -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue
if (-not $makeappx) { $makeappx = Find-InSDK 'makeappx.exe' }
if ($makeappx -is [System.Array]) {
  # 优先包含 \x64\ 的，其次第一个
  $picked = $makeappx | Where-Object { $_ -match '\\x64\\' } | Select-Object -First 1
  if (-not $picked) { $picked = $makeappx | Select-Object -First 1 }
  $makeappx = $picked
}
if (-not $makeappx) { Fail '未找到 makeappx.exe (请安装 Windows SDK: 启动“安装程序 -> 单个组件 -> Windows 10/11 SDK”)' }
Write-Host "makeappx: $makeappx" -ForegroundColor Green

$signtool = Get-Command signtool -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue
if (-not $signtool) { $signtool = Find-InSDK 'signtool.exe' }
if ($signtool -is [System.Array]) {
  $picked = $signtool | Where-Object { $_ -match '\\x64\\' } | Select-Object -First 1
  if (-not $picked) { $picked = $signtool | Select-Object -First 1 }
  $signtool = $picked
}
if (-not $signtool) { Fail '未找到 signtool.exe (请安装 Windows SDK)' }
Write-Host "signtool: $signtool" -ForegroundColor Green

# --- 打包 ---
Write-Section 'makeappx pack'
if (Test-Path $msixPath) { Remove-Item -Force $msixPath }
Write-Host "& $makeappx pack /d $layoutDir /p $msixPath" -ForegroundColor DarkGray
& $makeappx pack /d $layoutDir /p $msixPath
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $msixPath)) { Fail 'makeappx 打包失败' }

# --- 签名 ---
Write-Section '签名'
if (-not $Password) {
  $secure = Read-Host '输入 PFX 密码(不会显示)' -AsSecureString
  $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
  $Password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
  [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

if ($Sha1) {
  Write-Host "& $signtool sign /fd SHA256 /sha1 $Sha1 /tr $SignTimestampUrl /td SHA256 $msixPath" -ForegroundColor DarkGray
  & $signtool sign /fd SHA256 /sha1 $Sha1 /tr $SignTimestampUrl /td SHA256 $msixPath
} else {
  Write-Host "& $signtool sign /fd SHA256 /f $(Resolve-Path $PfxPath) /p <隐藏> /tr $SignTimestampUrl /td SHA256 $msixPath" -ForegroundColor DarkGray
  & $signtool sign /fd SHA256 /f (Resolve-Path $PfxPath) /p $Password /tr $SignTimestampUrl /td SHA256 $msixPath
}
if ($LASTEXITCODE -ne 0) { Fail '签名失败' }

# --- 验证 ---
Write-Section '验证签名'
Write-Host "& $signtool verify /pa /v $msixPath" -ForegroundColor DarkGray
& $signtool verify /pa /v $msixPath
if ($LASTEXITCODE -ne 0) { Fail '签名验证失败' }

$actualSubject = (Get-AuthenticodeSignature $msixPath).SignerCertificate.Subject
Write-Host "最终包签名 Subject: $actualSubject" -ForegroundColor Green
if ($actualSubject -ne $publisher) {
  Write-Warning '最终签名 Subject 与 Manifest Publisher 仍不一致，包分析器会报错。'
} else {
  Write-Host 'Subject 与 Manifest Publisher 匹配 ✅' -ForegroundColor Green
}

if (-not $KeepLayout) {
  Write-Section '清理 Layout'
  Remove-Item -Recurse -Force $layoutDir -ErrorAction SilentlyContinue
}

Write-Section '完成'
Write-Host "MSIX: $(Resolve-Path $msixPath)" -ForegroundColor Green
Write-Host '可用 MSIX Packaging Tool 进行包分析；若失败请保留 -KeepLayout 重新运行并发送日志。'
