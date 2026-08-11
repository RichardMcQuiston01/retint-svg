; ============================================================================
;  SVG Tools Shell Extension — Inno Setup installer script
; ============================================================================
;
;  Builds a self-contained, uninstallable Windows installer for the SVGToolsShell
;  Explorer context-menu extension. Replaces the raw install.bat/uninstall.bat
;  flow with a proper Add/Remove Programs entry.
;
;  Prerequisites to build the installer:
;    1. Build the project in Release first:
;         dotnet build -c Release
;       so that ..\bin\Release\net48\SVGToolsShell.dll exists.
;    2. Install Inno Setup 6+ (https://jrsoftware.org/isinfo.php).
;    3. Compile this script:
;         iscc installer\SVGToolsShell.iss
;       The signed-ready installer is written to installer\Output\.
;
;  Code signing (strongly recommended before public distribution — an unsigned
;  shell extension triggers SmartScreen warnings and loads unsigned native code
;  into explorer.exe):
;    - Sign BOTH the DLL (before compiling this script) and the resulting
;      installer .exe.
;    - Uncomment and configure the SignTool directive below, then pass the tool
;      definition to iscc, e.g.:
;         iscc /Ssigntool="signtool.exe sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f" installer\SVGToolsShell.iss
; ============================================================================

#define AppName        "SVG Tools Shell Extension"
#define AppVersion      "0.1.0"
#define AppPublisher    "Richard McQuiston"
#define AppId           "{{2ED7E239-89E8-4DAA-BB1D-40191EA65D70}"
#define ComGuid         "{FC258F52-702A-4AC2-BA22-43F59C7DC682}"
#define BuildDir        "..\bin\Release\net48"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\SVG Tools
DefaultGroupName=SVG Tools
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
OutputDir=Output
OutputBaseFilename=SVGToolsShell-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; A shell extension registers machine-wide (HKLM / HKCR) and writes to
; Program Files, so administrator rights are required.
PrivilegesRequired=admin
; The COM server targets x64; only install on 64-bit Windows and use the
; 64-bit registry/Framework view.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Uncomment to sign the compiled installer (define the "signtool" tool via iscc):
; SignTool=signtool

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "restartexplorer"; Description: "Restart Windows Explorer now so the menu appears immediately"; GroupDescription: "Finish setup:"

[Files]
; The extension DLL plus SharpShell and any other build dependencies.
Source: "{#BuildDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion

[Registry]
; --- Hook the handler onto the .svg ProgId (merged HKCR view) --------------
Root: HKCR; Subkey: ".svg\shellex\ContextMenuHandlers\SVGToolsShell"; \
    ValueType: string; ValueData: "{#ComGuid}"; Flags: uninsdeletekey

; --- Hook via SystemFileAssociations so the handler fires regardless of the
;     user's chosen default app (e.g. when .svg opens in a browser) ---------
Root: HKLM; Subkey: "SOFTWARE\Classes\SystemFileAssociations\.svg\ShellEx\ContextMenuHandlers\SVGToolsShell"; \
    ValueType: string; ValueData: "{#ComGuid}"; Flags: uninsdeletekey

; --- Add to the approved shell-extensions list (required to load) ----------
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"; \
    ValueType: string; ValueName: "{#ComGuid}"; ValueData: "SVGToolsShell"; \
    Flags: uninsdeletevalue

[Run]
; Register the COM server with /codebase so the CLSID resolves to {app}.
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: """{app}\SVGToolsShell.dll"" /codebase /nologo"; \
    StatusMsg: "Registering shell extension..."; Flags: runhidden

; Restart Explorer so the new context menu is picked up immediately (opt-in).
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; \
    Tasks: restartexplorer; Flags: runhidden

[UninstallRun]
; Unregister the COM server. Runs before files are removed.
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: """{app}\SVGToolsShell.dll"" /unregister /nologo"; \
    RunOnceId: "UnregisterSvgTools"; Flags: runhidden

; Restart Explorer so the menu disappears immediately after removal.
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; \
    RunOnceId: "RestartExplorerUninstall"; Flags: runhidden
