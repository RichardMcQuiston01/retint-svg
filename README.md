# SVGToolsShell

Windows Explorer shell extensions that add two context menus: **SVG Tools** (Re-Tint) when you right-click an `.svg` file, and **Resize Images** when you right-click a raster image or a folder of images. Both write new files alongside the originals — nothing is ever overwritten.

## Features

### SVG Tools (right-click a `.svg` file)

- **Re-Tint Black to Color** — replace black fills/strokes with a chosen color
- **Re-Tint White to Color** — replace white fills/strokes with a chosen color
- **Flatten SVG Layers** — merge all `<path>` elements into a single layer with one fill color

Each action offers a preset color palette (Black, White, Red, Green, Blue, Yellow, Orange, Purple, Gold, Silver) plus a **Custom…** option that opens the system color picker.

### Resize Images (right-click a `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.tif`, `.tiff`, or a folder)

- **Presets** — 25 %, 50 %, 75 %, 200 %, and longest-edge 1024 px / 1920 px
- **Custom…** — a dialog to enter a percentage, a longest-edge pixel count, or an exact width × height, with an option to allow enlarging
- **Edit presets…** — the preset list, JPEG quality, and upscale default live in a user-editable settings file (see [Resizing Images](#resizing-images))
- **Folders** — right-clicking a folder resizes its top-level images (shown only when the folder contains supported images)

Originals are never overwritten — output files are written alongside the source:

```
MyIcon.svg   →  MyIcon_tint_EFBF04.svg      (Re-Tint)
                MyIcon_flat.svg             (Flatten)
Photo.jpg    →  Photo_50pct.jpg            (50 % preset / Custom percent)
                Photo_1024px.jpg           (longest-edge preset)
                Photo_640x480.jpg          (Custom exact size)
                Photo_50pct_2.jpg          (if the first already exists)
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

This registers the COM server via `RegAsm.exe /codebase`, writes the required shell extension registry entries (for `.svg`, the image extensions, and folders), and restarts Explorer. The menus appear after Explorer restarts. On Windows 11, click **Show more options** to see the classic menu where shell-extension entries live.

To confirm everything registered correctly, run `verify-registration.ps1` (read-only, no admin needed) — it reports the COM class, approval, and file/folder associations for both handlers and flags anything missing.

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

## Resizing Images

Picking a size (a preset or a **Custom…** value) does **not** resize inside Explorer. The handler writes a small job file and launches a separate helper, `ImageResizer.Worker.exe`, which does the pixel work and shows a summary when it finishes. This keeps the heavy GDI+ decode/encode out of `explorer.exe`. The worker ships next to the DLL and is placed there automatically by the build (and by the release artifact).

Each image is: decoded from a copy (so the source is never locked) → corrected for EXIF orientation (portrait phone photos come out upright) → resampled with high-quality bicubic scaling → re-encoded by its file extension (JPEG honors the configured quality) → written to a **non-destructive sibling** whose name reflects the size (`Photo_50pct.jpg`, `Photo_1024px.jpg`, `Photo_640x480.jpg`), with a `_2`, `_3`, … suffix if that name already exists.

Supported inputs are what GDI+ handles natively: `.png`, `.jpg`, `.jpeg`, `.bmp`, `.gif`, `.tif`, `.tiff`. (WebP/HEIC/AVIF, animated-GIF frames, and metadata preservation are not covered yet.)

### Editing presets and settings

Choose **Resize Images → Edit presets…** to open the settings file, created from a commented template on first use at:

```
%APPDATA%\SVGToolsShell\resizer-settings.ini
```

Edits take effect on the next right-click — no reinstall needed. Example:

```ini
[settings]
jpeg-quality  = 85       ; 1-100
allow-upscale = true     ; may presets enlarge images smaller than the target?

[presets]
50%                  = 50%       ; a percentage
Longest edge 1024 px = 1024px    ; longest edge, aspect preserved
Web 800x600          = 800x600   ; exact width x height
```

A missing or malformed file (or an unparseable preset line) falls back to the built-in defaults, so the menu never fails to build.

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

## Support

If this library saved you some reverse-engineering, consider [buying me a coffee](https://donate.stripe.com/00w5kD3Gj1Xo9v7gVOcs800). ☕

## Copyright

(c)2026 Richard McQuiston.  All rights reserved.
