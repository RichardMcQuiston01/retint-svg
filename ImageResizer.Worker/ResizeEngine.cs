using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using ImageTools.Core;

namespace ImageResizer.Worker
{
    /// <summary>
    /// Resizes a single image file: decode → apply EXIF orientation → high-quality
    /// resample → encode → write a non-destructive sibling. All GDI+ work lives
    /// here (never in Explorer). Pure decisions (target size, output name, EXIF
    /// transform) come from ImageTools.Core.
    /// </summary>
    internal static class ResizeEngine
    {
        /// <summary>
        /// Resizes <paramref name="sourcePath"/> per <paramref name="job"/> and
        /// returns the path of the newly written file. Throws on failure.
        /// </summary>
        public static string ResizeFile(string sourcePath, ResizeJob job)
        {
            if (!RasterEncoding.IsSupportedExtension(sourcePath))
                throw new NotSupportedException($"Unsupported image type: {Path.GetExtension(sourcePath)}");

            // Load from a byte copy so the source file is never locked.
            var bytes = File.ReadAllBytes(sourcePath);
            using var ms = new MemoryStream(bytes);
            using var source = Image.FromStream(ms, useEmbeddedColorManagement: true, validateImageData: true);

            ApplyExifOrientation(source);

            // Compute the target against the post-orientation dimensions.
            var target = job.Size.Compute(source.Width, source.Height, job.AllowUpscale);

            using var resized = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppArgb);
            resized.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;

                // TileFlipXY avoids a faint transparent halo at the edges.
                using var attributes = new ImageAttributes();
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                var destRect = new Rectangle(0, 0, target.Width, target.Height);
                g.DrawImage(source, destRect, 0, 0, source.Width, source.Height,
                    GraphicsUnit.Pixel, attributes);
            }

            // Encode to a temp sibling first, then publish it with an atomic move.
            // A failure during encode or move never leaves a partial/empty final
            // file, and other processes never observe an incomplete output.
            return WriteAtomically(resized, sourcePath, job.Size.ToToken(), job.JpegQuality);
        }

        private static void ApplyExifOrientation(Image image)
        {
            OrientationTransform transform;
            try
            {
                if (!image.PropertyIdList.Contains(ExifOrientation.TagId))
                    return;

                var prop = image.GetPropertyItem(ExifOrientation.TagId);
                if (prop?.Value == null || prop.Value.Length < 2)
                    return;

                int orientation = BitConverter.ToUInt16(prop.Value, 0);
                transform = ExifOrientation.ToTransform(orientation);
            }
            catch
            {
                return; // no readable orientation — leave as-is
            }

            var rotateFlip = ToRotateFlip(transform);
            if (rotateFlip != RotateFlipType.RotateNoneFlipNone)
                image.RotateFlip(rotateFlip);
        }

        private static RotateFlipType ToRotateFlip(OrientationTransform transform)
        {
            switch (transform)
            {
                case OrientationTransform.FlipHorizontal: return RotateFlipType.RotateNoneFlipX;
                case OrientationTransform.Rotate180:      return RotateFlipType.Rotate180FlipNone;
                case OrientationTransform.FlipVertical:   return RotateFlipType.Rotate180FlipX;
                case OrientationTransform.Transpose:      return RotateFlipType.Rotate90FlipX;
                case OrientationTransform.Rotate90:       return RotateFlipType.Rotate90FlipNone;
                case OrientationTransform.Transverse:     return RotateFlipType.Rotate270FlipX;
                case OrientationTransform.Rotate270:      return RotateFlipType.Rotate270FlipNone;
                default:                                  return RotateFlipType.RotateNoneFlipNone;
            }
        }

        /// <summary>
        /// Encodes <paramref name="image"/> into a hidden temp file in the source
        /// directory, then atomically moves it onto a collision-free sibling name.
        /// The final file only ever appears complete; the temp file is removed on
        /// any failure. The output extension (hence encoder) comes from the source.
        /// </summary>
        private static string WriteAtomically(Image image, string sourcePath, string token, int jpegQuality)
        {
            var dir = Path.GetDirectoryName(sourcePath);
            var tempDir = string.IsNullOrEmpty(dir) ? "." : dir!;
            var temp = Path.Combine(tempDir, "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    // Format is chosen from the source extension (== output extension).
                    RasterEncoding.Save(image, stream, sourcePath, jpegQuality);
                }

                return PublishToUniqueSibling(temp, sourcePath, token);
            }
            catch
            {
                TryDelete(temp);
                throw;
            }
        }

        /// <summary>
        /// Moves <paramref name="temp"/> onto a name that is free per
        /// <see cref="OutputNaming"/>; if another writer claimed it first,
        /// <see cref="File.Move(string,string)"/> fails (it never overwrites) and
        /// we advance to the next candidate. This is where the real collision
        /// safety lives — the naming helper only proposes candidates.
        /// </summary>
        private static string PublishToUniqueSibling(string temp, string sourcePath, string token)
        {
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                var candidate = OutputNaming.BuildOutputPath(sourcePath, token, File.Exists);
                try
                {
                    File.Move(temp, candidate);
                    return candidate;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Lost the race for this name; BuildOutputPath skips it next time.
                }
            }

            throw new IOException($"Could not create a unique output file for '{sourcePath}'.");
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* best-effort temp cleanup */ }
        }
    }
}
