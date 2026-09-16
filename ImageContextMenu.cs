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
    /// (.png/.jpg/.jpeg/.bmp/.gif/.tif/.tiff) and for folders that contain them
    /// (Directory association) — right-clicking a folder resizes its top-level
    /// images. Adds a cascading "Resize Images" menu whose items are the user's
    /// configured presets (see <see cref="ResizerSettings"/>).
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
    [COMServerAssociation(AssociationType.Directory)]
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

        // Formats with meaningful EXIF/IPTC support (a subset of the above) — the
        // "Edit metadata…" item is offered only for these. .bmp/.gif carry no EXIF
        // or IPTC, so editing them would be pointless.
        private static readonly HashSet<string> MetadataExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".tif", ".tiff", ".png", ".webp",
            };

        // Presets + JPEG quality + upscale default, read from the user's config
        // file each time the menu is built (so edits apply without reinstalling).
        // Never null — a missing/unreadable file falls back to the defaults.
        private ResizerSettings _settings = ResizerSettings.Defaults;

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
            // Explorer queries this handler for image files (always show) and for
            // any folder (Directory association) — for a folder, show only when it
            // actually contains supported images, so we don't clutter every folder's
            // menu. EnumerateFiles is lazy, so this stops at the first image.
            foreach (var path in SelectedItemPaths)
            {
                if (Directory.Exists(path))
                {
                    if (FolderHasImage(path)) return true;
                }
                else if (SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True if <paramref name="dir"/> holds at least one supported
        /// image directly (non-recursive). Best-effort — an unreadable folder is
        /// treated as empty.</summary>
        private static bool FolderHasImage(string dir)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                        return true;
                }
            }
            catch
            {
                // Access denied / gone — treat as no images.
            }
            return false;
        }

        protected override ContextMenuStrip CreateMenu()
        {
            DebugLog("CreateMenu called");

            // Reload each time so edits to the settings file take effect on the
            // next right-click. Load() never throws — bad file => defaults.
            _settings = ResizerSettings.Load(ResizerSettings.DefaultPath);

            var menu = new ContextMenuStrip();

            // All image actions live under one top-level parent so the context
            // menu stays tidy as more tools are added.
            var parent = new ToolStripMenuItem("SVGToolsShell")
            {
                Image = CreateIcon(),
            };

            // ── Resize Images ▸ ───────────────────────────────────────────────
            var resize = new ToolStripMenuItem("Resize Images")
            {
                ToolTipText = "Create resized copies alongside the originals "
                    + "(a folder resizes the images inside it)",
            };
            foreach (var preset in _settings.Presets)
                resize.DropDownItems.Add(BuildPresetItem(preset));
            resize.DropDownItems.Add(new ToolStripSeparator());
            resize.DropDownItems.Add(BuildCustomItem());
            resize.DropDownItems.Add(BuildEditPresetsItem());
            parent.DropDownItems.Add(resize);

            // ── Rotate ▸ ──────────────────────────────────────────────────────
            var rotate = new ToolStripMenuItem("Rotate")
            {
                ToolTipText = "Write a rotated copy alongside each image",
            };
            rotate.DropDownItems.Add(BuildRotateItem("90° clockwise", 90));
            rotate.DropDownItems.Add(BuildRotateItem("180°", 180));
            rotate.DropDownItems.Add(BuildRotateItem("270° clockwise", 270));
            parent.DropDownItems.Add(rotate);

            // ── Convert to ▸ ──────────────────────────────────────────────────
            var convert = new ToolStripMenuItem("Convert to")
            {
                ToolTipText = "Write a copy in another format alongside each image",
            };
            convert.DropDownItems.Add(BuildConvertItem("PNG", "png"));
            convert.DropDownItems.Add(BuildConvertItem("JPG", "jpg"));
            convert.DropDownItems.Add(BuildConvertItem("TIFF", "tif"));
            convert.DropDownItems.Add(BuildConvertItem("BMP", "bmp"));
            convert.DropDownItems.Add(BuildConvertItem("WebP", "webp"));
            parent.DropDownItems.Add(convert);

            var selectedImages = SelectedImageFiles();

            // ── Edit metadata… (only for a single, metadata-capable image) ─────
            if (selectedImages.Count == 1
                && MetadataExtensions.Contains(Path.GetExtension(selectedImages[0])))
            {
                var editMeta = new ToolStripMenuItem("Edit metadata…")
                {
                    ToolTipText = "View and edit EXIF/IPTC fields (keeps an *_original backup)",
                };
                var metaFile = selectedImages[0];
                editMeta.Click += (_, __) => RunEditMetadata(metaFile);
                parent.DropDownItems.Add(editMeta);
            }

            // ── Power Rename ▸ (only for a multi-file image selection) ─────────
            if (selectedImages.Count >= 2)
            {
                parent.DropDownItems.Add(new ToolStripSeparator());
                var powerRename = new ToolStripMenuItem($"Power Rename… ({selectedImages.Count} files)")
                {
                    ToolTipText = "Batch rename the selected images (search/replace, regex, counter)",
                };
                powerRename.Click += (_, __) =>
                {
                    using var dlg = new PowerRenameDialog(selectedImages);
                    dlg.ShowDialog();
                };
                parent.DropDownItems.Add(powerRename);
            }

            menu.Items.Add(parent);
            return menu;
        }

        /// <summary>The directly-selected supported image files (no folder expansion).</summary>
        private List<string> SelectedImageFiles()
        {
            var files = new List<string>();
            foreach (var path in SelectedItemPaths)
            {
                if (!Directory.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
                    files.Add(path);
            }
            return files;
        }

        private ToolStripMenuItem BuildRotateItem(string label, int degrees)
        {
            var item = new ToolStripMenuItem(label);
            item.Click += (_, __) => RunRotate(degrees);
            return item;
        }

        private ToolStripMenuItem BuildConvertItem(string label, string extension)
        {
            var item = new ToolStripMenuItem(label);
            item.Click += (_, __) => RunConvert(extension);
            return item;
        }

        private ToolStripMenuItem BuildPresetItem(SizePreset preset)
        {
            var item = new ToolStripMenuItem(preset.Label);
            item.Click += (_, __) => RunResize(preset.Spec, _settings.AllowUpscale);
            return item;
        }

        private ToolStripMenuItem BuildCustomItem()
        {
            var item = new ToolStripMenuItem("Custom…")
            {
                ToolTipText = "Choose a percentage, longest-edge, or exact size",
            };
            item.Click += (_, __) =>
            {
                using var dlg = new CustomSizeDialog(_settings.AllowUpscale);
                if (dlg.ShowDialog() == DialogResult.OK)
                    RunResize(dlg.Spec, dlg.AllowUpscale);
            };
            return item;
        }

        private ToolStripMenuItem BuildEditPresetsItem()
        {
            var item = new ToolStripMenuItem("Edit presets…")
            {
                ToolTipText = "Open the settings file to add or change presets",
            };
            item.Click += (_, __) => EditPresets();
            return item;
        }

        /// <summary>
        /// Opens the settings file in the user's default editor, creating it from
        /// a commented template on first use. Editing it changes the menu without
        /// a reinstall.
        /// </summary>
        private static void EditPresets()
        {
            string path;
            try
            {
                // Seed the file from the template on first use (shared with the
                // worker's --init-settings mode), then open it.
                path = ResizerSettings.EnsureFileExists();

                // UseShellExecute=true so the file opens in whatever the user has
                // associated with .ini (Notepad by default).
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open the settings file:\n{ex.Message}\n\n{ResizerSettings.DefaultPath}",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunResize(SizeSpec spec, bool allowUpscale)
            => LaunchJob("resize", spec, rotateDegrees: 0, allowUpscale: allowUpscale);

        private void RunRotate(int degrees)
            => LaunchJob("rotate", new SizeSpec(), rotateDegrees: degrees, allowUpscale: true);

        private void RunConvert(string extension)
            => LaunchJob("convert", new SizeSpec(), rotateDegrees: 0, allowUpscale: true, format: extension);

        /// <summary>
        /// Reads the image's current EXIF/IPTC values with the bundled ExifTool and
        /// opens the metadata editor. Unlike resize/rotate/convert this doesn't use
        /// the worker — ExifTool is a separate process, so it's safe to run directly,
        /// and reading synchronously lets the dialog prefill the current values.
        /// </summary>
        private void RunEditMetadata(string filePath)
        {
            if (ExifTool.Locate() is null)
            {
                MessageBox.Show(
                    "exiftool.exe was not found next to the shell extension.\n"
                    + "Reinstall so ExifTool ships alongside the handler.",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ImageMetadata current;
            var previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                current = ExifTool.Read(filePath);
            }
            catch (Exception ex)
            {
                Cursor.Current = previousCursor;
                MessageBox.Show(
                    $"Could not read the image's metadata:\n{ex.Message}",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                Cursor.Current = previousCursor;
            }

            using var dlg = new MetadataDialog(filePath, current);
            dlg.ShowDialog();
        }

        /// <summary>
        /// Collects the selected images, writes a job file, and hands it to the
        /// worker. Shared by every operation (resize, rotate, convert, …).
        /// </summary>
        private void LaunchJob(string operation, SizeSpec spec, int rotateDegrees, bool allowUpscale, string format = "")
        {
            var files = CollectImageFiles();

            if (files.Count == 0)
            {
                MessageBox.Show(
                    "No supported images were found in the selection.",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var workerPath = LocateWorker();
            if (workerPath is null)
            {
                MessageBox.Show(
                    "ImageResizer.Worker.exe was not found next to the shell extension.\n"
                    + "Reinstall so the worker ships alongside the handler.",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string jobPath;
            try
            {
                jobPath = WriteJobFile(operation, spec, rotateDegrees, format, files, allowUpscale, _settings.JpegQuality);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not write the job file:\n{ex.Message}",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    $"Could not start the worker:\n{ex.Message}",
                    "SVG Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Gathers the images to resize from the selection: image files directly,
        /// and the top-level (non-recursive) supported images of any selected
        /// folder. Duplicates are removed so a file selected alongside its folder
        /// isn't resized twice.
        /// </summary>
        private List<string> CollectImageFiles()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<string>();

            void Add(string file)
            {
                if (seen.Add(file)) files.Add(file);
            }

            foreach (var path in SelectedItemPaths)
            {
                if (Directory.Exists(path))
                {
                    string[] entries;
                    try { entries = Directory.GetFiles(path); }
                    catch { continue; } // access denied / gone — skip this folder
                    foreach (var file in entries)
                    {
                        if (SupportedExtensions.Contains(Path.GetExtension(file)))
                            Add(file);
                    }
                }
                else if (SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    Add(path);
                }
            }

            return files;
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
        private static string WriteJobFile(
            string operation, SizeSpec spec, int rotateDegrees, string format,
            IReadOnlyList<string> files, bool allowUpscale, int jpegQuality)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"Operation\":");
            AppendJsonString(sb, operation);
            sb.Append(',');
            sb.Append("\"RotateDegrees\":").Append(rotateDegrees.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"Format\":");
            AppendJsonString(sb, format ?? "");
            sb.Append(',');
            sb.Append("\"Size\":{");
            sb.Append("\"Kind\":").Append((int)spec.Kind).Append(',');
            sb.Append("\"Percent\":")
              .Append(spec.Percent.ToString("R", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"LongestEdge\":").Append(spec.LongestEdge).Append(',');
            sb.Append("\"Width\":").Append(spec.Width).Append(',');
            sb.Append("\"Height\":").Append(spec.Height);
            sb.Append("},");
            sb.Append("\"JpegQuality\":").Append(jpegQuality.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"AllowUpscale\":").Append(allowUpscale ? "true" : "false").Append(',');
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
