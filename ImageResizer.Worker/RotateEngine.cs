using System;
using System.Drawing;
using System.IO;
using ImageTools.Core;

namespace ImageResizer.Worker
{
    /// <summary>
    /// Rotates a single image file by 90/180/270° clockwise and writes a
    /// non-destructive sibling (e.g. Photo_rot90.jpg). Reuses the GDI+ decode,
    /// EXIF-orientation, and atomic-write helpers from <see cref="ResizeEngine"/>.
    /// </summary>
    internal static class RotateEngine
    {
        public static string RotateFile(string sourcePath, ResizeJob job)
        {
            if (!RasterEncoding.IsSupportedExtension(sourcePath))
                throw new NotSupportedException($"Unsupported image type: {Path.GetExtension(sourcePath)}");

            var rotate = ToRotateFlip(job.RotateDegrees);

            // Load from a byte copy so the source file is never locked.
            var bytes = File.ReadAllBytes(sourcePath);
            using var ms = new MemoryStream(bytes);
            using var image = Image.FromStream(ms, useEmbeddedColorManagement: true, validateImageData: true);

            // Bake any EXIF orientation into the pixels first so the requested
            // rotation is relative to how the image actually displays...
            ResizeEngine.ApplyExifOrientation(image);
            // ...then drop the orientation tag so viewers don't re-apply it on top
            // of the pixels we just rotated (double rotation).
            TryRemoveOrientationTag(image);

            image.RotateFlip(rotate);

            return ResizeEngine.WriteAtomically(image, sourcePath, "rot" + job.RotateDegrees, job.JpegQuality);
        }

        private static System.Drawing.RotateFlipType ToRotateFlip(int degrees)
        {
            switch (degrees)
            {
                case 90:  return System.Drawing.RotateFlipType.Rotate90FlipNone;
                case 180: return System.Drawing.RotateFlipType.Rotate180FlipNone;
                case 270: return System.Drawing.RotateFlipType.Rotate270FlipNone;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(degrees), degrees, "Rotation must be 90, 180, or 270 degrees.");
            }
        }

        private static void TryRemoveOrientationTag(Image image)
        {
            try
            {
                if (Array.IndexOf(image.PropertyIdList, ExifOrientation.TagId) >= 0)
                    image.RemovePropertyItem(ExifOrientation.TagId);
            }
            catch
            {
                // Best-effort — not all formats carry property items.
            }
        }
    }
}
