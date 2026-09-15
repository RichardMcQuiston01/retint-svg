# ImageResizer.Worker — test scripts (Windows)

Manual/automated verification for the resize worker. The worker's pixel path
uses `System.Drawing` and can't run in the Linux CI, so these PowerShell scripts
exercise it on a real Windows machine against the built (or released) worker.

## `Run-ResizerTests.ps1` — automated smoke test

Generates its own test images (including a portrait JPEG with a real EXIF
orientation tag, and a noisy image for the quality check), runs every scenario,
and prints a PASS/FAIL table — no test images or clicking required (the worker's
summary dialog is waited out and closed automatically).

```powershell
powershell -ExecutionPolicy Bypass -File .\Run-ResizerTests.ps1
# point at the binary if it isn't the default location:
.\Run-ResizerTests.ps1 -ReleaseDir "F:\Downloads\SVGToolsShell-Release"
```

By default it searches `F:\Downloads\SVGToolsShell-Release` for
`ImageResizer.Worker.exe`; pass `-ReleaseDir` to point at a local build output
(`ImageResizer.Worker\bin\Release\net48`).

Checks: percent / longest-edge / exact sizing, **EXIF orientation actually
applied** (a rotated-tagged 400x200 image must come out 100x200, not 200x100),
format preserved, collision `_2` siblings, JPEG quality honored, a bad path in a
multi-file job doesn't sink the good files, no leftover `.tmp` files, and outputs
aren't hidden.

## `Resize-Image.ps1` — ad-hoc runner for your own images

Shows the worker's normal summary dialog (does not auto-close it).

```powershell
.\Resize-Image.ps1 -Path C:\pics\photo.jpg -Percent 50
.\Resize-Image.ps1 -Path C:\pics\a.jpg,C:\pics\b.png -LongestEdge 1024
.\Resize-Image.ps1 -Path C:\pics\photo.jpg -Width 640 -Height 480 -JpegQuality 90
```

> The worker is an unsigned exe; both scripts `Unblock-File` it first, but
> SmartScreen/Defender may still prompt on first launch — allow it once.
