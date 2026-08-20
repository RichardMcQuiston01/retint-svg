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

        private static Dimensions Scale(int width, int height, double scale) =>
            new Dimensions(RoundDim(width * scale), RoundDim(height * scale));

        private static int RoundDim(double value) =>
            Math.Max(1, (int)Math.Round(value, MidpointRounding.AwayFromZero));

        private static Dimensions ClampUpscale(Dimensions target, int sourceWidth, int sourceHeight, bool allowUpscale)
        {
            if (allowUpscale) return target;
            return target.Width > sourceWidth || target.Height > sourceHeight
                ? new Dimensions(sourceWidth, sourceHeight)
                : target;
        }
    }
}
