# Spike: Windows 11 context menu (`IExplorerCommand`) + MSIX / Store packaging

> **Status: exploratory scaffold. Not built, not signed, not tested.**
> This directory is a design spike, not shippable code. The `src/` files
> compile-in-principle but the COM/shell interop and the packaging manifest
> **must be validated on a real Windows 11 box** before any of it is trusted.
> Treat every code file here as a starting point to iterate on, not a drop-in.

## Why this spike exists

The shipping extension (`SVGToolsShell`, in the repo root) is a **classic
`IContextMenu` COM handler** built on SharpShell. On Windows 11 that design has
two hard ceilings:

1. **Menu placement** — Windows 11's primary right-click menu only renders
   commands registered through the modern **`IExplorerCommand`** API by an app
   that has **package identity**. Legacy `IContextMenu` handlers are relegated to
   the "Show more options" overflow (the old Shift+F10 menu).
2. **Store eligibility** — the Microsoft Store only accepts **MSIX** packages,
   and MSIX apps declare shell extensions **in the package manifest**; they
   cannot use the current `RegAsm` + `HKLM/HKCR` registry approach.

Both ceilings are removed by the same rewrite: implement `IExplorerCommand`,
give the handler package identity via MSIX, and declare the extension in the
manifest. This spike works out *how* and *how much effort*.

## The architecture

```
                        ┌───────────────────────────────────────┐
                        │  SvgTools.Core  (netstandard2.0)       │
                        │  • SvgProcessor  (Tint / Flatten)      │  ← shared, unchanged logic
                        │  • TintTarget                          │     (color list stays per-handler
                        └───────────────┬───────────────────────┘      for now — see below)
                        ┌───────────────┴───────────────┐
        ┌───────────────▼───────────────┐   ┌───────────▼───────────────────────┐
        │  SVGToolsShell (net48)        │   │  SvgTools.ShellExtension (net8.0)  │
        │  classic IContextMenu         │   │  IExplorerCommand + IEnumExplorer- │
        │  (existing, for Win10 / <Win11│   │  Command, EnableComHosting →       │
        │   "Show more options")        │   │  *.comhost.dll in-proc COM server  │
        └───────────────────────────────┘   └───────────┬───────────────────────┘
                                                         │ declared by
                                            ┌────────────▼────────────┐
                                            │  AppxManifest.xml        │
                                            │  com:SurrogateServer +   │
                                            │  desktop4:FileExplorer-  │
                                            │  ContextMenus            │
                                            └──────────┬───────────────┘
                                        ┌──────────────┴──────────────┐
                              ┌─────────▼─────────┐        ┌──────────▼──────────┐
                              │ Sparse package    │        │ Full MSIX → Store   │
                              │ (self-distributed │        │ (Microsoft signs)   │
                              │  sideload, you    │        │                     │
                              │  sign)            │        │                     │
                              └───────────────────┘        └─────────────────────┘
```

