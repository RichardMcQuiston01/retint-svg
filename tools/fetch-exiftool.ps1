<#
.SYNOPSIS
    Downloads the 64-bit Windows build of ExifTool and stages exiftool.exe (plus
    its required exiftool_files\ runtime folder) into a destination directory.

.DESCRIPTION
    The "Edit metadata…" context-menu action shells out to a bundled exiftool.exe.
    ExifTool's ~6 MB Windows package is NOT committed to the repository; instead it
    is fetched at build time by this script (run by CI, or manually by a developer
    who wants to test metadata editing from a local `dotnet build`).

    The version is resolved from exiftool.org/ver.txt (the current production
    release) so the pin never goes stale as old versions are retired from the site.
    Pass -Version to override, or edit $DefaultVersion below for the offline fallback.

    Nothing else in the build depends on this succeeding — if ExifTool is absent the
    menu item still appears but reports "exiftool.exe was not found" when used.

.PARAMETER Destination
    The directory to stage exiftool.exe and exiftool_files\ into (created if needed).

.PARAMETER Version
    Optional explicit ExifTool version (e.g. "13.30"). Defaults to the current
    production version from exiftool.org, falling back to $DefaultVersion.

.EXAMPLE
    pwsh tools/fetch-exiftool.ps1 -Destination bin/Release/net48
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Destination,

    [string] $Version
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Offline fallback if exiftool.org/ver.txt can't be reached. Bump when it ages out.
$DefaultVersion = '13.30'

if (-not $Version) {
    try {
        $Version = (Invoke-RestMethod -Uri 'https://exiftool.org/ver.txt' -TimeoutSec 30).ToString().Trim()
        Write-Host "Resolved current ExifTool production version: $Version"
    }
    catch {
        $Version = $DefaultVersion
        Write-Warning "Could not resolve current version from exiftool.org/ver.txt; using pinned $Version."
    }
}

$zipName = "exiftool-${Version}_64.zip"
$url = "https://exiftool.org/$zipName"

$work = Join-Path ([IO.Path]::GetTempPath()) ("exiftool-" + [Guid]::NewGuid().ToString('N'))
$zipPath = Join-Path $work $zipName
$extractDir = Join-Path $work 'extracted'
New-Item -ItemType Directory -Path $work -Force | Out-Null

try {
    Write-Host "Downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $zipPath -TimeoutSec 300

    Write-Host "Extracting..."
    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force

    # The zip lays out exiftool.exe alongside an exiftool_files\ folder (possibly
    # nested one level under exiftool-<ver>_64\). Find the exe and take its sibling.
    $exe = Get-ChildItem -Path $extractDir -Recurse -Filter 'exiftool.exe' | Select-Object -First 1
    if (-not $exe) { throw "exiftool.exe was not found inside $zipName." }

    $sourceDir = $exe.Directory.FullName
    $filesDir = Join-Path $sourceDir 'exiftool_files'
    if (-not (Test-Path $filesDir)) {
        throw "exiftool_files\ folder was not found next to exiftool.exe (unexpected package layout)."
    }

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    $destResolved = (Resolve-Path $Destination).Path

    Copy-Item -Path $exe.FullName -Destination (Join-Path $destResolved 'exiftool.exe') -Force
    Copy-Item -Path $filesDir -Destination (Join-Path $destResolved 'exiftool_files') -Recurse -Force

    Write-Host "Staged ExifTool $Version into $destResolved"
}
finally {
    Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue
}
