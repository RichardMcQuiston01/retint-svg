#Requires -Version 5.1
<#
  verify-registration.ps1
  Read-only health check for the SVG Tools shell extensions. Reports, for BOTH
  handlers (SVG re-tint and image resizer), whether:
    - the COM class is registered (HKCR\CLSID\{guid}\InprocServer32) and its DLL
      still exists on disk,
    - the handler is on the shell "Approved" list,
    - every file-type association points at the handler's GUID.
  For the image resizer it also checks that ImageResizer.Worker.exe sits beside
  the registered DLL (the handler launches it from there).

  No admin rights needed — it only reads the registry. Run it after install.bat
  to confirm the menus will actually appear, or when a menu is missing to see
  exactly which piece is absent.

    powershell -ExecutionPolicy Bypass -File .\verify-registration.ps1

  Exit code 0 = all checks passed, 1 = at least one problem found.
#>
[CmdletBinding()]
param()

$ok = $true

function Test-RegPath { param([string]$Path) try { Test-Path -LiteralPath $Path } catch { $false } }

function Get-RegValue {
    param([string]$Path, [string]$Name)
    try { (Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction Stop).$Name }
    catch { $null }
}

function Report {
    param([bool]$Pass, [string]$Message)
    if ($Pass) {
        Write-Host ("  [ OK ] " + $Message) -ForegroundColor Green
    } else {
        Write-Host ("  [FAIL] " + $Message) -ForegroundColor Red
        $script:ok = $false
    }
}

$handlers = @(
    [pscustomobject]@{
        Title = 'SVG re-tint handler'
        Name  = 'SVGToolsShell'
        Guid  = '{FC258F52-702A-4AC2-BA22-43F59C7DC682}'
        Exts  = @('.svg')
        Worker = $null
    },
    [pscustomobject]@{
        Title = 'Image resizer handler'
        Name  = 'SVGToolsImageResizer'
        Guid  = '{25EF2E9B-582C-46C0-9FF2-EF10313F09D1}'
        Exts  = @('.png', '.jpg', '.jpeg', '.bmp', '.gif', '.tif', '.tiff')
        Worker = 'ImageResizer.Worker.exe'
    }
)

$approvedKey = 'Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved'

foreach ($h in $handlers) {
    Write-Host ""
    Write-Host ("== {0}  {1} ==" -f $h.Title, $h.Guid) -ForegroundColor Cyan

    # 1) COM class registered, and its DLL present on disk.
    $inproc = "Registry::HKEY_CLASSES_ROOT\CLSID\$($h.Guid)\InprocServer32"
    $clsidRegistered = Test-RegPath $inproc
    Report $clsidRegistered "COM class registered (HKCR\CLSID\...\InprocServer32)"

    $dllPath = $null
    if ($clsidRegistered) {
        $codebase = Get-RegValue $inproc 'CodeBase'
        if ($codebase) {
            try { $dllPath = ([Uri]$codebase).LocalPath } catch { $dllPath = $codebase }
            Report (Test-Path -LiteralPath $dllPath) "DLL exists on disk: $dllPath"
        } else {
            Report $false "CodeBase value present (registered without /codebase?)"
        }
    }

    # 2) Approved list.
    $approved = Get-RegValue $approvedKey $h.Guid
    Report ($approved -eq $h.Name) "On the shell Approved list (value = '$approved')"

    # 3) Associations for each extension (both HKCR and SystemFileAssociations).
    foreach ($ext in $h.Exts) {
        $hkcr = "Registry::HKEY_CLASSES_ROOT\$ext\shellex\ContextMenuHandlers\$($h.Name)"
        $sfa  = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\$ext\ShellEx\ContextMenuHandlers\$($h.Name)"
        $hkcrVal = Get-RegValue $hkcr '(default)'
        $sfaVal  = Get-RegValue $sfa  '(default)'
        Report ($hkcrVal -eq $h.Guid) "$ext -> HKCR association"
        Report ($sfaVal  -eq $h.Guid) "$ext -> SystemFileAssociations association"

        # The exact failure mode install.bat's guard now prevents: an association
        # that points at a CLSID which isn't registered. Call it out explicitly.
        if (($hkcrVal -eq $h.Guid -or $sfaVal -eq $h.Guid) -and -not $clsidRegistered) {
            Write-Host "         ^ association points at an UNREGISTERED class — the menu will not appear." -ForegroundColor Yellow
            Write-Host "           The registered DLL is likely an old build. Re-register a current build." -ForegroundColor Yellow
        }
    }

    # 4) Worker sits beside the DLL (image handler only).
    if ($h.Worker -and $dllPath) {
        $workerPath = Join-Path (Split-Path -Parent $dllPath) $h.Worker
        Report (Test-Path -LiteralPath $workerPath) "Worker beside the DLL: $workerPath"
    }
}

Write-Host ""
if ($ok) {
    Write-Host "All checks passed. The context menus should appear (use 'Show more options' on Windows 11)." -ForegroundColor Green
    exit 0
} else {
    Write-Host "One or more checks failed — see [FAIL] lines above. Re-run install.bat as Administrator" -ForegroundColor Red
    Write-Host "against a current build, then run this script again." -ForegroundColor Red
    exit 1
}
