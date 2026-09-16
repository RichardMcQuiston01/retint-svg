# ImageResizer.Worker

The out-of-process worker for the image tools. The `ImageContextMenu` handler
writes a job file and launches this exe, so the heavy GDI+ / ImageMagick decode
and encode never run inside `explorer.exe`. Despite the name, it now handles
**resize, rotate, and convert** (the metadata editor does *not* go through the
worker — the handler shells out to ExifTool directly).

## Contract

```text
ImageResizer.Worker.exe <path-to-job.json>
ImageResizer.Worker.exe --init-settings      # maintenance mode (see below)
```

The job file is a serialized `ImageTools.Core.ResizeJob`. `Operation` selects the
engine and defaults to `"resize"` when absent, so older job files still work:

```jsonc
{
  "Operation":    "resize",                    // "resize" | "rotate" | "convert"
  "Size":         { "Kind": 0, "Percent": 50 },// Kind: 0=Percent, 1=LongestEdge, 2=ExactWidthHeight (resize)
  "RotateDegrees": 0,                          // 90 | 180 | 270 (rotate)
  "Format":       "",                          // "png" | "jpg" | "tif" | "bmp" | "webp" (convert)
  "JpegQuality":  85,
  "AllowUpscale": true,
  "OutputLocation": "sibling",
  "Files": [ "C:\\pics\\a.jpg", "C:\\pics\\b.png" ]
}
```

`Program.Main` dispatches per `Operation`:

- **resize** → `ResizeEngine` (GDI+): decode from a byte copy (source never
  locked) → apply EXIF orientation → resample `HighQualityBicubic` (halo
  suppressed via `TileFlipXY`) → encode by the source extension (JPEG honors
  `JpegQuality`) → non-destructive sibling `Photo_50pct.jpg`.
- **rotate** → `RotateEngine` (GDI+): bake in EXIF orientation, drop the
  orientation tag (so viewers don't double-rotate), rotate 90/180/270 → sibling
  `Photo_rot90.jpg`.
- **convert** → `ConvertEngine` (Magick.NET / ImageMagick): auto-orient, set the
  target format, JPEG honors `JpegQuality` → sibling with the new extension
  (`Photo.png`). Handles formats GDI+ can't, notably **WebP**.

All engines write via an atomic temp-file + `CreateNew`/rename-and-retry so
concurrent runs never clobber each other. The job file is single-use and deleted
when the worker finishes; a summary `MessageBox` reports the result. A bad path in
a multi-file job is reported without sinking the good files.

### `--init-settings`

Silently seeds `%APPDATA%\SVGToolsShell\resizer-settings.ini` from the canonical
template (`ResizerSettings.EnsureFileExists()`) and exits — no pixel work, no
dialog. Used by the installer's "Configure presets" option so the presets file
exists (and can be opened) right after install.

## Dependencies

- **System.Drawing** (GDI+) for resize/rotate — Windows-only.
- **Magick.NET-Q8-x64** (ImageMagick) for convert; its native `Magick.Native`
  DLL is emitted into the output and travels with the worker.
- **System.Text.Json** to read the job file (binding redirects via app.config).

## Supported inputs

`.jpg .jpeg .png .bmp .gif .tif .tiff`. Convert can additionally **write** WebP
(and the other formats above). Reading WebP/HEIC/AVIF sources is not wired up.

## Verifying (Windows only)

CI compiles this project but cannot exercise the pixel path (System.Drawing is
Windows-only and Magick.NET needs a real run). The `test/` folder has PowerShell
scripts for the resize path: `test/Run-ResizerTests.ps1` is a self-contained
automated smoke test (generates its own images, asserts on the outputs), and
`test/Resize-Image.ps1` runs the worker against your own files. To verify resize
by hand:

```powershell
dotnet build ImageResizer.Worker/ImageResizer.Worker.csproj -c Release

# Write a job file, then run the worker against it:
$job = @{ Operation="resize"; Size=@{Kind=0;Percent=50}; JpegQuality=85;
          AllowUpscale=$true; OutputLocation="sibling";
          Files=@("C:\pics\photo.jpg") } | ConvertTo-Json
$job | Out-File -Encoding utf8 $env:TEMP\resize-job.json
.\ImageResizer.Worker\bin\Release\net48\ImageResizer.Worker.exe $env:TEMP\resize-job.json
```

Swap `Operation`/fields to spot-check rotate (`Operation="rotate"; RotateDegrees=90`)
and convert (`Operation="convert"; Format="webp"`). Check: output dimensions, that
a portrait phone photo (EXIF orientation 6/8) comes out upright, JPEG quality,
WebP actually produced, and that re-running yields `_2` siblings rather than
overwriting.

## Known limitations

- No live progress UI — just a completion/error summary. A progress window is a
  sensible follow-up for large batches.
- Resize/rotate flatten animated GIFs to the first frame and do not preserve
  EXIF/ICC on the re-encoded output (inherent to the GDI+ engine). Convert bakes
  orientation into the pixels via Magick.NET.
