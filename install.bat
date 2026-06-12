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
set REGASM=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe

:: Confirm the DLL exists
if not exist "%DLL%" (
    echo ERROR: SVGToolsShell.dll not found at:
    echo   %DLL%
    echo Build the project in Visual Studio first ^(Release configuration^).
    pause
    exit /b 1
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

:: Register the context menu handler directly under the .svg extension
echo Registering .svg context menu handler...
reg add "HKCR\.svg\shellex\ContextMenuHandlers\SVGToolsShell" /ve /d "{a64a7e95-943d-4f79-891a-1e1176f2fc20}" /f >nul
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
reg add "HKLM\SOFTWARE\Classes\SystemFileAssociations\.svg\ShellEx\ContextMenuHandlers\SVGToolsShell" /ve /d "{a64a7e95-943d-4f79-891a-1e1176f2fc20}" /f >nul
if errorlevel 1 (
    echo ERROR: Failed to register SystemFileAssociations handler.
    pause
    exit /b 1
)

:: Add to Windows shell extension approved list (required to load)
echo Approving shell extension...
reg add "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v "{a64a7e95-943d-4f79-891a-1e1176f2fc20}" /d "SVGToolsShell" /f >nul
if errorlevel 1 (
    echo ERROR: Failed to approve shell extension.
    pause
    exit /b 1
)

echo.
echo Restarting Windows Explorer to apply the context menu...
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start explorer.exe

echo.
echo Done. Right-click any .svg file to see "SVG Tools" in the context menu.
pause