The key insight that makes this feasible in C# (rather than a C++ rewrite):
**the Win11 modern context menu loads handlers in a separate surrogate/host
process, not directly inside `explorer.exe`.** That sidesteps the classic
"you can't load CoreCLR in-proc into Explorer" problem which historically
forced shell extensions to be C++. Combined with .NET 8's
`<EnableComHosting>` (which emits a native `*.comhost.dll` that acts as the
in-proc COM server), a managed `IExplorerCommand` handler becomes viable —
**and it lets us reuse `SvgProcessor` verbatim.** That reuse is already wired:
`src/SvgTools.ShellExtension.csproj` has a `ProjectReference` to the shared
`SvgTools.Core` (netstandard2.0) project, the same assembly the classic net48
handler consumes. The preset color list is still declared per-handler (the
spike's `Palette`), since Core is intentionally kept free of any color/UI model.

### Language choice: C# vs C++

| | C# (.NET 8 + COM hosting) — **recommended for this spike** | C++/WinRT |
|---|---|---|
| Reuses existing `SvgProcessor` | ✅ directly | ❌ full reimplementation |
| Team familiarity | ✅ | ❌ |
| Proven in the wild | ⚠️ works, less common | ✅ PowerToys, File Explorer add-ins |
| Interop friction | ⚠️ hand-written `IExplorerCommand` interop | ✅ native headers |
| Package/runtime size | ⚠️ carries .NET runtime unless self-contained trimmed | ✅ small |

**Recommendation:** go C# to reuse `SvgProcessor`, but **de-risk first** with a
hello-world single command (no submenus) — build it, package it as a sparse
package, and confirm it renders in the Win11 main menu **before** porting the
full Re-Tint tree. If COM hosting proves flaky in the surrogate, the fallback
is a thin C++ handler that shells out to a small .NET CLI carrying
`SvgProcessor` — you still reuse the logic, just across a process boundary.

## Mapping the current menu to `IExplorerCommand`

The classic handler builds a `ContextMenuStrip` tree. `IExplorerCommand`
expresses the same nesting through **sub-commands**: a command returns
`ECF_HASSUBCOMMANDS` from `GetFlags()` and yields children from
`EnumSubCommands()` (via `IEnumExplorerCommand`). Children can themselves have
children, so the three-level cascade maps cleanly:

```
Re-Tint                         ← root command (ECF_HASSUBCOMMANDS)
├── Black to Color…             ← sub-command (ECF_HASSUBCOMMANDS)
│   ├── Red   → SvgProcessor.Tint(path, Black, "#FF0000")
│   ├── …
│   └── Custom…                 ← see "Open problems" re: color picker
├── White to Color…             ← sub-command (ECF_HASSUBCOMMANDS)
│   └── …
├── ──────────                  ← ECF_SEPARATORBEFORE on the next item
└── Flatten SVG Layers…         ← sub-command (ECF_HASSUBCOMMANDS)
    └── Red → SvgProcessor.Flatten(path, "#FF0000")
```

- **Leaf `Invoke`** receives the selected items as an `IShellItemArray`; iterate
  it, pull each `.svg` path, and call the same `SvgProcessor` methods the classic
  handler calls today.
- **Icons** come from `GetIcon()` (a resource/path string), not a `Bitmap`.
  The per-color swatches would become small `.ico`/PNG resources or be dropped
  for v1 — so the handler's `Palette` needs only `Label` + `Hex`, no
  `System.Drawing.Color`. (If the color list is later promoted into
  `SvgTools.Core` as a shared `Label`+`Hex` model, keep it free of
  `System.Drawing` so Core stays portable.)

See `src/ReTintCommands.cs` for the skeleton of this tree.

## Files in this spike

| Path | What it is |
|---|---|
| `AppxManifest.xml` | Package manifest declaring the COM surrogate server + `.svg` context-menu extension. The core artifact — this is what makes it a Win11 menu / Store-eligible. |
| `src/SvgTools.ShellExtension.csproj` | .NET 8 class library with `EnableComHosting`, producing `SvgTools.ShellExtension.comhost.dll`. |
| `src/Interop/ExplorerCommandInterop.cs` | Hand-written COM interop for `IExplorerCommand`, `IEnumExplorerCommand`, `IShellItemArray`, and the `EXPCMDSTATE`/`EXPCMDFLAGS` enums. |
| `src/ExplorerCommandBase.cs` | Abstract base implementing the boilerplate of `IExplorerCommand` so concrete commands only override title/flags/invoke. |
| `src/ReTintCommands.cs` | The Re-Tint command tree (root → tint/flatten → color leaves) wired to `SvgProcessor`. |
| `package/build.ps1` | `makeappx` + `signtool` to produce a signed sparse/MSIX package. |
| `package/register-dev.ps1` | `Add-AppxPackage -Register … -ExternalLocation` for iterative dev testing without repackaging. |

## How you'd actually build & test this (on Windows 11)

```powershell
# 0. Prereqs: Windows 11, .NET 8 SDK, Windows SDK (makeappx/signtool),
#    and a code-signing cert (a self-signed one is fine for local dev).

# 1. Build the handler DLL (+ its native comhost shim)
dotnet publish spike/msix-iexplorercommand/src/SvgTools.ShellExtension.csproj `
  -c Release -r win-x64 --self-contained false

# 2. Point AppxManifest.xml's Identity/Publisher at your signing cert subject,
#    then register the built output as a sparse package for dev iteration:
./spike/msix-iexplorercommand/package/register-dev.ps1

# 3. Restart Explorer, right-click a .svg  →  "Re-Tint" should appear in the
#    MAIN Win11 menu (not under "Show more options").

# 4. To produce a distributable package instead:
./spike/msix-iexplorercommand/package/build.ps1
```

## Open problems / decisions to make before committing to this

1. **Feasibility gate (do this first).** Confirm a managed `IExplorerCommand`
   handler actually loads in the Win11 surrogate via `EnableComHosting`. If it
   doesn't, switch to the C++-shim fallback above. Everything else depends on
   this answer.
2. **The "Custom…" color picker.** The classic handler opens a WinForms
   `ColorDialog` inline. Showing arbitrary UI from a shell surrogate is fragile
   (STA/threading, no real parent window). Options: (a) drop Custom from the
   fast menu and provide it via a launched helper app; (b) ship a fixed preset
   list only for v1. Recommend (b) for the spike.
3. **Output feedback.** The classic handler pops a `MessageBox` summarising the
   files created. From a surrogate, prefer a toast notification (the package has
   identity, so Windows toasts are available) or silent success + Explorer
   refresh.
4. **Runtime dependency.** `framework-dependent` keeps the package small but
   requires the .NET 8 Desktop Runtime on the target. `self-contained` is bigger
   but dependency-free — likely the right call for Store distribution.
5. **Signing / identity.** The `Identity/@Publisher` in the manifest must match
   the signing certificate subject exactly. Store submission: Microsoft signs;
   the manifest Identity is assigned in Partner Center. Sideload: you sign, and
   users must trust your cert (or you distribute via a signed `.msixbundle`).
6. **Coexistence.** Decide whether the MSIX ships *alongside* the classic
   handler (classic for Win10, modern for Win11) or replaces it. If both register
   for `.svg`, guard against the menu appearing twice on Win11.

## Rough effort estimate

| Task | Estimate |
|---|---|
| Feasibility gate: hello-world `IExplorerCommand` renders in Win11 menu | 1–2 days |
| ~~Extract `SvgTools.Core` (netstandard2.0) shared lib~~ | ✅ done — referenced by this project |
| Full Re-Tint command tree + icons + invoke wiring | 2–3 days |
| Sparse-package build/sign/register scripts hardened | 1 day |
| Toast/feedback + Custom-color decision + polish | 1–2 days |
| MSIX packaging + Store submission (Partner Center, assets, listing) | 2–3 days |
| **Total** | **~1.5–2 weeks** of focused work |

The feasibility gate is the critical path — nail it before investing in the rest.
```
