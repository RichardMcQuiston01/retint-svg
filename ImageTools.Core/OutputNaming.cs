using System;
using System.IO;

namespace ImageTools.Core
{
    /// <summary>
    /// Picks a non-destructive output path for a resized image, preserving the
    /// source extension and appending a counter to avoid names the caller
    /// reports as taken:
    ///
    ///   Photo.jpg  →  Photo_50pct.jpg
    ///                 Photo_50pct_2.jpg   (if the first is reported as existing)
    ///
    /// The existence check is injected so the logic stays pure and unit-testable
    /// (the worker passes <c>File.Exists</c>; tests pass an in-memory set).
    ///
    /// This selects a name that is free <i>at call time</i>; it is not an atomic
    /// reservation. The worker that actually writes the file owns the atomic
    /// create-or-retry (e.g. <c>FileMode.CreateNew</c>, advancing the counter on
    /// collision) so concurrent runs never clobber each other.
    /// </summary>
    public static class OutputNaming
    {
        public static string BuildOutputPath(string sourcePath, string token, Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("Source path is required.", nameof(sourcePath));
            if (exists is null)
                throw new ArgumentNullException(nameof(exists));

            // The token becomes part of a filename, so it must be a single path
            // component — reject separators and invalid filename characters so a
            // crafted token can't redirect the output or traverse directories.
            if (string.IsNullOrWhiteSpace(token)
                || token.IndexOf('/') >= 0
                || token.IndexOf('\\') >= 0
                || token.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Token must be a single filename component (no path separators or invalid characters).",
                    nameof(token));
            }

            var dir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            var ext = Path.GetExtension(sourcePath); // includes the leading dot, or empty

            var candidate = Combine(dir, $"{stem}_{token}{ext}");
            var counter = 2;
            while (exists(candidate))
                candidate = Combine(dir, $"{stem}_{token}_{counter++}{ext}");

            return candidate;
        }

        /// <summary>
        /// Picks a non-destructive output path for a format conversion: the source
        /// stem with a new extension, plus a counter to dodge names the caller
        /// reports as taken (and to avoid overwriting the source when converting to
        /// the same extension):
        ///
        ///   Photo.jpg  →  Photo.png
        ///   Photo.png  →  Photo_2.png   (converting .png to png, or if Photo.png exists)
        /// </summary>
        public static string BuildConvertedPath(string sourcePath, string newExtension, Func<string, bool> exists)
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("Source path is required.", nameof(sourcePath));
            if (exists is null)
                throw new ArgumentNullException(nameof(exists));

            var ext = (newExtension ?? string.Empty).TrimStart('.');
            // The extension becomes part of a filename; keep it a bare, safe token.
            if (ext.Length == 0
                || ext.IndexOf('/') >= 0
                || ext.IndexOf('\\') >= 0
                || ext.IndexOf('.') >= 0
                || ext.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Extension must be a bare filename extension (letters/digits, no dot or separators).",
                    nameof(newExtension));
            }

            var dir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
            var stem = Path.GetFileNameWithoutExtension(sourcePath);

            var candidate = Combine(dir, $"{stem}.{ext}");
            var counter = 2;
            // Also skip the source itself so a same-extension conversion never
            // targets (and clobbers) the original.
            while (exists(candidate) || PathsEqual(candidate, sourcePath))
                candidate = Combine(dir, $"{stem}_{counter++}.{ext}");

            return candidate;
        }

        private static bool PathsEqual(string a, string b) =>
            string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static string Combine(string dir, string fileName) =>
            dir.Length == 0 ? fileName : Path.Combine(dir, fileName);
    }
}
