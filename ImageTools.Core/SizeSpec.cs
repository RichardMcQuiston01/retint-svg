using System;
using System.Globalization;

namespace ImageTools.Core
{
    /// <summary>
    /// Describes a resize target independently of any source image. Serializable
    /// (plain get/set props + parameterless ctor) so it travels inside a
    /// <see cref="ResizeJob"/>; use the factory methods for construction in code.
    /// </summary>
    public sealed class SizeSpec
    {
        public SizeKind Kind { get; set; }

        /// <summary>Percentage for <see cref="SizeKind.Percent"/> (100 = unchanged).</summary>
        public double Percent { get; set; } = 100;

        /// <summary>Target longer-edge length for <see cref="SizeKind.LongestEdge"/>.</summary>
        public int LongestEdge { get; set; }

        /// <summary>Target width for <see cref="SizeKind.ExactWidthHeight"/>.</summary>
        public int Width { get; set; }

        /// <summary>Target height for <see cref="SizeKind.ExactWidthHeight"/>.</summary>
        public int Height { get; set; }

        public static SizeSpec FromPercent(double percent) =>
            new SizeSpec { Kind = SizeKind.Percent, Percent = percent };

        public static SizeSpec FromLongestEdge(int pixels) =>
            new SizeSpec { Kind = SizeKind.LongestEdge, LongestEdge = pixels };

        public static SizeSpec FromExact(int width, int height) =>
            new SizeSpec { Kind = SizeKind.ExactWidthHeight, Width = width, Height = height };

        /// <summary>
        /// Computes the target dimensions for a source of the given size.
        /// Percent and LongestEdge preserve aspect ratio; each dimension is
        /// rounded (half away from zero) and clamped to a minimum of 1px.
        /// </summary>
        /// <param name="allowUpscale">
        /// When false, a target larger than the source is clamped back to the
        /// source size (never enlarge). Ignored for <see cref="SizeKind.ExactWidthHeight"/>.
        /// </param>
        public Dimensions Compute(int sourceWidth, int sourceHeight, bool allowUpscale = true)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(sourceWidth),
                    "Source dimensions must be positive.");

            switch (Kind)
            {
                case SizeKind.Percent:
                {
                    var scale = Percent / 100.0;
                    return ClampUpscale(Scale(sourceWidth, sourceHeight, scale),
                        sourceWidth, sourceHeight, allowUpscale);
                }

                case SizeKind.LongestEdge:
                {
                    var longer = Math.Max(sourceWidth, sourceHeight);
                    var scale = (double)LongestEdge / longer;
                    return ClampUpscale(Scale(sourceWidth, sourceHeight, scale),
                        sourceWidth, sourceHeight, allowUpscale);
                }

                case SizeKind.ExactWidthHeight:
                    return new Dimensions(Math.Max(1, Width), Math.Max(1, Height));

                default:
                    throw new InvalidOperationException($"Unknown size kind: {Kind}");
            }
        }

        /// <summary>A short filename-safe token for this spec, e.g. "50pct", "1024px", "640x480".</summary>
        public string ToToken()
        {
            switch (Kind)
            {
                case SizeKind.Percent:
                    return Percent.ToString("0.##", CultureInfo.InvariantCulture) + "pct";
                case SizeKind.LongestEdge:
                    return LongestEdge.ToString(CultureInfo.InvariantCulture) + "px";
                case SizeKind.ExactWidthHeight:
                    return $"{Width}x{Height}";
                default:
                    return "resized";
            }
        }

        /// <summary>
        /// Parses a short size token — "50%", "50pct", "1024px", or "640x480" —
        /// into a <see cref="SizeSpec"/>. Whitespace is ignored and suffixes are
        /// case-insensitive. Returns false (spec null) for anything unrecognized or
        /// non-positive. The inverse of the human-friendly tokens the settings file
        /// uses; the "%"/"pct" forms both map to <see cref="SizeKind.Percent"/>.
        /// </summary>
        public static bool TryParse(string? token, out SizeSpec? spec)
        {
            spec = null;
            if (string.IsNullOrWhiteSpace(token)) return false;

            var t = token!.Trim();

            // Percent: "50%" or "50pct".
            string? pctBody = null;
            if (t.EndsWith("%", StringComparison.Ordinal))
                pctBody = t.Substring(0, t.Length - 1);
            else if (t.EndsWith("pct", StringComparison.OrdinalIgnoreCase))
                pctBody = t.Substring(0, t.Length - 3);

            if (pctBody != null)
            {
                if (double.TryParse(pctBody.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var pct)
                    && pct > 0 && !double.IsInfinity(pct))
                {
                    spec = FromPercent(pct);
                    return true;
                }
                return false;
            }

            // Longest edge: "1024px". Checked before the WxH split so the trailing
            // 'x' in "px" isn't mistaken for a width/height separator.
            if (t.EndsWith("px", StringComparison.OrdinalIgnoreCase))
            {
                var body = t.Substring(0, t.Length - 2).Trim();
                if (int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out var px) && px > 0)
                {
                    spec = FromLongestEdge(px);
                    return true;
                }
                return false;
            }

            // Exact: "640x480" (uppercase X accepted too).
            var parts = t.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
                && w > 0 && h > 0)
            {
                spec = FromExact(w, h);
                return true;
            }

            return false;
        }

        private static Dimensions Scale(int width, int height, double scale) =>
            new Dimensions(RoundDim(width * scale), RoundDim(height * scale));

        private static int RoundDim(double value)
        {
            // Validate the rounded value is finite and within Int32 range BEFORE
            // casting: on netstandard2.0 running under .NET Framework, casting a
            // NaN/Infinity/out-of-range double to int is unspecified. A huge or
            // non-finite percentage would otherwise yield a garbage dimension.
            var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
            if (double.IsNaN(rounded) || double.IsInfinity(rounded)
                || rounded > int.MaxValue || rounded < int.MinValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"Computed dimension ({value}) is not a finite value within the supported range.");
            }

            var result = (int)rounded;
            return result < 1 ? 1 : result; // clamp small/negative results up to 1px
        }

        private static Dimensions ClampUpscale(Dimensions target, int sourceWidth, int sourceHeight, bool allowUpscale)
        {
            if (allowUpscale) return target;
            return target.Width > sourceWidth || target.Height > sourceHeight
                ? new Dimensions(sourceWidth, sourceHeight)
                : target;
        }
    }
}
