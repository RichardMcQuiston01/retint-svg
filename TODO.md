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

### Code Signing — wired, pending Azure setup

Azure Trusted Signing is wired into the CI `installer` job (signs the build
outputs and the installer on release builds). It activates once the repository
signing secrets are added; until then release installers ship unsigned. See
README "Code signing" for the Azure account/profile/service-principal setup and
the required secrets.

## Ideas (not started)

### Image Viewer

View and organize images with a simple, easy-to-use GUI. Right-click on an image,
a set of images, or a folder, and open the Image Viewer to that folder. Users can
rate, favorite, tag, move, and rename photos.

- Example: <https://www.xnview.com/en/xnview/>
