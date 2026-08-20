using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace ImageResizer.Worker
{
    /// <summary>
    /// Chooses the encoder for an output file based on its extension and applies
    /// JPEG quality. Formats are limited to what GDI+ handles natively; broader
    /// format support (WebP/HEIC/AVIF) is a later imaging-engine swap.
    /// </summary>
    internal static class RasterEncoding
    {
        private static readonly ImageCodecInfo? JpegCodec =
            ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

        /// <summary>True if the extension is one this worker can encode.</summary>
        public static bool IsSupportedExtension(string path) =>
            FormatFor(Path.GetExtension(path)) != null;

        /// <summary>
        /// Saves <paramref name="image"/> to <paramref name="stream"/> in the
        /// format implied by <paramref name="targetPath"/>'s extension.
        /// </summary>
        public static void Save(Image image, Stream stream, string targetPath, int jpegQuality)
        {
            var ext = Path.GetExtension(targetPath);
            var format = FormatFor(ext)
                ?? throw new NotSupportedException($"Unsupported output format: {ext}");

            if (format.Guid == ImageFormat.Jpeg.Guid && JpegCodec != null)
            {
                var quality = Math.Min(100, Math.Max(1, jpegQuality));
                using var parameters = new EncoderParameters(1);
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                image.Save(stream, JpegCodec, parameters);
            }
            else
            {
                image.Save(stream, format);
            }
        }

        private static ImageFormat? FormatFor(string extension)
        {
            switch (extension.TrimStart('.').ToLowerInvariant())
            {
                case "jpg":
                case "jpeg":
                    return ImageFormat.Jpeg;
                case "png":
                    return ImageFormat.Png;
                case "bmp":
                    return ImageFormat.Bmp;
                case "gif":
                    return ImageFormat.Gif;
                case "tif":
                case "tiff":
                    return ImageFormat.Tiff;
                default:
                    return null;
            }
        }
    }
}
