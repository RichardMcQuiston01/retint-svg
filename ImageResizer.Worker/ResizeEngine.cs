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

            using var stream = CreateUniqueFile(sourcePath, job.Size.ToToken(), out var outputPath);
            RasterEncoding.Save(resized, stream, outputPath, job.JpegQuality);
            return outputPath;
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
        /// Atomically claims a unique output path: pick a name free per
        /// <see cref="OutputNaming"/>, then open it with <c>CreateNew</c>; if
        /// another writer won the race, advance to the next name and retry. This
        /// is where the actual collision safety lives (the naming helper only
        /// proposes candidates).
        /// </summary>
        private static FileStream CreateUniqueFile(string sourcePath, string token, out string outputPath)
        {
            for (int attempt = 0; attempt < 1000; attempt++)
            {
                var candidate = OutputNaming.BuildOutputPath(sourcePath, token, File.Exists);
                try
                {
                    var stream = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    outputPath = candidate;
                    return stream;
                }
                catch (IOException) when (File.Exists(candidate))
                {
                    // Lost the race for this name; BuildOutputPath skips it next time.
                }
            }

            throw new IOException($"Could not create a unique output file for '{sourcePath}'.");
        }
    }
}
