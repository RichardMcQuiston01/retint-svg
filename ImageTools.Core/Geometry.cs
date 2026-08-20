using System;

namespace ImageTools.Core
{
    /// <summary>How a <see cref="SizeSpec"/> expresses a target size.</summary>
    public enum SizeKind
    {
        /// <summary>Scale both dimensions by a percentage (e.g. 50 = half).</summary>
        Percent,

        /// <summary>Scale so the longer edge equals a pixel count; aspect preserved.</summary>
        LongestEdge,

        /// <summary>Use an exact width and height (aspect not preserved).</summary>
        ExactWidthHeight,
    }

    /// <summary>A width/height pair in pixels. Value type, no System.Drawing dependency.</summary>
    public readonly struct Dimensions : IEquatable<Dimensions>
    {
        public int Width { get; }
        public int Height { get; }

        public Dimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool Equals(Dimensions other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object? obj) => obj is Dimensions d && Equals(d);
        public override int GetHashCode() => (Width * 397) ^ Height;
        public static bool operator ==(Dimensions a, Dimensions b) => a.Equals(b);
        public static bool operator !=(Dimensions a, Dimensions b) => !a.Equals(b);
        public override string ToString() => $"{Width}x{Height}";
    }
}
