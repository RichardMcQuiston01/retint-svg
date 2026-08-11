#Requires -Version 5.1
<#
  SPIKE — dev registration for iterative testing on Windows 11.

  Publishes the handler, stages it next to the manifest as a loose-file
  layout, and registers it with Add-AppxPackage -Register. No signing needed
  for loose-file registration, but Developer Mode must be ON
  (Settings > Privacy & security > For developers).

  This is the fast inner loop. For a distributable package use build.ps1.

  NOTE: AppxManifest.xml references SvgTools.Launcher.exe as the package's
  Executable. A context-menu-only package still needs a real executable file
  present or registration fails. Provide a stub (a 1-line no-op console app is
  enough) at package\stub\SvgTools.Launcher.exe, or edit the manifest's
  Application/@Executable to point at whatever stub you ship. This script will
  stop with a clear message if it is missing.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$SpikeRoot = (Resolve-Path "$PSScriptRoot\.."),
    [switch]$Unregister
)

$ErrorActionPreference = 'Stop'

$PackageFamilyName = 'RichardMcQuiston.SVGTools'  # matches Identity/@Name
$project = Join-Path $SpikeRoot 'src\SvgTools.ShellExtension.csproj'
$manifest = Join-Path $SpikeRoot 'AppxManifest.xml'
$stage    = Join-Path $SpikeRoot 'build\stage'
$stub     = Join-Path $SpikeRoot 'package\stub\SvgTools.Launcher.exe'

if ($Unregister) {
    $pkg = Get-AppxPackage -Name $PackageFamilyName -ErrorAction SilentlyContinue
    if ($pkg) { Remove-AppxPackage $pkg.PackageFullName; Write-Host "Unregistered $($pkg.PackageFullName)" }
    else      { Write-Host "Nothing registered under $PackageFamilyName" }
    return
}

Write-Host "==> Publishing handler ($Configuration, win-x64)…"
dotnet publish $project -c $Configuration -r win-x64 --self-contained false `
    -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Staging manifest + assets…"
Copy-Item $manifest (Join-Path $stage 'AppxManifest.xml') -Force
$assetsSrc = Join-Path $SpikeRoot 'Assets'
if (Test-Path $assetsSrc) { Copy-Item $assetsSrc (Join-Path $stage 'Assets') -Recurse -Force }

if (-not (Test-Path $stub)) {
    throw "Stub executable not found at $stub. See the note at the top of this script."
}
Copy-Item $stub (Join-Path $stage 'SvgTools.Launcher.exe') -Force

Write-Host "==> Registering loose-file package (Developer Mode required)…"
Add-AppxPackage -Register (Join-Path $stage 'AppxManifest.xml') -ForceUpdateFromAnyVersion

Write-Host "==> Restarting Explorer…"
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Process explorer

Write-Host "Done. Right-click a .svg file — 'Re-Tint' should appear in the main Win11 menu."
Write-Host "Re-run with -Unregister to remove."
