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
;  Code signing:
;    Release builds are signed by CI via Azure Trusted Signing — see the
;    `installer` job in .github/workflows/build.yml, which signs the build
;    outputs BEFORE this script bundles them and signs the finished installer
;    afterwards. See README "Code signing" for the required repository secrets.
;
;    To sign a LOCAL build instead, uncomment and configure the SignTool
;    directive below and pass the tool definition to iscc, e.g.:
;         iscc /Ssigntool="signtool.exe sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f" installer\SVGToolsShell.iss
; ============================================================================

#define AppName        "SVG Tools Shell Extension"
#define AppVersion      "0.4.0"
#define AppPublisher    "Richard McQuiston"
#define AppId           "{{2ED7E239-89E8-4DAA-BB1D-40191EA65D70}"
; GUIDs are written as registry data, where Inno would treat a leading "{" as
; the start of a constant — so double it ("{{") exactly like AppId above. It
; renders back to a single "{FC258F52-...}" in the registry.
#define ComGuid         "{{FC258F52-702A-4AC2-BA22-43F59C7DC682}"
#define ImgGuid         "{{25EF2E9B-582C-46C0-9FF2-EF10313F09D1}"
#define BuildDir        "..\bin\Release\net48"
#define DonateUrl       "https://donate.stripe.com/00w5kD3Gj1Xo9v7gVOcs800"

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
Name: "configpresets"; Description: "Configure image-resize presets now (opens the settings file)"; GroupDescription: "Optional:"; Flags: unchecked
Name: "opendonate"; Description: "Open the donation page after installation"; GroupDescription: "Optional:"; Flags: unchecked

[Files]
; The extension DLL plus SharpShell and any other build dependencies.
Source: "{#BuildDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; The out-of-process resize worker (ImageResizer.Worker.exe) — the image-resize
; handler launches it from beside the DLL, so it must be installed too.
Source: "{#BuildDir}\*.exe"; DestDir: "{app}"; Flags: ignoreversion
; App-config files carry the worker's binding redirects for System.Text.Json.
Source: "{#BuildDir}\*.config"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
; Read-only post-install health check for the registered handlers.
Source: "..\verify-registration.ps1"; DestDir: "{app}"; Flags: ignoreversion
; ExifTool runtime for the "Edit metadata…" action. exiftool.exe itself is covered
; by the *.exe glob above; its required exiftool_files\ Perl runtime folder is a
; subdirectory, so install it recursively. skipifsourcedoesntexist keeps the build
; working if ExifTool wasn't staged (the menu item then reports it's missing).
Source: "{#BuildDir}\exiftool_files\*"; DestDir: "{app}\exiftool_files"; \
    Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

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

; --- Image resizer handler (separate COM server) --------------------------
;     Hooked onto each supported raster extension the same two ways as the SVG
;     handler, plus one Approved entry. Uses ISPP to loop the extension list.
#define ImgExts "png,jpg,jpeg,bmp,gif,tif,tiff"
#sub EmitImgExt
  #define Ext Copy(ImgExts, 1, Pos(",", ImgExts + ",") - 1)
  #expr ImgExts = Copy(ImgExts, Len(Ext) + 2)
Root: HKCR; Subkey: ".{#Ext}\shellex\ContextMenuHandlers\SVGToolsImageResizer"; \
    ValueType: string; ValueData: "{#ImgGuid}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\SystemFileAssociations\.{#Ext}\ShellEx\ContextMenuHandlers\SVGToolsImageResizer"; \
    ValueType: string; ValueData: "{#ImgGuid}"; Flags: uninsdeletekey
#endsub
#for {0; ImgExts != ""; ""} EmitImgExt

; Also hook folders (right-click a folder -> resize the images inside it).
Root: HKCR; Subkey: "Directory\shellex\ContextMenuHandlers\SVGToolsImageResizer"; \
    ValueType: string; ValueData: "{#ImgGuid}"; Flags: uninsdeletekey

Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"; \
    ValueType: string; ValueName: "{#ImgGuid}"; ValueData: "SVGToolsImageResizer"; \
    Flags: uninsdeletevalue

[Run]
; Register the COM server with /codebase so the CLSID resolves to {app}.
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: """{app}\SVGToolsShell.dll"" /codebase /nologo"; \
    StatusMsg: "Registering shell extension..."; Flags: runhidden

; Restart Explorer so the new context menu is picked up immediately (opt-in).
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; \
    Tasks: restartexplorer; Flags: runhidden

; Configure presets (opt-in): seed the settings file via the worker's canonical
; template, then open it in the user's default editor.
Filename: "{app}\ImageResizer.Worker.exe"; Parameters: "--init-settings"; \
    StatusMsg: "Preparing the presets file..."; Tasks: configpresets; Flags: runhidden skipifsilent
Filename: "{userappdata}\SVGToolsShell\resizer-settings.ini"; \
    Description: "Open the image-resize presets file"; \
    Tasks: configpresets; Flags: shellexec nowait skipifsilent

; Open the donation page (opt-in).
Filename: "{#DonateUrl}"; Description: "Open the donation page"; \
    Tasks: opendonate; Flags: shellexec nowait skipifsilent

[UninstallRun]
; Unregister the COM server. Runs before files are removed.
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; \
    Parameters: """{app}\SVGToolsShell.dll"" /unregister /nologo"; \
    RunOnceId: "UnregisterSvgTools"; Flags: runhidden

; Restart Explorer so the menu disappears immediately after removal.
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; \
    RunOnceId: "RestartExplorerUninstall"; Flags: runhidden
