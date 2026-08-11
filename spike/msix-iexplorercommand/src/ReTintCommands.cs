using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SvgTools.ShellExtension.Interop;
using SVGToolsShell; // SvgProcessor + TintTarget, reused verbatim from the repo root

namespace SvgTools.ShellExtension
{
    // ─────────────────────────────────────────────────────────────────────────
    //  SPIKE: the Re-Tint command tree, mapping the classic ContextMenuStrip
    //  cascade onto IExplorerCommand sub-commands.
    //
    //      Re-Tint  (ROOT — this is the CLSID the manifest registers)
    //      ├── Black to Color…   → tint black → <color>
    //      ├── White to Color…   → tint white → <color>
    //      └── Flatten SVG Layers…→ flatten → <color>
    //
    //  In a real build, SvgProcessor/ColorPresets would move into a shared
    //  netstandard2.0 "SvgTools.Core" project referenced by both this handler
    //  and the classic net48 extension. For the spike the csproj <Compile>-links
    //  the existing source files directly (see SvgTools.ShellExtension.csproj).
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Preset colors for the modern menu — label + hex only (no Bitmap).</summary>
    internal static class Palette
    {
        // Mirrors ColorPresets.All minus the "Custom…" entry (see README open
        // problem #2 — an inline color picker from a surrogate is deferred).
        public static readonly IReadOnlyList<(string Label, string Hex)> Colors = new[]
        {
            ("Black",  "#000000"),
            ("White",  "#FFFFFF"),
            ("Red",    "#FF0000"),
            ("Green",  "#00AA00"),
            ("Blue",   "#0000FF"),
            ("Yellow", "#FFD700"),
            ("Orange", "#FF8000"),
            ("Purple", "#800080"),
            ("Gold",   "#EFBF04"),
            ("Silver", "#C0C0C0"),
        };
    }

    /// <summary>
    /// Root "Re-Tint" command. This CLSID is the one declared in AppxManifest.xml
    /// (com:Class Id + desktop5:Verb Clsid). ComVisible so .NET COM hosting emits
    /// a class factory for it in SvgTools.ShellExtension.comhost.dll.
    /// </summary>
    [ComVisible(true)]
    [Guid("B2A9F2C4-3E17-4D8A-9C21-7F6E5D4C3B2A")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class ReTintCommand : ExplorerCommandBase
    {
        protected override string Title => "Re-Tint";
        protected override EXPCMDFLAGS Flags => EXPCMDFLAGS.ECF_HASSUBCOMMANDS;

        protected override IReadOnlyList<ExplorerCommandBase> SubCommands => new ExplorerCommandBase[]
        {
            new TintFlyoutCommand("Black to Color…", TintTarget.Black),
            new TintFlyoutCommand("White to Color…", TintTarget.White),
            new FlattenFlyoutCommand(),
        };
    }

    /// <summary>A "Black/White to Color…" flyout — its children are the colors.</summary>
    internal sealed class TintFlyoutCommand : ExplorerCommandBase
    {
        private readonly string _title;
        private readonly TintTarget _target;

        public TintFlyoutCommand(string title, TintTarget target)
        {
            _title = title;
            _target = target;
        }

        protected override string Title => _title;
        protected override EXPCMDFLAGS Flags => EXPCMDFLAGS.ECF_HASSUBCOMMANDS;

        protected override IReadOnlyList<ExplorerCommandBase> SubCommands
        {
            get
            {
                var items = new List<ExplorerCommandBase>(Palette.Colors.Count);
                foreach (var (label, hex) in Palette.Colors)
                    items.Add(new TintLeafCommand(label, _target, hex));
                return items;
            }
        }
    }

    /// <summary>A leaf color under a tint flyout — invokes SvgProcessor.Tint.</summary>
    internal sealed class TintLeafCommand : ExplorerCommandBase
    {
        private readonly string _label;
        private readonly TintTarget _target;
        private readonly string _hex;

        public TintLeafCommand(string label, TintTarget target, string hex)
        {
            _label = label;
            _target = target;
            _hex = hex;
        }

        protected override string Title => _label;

        protected override void Execute(IReadOnlyList<string> selectedPaths)
        {
            foreach (var path in selectedPaths)
                SvgProcessor.Tint(path, _target, _hex);
            // TODO: replace the classic MessageBox summary with a toast (README #3).
        }
    }

    /// <summary>The "Flatten SVG Layers…" flyout — children are the fill colors.</summary>
    internal sealed class FlattenFlyoutCommand : ExplorerCommandBase
    {
        protected override string Title => "Flatten SVG Layers…";

        // Separator before Flatten mirrors the classic menu's visual grouping.
        protected override EXPCMDFLAGS Flags =>
            EXPCMDFLAGS.ECF_HASSUBCOMMANDS | EXPCMDFLAGS.ECF_SEPARATORBEFORE;

        protected override IReadOnlyList<ExplorerCommandBase> SubCommands
        {
            get
            {
                var items = new List<ExplorerCommandBase>(Palette.Colors.Count);
                foreach (var (label, hex) in Palette.Colors)
                    items.Add(new FlattenLeafCommand(label, hex));
                return items;
            }
        }
    }

    /// <summary>A leaf fill color under Flatten — invokes SvgProcessor.Flatten.</summary>
    internal sealed class FlattenLeafCommand : ExplorerCommandBase
    {
        private readonly string _label;
        private readonly string _hex;

        public FlattenLeafCommand(string label, string hex)
        {
            _label = label;
            _hex = hex;
        }

        protected override string Title => _label;

        protected override void Execute(IReadOnlyList<string> selectedPaths)
        {
            foreach (var path in selectedPaths)
                SvgProcessor.Flatten(path, _hex);
        }
    }
}
