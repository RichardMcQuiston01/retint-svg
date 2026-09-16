<#
.SYNOPSIS
    Installs ExifTool via Chocolatey and stages exiftool.exe (plus its
    exiftool_files\ runtime folder, if the package ships one) into a destination
    directory.

.DESCRIPTION
    The "Edit metadata…" context-menu action shells out to a bundled exiftool.exe.
    ExifTool's ~6 MB Windows build is NOT committed to the repository; it is
    installed at build time by this script (run by CI, or manually by a developer
    who wants to test metadata editing from a local `dotnet build`) and copied next
    to the handler.

    Acquisition goes through **Chocolatey** (the same package manager CI already
    uses for Inno Setup) rather than a direct exiftool.org download: the site keeps
    only its current versions and its per-version _64.zip URLs are unreliable (a
    version reported by ver.txt / linked on the home page can still 404), whereas
    `choco install exiftool` resolves a known-good package every time.

    The real exiftool.exe lives under the Chocolatey package's tools folder (not the
    shim in choco\bin); this script finds it there and copies it — with its sibling
    exiftool_files\ folder when the package is the folder-based build — into the
    destination. If the package is the standalone single-exe build, exiftool.exe is
    copied alone (it runs without exiftool_files\).

    Nothing else in the build depends on this succeeding — if ExifTool is absent the
    menu item still appears but reports "exiftool.exe was not found" when used.

.PARAMETER Destination
    The directory to stage exiftool.exe (and exiftool_files\) into (created if needed).

.PARAMETER Version
    Optional explicit ExifTool package version passed to `choco install --version`.
    Omit to install the latest packaged version.

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

$choco = Get-Command choco -ErrorAction SilentlyContinue
if (-not $choco) {
    throw "Chocolatey (choco) was not found. Install it (https://chocolatey.org/install) " +
          "or place exiftool.exe manually in '$Destination'."
}

$installArgs = @('install', 'exiftool', '-y', '--no-progress')
if ($Version) { $installArgs += @('--version', $Version) }

Write-Host "choco $($installArgs -join ' ')"
& choco @installArgs
if ($LASTEXITCODE -ne 0) { throw "choco install exiftool failed with exit code $LASTEXITCODE." }

# The real exe is under <ChocolateyInstall>\lib\exiftool\ (the choco\bin\exiftool.exe
# is only a shim). Search the package folder for it.
$chocoRoot = if ($env:ChocolateyInstall) { $env:ChocolateyInstall } else { 'C:\ProgramData\chocolatey' }
$packageDir = Join-Path $chocoRoot 'lib\exiftool'
if (-not (Test-Path $packageDir)) {
    throw "ExifTool package folder not found at '$packageDir' after install."
}

$exe = Get-ChildItem -Path $packageDir -Recurse -Filter 'exiftool.exe' -ErrorAction SilentlyContinue |
    Select-Object -First 1
if (-not $exe) {
    # Some package versions ship the standalone build named exiftool(-k).exe.
    $exe = Get-ChildItem -Path $packageDir -Recurse -Filter 'exiftool(-k).exe' -ErrorAction SilentlyContinue |
        Select-Object -First 1
}
if (-not $exe) { throw "exiftool executable not found under '$packageDir'." }

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$destResolved = (Resolve-Path $Destination).Path

# Always land it as exiftool.exe (the handler looks for that exact name).
Copy-Item -Path $exe.FullName -Destination (Join-Path $destResolved 'exiftool.exe') -Force

# Folder-based builds keep a required exiftool_files\ next to the exe; copy it too.
$filesDir = Join-Path $exe.Directory.FullName 'exiftool_files'
if (Test-Path $filesDir) {
    Copy-Item -Path $filesDir -Destination (Join-Path $destResolved 'exiftool_files') -Recurse -Force
    Write-Host "Staged exiftool.exe + exiftool_files\ into $destResolved"
}
else {
    Write-Host "Staged standalone exiftool.exe into $destResolved"
}
