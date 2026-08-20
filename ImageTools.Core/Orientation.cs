namespace ImageTools.Core
{
    /// <summary>
    /// The geometric transform needed to display an image upright, derived from
    /// its EXIF orientation tag. Kept here (no System.Drawing) so the mapping is
    /// unit-testable; the worker translates it to a GDI+ RotateFlipType.
    /// </summary>
    public enum OrientationTransform
    {
        None,
        FlipHorizontal,
        Rotate180,
        FlipVertical,
        Transpose,   // flip across the main diagonal
        Rotate90,    // 90° clockwise
        Transverse,  // flip across the anti-diagonal
        Rotate270,   // 270° clockwise (= 90° counter-clockwise)
    }

    /// <summary>Maps EXIF orientation tag values (1–8) to an upright transform.</summary>
    public static class ExifOrientation
    {
        /// <summary>The EXIF tag id for orientation (0x0112).</summary>
        public const int TagId = 0x0112;

        /// <summary>
        /// Returns the transform that makes an image with the given EXIF
        /// orientation display upright. Unknown/absent values (including the
        /// normal value 1) map to <see cref="OrientationTransform.None"/>.
        /// </summary>
        public static OrientationTransform ToTransform(int exifOrientation)
        {
            switch (exifOrientation)
            {
                case 2: return OrientationTransform.FlipHorizontal;
                case 3: return OrientationTransform.Rotate180;
                case 4: return OrientationTransform.FlipVertical;
                case 5: return OrientationTransform.Transpose;
                case 6: return OrientationTransform.Rotate90;
                case 7: return OrientationTransform.Transverse;
                case 8: return OrientationTransform.Rotate270;
                default: return OrientationTransform.None; // 1 or unknown
            }
        }

        /// <summary>
        /// True when the transform swaps width and height (the 90°/270° family),
        /// so the target size must be computed against the post-orientation
        /// dimensions.
        /// </summary>
        public static bool SwapsWidthHeight(OrientationTransform transform)
        {
            switch (transform)
            {
                case OrientationTransform.Transpose:
                case OrientationTransform.Rotate90:
                case OrientationTransform.Transverse:
                case OrientationTransform.Rotate270:
                    return true;
                default:
                    return false;
            }
        }
    }
}
