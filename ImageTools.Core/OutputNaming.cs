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

        private static string Combine(string dir, string fileName) =>
            dir.Length == 0 ? fileName : Path.Combine(dir, fileName);
    }
}
