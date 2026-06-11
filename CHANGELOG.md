# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [0.1.0] - 2026-06-11

### Added
- Initial release of SVGToolsShell Windows Explorer context menu extension
- **Re-Tint Black to Color** — replaces black fills/strokes with a chosen color
- **Re-Tint White to Color** — replaces white fills/strokes with a chosen color
- **Flatten SVG Layers** — merges all `<path>` elements into a single-layer SVG with one fill color
- Preset color palette: Black, White, Red, Green, Blue, Yellow, Orange, Purple, Gold, Silver
- **Custom…** option opens system color picker for any arbitrary color
- Non-destructive output: originals are never overwritten; results written as sibling files
- Collision-safe output naming (`_tint_RRGGBB.svg`, `_tint_RRGGBB_2.svg`, etc.)
- Implicit fill detection for SVGs exported from Illustrator/xTool with no explicit fill attribute
- `install.bat` / `uninstall.bat` scripts for RegAsm-based COM registration
- `.gitignore`, `README.md`, and `CLAUDE.md` project documentation

### Fixed
- Removed `<RegisterForComInterop>` from `.csproj` (no-op with `dotnet build`, caused MSBuild error)
- Corrected `install.bat` and `uninstall.bat` DLL path to `bin\Release\net48\SVGToolsShell.dll`
- Added `<PlatformTarget>x64</PlatformTarget>` to match 64-bit Explorer process
- Added nuget.org package source for SharpShell restore
- Added explicit `.svg` `ContextMenuHandlers` and Shell Extensions `Approved` registry entries to `install.bat`
