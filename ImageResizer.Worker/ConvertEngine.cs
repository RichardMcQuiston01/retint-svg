using System;
using System.IO;
using ImageMagick;
using ImageTools.Core;

namespace ImageResizer.Worker
{
    /// <summary>
    /// Converts a single image to another format using ImageMagick (Magick.NET),
    /// which handles formats GDI+ can't (notably WebP). Writes a non-destructive
    /// sibling with the new extension (Photo.jpg → Photo.png). EXIF orientation is
    /// baked in so the converted image displays the same way.
    /// </summary>
    internal static class ConvertEngine
    {
        public static string ConvertFile(string sourcePath, ResizeJob job)
        {
            var ext = NormalizeExtension(job.Format);
            var format = ToMagickFormat(ext);

            using var image = new MagickImage(sourcePath);
            image.AutoOrient(); // apply EXIF orientation into the pixels
            image.Format = format;
            if (format == MagickFormat.Jpeg)
                image.Quality = (uint)Clamp(job.JpegQuality, 1, 100);

            return WriteAtomically(image, sourcePath, ext);
        }

        /// <summary>
        /// Writes <paramref name="image"/> to a temp file, then atomically moves it
        /// onto a collision-free sibling with the new extension (never overwriting,
        /// including the source itself). A failure never leaves a partial output.
        /// </summary>
        private static string WriteAtomically(MagickImage image, string sourcePath, string newExtension)
        {
            var dir = Path.GetDirectoryName(sourcePath);
            var tempDir = string.IsNullOrEmpty(dir) ? "." : dir!;
            var temp = Path.Combine(tempDir, "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                // image.Format is set, so the encoder is chosen by Format, not by
                // the temp file's extension.
                image.Write(temp);

                for (int attempt = 0; attempt < 1000; attempt++)
                {
                    var candidate = OutputNaming.BuildConvertedPath(sourcePath, newExtension, File.Exists);
                    try
                    {
                        File.Move(temp, candidate);
                        return candidate;
                    }
                    catch (IOException) when (File.Exists(candidate))
                    {
                        // Lost the race for this name; the next candidate skips it.
                    }
                }

                throw new IOException($"Could not create a unique output file for '{sourcePath}'.");
            }
            catch
            {
                try { File.Delete(temp); } catch { /* best-effort temp cleanup */ }
                throw;
            }
        }

        private static string NormalizeExtension(string format)
        {
            var ext = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            if (ext.Length == 0)
                throw new ArgumentException("No target format was supplied.", nameof(format));
            return ext;
        }

        private static MagickFormat ToMagickFormat(string ext)
        {
            switch (ext)
            {
                case "png":  return MagickFormat.Png;
                case "jpg":
                case "jpeg": return MagickFormat.Jpeg;
                case "tif":
                case "tiff": return MagickFormat.Tiff;
                case "bmp":  return MagickFormat.Bmp;
                case "webp": return MagickFormat.WebP;
                default:
                    throw new NotSupportedException($"Unsupported target format: {ext}");
            }
        }

        private static int Clamp(int value, int min, int max) =>
            value < min ? min : (value > max ? max : value);
    }
}
