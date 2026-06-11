# SVGToolsShell

A Windows Explorer shell extension that adds an **SVG Tools** context menu when you right-click any `.svg` file.

## Features

- **Re-Tint Black to Color** — replace black fills/strokes with a chosen color
- **Re-Tint White to Color** — replace white fills/strokes with a chosen color
- **Flatten SVG Layers** — merge all `<path>` elements into a single layer with one fill color

Each action offers a preset color palette (Black, White, Red, Green, Blue, Yellow, Orange, Purple, Gold, Silver) plus a **Custom…** option that opens the system color picker. Originals are never overwritten — output files are written alongside the source:

```
MyIcon.svg  →  MyIcon_tint_EFBF04.svg
               MyIcon_flat.svg
```

## Requirements

- Windows 10 or 11
- [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)
- Visual Studio 2022 (or Build Tools) with **.NET Framework 4.8 SDK** — to build from source

> **Why .NET Framework 4.8?**
> COM shell extensions load in-process inside `explorer.exe`, which runs against the full .NET Framework runtime. .NET 5+ cannot be loaded in-process without a native shim.

## Build

```powershell
dotnet restore
dotnet build -c Release
```

Output DLL: `bin\Release\net48\SVGToolsShell.dll`

## Install

Run as Administrator from the project root:

```
install.bat
```

This registers the COM server via `RegAsm.exe /codebase`, writes the required shell extension registry entries, and restarts Explorer. The context menu will appear on `.svg` files after Explorer restarts.

## Uninstall

```
uninstall.bat
```

Unregisters the COM server and restarts Explorer.

## How Tinting Works

The tint operation applies in three passes:

1. **Explicit attribute** — `fill="black"`, `fill="#000"`, `fill="#000000"` (and `stroke` equivalents)
2. **Inline style** — `fill:#000000` inside a `style=""` attribute
3. **Implicit default** — SVGs exported from Illustrator or xTool often have no `fill` attribute at all; their paths inherit the SVG default (black). Detected by absence of any `fill=` or `fill:` in the document; fixed by injecting `fill="color"` onto the first `<g>` (or `<svg>` root as fallback).

### Known gaps

The following color forms are not yet handled:
- `rgb(0,0,0)` / `rgba(0,0,0,1)` color functions
- `currentColor` keyword
- Colors defined in a `<style>` block or external stylesheet
- CSS `var(--color)` custom properties

## How Flatten Works

Collects every `<path d="...">` in document order, joins the `d` data into a single path, and writes a minimal SVG with that one path and the chosen fill color. The original `width`, `height`, and `viewBox` are preserved. All path IDs, `transform` attributes, and clip paths are discarded.

## Extending

### Add a color preset

Edit `ColorPresets.cs` — every submenu updates automatically:

```csharp
("Teal", "#008080", Color.Teal),
```

A `null` hex value renders as **Custom…** and opens a `ColorDialog` at runtime.

### Add a new action

1. Add a `static` method to `SvgProcessor.cs` that accepts a path, transforms it, writes a sibling file, and returns the output path
2. Add a `ToolStripMenuItem` block in `SvgContextMenu.CreateMenu()` following the same pattern as `tintBlack` / `flatten`

### Support additional file types (e.g. `.svgz`)

Add a `[COMServerAssociation]` attribute to `SvgContextMenu`:

```csharp
[COMServerAssociation(AssociationType.ClassOfExtension, ".svgz")]
```

Note: `.svgz` files are gzip-compressed and will need decompression in `SvgProcessor` before text/XML manipulation.

## Debugging

Shell extensions load inside `explorer.exe`:

1. Build in **Debug** configuration
2. Register the debug DLL via `install.bat`
3. In Visual Studio: **Debug → Attach to Process → explorer.exe**
4. Set breakpoints in `CreateMenu()` or `SvgProcessor`

Alternatively, use the **SharpShell Server Manager** (included with SharpShell tools) for isolated testing outside of Explorer.

## Before Distributing

The `[Guid]` attribute on `SvgContextMenu` is a placeholder and **must be replaced** before shipping:

```powershell
[System.Guid]::NewGuid()
```

Then update `SvgContextMenu.cs`:

```csharp
[Guid("YOUR-NEW-GUID-HERE")]
```

## References

- [SharpShell GitHub](https://github.com/dwmkerr/sharpshell)
- [SharpShell CodeProject article](https://www.codeproject.com/Articles/512956/NET-Shell-Extensions-Shell-Context-Menus)
- [SVG specification — painting](https://www.w3.org/TR/SVG11/painting.html)
- [RegAsm docs](https://learn.microsoft.com/en-us/dotnet/framework/tools/regasm-exe-assembly-registration-tool)
