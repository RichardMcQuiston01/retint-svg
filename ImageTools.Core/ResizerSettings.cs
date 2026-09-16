using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ImageTools.Core
{
    /// <summary>
    /// User-editable configuration for the Resize Images menu: the preset list
    /// plus the JPEG quality and the default upscale behavior. The context-menu
    /// handler reads it from a plain INI-style file in %APPDATA%, so the menu can
    /// be changed without rebuilding or reinstalling.
    ///
    /// All parsing is deliberately tolerant: a missing, unreadable, or malformed
    /// file yields <see cref="Defaults"/>, and individual bad preset lines are
    /// skipped rather than throwing — the menu must never fail to build.
    /// </summary>
    public sealed class ResizerSettings
    {
        /// <summary>The presets shown on the menu (never empty).</summary>
        public IReadOnlyList<SizePreset> Presets { get; }

        /// <summary>JPEG encode quality (1–100) applied to lossy outputs.</summary>
        public int JpegQuality { get; }

        /// <summary>Whether presets may enlarge images smaller than the target.</summary>
        public bool AllowUpscale { get; }

        public ResizerSettings(IReadOnlyList<SizePreset>? presets, int jpegQuality, bool allowUpscale)
        {
            Presets = (presets != null && presets.Count > 0) ? presets : SizePreset.Defaults;
            JpegQuality = ClampQuality(jpegQuality);
            AllowUpscale = allowUpscale;
        }

        /// <summary>The built-in settings used when no config file is present.</summary>
        public static ResizerSettings Defaults { get; } =
            new ResizerSettings(SizePreset.Defaults, 85, true);

        /// <summary>Default on-disk location: %APPDATA%\SVGToolsShell\resizer-settings.ini.</summary>
        public static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SVGToolsShell",
                "resizer-settings.ini");

        /// <summary>
        /// Ensures the settings file at <see cref="DefaultPath"/> exists, seeding it
        /// from <see cref="DefaultFileTemplate"/> if missing, and returns its path.
        /// Used by both the handler's "Edit presets…" action and the worker's
        /// <c>--init-settings</c> mode so the template has a single source.
        /// </summary>
        public static string EnsureFileExists() => EnsureFileExists(DefaultPath);

        /// <summary>
        /// Ensures the settings file at <paramref name="path"/> exists (seeding the
        /// default template if missing) and returns it. An existing file is left
        /// untouched.
        /// </summary>
        public static string EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, DefaultFileTemplate());
            }
            return path;
        }

        /// <summary>
        /// Loads settings from <paramref name="path"/>, returning <see cref="Defaults"/>
        /// if the file is missing or cannot be read. Never throws.
        /// </summary>
        public static ResizerSettings Load(string path)
        {
            try
            {
                return File.Exists(path) ? Parse(File.ReadAllText(path)) : Defaults;
            }
            catch
            {
                return Defaults;
            }
        }

        /// <summary>
        /// Parses INI-style settings text. Comments start with ';' or '#'. A
        /// <c>[settings]</c> section understands <c>jpeg-quality</c> and
        /// <c>allow-upscale</c>; a <c>[presets]</c> section lists
        /// <c>Label = size</c> lines (size per <see cref="SizeSpec.TryParse"/>).
        /// Unknown keys and unparseable preset lines are ignored; if the file
        /// yields no valid presets, the default presets are used.
        /// </summary>
        public static ResizerSettings Parse(string text)
        {
            if (text == null) return Defaults;

            var presets = new List<SizePreset>();
            var jpegQuality = Defaults.JpegQuality;
            var allowUpscale = Defaults.AllowUpscale;
            var section = string.Empty;

            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;

                if (section == "presets")
                {
                    if (SizeSpec.TryParse(value, out var spec) && spec != null)
                        presets.Add(new SizePreset(key, spec));
                }
                else if (section == "settings")
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "jpeg-quality":
                            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var q))
                                jpegQuality = ClampQuality(q);
                            break;
                        case "allow-upscale":
                            if (TryParseBool(value, out var b)) allowUpscale = b;
                            break;
                    }
                }
            }

            return new ResizerSettings(presets, jpegQuality, allowUpscale);
        }

        /// <summary>
        /// A commented default settings file, seeded on first "Edit presets…" so
        /// the user has a working example to edit. Its presets equal
        /// <see cref="SizePreset.Defaults"/>.
        /// </summary>
        public static string DefaultFileTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; SVG Tools - Image Resizer settings");
            sb.AppendLine("; Edit this file to change the \"Resize Images\" menu, then save. The next");
            sb.AppendLine("; right-click picks up your changes - no reinstall needed.");
            sb.AppendLine(";");
            sb.AppendLine("; Lines starting with ; or # are comments. Sections are in [brackets].");
            sb.AppendLine();
            sb.AppendLine("[settings]");
            sb.AppendLine("; JPEG quality for .jpg/.jpeg outputs, 1-100 (higher = better and larger).");
            sb.AppendLine("jpeg-quality = 85");
            sb.AppendLine("; May presets enlarge images smaller than the target? (true/false)");
            sb.AppendLine("allow-upscale = true");
            sb.AppendLine();
            sb.AppendLine("[presets]");
            sb.AppendLine("; One per line:  Menu label = size");
            sb.AppendLine(";   size is a percent (50%), a longest-edge pixel count (1024px),");
            sb.AppendLine(";   or an exact width x height (640x480).");
            foreach (var p in SizePreset.Defaults)
                sb.AppendLine(p.Label + " = " + FriendlyToken(p.Spec));
            return sb.ToString();
        }

        /// <summary>Human-friendly token for the template ("25%" rather than the
        /// filename-safe "25pct" that <see cref="SizeSpec.ToToken"/> emits).</summary>
        private static string FriendlyToken(SizeSpec s) =>
            s.Kind == SizeKind.Percent
                ? s.Percent.ToString("0.##", CultureInfo.InvariantCulture) + "%"
                : s.ToToken();

        private static int ClampQuality(int q) => q < 1 ? 1 : (q > 100 ? 100 : q);

        private static bool TryParseBool(string value, out bool result)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "true": case "yes": case "1": case "on": result = true; return true;
                case "false": case "no": case "0": case "off": result = false; return true;
                default: result = false; return false;
            }
        }
    }
}
