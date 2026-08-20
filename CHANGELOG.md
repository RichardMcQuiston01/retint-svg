# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- `ImageTools.Core` — a shared `netstandard2.0` project that begins a batch
  image resizer (first feature from `TODO.md`). Phase 1 lands the pure,
  UI-agnostic pieces only: size math (`SizeSpec`/`Dimensions`), presets
  (`SizePreset`), collision-safe output naming (`OutputNaming`), and the
  worker job model (`ResizeJob`) — no `System.Drawing`, all unit-tested. The
  actual pixel work will live in a separate worker process (Phase 2), never
  in-process in Explorer. `OutputNaming` rejects tokens containing path
  separators or invalid filename characters, and `SizeSpec` validates computed
  dimensions are finite and within range before casting (guarding against
  NaN/Infinity/overflow from extreme percentages).
- `ImageTools.Core.Tests` — the repo's first unit-test project (xUnit, net8.0),
  run in CI via `dotnet test`.
- `installer/SVGToolsShell.iss` — an Inno Setup script that builds an
  uninstallable installer (Add/Remove Programs entry) as an alternative to the
  raw `install.bat`/`uninstall.bat` flow. Includes a code-signing hook and
  documented signing steps for public distribution.
- `SvgTools.Core` — a shared `netstandard2.0` project holding the UI-agnostic
  SVG logic (`SvgProcessor`, `TintTarget`), so the same code can back both the
  classic net48 handler and a future Windows 11 `IExplorerCommand` handler.
- `.github/workflows/build.yml` — a GitHub Actions workflow that builds both
  `SvgTools.Core` and `SVGToolsShell` (Release) on Windows for every PR and on
  pushes to `main`, and uploads the build output as an artifact. The workflow's
  actions are pinned to commit SHAs for reproducibility.
- `.github/dependabot.yml` — weekly Dependabot updates for the `github-actions`
  ecosystem, so the SHA-pinned actions get bumped via reviewed PRs (grouped
  into one PR) rather than drifting.

### Changed
- Extracted `SvgProcessor.cs` out of the shell project into `SvgTools.Core`;
  `SVGToolsShell` now references it as a project. Types remain in the
  `SVGToolsShell` namespace, so no call sites changed. `SvgTools.Core.dll` is
  emitted alongside `SVGToolsShell.dll` and is picked up by the installer.
- Regenerated the COM CLSID (`Guid` on `SvgContextMenu`, plus matching
  `install.bat`/`uninstall.bat` registry entries) to a deployment-unique value.
- Debug logging is now compiled out of Release builds (`DebugLog` marked
  `[Conditional("DEBUG")]`), so shipping binaries no longer write
  `%TEMP%\svgtools_debug.log` on every menu open.
- Removed the unused `Newtonsoft.Json` package reference from the project.
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
