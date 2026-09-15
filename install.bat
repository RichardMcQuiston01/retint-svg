@echo off
setlocal EnableDelayedExpansion

echo ============================================================
echo  SVG Tools Shell Extension - Installer
echo ============================================================
echo.

:: Verify we are running as Administrator
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click install.bat and choose "Run as administrator".
    pause
    exit /b 1
)

set DLL=%~dp0bin\Release\net48\SVGToolsShell.dll
set WORKER=%~dp0bin\Release\net48\ImageResizer.Worker.exe
set REGASM=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe
set SVGGUID={FC258F52-702A-4AC2-BA22-43F59C7DC682}
set IMGGUID={25EF2E9B-582C-46C0-9FF2-EF10313F09D1}

:: Confirm the DLL exists
if not exist "%DLL%" (
    echo ERROR: SVGToolsShell.dll not found at:
    echo   %DLL%
    echo Build the project in Visual Studio first ^(Release configuration^).
    pause
    exit /b 1
)

:: The image resizer menu launches ImageResizer.Worker.exe from beside the DLL.
:: Warn (don't fail — the SVG menu works without it) if the worker is missing.
if not exist "%WORKER%" (
    echo WARNING: ImageResizer.Worker.exe was not found next to the DLL:
    echo   %WORKER%
    echo The "Resize Images" menu will appear but cannot run until the worker
    echo ships alongside the DLL. Build the whole solution ^(the build stages the
    echo worker automatically^) or use a Release artifact that includes it.
    echo.
)

:: Confirm RegAsm exists
if not exist "%REGASM%" (
    echo ERROR: RegAsm.exe not found. Ensure .NET Framework 4.8 is installed.
    pause
    exit /b 1
)

echo Registering COM server...
"%REGASM%" "%DLL%" /codebase /nologo
if errorlevel 1 (
    echo ERROR: RegAsm registration failed.
    pause
    exit /b 1
)

echo.
echo Registration successful!
echo.

:: Guard: confirm RegAsm actually created the COM classes for BOTH handlers.
:: RegAsm writes HKCR\CLSID\{guid}\InprocServer32 for every COM-visible class in
:: the DLL, so a missing key here means the DLL just registered is an OLD build
:: that predates that handler. The association keys added below would then point
:: at a CLSID Explorer cannot load, and the menu silently never appears (exactly
:: the failure this guard prevents). Fail loudly instead of registering a dangling
:: association.
echo Verifying COM classes were registered...
reg query "HKCR\CLSID\%SVGGUID%\InprocServer32" >nul 2>&1
if errorlevel 1 (
    echo ERROR: The SVG handler COM class %SVGGUID% is not registered after RegAsm.
    echo        "%DLL%" appears to be missing SvgContextMenu ^(wrong or corrupt build^).
    pause
    exit /b 1
)
reg query "HKCR\CLSID\%IMGGUID%\InprocServer32" >nul 2>&1
if errorlevel 1 (
    echo ERROR: The image resizer COM class %IMGGUID% is not registered after RegAsm.
    echo        "%DLL%" does not contain ImageContextMenu — it is an old build that
    echo        predates the image resizer. Build or download a current Release
    echo        ^(one that includes ImageContextMenu^) and re-run this installer.
    pause
    exit /b 1
)

:: Register the context menu handler directly under the .svg extension
echo Registering .svg context menu handler...
reg add "HKCR\.svg\shellex\ContextMenuHandlers\SVGToolsShell" /ve /d "{FC258F52-702A-4AC2-BA22-43F59C7DC682}" /f >nul
if errorlevel 1 (
    echo ERROR: Failed to register context menu handler.
    pause
    exit /b 1
)

:: Register under SystemFileAssociations — this location is consulted by
:: Explorer regardless of the user's chosen default app (UserChoice ProgId).
:: Without it, the handler is never queried when .svg is associated with a
:: browser (e.g. ChromeHTML), because the svgfile ProgId is bypassed.
echo Registering SystemFileAssociations handler...
reg add "HKLM\SOFTWARE\Classes\SystemFileAssociations\.svg\ShellEx\ContextMenuHandlers\SVGToolsShell" /ve /d "{FC258F52-702A-4AC2-BA22-43F59C7DC682}" /f >nul
if errorlevel 1 (
    echo ERROR: Failed to register SystemFileAssociations handler.
    pause
    exit /b 1
)

:: Add to Windows shell extension approved list (required to load)
echo Approving shell extension...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v "{FC258F52-702A-4AC2-BA22-43F59C7DC682}" /d "SVGToolsShell" /f >nul
if errorlevel 1 (
    echo ERROR: Failed to approve shell extension.
    pause
    exit /b 1
)

:: ── Image Resizer context menu (separate COM server) ─────────────────────
:: A distinct handler (GUID below) associated with common raster image types.
:: Registered the same three ways as the SVG handler, for each extension.
echo Registering image resizer context menu handler...
for %%E in (.png .jpg .jpeg .bmp .gif .tif .tiff) do (
    reg add "HKCR\%%E\shellex\ContextMenuHandlers\SVGToolsImageResizer" /ve /d "%IMGGUID%" /f >nul
    reg add "HKLM\SOFTWARE\Classes\SystemFileAssociations\%%E\ShellEx\ContextMenuHandlers\SVGToolsImageResizer" /ve /d "%IMGGUID%" /f >nul
)
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v "%IMGGUID%" /d "SVGToolsImageResizer" /f >nul

echo.
echo Restarting Windows Explorer to apply the context menu...
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start explorer.exe

echo.
echo Done. Right-click any .svg file to see "SVG Tools", or any image
echo (.png/.jpg/.jpeg/.bmp/.gif/.tif/.tiff) to see "Resize Images".
pause
