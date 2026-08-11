#Requires -Version 5.1
<#
  SPIKE — build a distributable MSIX package for the handler.

  Produces a signed .msix from the published handler + manifest. Requires the
  Windows SDK (makeappx.exe, signtool.exe on PATH) and a code-signing cert.

  For Store submission you typically DON'T sign here — Partner Center signs the
  package and assigns Identity/@Name + @Publisher. For sideloading, sign with a
  cert users can trust and distribute the .msix (or an .msixbundle).

  See register-dev.ps1 for the faster loose-file dev loop.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$SpikeRoot = (Resolve-Path "$PSScriptRoot\.."),
    [string]$OutputMsix = (Join-Path (Resolve-Path "$PSScriptRoot\..") 'build\SvgTools.msix'),

    # Signing (omit -Sign for an unsigned package, e.g. for Store upload).
    [switch]$Sign,
    [string]$CertThumbprint,                       # cert in CurrentUser\My …
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$project  = Join-Path $SpikeRoot 'src\SvgTools.ShellExtension.csproj'
$manifest = Join-Path $SpikeRoot 'AppxManifest.xml'
$stage    = Join-Path $SpikeRoot 'build\stage'
$stub     = Join-Path $SpikeRoot 'package\stub\SvgTools.Launcher.exe'

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "${env:ProgramFiles}\Windows Kits\10\bin")
    foreach ($root in $roots) {
        if (Test-Path $root) {
            $hit = Get-ChildItem $root -Recurse -Filter $name -ErrorAction SilentlyContinue |
                   Where-Object { $_.FullName -match 'x64' } | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    throw "$name not found. Install the Windows 10/11 SDK."
}

$makeappx = Find-SdkTool 'makeappx.exe'

Write-Host "==> Publishing handler ($Configuration, win-x64)…"
dotnet publish $project -c $Configuration -r win-x64 --self-contained false -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Staging manifest + assets + stub…"
Copy-Item $manifest (Join-Path $stage 'AppxManifest.xml') -Force
$assetsSrc = Join-Path $SpikeRoot 'Assets'
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc (Join-Path $stage 'Assets') -Recurse -Force }
if (-not (Test-Path $stub)) { throw "Stub executable not found at $stub (see register-dev.ps1 note)." }
Copy-Item $stub (Join-Path $stage 'SvgTools.Launcher.exe') -Force

Write-Host "==> Packing $OutputMsix…"
New-Item -ItemType Directory -Force -Path (Split-Path $OutputMsix) | Out-Null
& $makeappx pack /d $stage /p $OutputMsix /overwrite
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

if ($Sign) {
    if (-not $CertThumbprint) { throw "-Sign requires -CertThumbprint" }
    $signtool = Find-SdkTool 'signtool.exe'
    Write-Host "==> Signing…"
    & $signtool sign /fd SHA256 /sha1 $CertThumbprint /tr $TimestampUrl /td SHA256 $OutputMsix
    if ($LASTEXITCODE -ne 0) { throw "signtool sign failed" }
    Write-Host "Signed: $OutputMsix"
} else {
    Write-Host "Unsigned package: $OutputMsix (fine for Store upload; sign for sideloading)."
}
