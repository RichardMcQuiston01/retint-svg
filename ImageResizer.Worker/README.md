# ImageResizer.Worker

The out-of-process resize worker (Phase 2 of the batch image resizer). The
context-menu handler (Phase 3, not yet built) writes a job file and launches
this exe, so the heavy GDI+ decode/encode never runs inside `explorer.exe`.

## Contract

```
ImageResizer.Worker.exe <path-to-job.json>
```

The job file is a serialized `ImageTools.Core.ResizeJob`:

```jsonc
{
  "Size":         { "Kind": 0, "Percent": 50 },   // Kind: 0=Percent, 1=LongestEdge, 2=ExactWidthHeight
  "JpegQuality":  85,
  "AllowUpscale": true,
  "OutputLocation": "sibling",
  "Files": [ "C:\\pics\\a.jpg", "C:\\pics\\b.png" ]
}
```

For each file the worker: decodes it (from a byte copy, so the source is never
locked) → applies EXIF orientation → resamples with `HighQualityBicubic` (edge
halo suppressed via `TileFlipXY`) → encodes by the source extension (JPEG honors
`JpegQuality`) → writes a **non-destructive sibling** (`Photo_50pct.jpg`) using
an atomic `CreateNew`-and-retry so concurrent runs never clobber each other.
The job file is single-use and is deleted when the worker finishes; a summary
`MessageBox` reports the result.

Supported inputs: `.jpg .jpeg .png .bmp .gif .tif .tiff` (what GDI+ handles
natively). WebP/HEIC/AVIF and metadata preservation are a later imaging-engine
swap.

## Verifying (Windows only)

CI compiles this project but cannot exercise the pixel path (System.Drawing is
Windows-only and there are no test images). To verify by hand on Windows:

```powershell
dotnet build ImageResizer.Worker/ImageResizer.Worker.csproj -c Release

# Write a job file, then run the worker against it:
$job = @{ Size=@{Kind=0;Percent=50}; JpegQuality=85; AllowUpscale=$true;
          OutputLocation="sibling"; Files=@("C:\pics\photo.jpg") } | ConvertTo-Json
$job | Out-File -Encoding utf8 $env:TEMP\resize-job.json
.\ImageResizer.Worker\bin\Release\net48\ImageResizer.Worker.exe $env:TEMP\resize-job.json
```

Check: output dimensions, that a portrait phone photo (EXIF orientation 6/8)
comes out upright, JPEG quality, and that re-running produces `_2` siblings
rather than overwriting.

## Known limitations (this slice)

- No live progress UI yet — just a completion/error summary. A progress window
  is a sensible follow-up for large batches.
- Animated GIFs are flattened to the first frame; metadata (EXIF/ICC) is not
  preserved on re-encode — both inherent to the GDI+ engine.
