<#
.SYNOPSIS
    Downloads the 64-bit Windows build of ExifTool and stages exiftool.exe (plus
    its required exiftool_files\ runtime folder) into a destination directory.

.DESCRIPTION
    The "Edit metadata…" context-menu action shells out to a bundled exiftool.exe.
    ExifTool's ~6 MB Windows package is NOT committed to the repository; instead it
    is fetched at build time by this script (run by CI, or manually by a developer
    who wants to test metadata editing from a local `dotnet build`).

    The exact download is discovered by scraping the "Windows Executable" link
    (exiftool-<ver>_64.zip) off exiftool.org's home page, so the URL always points
    at a file the site currently offers — unlike constructing it from a version
    number (exiftool.org/ver.txt reports the newest version overall, whose Windows
    _64.zip is not always the one currently published). Pass -Version to pin an
    explicit version instead; $DefaultVersion is the last-resort offline fallback.

    Nothing else in the build depends on this succeeding — if ExifTool is absent the
    menu item still appears but reports "exiftool.exe was not found" when used.

.PARAMETER Destination
    The directory to stage exiftool.exe and exiftool_files\ into (created if needed).

.PARAMETER Version
    Optional explicit ExifTool version (e.g. "13.30"). When omitted, the script
    scrapes the current Windows build off exiftool.org, falling back to $DefaultVersion.

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

# Last-resort fallback if exiftool.org can't be scraped. Bump when it ages out.
$DefaultVersion = '13.30'

$zipName = $null
if ($Version) {
    $zipName = "exiftool-${Version}_64.zip"
}
else {
    # Discover the actual "Windows Executable" zip currently offered on the home
    # page, e.g. <a href="exiftool-13.30_64.zip">Windows Executable</a>. This is
    # the definitive filename — ver.txt can report a version whose _64.zip 404s.
    try {
        $html = (Invoke-WebRequest -Uri 'https://exiftool.org/' -TimeoutSec 60 -UseBasicParsing).Content
        $m = [regex]::Match($html, 'exiftool-[0-9]+(?:\.[0-9]+)+_64\.zip')
        if ($m.Success) {
            $zipName = $m.Value
            Write-Host "Discovered current ExifTool Windows build: $zipName"
        }
        else {
            Write-Warning "Could not find a _64.zip link on exiftool.org; using pinned $DefaultVersion."
        }
    }
    catch {
        Write-Warning "Could not reach exiftool.org to discover the build; using pinned $DefaultVersion."
    }

    if (-not $zipName) { $zipName = "exiftool-${DefaultVersion}_64.zip" }
}

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

    Write-Host "Staged $zipName into $destResolved"
}
finally {
    Remove-Item -Path $work -Recurse -Force -ErrorAction SilentlyContinue
}
