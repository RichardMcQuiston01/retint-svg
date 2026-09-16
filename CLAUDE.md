# SVGToolsShell — CLAUDE.md

## Project Overview

**SVGToolsShell** is a pair of Windows Explorer shell extensions (context menus),
built as .NET Framework 4.8 COM-visible class libraries using the
[SharpShell](https://github.com/dwmkerr/sharpshell) library. Two independent COM
servers ship in one DLL:

1. **SVG Re-Tint** — right-clicking a `.svg` file adds a cascading **Re-Tint** menu:
   - **Black to Color** — replace black fills/strokes with a chosen color
   - **White to Color** — replace white fills/strokes with a chosen color
   - **Flatten SVG Layers** — merge all `<path>` elements into a single layer

2. **SVGToolsShell (image tools)** — right-clicking a raster image
   (`.png/.jpg/.jpeg/.bmp/.gif/.tif/.tiff`) or a folder that contains one adds a
   cascading **SVGToolsShell** menu:
   - **Resize Images ▸** — presets, a **Custom…** dialog, and **Edit presets…**
   - **Rotate ▸** — 90° / 180° / 270° (clockwise)
   - **Convert to ▸** — PNG / JPG / TIFF / BMP / **WebP**
   - **Edit metadata…** — an EXIF/IPTC editor (single image selection)
   - **Power Rename…** — a PowerToys-style batch rename (2+ images selected)

Both extensions only ever write **new files alongside the originals** — nothing is
overwritten. The image tools do their heavy pixel work in a **separate worker
process**, never inside `explorer.exe` (see Architecture).

---

## Tech Stack

| Concern                    | Technology                                           |
|----------------------------|------------------------------------------------------|
| Language                   | C# (.NET Framework 4.8; netstandard2.0 cores)        |
| Shell integration          | [SharpShell](https://github.com/dwmkerr/sharpshell) v2.7.2 |
| SVG processing             | `System.Xml.Linq` + `System.Text.RegularExpressions` |
| Raster resize/rotate       | GDI+ (`System.Drawing`) in the worker                |
| Raster convert (incl. WebP)| [Magick.NET](https://github.com/dlemstra/Magick.NET) (`Magick.NET-Q8-x64`) in the worker |
| Metadata (EXIF/IPTC)       | Bundled [ExifTool](https://exiftool.org/), shelled out from the handler |
| Handler ⇄ worker transport | `ResizeJob` serialized to a temp JSON file           |
| UI (dialogs, color picker) | `System.Windows.Forms`                               |
| Unit tests                 | xUnit (net8.0), run on Linux in CI                   |
| Registration               | `RegAsm.exe` + batch scripts                          |
| Installer                  | [Inno Setup](https://jrsoftware.org/isinfo.php) (`installer/SVGToolsShell.iss`) |
| CI                         | GitHub Actions (`.github/workflows/build.yml`)       |

> **Why .NET Framework 4.8 (not .NET 8+)?**
> COM shell extensions must load in-process inside `explorer.exe`, which runs
> against the full .NET Framework runtime. .NET Core / .NET 5+ CLR cannot be
> loaded in-process this way without a native shim. (The pixel/metadata work is
> deliberately pushed out of `explorer.exe` — see Architecture — which also keeps
> the door open for a future .NET 8 `IExplorerCommand` handler reusing the cores.)

---

## Repository Structure

```
retint-svg/
├── SVGToolsShell.csproj        # net48 class library — BOTH COM handlers ship here
│
│   # ── SVG Re-Tint handler ───────────────────────────────────────────────
├── SvgContextMenu.cs           # SharpShell handler for .svg (Re-Tint / Flatten)
├── ColorPresets.cs             # Named color palette for the SVG submenus
├── SvgTools.Core/              # Shared, UI-agnostic SVG logic (netstandard2.0)
│   └── SvgProcessor.cs         # Pure SVG manipulation (Tint, Flatten) + TintTarget
│
│   # ── Image-tools handler ──────────────────────────────────────────────
├── ImageContextMenu.cs         # SharpShell handler for raster images + folders
├── CustomSizeDialog.cs         # WinForms dialog for a custom resize size
├── PowerRenameDialog.cs        # WinForms batch-rename dialog
├── MetadataDialog.cs           # WinForms EXIF/IPTC editor
├── ExifTool.cs                 # Locates & runs the bundled exiftool.exe
│
├── ImageTools.Core/            # Shared, UI-agnostic raster logic (netstandard2.0)
│   ├── Geometry.cs             # Dimensions / size math
│   ├── SizeSpec.cs             # A resize spec (percent / longest-edge / exact) + TryParse
│   ├── SizePreset.cs           # Built-in preset list
│   ├── ResizerSettings.cs      # INI parser for presets/quality/upscale + EnsureFileExists
│   ├── ResizeJob.cs            # DTO passed handler → worker (Operation/Size/Rotate/Format/…)
│   ├── OutputNaming.cs         # Collision-safe sibling paths (BuildOutputPath/BuildConvertedPath)
│   ├── Orientation.cs          # EXIF orientation helpers
│   ├── RenameOptions.cs        # Power Rename options + result DTOs
│   ├── RenameEngine.cs         # Pure batch-rename planning (literal/regex/counter)
│   ├── ImageMetadata.cs        # The editable EXIF/IPTC field DTO
│   ├── ExifToolCommand.cs      # Field⇄tag map + read(-csv)/write command-line builders
│   └── Csv.cs                  # Minimal RFC 4180 reader for ExifTool -csv output
│
├── ImageResizer.Worker/        # Out-of-process worker (net48 WinExe)
│   ├── Program.cs              # Reads a ResizeJob, dispatches per Operation
│   ├── ResizeEngine.cs         # GDI+ resize (+ EXIF orientation, atomic write)
│   ├── RotateEngine.cs         # GDI+ rotate 90/180/270
│   ├── ConvertEngine.cs        # Magick.NET format conversion (incl. WebP)
│   ├── RasterEncoding.cs       # GDI+ encoder selection by extension
│   └── test/                   # Windows-only PowerShell verification scripts
│
├── ImageTools.Core.Tests/      # xUnit tests (net8.0) for the pure Core logic
│
├── tools/fetch-exiftool.ps1    # Installs ExifTool via Chocolatey, stages it in the build output
├── installer/SVGToolsShell.iss # Inno Setup installer script
├── install.bat / uninstall.bat # RegAsm-based COM registration (run as Administrator)
├── verify-registration.ps1     # Read-only health check for both handlers
├── .github/workflows/build.yml # CI: build + test, then compile the installer
├── CHANGELOG.md
├── README.md
└── CLAUDE.md                   # This file
```

> **Why the `*.Core` split?** `SvgTools.Core` and `ImageTools.Core` target
> **netstandard2.0** and hold only the pure, testable logic (no
> `System.Drawing`/WinForms). That keeps them portable — the same logic can back
> the classic net48 handler, the worker, and a future .NET 8 handler — and lets
> the xUnit suite run on Linux in CI. Types stay in the consumers' namespaces so
> callers compile unchanged.

---

## Architecture

### Two COM servers, one DLL
`SvgContextMenu` and `ImageContextMenu` are separate `SharpContextMenu` handlers
with **distinct `[Guid]`s**, both `[ComVisible(true)]` in `SVGToolsShell.dll`.
Each registers its own file associations (SVG on `.svg`; the image handler on the
raster extensions **and** `AssociationType.Directory` for folders). `RegAsm
/codebase` registers the DLL; the association + Approved registry keys are written
by `install.bat` / the installer.

### Worker-process model (image resize / rotate / convert)
The handler does **no pixel work**. Picking an action:
1. Collects the target files (image files directly; the top-level images of any
   selected folder; de-duplicated).
2. Hand-writes a `ResizeJob` to a temp JSON file (hand-serialized via
   `StringBuilder` so the in-Explorer handler carries **no JSON dependency**).
3. Launches `ImageResizer.Worker.exe <job.json>` and returns immediately.

`ResizeJob.Operation` (`"resize"` | `"rotate"` | `"convert"`, default `"resize"`
so old job files still deserialize) drives the worker's dispatch to
`ResizeEngine` / `RotateEngine` / `ConvertEngine`. GDI+ and Magick.NET therefore
never load inside `explorer.exe`. The worker shows its own summary `MessageBox`
and deletes the single-use job file. The build stages the worker (and its deps)
next to `SVGToolsShell.dll`; the handler locates it relative to its own assembly.

The worker also has a maintenance mode, `--init-settings`, that silently seeds the
presets file (used by the installer's "Configure presets" option).

### Metadata editing (direct, not via the worker)
`Edit metadata…` is interactive, so it does **not** use the fire-and-forget
worker. ExifTool is a *separate process* (not native code loaded in-process), so
it is safe to invoke straight from the handler: `ExifTool.cs` shells out to the
bundled `exiftool.exe` to read current values (prefill) and to write edits.
The command lines and the field⇄tag mapping are pure logic in
`ImageTools.Core` (`ExifToolCommand`, `Csv`); the handler only runs the process.

---

## Key Implementation Details

### GUIDs (two of them)
Each handler's `[Guid]` **must be unique per deployment**. Regenerate before
distributing:
```powershell
[System.Guid]::NewGuid()
```
- `SvgContextMenu` — the SVG Re-Tint server
- `ImageContextMenu` — the image-tools server (`25EF2E9B-…`)

The installer (`installer/SVGToolsShell.iss`) hard-codes the same GUIDs as
`ComGuid` / `ImgGuid`; keep them in sync with the source attributes.

### SVG tinting strategy (`SvgProcessor.cs`)
1. **Explicit attribute** — `fill="black"`, `fill="#000"`, `fill="#000000"` (and stroke equivalents)
2. **Inline style** — `fill:#000000` inside a `style=""` attribute
3. **Implicit default** — Illustrator/xTool exports often have no fill at all;
   paths inherit SVG's default black. Detected by the absence of any `fill=` /
   `fill:` and fixed by injecting `fill="color"` onto the first `<g>` (or the
   `<svg>` root).

### Output file naming (`OutputNaming.cs`)
Originals are never overwritten; outputs are collision-safe siblings:
```
MyIcon.svg  →  MyIcon_tint_EFBF04.svg  /  MyIcon_flat.svg
Photo.jpg   →  Photo_50pct.jpg  Photo_1024px.jpg  Photo_640x480.jpg   (resize)
               Photo_rot90.jpg                                        (rotate)
               Photo.png  /  Photo_2.png (if taken / same ext)        (convert)
               …_2, …_3 suffixes when a name is taken
```
`BuildConvertedPath` also refuses to target the source itself.

### Resize presets & settings (`ResizerSettings.cs`)
Presets, JPEG quality, and the upscale default live in
`%APPDATA%\SVGToolsShell\resizer-settings.ini`, read fresh on every right-click.
**Edit presets…** (and the installer / `--init-settings`) seed it from a commented
template via the shared `EnsureFileExists()`. Parsing is deliberately tolerant — a
missing/malformed file falls back to built-in defaults so the menu never fails.

### Metadata fields (`ImageMetadata` / `ExifToolCommand`)
- EXIF: Artist, Copyright, Description (`ImageDescription`), Date taken (`DateTimeOriginal`)
- IPTC: Title (`ObjectName`), Caption (`Caption-Abstract`), Keywords, Creator (`By-line`), City, Country (`Country-PrimaryLocationName`)

`ExifToolCommand.Fields` is the single source of truth for order and tag mapping;
read (CSV columns) and write (`-Group:Tag=value` assignments) both follow it.
Writes omit `-overwrite_original`, so ExifTool keeps a `<name>_original` backup.
Values are Windows-quoted (`CommandLineToArgvW` rules) so arbitrary text round-trips.

### ExifTool bundling
ExifTool's ~6 MB Windows build is **not committed**. `tools/fetch-exiftool.ps1`
installs it via **Chocolatey** (`choco install exiftool`) and copies `exiftool.exe`
(plus its `exiftool_files\` runtime, when present) into the build output. CI runs
this after staging the worker; the installer bundles both. If ExifTool is absent
the menu item still appears but reports it's missing.

---

## Build Instructions

### Prerequisites
- Visual Studio 2022 (or Build Tools) with the **.NET Framework 4.8 SDK**
- The .NET SDK (8.0) for building the netstandard cores and running tests
- Windows 10/11 (shell extensions are Windows-only)

### Steps
```powershell
dotnet restore
dotnet build -c Release          # builds both cores, both handlers, and the worker
dotnet test ImageTools.Core.Tests/ImageTools.Core.Tests.csproj -c Release

# Optional: stage ExifTool for local metadata testing (requires Chocolatey)
pwsh tools/fetch-exiftool.ps1 -Destination bin/Release/net48
```
Output: `bin\Release\net48\SVGToolsShell.dll` (with `ImageResizer.Worker.exe` and
deps staged beside it).

### Registration (required after every build)
```
install.bat      # run as Administrator — RegAsm /codebase both handlers, restart Explorer
verify-registration.ps1   # read-only check that both handlers registered correctly
uninstall.bat    # unregister + restart Explorer
```

### Installer
`installer/SVGToolsShell.iss` (Inno Setup 6+) builds a signed-ready installer with
an Add/Remove Programs entry; CI compiles it from the build output and, on a
published GitHub Release, attaches `SVGToolsShell-Setup-<version>.exe`.

---

## Release Flow

1. Land features on `dev` via draft PRs (author merges).
2. Finalize `CHANGELOG.md` (promote **Unreleased → [x.y.z]**) and bump
   `AppVersion` in `installer/SVGToolsShell.iss`.
3. Open a `dev → main` PR; merge once CI is green.
4. Publish a GitHub Release tagged `vX.Y.Z` (manual — the tag push is a human
   step). CI's `installer` job attaches the installer to the release.

---

## Extending the Project

### Add a color preset (SVG)
Edit `ColorPresets.cs` — every SVG submenu updates automatically:
```csharp
("Teal", "#008080", Color.Teal),
```

### Add an image action
1. Add a pure operation to `ImageTools.Core` (and a worker engine if it does pixel work).
2. If it uses the worker, extend `ResizeJob` / the `Operation` switch in the worker's `Program.cs`.
3. Add a `ToolStripMenuItem` under the `SVGToolsShell` parent in `ImageContextMenu.CreateMenu()`.
4. Cover the pure logic with an xUnit test in `ImageTools.Core.Tests`.

### Support another file type
Add a `[COMServerAssociation]` attribute to the relevant handler (e.g. `.svgz`
on `SvgContextMenu`, a new raster extension on `ImageContextMenu`), and register
it in `install.bat` / the installer's `[Registry]` section.

---

## Known Limitations

- **SVG tinting is single-pass regex** — `rgb()/rgba()`, `currentColor`,
  `var(--color)`, and `<style>`-block/external CSS colors are not resolved.
- **Flatten discards structure** — path IDs, per-element `transform`s, and clip
  paths are dropped; only `d` data and the root `width/height/viewBox` are kept.
- **Resize/rotate re-encode via GDI+** — animated GIFs flatten to one frame and
  EXIF/ICC metadata is not preserved on the resized/rotated output (Convert bakes
  in orientation via Magick.NET). Use **Edit metadata…** to author EXIF/IPTC.
- **No undo** — outputs are new siblings; originals are untouched (and ExifTool
  keeps a `_original` backup on metadata writes).
- **x64 only** — the COM server and installer target 64-bit Windows/Explorer.

---

## Useful References

- [SharpShell GitHub](https://github.com/dwmkerr/sharpshell)
- [SVG specification — painting](https://www.w3.org/TR/SVG11/painting.html)
- [Magick.NET](https://github.com/dlemstra/Magick.NET)
- [ExifTool](https://exiftool.org/) · [ExifTool tag names](https://exiftool.org/TagNames/)
- [Inno Setup](https://jrsoftware.org/isinfo.php)
- [RegAsm docs](https://learn.microsoft.com/en-us/dotnet/framework/tools/regasm-exe-assembly-registration-tool)
```
