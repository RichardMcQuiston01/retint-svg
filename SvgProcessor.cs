using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SVGToolsShell
{
    public enum TintTarget { Black, White }

    /// <summary>
    /// Stateless SVG manipulation logic. All methods accept a source path,
    /// perform their transformation, write a new file alongside the original,
    /// and return the output path. Errors bubble as exceptions to the caller.
    /// </summary>
    public static class SvgProcessor
    {
        private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

        // ── Compiled patterns ────────────────────────────────────────────────

        // Attribute form:  fill="black"  fill="#000"  fill="#000000"
        private static readonly Regex RxBlackAttr = new Regex(
            @"(?<=\b(?:fill|stroke)="")(?:black|#000(?:000)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxWhiteAttr = new Regex(
            @"(?<=\b(?:fill|stroke)="")(?:white|#[Ff]{3}(?:[Ff]{3})?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Inline style form:  fill:#000000   stroke: black
        private static readonly Regex RxBlackStyle = new Regex(
            @"(?<=\b(?:fill|stroke)\s*:\s*)(?:black|#000(?:000)?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RxWhiteStyle = new Regex(
            @"(?<=\b(?:fill|stroke)\s*:\s*)(?:white|#[Ff]{3}(?:[Ff]{3})?)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Replaces all occurrences of the target color (explicit attribute,
        /// inline style, or implicit default-black) with <paramref name="newHex"/>.
        /// Writes a sibling file and returns its path.
        /// </summary>
        public static string Tint(string svgPath, TintTarget target, string newHex)
        {
            var content  = File.ReadAllText(svgPath);
            var attrRx   = target == TintTarget.Black ? RxBlackAttr  : RxWhiteAttr;
            var styleRx  = target == TintTarget.Black ? RxBlackStyle : RxWhiteStyle;

            var result = attrRx.Replace(content, newHex);
            result     = styleRx.Replace(result, newHex);

            // SVGs exported from Illustrator / xTool often have no explicit fill
            // attribute — their paths inherit the SVG default (black). Inject a
            // fill on the top-level group so the color applies to everything.
            if (target == TintTarget.Black && !HasExplicitFill(content))
                result = InjectTopLevelFill(result, newHex);

            var outPath = BuildOutputPath(svgPath, $"tint_{SanitizeHex(newHex)}");
            File.WriteAllText(outPath, result);
            return outPath;
        }

        /// <summary>
        /// Merges every &lt;path&gt; element in the SVG into a single path on a
        /// clean single-layer document with one unified fill color.
        /// Useful for SVGs where the same visual shape is split across many groups.
        /// </summary>
        public static string Flatten(string svgPath, string fillHex)
        {
            var xdoc  = XDocument.Load(svgPath);
            XNamespace ns = "http://www.w3.org/2000/svg";

            var svgEl = xdoc.Root
                ?? throw new InvalidOperationException("File does not appear to be a valid SVG.");

            // Collect path data in document order
            var combinedD = string.Join(" ",
                svgEl.Descendants(ns + "path")
                     .Select(p => ((string?)p.Attribute("d") ?? string.Empty).Trim())
                     .Where(d => d.Length > 0));

            if (string.IsNullOrWhiteSpace(combinedD))
                throw new InvalidOperationException("No <path> elements with data found in SVG.");

            // Preserve dimensions / viewBox from the original
            var width   = (string?)svgEl.Attribute("width")   ?? "100%";
            var height  = (string?)svgEl.Attribute("height")  ?? "100%";
            var viewBox = (string?)svgEl.Attribute("viewBox");

            var flatSvg = new XDocument(
                new XElement(ns + "svg",
                    new XAttribute("xmlns",   ns.NamespaceName),
                    new XAttribute("version", "1.1"),
                    new XAttribute("width",   width),
                    new XAttribute("height",  height),
                    viewBox is not null ? new XAttribute("viewBox", viewBox) : null,
                    new XElement(ns + "path",
                        new XAttribute("fill", fillHex),
                        new XAttribute("d",    combinedD))));

            var outPath = BuildOutputPath(svgPath, "flat");
            flatSvg.Save(outPath);
            return outPath;
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private static bool HasExplicitFill(string content) =>
            Regex.IsMatch(content, @"\bfill\s*[=:]", RegexOptions.IgnoreCase, RegexTimeout);

        private static string InjectTopLevelFill(string content, string hex)
        {
            // 1. Replace fill on an existing top-level <g fill="...">
            var result = Regex.Replace(content,
                @"(<g\b[^>]*?)fill=""[^""]*""",
                $"$1fill=\"{hex}\"",
                RegexOptions.IgnoreCase, RegexTimeout);

            if (result != content) return result;

            // 2. Inject onto the first <g> that has no fill attribute
            result = new Regex(@"(<g\b)", RegexOptions.None, RegexTimeout)
                .Replace(content, $"$1 fill=\"{hex}\"", count: 1);

            if (result != content) return result;

            // 3. Last resort — inject onto the <svg> root element
            return new Regex(@"(<svg\b)", RegexOptions.None, RegexTimeout)
                .Replace(content, $"$1 fill=\"{hex}\"", count: 1);
        }

        /// <summary>
        /// Builds an output path that does not collide with any existing file.
        /// e.g.  MyIcon_tint_EFBF04.svg  →  MyIcon_tint_EFBF04_2.svg
        /// </summary>
        private static string BuildOutputPath(string sourcePath, string suffix)
        {
            var dir       = Path.GetDirectoryName(sourcePath) ?? ".";
            var stem      = Path.GetFileNameWithoutExtension(sourcePath);
            var candidate = Path.Combine(dir, $"{stem}_{suffix}.svg");
            var counter   = 2;

            while (File.Exists(candidate))
                candidate = Path.Combine(dir, $"{stem}_{suffix}_{counter++}.svg");

            return candidate;
        }

        private static string SanitizeHex(string hex) =>
            hex.TrimStart('#').ToUpperInvariant();
    }
}
