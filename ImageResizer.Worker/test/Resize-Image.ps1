#Requires -Version 5.1
<#
  Resize-Image.ps1
  Ad-hoc runner for ImageResizer.Worker.exe against your own image(s).
  Shows the worker's normal summary dialog (does not auto-close it).

  Examples:
    .\Resize-Image.ps1 -Path C:\pics\photo.jpg -Percent 50
    .\Resize-Image.ps1 -Path C:\pics\a.jpg,C:\pics\b.png -LongestEdge 1024
    .\Resize-Image.ps1 -Path C:\pics\photo.jpg -Width 640 -Height 480
    .\Resize-Image.ps1 -Path C:\pics\photo.jpg -Percent 100 -JpegQuality 40
#>
[CmdletBinding(DefaultParameterSetName = 'Percent')]
param(
    [Parameter(Mandatory)][string[]]$Path,

    [Parameter(ParameterSetName = 'Percent')][int]$Percent = 50,
    [Parameter(ParameterSetName = 'LongestEdge', Mandatory)][int]$LongestEdge,
    [Parameter(ParameterSetName = 'Exact', Mandatory)][int]$Width,
    [Parameter(ParameterSetName = 'Exact', Mandatory)][int]$Height,

    [int]$JpegQuality = 85,
    [switch]$NoUpscale,
    [string]$ReleaseDir = "F:\Downloads\SVGToolsShell-Release"
)

$ErrorActionPreference = 'Stop'

$exe = Get-ChildItem -Path $ReleaseDir -Recurse -Filter 'ImageResizer.Worker.exe' -ErrorAction SilentlyContinue |
       Select-Object -First 1
if (-not $exe) { throw "ImageResizer.Worker.exe not found under '$ReleaseDir'." }
Get-ChildItem -Path (Split-Path $exe.FullName) -Recurse -File | Unblock-File -ErrorAction SilentlyContinue

$files = $Path | ForEach-Object { (Resolve-Path $_).Path }

$size = switch ($PSCmdlet.ParameterSetName) {
    'Percent'     { @{ Kind = 0; Percent = $Percent } }
    'LongestEdge' { @{ Kind = 1; LongestEdge = $LongestEdge } }
    'Exact'       { @{ Kind = 2; Width = $Width; Height = $Height } }
}

$job = @{ Size = $size; JpegQuality = $JpegQuality; AllowUpscale = (-not $NoUpscale)
          OutputLocation = 'sibling'; Files = $files }

$jobPath = Join-Path $env:TEMP ("resize-job-" + [guid]::NewGuid().ToString('N') + ".json")
$job | ConvertTo-Json -Depth 6 | Set-Content -Path $jobPath -Encoding UTF8

Write-Host "Job: $jobPath"
Write-Host ($job | ConvertTo-Json -Depth 6)
Start-Process -FilePath $exe.FullName -ArgumentList "`"$jobPath`"" -Wait
Remove-Item $jobPath -ErrorAction SilentlyContinue
Write-Host "Done. Outputs are written beside each source file."
