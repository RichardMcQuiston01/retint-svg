#Requires -Version 5.1
<#
  Run-ResizerTests.ps1
  Automated smoke test for ImageResizer.Worker.exe (the Phase 2 resize worker).

  It generates its own test images (no need to supply any), runs the worker
  across every scenario, and verifies the outputs automatically — printing a
  PASS/FAIL table. The worker shows a summary dialog per run; this script waits
  for the output file, then closes the worker so the dialog auto-dismisses (you
  may see brief flashes — no clicking needed).

  Usage:
    powershell -ExecutionPolicy Bypass -File .\Run-ResizerTests.ps1
    # or, if the release is elsewhere:
    .\Run-ResizerTests.ps1 -ReleaseDir "F:\Downloads\SVGToolsShell-Release"
#>
[CmdletBinding()]
param(
    [string]$ReleaseDir = "F:\Downloads\SVGToolsShell-Release",
    [string]$WorkDir    = (Join-Path $env:TEMP ("resizer-tests-" + [guid]::NewGuid().ToString('N')))
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# --- Locate the worker (and clear any mark-of-the-web so it runs) -------------
$exe = Get-ChildItem -Path $ReleaseDir -Recurse -Filter 'ImageResizer.Worker.exe' -ErrorAction SilentlyContinue |
       Select-Object -First 1
if (-not $exe) { throw "ImageResizer.Worker.exe not found under '$ReleaseDir'." }
$worker = $exe.FullName
Get-ChildItem -Path (Split-Path $worker) -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
Write-Host "Worker : $worker"
Write-Host "Images : $WorkDir`n"

# --- Helpers ------------------------------------------------------------------
function New-QuadImage {
    param([string]$Path, [int]$W, [int]$H, [int]$Exif = 0, [string]$Format = 'jpg')
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $hw = [int]($W / 2); $hh = [int]($H / 2)
        $g.FillRectangle([System.Drawing.Brushes]::Red,    0,   0,   $hw, $hh)
        $g.FillRectangle([System.Drawing.Brushes]::Green,  $hw, 0,   $hw, $hh)
        $g.FillRectangle([System.Drawing.Brushes]::Blue,   0,   $hh, $hw, $hh)
        $g.FillRectangle([System.Drawing.Brushes]::Yellow, $hw, $hh, $hw, $hh)
    } finally { $g.Dispose() }

    if ($Exif -gt 0) {
        # PropertyItem has no public ctor; materialize one and set the EXIF
        # orientation tag (0x0112, SHORT, little-endian).
        $pi = [System.Runtime.Serialization.FormatterServices]::GetUninitializedObject(
                [System.Drawing.Imaging.PropertyItem])
        $pi.Id = 0x0112; $pi.Type = 3; $pi.Len = 2; $pi.Value = [byte[]]@([byte]$Exif, 0)
        $bmp.SetPropertyItem($pi)
    }

    $fmt = if ($Format -eq 'png') { [System.Drawing.Imaging.ImageFormat]::Png }
           else { [System.Drawing.Imaging.ImageFormat]::Jpeg }
    $bmp.Save($Path, $fmt); $bmp.Dispose()
}

function New-NoiseImage {
    param([string]$Path, [int]$W, [int]$H)
    $bmp = New-Object System.Drawing.Bitmap $W, $H
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $r = New-Object Random 12345
    try {
        for ($i = 0; $i -lt 4000; $i++) {
            $c = [System.Drawing.Color]::FromArgb($r.Next(256), $r.Next(256), $r.Next(256))
            $b = New-Object System.Drawing.SolidBrush $c
            $g.FillRectangle($b, $r.Next($W), $r.Next($H), $r.Next(1, 9), $r.Next(1, 9))
            $b.Dispose()
        }
    } finally { $g.Dispose() }
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Jpeg); $bmp.Dispose()
}

function Get-Dims {
    param([string]$Path)
    $img = [System.Drawing.Image]::FromFile($Path)
    try { [pscustomobject]@{ W = $img.Width; H = $img.Height } } finally { $img.Dispose() }
}

