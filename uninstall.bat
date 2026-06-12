@echo off
setlocal

echo ============================================================
echo  SVG Tools Shell Extension - Uninstaller
echo ============================================================
echo.

:: Verify we are running as Administrator
net session >nul 2>&1
if errorlevel 1 (
    echo ERROR: This script must be run as Administrator.
    echo Right-click uninstall.bat and choose "Run as administrator".
    pause
    exit /b 1
)

set DLL=%~dp0bin\Release\net48\SVGToolsShell.dll
set REGASM=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe

if not exist "%DLL%" (
    echo WARNING: SVGToolsShell.dll not found — attempting registry cleanup only.
) else (
    echo Unregistering COM server...
    "%REGASM%" "%DLL%" /unregister /nologo
)

:: Remove context menu handler and approval entries
echo Removing registry entries...
reg delete "HKCR\.svg\shellex\ContextMenuHandlers\SVGToolsShell" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Classes\SystemFileAssociations\.svg\ShellEx\ContextMenuHandlers\SVGToolsShell" /f >nul 2>&1
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" /v "{a64a7e95-943d-4f79-891a-1e1176f2fc20}" /f >nul 2>&1

echo.
echo Restarting Windows Explorer...
taskkill /f /im explorer.exe >nul 2>&1
timeout /t 1 /nobreak >nul
start explorer.exe

echo.
echo SVG Tools Shell Extension has been removed.
pause
