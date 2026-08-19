using System;
using System.IO;

namespace ImageTools.Core
{
    /// <summary>
    /// Builds non-destructive, collision-safe output paths for resized images,
    /// preserving the source extension:
    ///
    ///   Photo.jpg  →  Photo_50pct.jpg
    ///                 Photo_50pct_2.jpg   (if the first already exists)
    ///
    /// The existence check is injected so the logic is pure and unit-testable
    /// (the worker passes <c>File.Exists</c>; tests pass an in-memory set).
    /// </summary>
    public static class OutputNaming
    {
        public static string BuildOutputPath(string sourcePath, string token, Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("Source path is required.", nameof(sourcePath));
            if (exists is null)
                throw new ArgumentNullException(nameof(exists));

            var dir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            var ext = Path.GetExtension(sourcePath); // includes the leading dot, or empty

            var candidate = Combine(dir, $"{stem}_{token}{ext}");
            var counter = 2;
            while (exists(candidate))
                candidate = Combine(dir, $"{stem}_{token}_{counter++}{ext}");

            return candidate;
        }

        private static string Combine(string dir, string fileName) =>
            dir.Length == 0 ? fileName : Path.Combine(dir, fileName);
    }
}
