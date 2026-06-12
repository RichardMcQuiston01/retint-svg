using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;

namespace SVGToolsShell
{
    /// <summary>
    /// Windows Explorer context menu shell extension for .svg files.
    /// Provides cascading submenus for Re-Tint Black, Re-Tint White, and Flatten.
    ///
    /// Registration:
    ///   Run install.bat as Administrator after building, then restart Explorer.
    ///
    /// GUID:
    ///   Regenerate before distributing — use Tools → Create GUID in Visual Studio
    ///   or run:  [System.Guid]::NewGuid()  in PowerShell.
    /// </summary>
    [ComVisible(true)]
    [Guid("a64a7e95-943d-4f79-891a-1e1176f2fc20")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".svg")]
    public class SvgContextMenu : SharpContextMenu
    {
        // Diagnostic log lives in the user's %TEMP% — explorer.exe runs as the
        // user, so this is always writable (unlike C:\ root or Program Files).
        private static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "svgtools_debug.log");

        static SvgContextMenu()
        {
            DebugLog("Assembly loaded into process");
        }

        private static void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}\r\n");
            }
            catch
            {
                // Logging must never take down Explorer.
            }
        }

        // Show the menu for any .svg selection
        protected override bool CanShowMenu()
        {
            DebugLog("CanShowMenu called");
            return true;
        }

        protected override ContextMenuStrip CreateMenu()
        {
            DebugLog("CreateMenu called");
            var menu = new ContextMenuStrip();

            // ── Re-Tint ▸ Black/White → Color ─────────────────────────────────
            var reTint = new ToolStripMenuItem("Re-Tint")
            {
                Image = CreateSwatch(Color.Black),
            };

            var tintBlack = new ToolStripMenuItem("Black to Color…")
            {
                Image = CreateSwatch(Color.Black),
            };
            foreach (var preset in ColorPresets.All)
                tintBlack.DropDownItems.Add(BuildTintItem(preset, TintTarget.Black));

            reTint.DropDownItems.Add(tintBlack);

            var tintWhite = new ToolStripMenuItem("White to Color…")
            {
                Image = CreateSwatch(Color.White, bordered: true),
            };
            foreach (var preset in ColorPresets.All)
                tintWhite.DropDownItems.Add(BuildTintItem(preset, TintTarget.White));

            reTint.DropDownItems.Add(tintWhite);

            reTint.DropDownItems.Add(new ToolStripSeparator());

            // ── Flatten SVG Layers ────────────────────────────────────────────
            var flatten = new ToolStripMenuItem("Flatten SVG Layers…")
            {
                Image = CreateSwatch(Color.DimGray),
                ToolTipText = "Merge all paths into one layer with a single fill color",
            };
            foreach (var preset in ColorPresets.All)
                flatten.DropDownItems.Add(BuildFlattenItem(preset));

            reTint.DropDownItems.Add(flatten);

            menu.Items.Add(reTint);

            return menu;
        }

        // ── Item builders ─────────────────────────────────────────────────────

        private ToolStripMenuItem BuildTintItem(
            (string Label, string? Hex, Color Swatch) preset,
            TintTarget target)
        {
            var item = new ToolStripMenuItem(preset.Label)
            {
                Image = CreateSwatch(
                    preset.Swatch,
                    bordered: preset.Swatch == Color.White || preset.Swatch == Color.Transparent),
            };

            item.Click += (_, __) =>
            {
                var hex = ResolveHex(preset.Hex);
                if (hex is null) return;
                RunOnEachFile(path => SvgProcessor.Tint(path, target, hex));
            };

            return item;
        }

        private ToolStripMenuItem BuildFlattenItem(
            (string Label, string? Hex, Color Swatch) preset)
        {
            var item = new ToolStripMenuItem(preset.Label)
            {
                Image = CreateSwatch(
                    preset.Swatch,
                    bordered: preset.Swatch == Color.White || preset.Swatch == Color.Transparent),
            };

            item.Click += (_, __) =>
            {
                var hex = ResolveHex(preset.Hex);
                if (hex is null) return;
                RunOnEachFile(path => SvgProcessor.Flatten(path, hex));
            };

            return item;
        }

        // ── Execution ─────────────────────────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="action"/> against every selected SVG path.
        /// Collects all errors rather than stopping at the first failure.
        /// </summary>
        private void RunOnEachFile(Func<string, string> action)
        {
            var errors  = new List<string>();
            var outputs = new List<string>();

            foreach (var filePath in SelectedItemPaths)
            {
                try
                {
                    outputs.Add(action(filePath));
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                MessageBox.Show(
                    $"The following error(s) occurred:\n\n{string.Join("\n", errors)}",
                    "SVG Tools — Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (outputs.Count > 0)
            {
                MessageBox.Show(
                    $"Done! Created {outputs.Count} file(s):\n\n{string.Join("\n", outputs)}",
                    "SVG Tools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the hex string for a preset, or opens a ColorDialog for "Custom…".
        /// Returns null if the user cancels.
        /// </summary>
        private static string? ResolveHex(string? presetHex)
        {
            if (presetHex is not null) return presetHex;

            using var dlg = new ColorDialog { FullOpen = true };
            return dlg.ShowDialog() == DialogResult.OK
                ? $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}"
                : null;
        }

        /// <summary>Creates a 16×16 solid-color swatch bitmap for menu icons.</summary>
        private static Bitmap CreateSwatch(Color color, bool bordered = false)
        {
            var bmp    = new Bitmap(16, 16);
            var fill   = color == Color.Transparent ? Color.LightGray : color;

            using var g   = Graphics.FromImage(bmp);
            using var bg  = new SolidBrush(fill);

            g.FillRectangle(bg, 1, 1, 14, 14);

            if (bordered)
            {
                using var pen = new Pen(Color.Gray);
                g.DrawRectangle(pen, 1, 1, 13, 13);
            }

            return bmp;
        }
    }
}
