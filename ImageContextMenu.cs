using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ImageTools.Core;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;

namespace SVGToolsShell
{
    /// <summary>
    /// Windows Explorer context menu shell extension for raster images
    /// (.png/.jpg/.jpeg/.bmp/.gif/.tif/.tiff). Adds a cascading "Resize Images"
    /// menu whose items are the built-in <see cref="SizePreset.Defaults"/>.
    ///
    /// This handler does no pixel work: picking a preset writes a
    /// <see cref="ResizeJob"/> to a temp JSON file and launches the separate
    /// ImageResizer.Worker.exe against it, so GDI+ never runs inside explorer.exe.
    /// The worker ships alongside this DLL (staged there by the build) and is
    /// located relative to this assembly.
    ///
    /// Registration:
    ///   Run install.bat as Administrator after building, then restart Explorer.
    ///
    /// GUID:
    ///   Distinct from SvgContextMenu — this is a separate COM server. Regenerate
    ///   before distributing:  [System.Guid]::NewGuid()  in PowerShell.
    /// </summary>
    [ComVisible(true)]
    [Guid("25EF2E9B-582C-46C0-9FF2-EF10313F09D1")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".png")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".jpg")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".jpeg")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".bmp")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".gif")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".tif")]
    [COMServerAssociation(AssociationType.ClassOfExtension, ".tiff")]
    public class ImageContextMenu : SharpContextMenu
    {
        private static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "svgtools_debug.log");

        // The file types the resize worker (GDI+) handles natively. Explorer only
        // queries this handler for the registered extensions, but a multi-select
        // can still mix in unrelated files — those are filtered out of the job.
        private static readonly HashSet<string> SupportedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff",
            };

        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
            try
            {
                File.AppendAllText(
                    LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [ImageResizer] {message}\r\n");
            }
            catch
            {
                // Logging must never take down Explorer.
            }
        }

        protected override bool CanShowMenu()
        {
            DebugLog("CanShowMenu called");
            return true;
        }

        protected override ContextMenuStrip CreateMenu()
        {
            DebugLog("CreateMenu called");
            var menu = new ContextMenuStrip();

            var resize = new ToolStripMenuItem("Resize Images")
            {
                Image = CreateIcon(),
                ToolTipText = "Create resized copies alongside the originals",
            };

            foreach (var preset in SizePreset.Defaults)
                resize.DropDownItems.Add(BuildPresetItem(preset));

            menu.Items.Add(resize);
            return menu;
        }

        private ToolStripMenuItem BuildPresetItem(SizePreset preset)
        {
            var item = new ToolStripMenuItem(preset.Label);
            item.Click += (_, __) => RunResize(preset.Spec);
            return item;
        }

        /// <summary>
        /// Writes a job file for the selected images and hands it to the worker.
        /// </summary>
        private void RunResize(SizeSpec spec)
        {
            var files = new List<string>();
            foreach (var path in SelectedItemPaths)
            {
                if (SupportedExtensions.Contains(Path.GetExtension(path)))
                    files.Add(path);
            }

            if (files.Count == 0)
            {
                MessageBox.Show(
                    "None of the selected files are supported image types.",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var workerPath = LocateWorker();
            if (workerPath is null)
            {
                MessageBox.Show(
                    "ImageResizer.Worker.exe was not found next to the shell extension.\n"
                    + "Reinstall so the worker ships alongside the handler.",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string jobPath;
            try
            {
                jobPath = WriteJobFile(spec, files);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not write the resize job:\n{ex.Message}",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                // UseShellExecute=false launches the exe directly (no ShellExecute
                // verb lookup); the worker shows its own summary dialog, so we
                // don't wait on it — Explorer's menu thread returns immediately.
                Process.Start(new ProcessStartInfo
                {
                    FileName = workerPath,
                    Arguments = "\"" + jobPath + "\"",
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not start the resize worker:\n{ex.Message}",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>The worker exe next to this assembly, or null if missing.</summary>
        private static string? LocateWorker()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return null;
            var candidate = Path.Combine(dir, "ImageResizer.Worker.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// Serializes a <see cref="ResizeJob"/> to a temp JSON file. Hand-written
        /// (not System.Text.Json) so the in-Explorer handler carries no JSON
        /// dependency; the shape and casing match what the worker deserializes
        /// (PascalCase properties, numeric enum for <see cref="SizeKind"/>).
        /// </summary>
        private static string WriteJobFile(SizeSpec spec, IReadOnlyList<string> files)
        {
            var sb = new StringBuilder();
            sb.Append("{\"Size\":{");
            sb.Append("\"Kind\":").Append((int)spec.Kind).Append(',');
            sb.Append("\"Percent\":")
              .Append(spec.Percent.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"LongestEdge\":").Append(spec.LongestEdge).Append(',');
            sb.Append("\"Width\":").Append(spec.Width).Append(',');
            sb.Append("\"Height\":").Append(spec.Height);
            sb.Append("},");
            sb.Append("\"JpegQuality\":85,");
            sb.Append("\"AllowUpscale\":true,");
            sb.Append("\"OutputLocation\":\"sibling\",");
            sb.Append("\"Files\":[");
            for (var i = 0; i < files.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendJsonString(sb, files[i]);
            }
            sb.Append("]}");

            var jobPath = Path.Combine(
                Path.GetTempPath(),
                "resize-job-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(jobPath, sb.ToString(), new UTF8Encoding(false));
            return jobPath;
        }

        /// <summary>Appends a JSON-escaped, quoted string.</summary>
        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b");  break;
                    case '\f': sb.Append("\\f");  break;
                    case '\n': sb.Append("\\n");  break;
                    case '\r': sb.Append("\\r");  break;
                    case '\t': sb.Append("\\t");  break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        /// <summary>Creates a simple 16×16 icon for the "Resize Images" menu item.</summary>
        private static Bitmap CreateIcon()
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            using var pen = new Pen(Color.DimGray);
            // A small rectangle with a "grow" corner arrow motif.
            g.DrawRectangle(pen, 2, 4, 8, 8);
            g.DrawRectangle(pen, 7, 2, 6, 6);
            return bmp;
        }
    }
}
