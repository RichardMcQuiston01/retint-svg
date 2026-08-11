# SVGToolsShell — CLAUDE.md

## Project Overview

**SVGToolsShell** is a Windows Explorer shell extension (context menu) for `.svg` files, built as a .NET Framework 4.8 COM-visible class library using the [SharpShell](https://github.com/dwmkerr/sharpshell) library.

Right-clicking any `.svg` file in Explorer adds a single cascading **Re-Tint** menu containing:
- **Black to Color** — replace black fills/strokes with a chosen color
- **White to Color** — replace white fills/strokes with a chosen color
- **Flatten SVG Layers** — merge all `<path>` elements into a single layer with one fill

---

## Tech Stack

| Concern             | Technology                          |
|---------------------|-------------------------------------|
| Language            | C# (.NET Framework 4.8)             |
| Shell integration   | [SharpShell](https://github.com/dwmkerr/sharpshell) v2.7.2 |
| XML processing      | `System.Xml.Linq` (XDocument)       |
| Color regex         | `System.Text.RegularExpressions`    |
| UI (color picker)   | `System.Windows.Forms.ColorDialog`  |
| Registration        | `RegAsm.exe` + batch scripts        |

> **Why .NET Framework 4.8 (not .NET 8+)?**
> COM shell extensions must load in-process inside `explorer.exe`, which runs
> against the full .NET Framework runtime. .NET Core / .NET 5+ CLR cannot be
> loaded in-process this way without a native shim.

---

## Repository Structure

```
SVGToolsShell/
├── SVGToolsShell.csproj    # Class library, net48, COM-visible (the shell handler)
├── ColorPresets.cs         # Named color palette for all submenus
├── SvgContextMenu.cs       # SharpShell handler — builds the Explorer menu
├── SvgTools.Core/          # Shared, UI-agnostic SVG logic (netstandard2.0)
│   ├── SvgTools.Core.csproj
│   └── SvgProcessor.cs     # Pure SVG manipulation logic (Tint, Flatten)
├── install.bat             # Registers DLL via RegAsm (run as Administrator)
├── uninstall.bat           # Unregisters and restarts Explorer
└── CLAUDE.md               # This file
```

> **Why the split?** `SvgProcessor` (and `TintTarget`) live in a separate
> `SvgTools.Core` project targeting **netstandard2.0** so the exact same logic
> can be referenced by both the classic net48 handler *and* a future
> Windows 11 `IExplorerCommand` / .NET 8 handler, without duplicating code.
> The types stay in the `SVGToolsShell` namespace so consumers compile
> unchanged. Core carries no `System.Drawing`/WinForms dependency — only
> `System.IO`, `RegularExpressions`, and `Xml.Linq` — which is what keeps it
> portable. `SvgTools.Core.dll` is emitted alongside `SVGToolsShell.dll` in the
> build output and travels with it during registration.

---

## Build Instructions

### Prerequisites
- Visual Studio 2022 (or Build Tools) with **.NET Framework 4.8 SDK**
- Windows 10/11 (shell extensions are Windows-only)
- .NET Framework 4.8 runtime on the target machine

### Steps
```powershell
# Restore NuGet packages and build Release
dotnet restore
dotnet build -c Release
```

The output DLL will be at:
```
bin\Release\net48\SVGToolsShell.dll
```

### Registration (required after every build)
```
# Run as Administrator
install.bat
```
This calls `RegAsm.exe /codebase` and restarts Explorer.

---

## Key Implementation Details

### GUID
The `[Guid]` attribute on `SvgContextMenu` **must be unique per deployment**.
Before distributing, regenerate it:
```powershell
[System.Guid]::NewGuid()
```
Then update `SvgContextMenu.cs`:
```csharp
[Guid("YOUR-NEW-GUID-HERE")]
```

### How SharpShell Works
- `SvgContextMenu` inherits `SharpContextMenu`
- `CanShowMenu()` — return `false` to hide the menu for specific selections
- `CreateMenu()` — return a `ContextMenuStrip`; SharpShell handles COM marshaling
- `SelectedItemPaths` — `IEnumerable<string>` of all selected file paths

### SVG Tinting Strategy (in `SvgProcessor.cs`)
1. **Explicit attribute** — `fill="black"`, `fill="#000"`, `fill="#000000"` (and stroke equivalents)
2. **Inline style** — `fill:#000000` inside a `style=""` attribute
3. **Implicit default** — SVGs exported from Illustrator/xTool often have no fill at all; paths inherit SVG's default black. Detected by absence of any `fill=` or `fill:` in the document; fixed by injecting `fill="color"` onto the first `<g>` (or `<svg>` root as fallback).

### Output File Naming
Originals are **never overwritten**. Output is written alongside the source:
```
MyIcon.svg  →  MyIcon_tint_EFBF04.svg
              MyIcon_tint_EFBF04_2.svg  (if first already exists)
              MyIcon_flat.svg
```

---

## Extending the Project

### Adding a new color preset
Edit `ColorPresets.cs` — every submenu updates automatically:
```csharp
("Teal", "#008080", Color.Teal),
```

### Adding a new top-level action
1. Add a new `static` method to `SvgProcessor.cs`
2. Add a new `ToolStripMenuItem` block in `SvgContextMenu.CreateMenu()`
3. Wire it up the same way as `tintBlack` / `flatten`

### Supporting other file types (e.g. `.svgz`)
Add additional `[COMServerAssociation]` attributes to `SvgContextMenu`:
```csharp
[COMServerAssociation(AssociationType.ClassOfExtension, ".svg")]
[COMServerAssociation(AssociationType.ClassOfExtension, ".svgz")]
```
Note: `.svgz` files are gzip-compressed and will need decompression in `SvgProcessor`
before text/XML manipulation.

### Improving regex coverage
Current patterns cover:
- `fill="black"` / `fill="#000"` / `fill="#000000"`
- `stroke="..."` equivalents
- Inline style `fill:#000000` / `stroke:black`

Not yet covered (potential future improvements):
- `rgb(0,0,0)` / `rgba(0,0,0,1)` color functions
- CSS `var(--color)` custom properties
- `currentColor` keyword
- Colors defined in a `<style>` block or external stylesheet

### Debugging shell extensions
Shell extensions are loaded into `explorer.exe` — standard debugger attach works:
1. Build in **Debug** configuration
2. Register the debug DLL via `install.bat`
3. In Visual Studio: **Debug → Attach to Process → explorer.exe**
4. Set breakpoints in `CreateMenu()` or `SvgProcessor`

Alternatively, use **SharpShell Server Manager** (included with SharpShell tools)
for isolated testing outside of Explorer.

---

## Known Limitations

- **Single-pass regex** — nested `style` blocks with cascade overrides are not resolved
- **No undo** — output files are new siblings; originals untouched
- **Flatten is destructive to structure** — all path IDs, `transform` attributes, and
  clip paths on individual elements are discarded; only `d` data is preserved
- **x86 vs x64** — `RegAsm.exe` path in `install.bat` points to 64-bit Framework;
  if targeting 32-bit Explorer (rare), switch to `Framework\v4.0.30319\RegAsm.exe`

---

## Useful References

- [SharpShell GitHub](https://github.com/dwmkerr/sharpshell)
- [SharpShell CodeProject article](https://www.codeproject.com/Articles/512956/NET-Shell-Extensions-Shell-Context-Menus)
- [SVG specification — painting](https://www.w3.org/TR/SVG11/painting.html)
- [RegAsm docs](https://learn.microsoft.com/en-us/dotnet/framework/tools/regasm-exe-assembly-registration-tool)