function Invoke-Resize {
    param([hashtable]$Size, [string[]]$Files, [int]$JpegQuality = 85,
          [bool]$AllowUpscale = $true, [string]$ExpectOutput)
    $job = @{ Size = $Size; JpegQuality = $JpegQuality; AllowUpscale = $AllowUpscale
              OutputLocation = 'sibling'; Files = $Files }
    $jobPath = Join-Path $WorkDir ("job-" + [guid]::NewGuid().ToString('N') + ".json")
    $job | ConvertTo-Json -Depth 6 | Set-Content -Path $jobPath -Encoding UTF8

    $p = Start-Process -FilePath $worker -ArgumentList "`"$jobPath`"" -PassThru
    $deadline = (Get-Date).AddSeconds(25)
    while ((Get-Date) -lt $deadline) {
        if ($ExpectOutput -and (Test-Path $ExpectOutput)) { break }
        if ($p.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    Start-Sleep -Milliseconds 400          # let the atomic move settle
    if (-not $p.HasExited) { try { $p.Kill() } catch { } }   # dismiss summary dialog
    Remove-Item $jobPath -ErrorAction SilentlyContinue
}

$script:results = [System.Collections.Generic.List[object]]::new()
function Check {
    param([string]$Name, [scriptblock]$Test)
    $status = 'FAIL'
    try { if (& $Test) { $status = 'PASS' } } catch { $status = "ERROR: $($_.Exception.Message)" }
    $script:results.Add([pscustomobject]@{ Status = $status; Test = $Name })
    Write-Host ("  [{0}] {1}" -f $status.PadRight(5), $Name)
}

# --- Source images ------------------------------------------------------------
$land  = Join-Path $WorkDir 'land.jpg';           New-QuadImage $land 400 200
$png   = Join-Path $WorkDir 'shot.png';           New-QuadImage $png  300 300 -Format png
$exif  = Join-Path $WorkDir 'portrait_exif.jpg';  New-QuadImage $exif 400 200 -Exif 6   # display => 200x400
$noise = Join-Path $WorkDir 'noise.jpg';          New-NoiseImage $noise 400 400
$errok = Join-Path $WorkDir 'errtest.jpg';        New-QuadImage $errok 100 100

Write-Host "Running scenarios..."

# 1) Percent
Invoke-Resize -Size @{Kind=0;Percent=50} -Files @($land) -ExpectOutput (Join-Path $WorkDir 'land_50pct.jpg')
Check "50% halves dimensions (400x200 -> 200x100)" {
    $d = Get-Dims (Join-Path $WorkDir 'land_50pct.jpg'); $d.W -eq 200 -and $d.H -eq 100 }

# 2) Longest edge, format preserved
Invoke-Resize -Size @{Kind=1;LongestEdge=150} -Files @($png) -ExpectOutput (Join-Path $WorkDir 'shot_150px.png')
Check "Longest-edge 150 on 300x300 -> 150x150, stays .png" {
    $d = Get-Dims (Join-Path $WorkDir 'shot_150px.png'); $d.W -eq 150 -and $d.H -eq 150 }

# 3) Exact dimensions
Invoke-Resize -Size @{Kind=2;Width=640;Height=480} -Files @($land) -ExpectOutput (Join-Path $WorkDir 'land_640x480.jpg')
Check "Exact 640x480" {
    $d = Get-Dims (Join-Path $WorkDir 'land_640x480.jpg'); $d.W -eq 640 -and $d.H -eq 480 }

# 4) EXIF orientation: stored 400x200, orientation 6 => display 200x400; 50% => 100x200.
#    If orientation were IGNORED, the output would be 200x100 instead.
Invoke-Resize -Size @{Kind=0;Percent=50} -Files @($exif) -ExpectOutput (Join-Path $WorkDir 'portrait_exif_50pct.jpg')
Check "EXIF orientation applied (upright => 100x200, not 200x100)" {
    $d = Get-Dims (Join-Path $WorkDir 'portrait_exif_50pct.jpg'); $d.W -eq 100 -and $d.H -eq 200 }

# 5) Collision -> _2 sibling (run the 50% land job again)
Invoke-Resize -Size @{Kind=0;Percent=50} -Files @($land) -ExpectOutput (Join-Path $WorkDir 'land_50pct_2.jpg')
Check "Collision creates _2 sibling (originals untouched)" {
    (Test-Path (Join-Path $WorkDir 'land_50pct.jpg')) -and (Test-Path (Join-Path $WorkDir 'land_50pct_2.jpg')) }

# 6) JPEG quality honored (low quality file smaller than high quality)
$q20 = Join-Path $WorkDir 'noise_100pct.jpg'
$q90 = Join-Path $WorkDir 'noise_100pct_2.jpg'
Invoke-Resize -Size @{Kind=0;Percent=100} -JpegQuality 20 -Files @($noise) -ExpectOutput $q20
Invoke-Resize -Size @{Kind=0;Percent=100} -JpegQuality 90 -Files @($noise) -ExpectOutput $q90
Check "JPEG quality honored (q20 file smaller than q90 file)" {
    (Get-Item $q20).Length -lt (Get-Item $q90).Length }

# 7) Multiple files with one bad path: the good one still succeeds
Invoke-Resize -Size @{Kind=0;Percent=50} `
    -Files @((Join-Path $WorkDir 'does-not-exist.jpg'), $errok) `
    -ExpectOutput (Join-Path $WorkDir 'errtest_50pct.jpg')
Check "Valid file still processed alongside a missing one" {
    Test-Path (Join-Path $WorkDir 'errtest_50pct.jpg') }

# 8) Hygiene: no leftover temp files, outputs not hidden
Check "No leftover .*.tmp work files" {
    -not (Get-ChildItem -Path $WorkDir -Filter '.*.tmp' -Force -ErrorAction SilentlyContinue) }
Check "Output file is not marked Hidden" {
    -not ((Get-Item (Join-Path $WorkDir 'land_50pct.jpg')).Attributes -band [IO.FileAttributes]::Hidden) }

# --- Report -------------------------------------------------------------------
Write-Host "`n================ RESULTS ================"
$script:results | Format-Table -AutoSize | Out-Host
$fail = @($script:results | Where-Object { $_.Status -ne 'PASS' }).Count
if ($fail -eq 0) {
    Write-Host "ALL $($script:results.Count) CHECKS PASSED" -ForegroundColor Green
} else {
    Write-Host "$fail of $($script:results.Count) CHECKS DID NOT PASS" -ForegroundColor Red
}
Write-Host "`nOpen the images folder to eyeball results (esp. orientation):"
Write-Host "  explorer `"$WorkDir`""
