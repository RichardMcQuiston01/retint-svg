# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Changed
- Context menu structure: all actions are now nested under a single "Re-Tint"
  parent menu — "Black to Color…", "White to Color…", and (below a separator)
  "Flatten SVG Layers…" — instead of appearing as separate top-level items.
- Debug logging (`DebugLog` in `SvgContextMenu`) now writes to
  `%TEMP%\svgtools_debug.log` (the user's temp directory) instead of
  `C:\Users\Public\svgtools_debug.log`.

### Fixed
- Context menu not appearing in Explorer when `.svg` is associated with a browser
  (e.g. Chrome). Explorer resolves the file's ProgId from the user's UserChoice
  (`ChromeHTML`), bypassing both the `svgfile` ProgId and the bare `.svg`
  extension-key handler registrations. Fix: register the handler under
  `HKLM\SOFTWARE\Classes\SystemFileAssociations\.svg\ShellEx\ContextMenuHandlers`,
  which Explorer consults regardless of the default-app association. Added to
  `install.bat` and removal added to `uninstall.bat`.

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
