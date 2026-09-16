# TODO

## Overview

Potential functions to incorporate into the software package. See `CHANGELOG.md`
for what has actually shipped.

## Shipped ✅

- **Batch Image Resizer** — Resize Images ▸ (presets, Custom…, editable presets).
- **Bulk Image Converter** — Convert to ▸ (PNG / JPG / TIFF / BMP / WebP).
- **Rotate** — 90° / 180° / 270°.
- **Power Rename** — PowerToys-style batch rename for multi-image selections.
- **Edit metadata** — EXIF/IPTC editor (bundled ExifTool, `_original` backup).

## Planned / in progress

### Code Signing

Sign the DLL and the installer (Azure Trusted Signing) so the shell extension and
setup don't trigger SmartScreen warnings. Wire `signtool` into the CI installer
job once the certificate/secrets are available.

## Ideas (not started)

### Image Viewer

View and organize images with a simple, easy-to-use GUI. Right-click on an image,
a set of images, or a folder, and open the Image Viewer to that folder. Users can
rate, favorite, tag, move, and rename photos.

- Example: <https://www.xnview.com/en/xnview/>
