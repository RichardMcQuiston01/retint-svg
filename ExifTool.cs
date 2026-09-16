using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using ImageTools.Core;

namespace SVGToolsShell
{
    /// <summary>
    /// Thin wrapper that shells out to the bundled <c>exiftool.exe</c> to read and
    /// write image metadata for the "Edit metadata…" dialog. ExifTool is a separate
    /// process (not native code loaded in-process), so it is safe to invoke directly
    /// from the handler running inside explorer.exe. The command lines themselves are
    /// built by <see cref="ExifToolCommand"/> (Core, unit-tested).
    /// </summary>
    internal static class ExifTool
    {
        /// <summary>The outcome of a write, surfaced to the user by the dialog.</summary>
        internal sealed class WriteResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
        }

        // exiftool can be slow to cold-start (it unpacks its Perl runtime once), so
        // allow a generous window before giving up rather than hanging the dialog.
        private const int TimeoutMs = 60_000;

        /// <summary>The bundled exiftool.exe next to this assembly, or null if absent.</summary>
        public static string? Locate()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) return null;
            var candidate = Path.Combine(dir, "exiftool.exe");
            return File.Exists(candidate) ? candidate : null;
        }

        /// <summary>
        /// Reads the mapped metadata fields from <paramref name="filePath"/>. Returns
        /// an all-empty <see cref="ImageMetadata"/> if the file simply has none; throws
        /// if exiftool can't be found or run.
        /// </summary>
        public static ImageMetadata Read(string filePath)
        {
            var exe = Locate() ?? throw new FileNotFoundException(
                "exiftool.exe was not found next to the shell extension.");

            var result = Run(exe, ExifToolCommand.BuildReadArguments(filePath));
            // A non-zero exit on read usually means an unreadable file; surface it.
            if (result.ExitCode != 0 && string.IsNullOrEmpty(result.StdOut))
            {
                throw new IOException(
                    result.StdErr.Length > 0 ? result.StdErr : "ExifTool could not read the file.");
            }
            return ExifToolCommand.ReadFromCsv(result.StdOut);
        }

        /// <summary>
        /// Writes <paramref name="metadata"/> back to <paramref name="filePath"/>,
        /// keeping a <c>&lt;name&gt;_original</c> backup. Never throws for a normal
        /// ExifTool error — the outcome (including the backup note) is returned.
        /// </summary>
        public static WriteResult Write(string filePath, ImageMetadata metadata)
        {
            var exe = Locate();
            if (exe is null)
            {
                return new WriteResult
                {
                    Success = false,
                    Message = "exiftool.exe was not found next to the shell extension.\n"
                        + "Reinstall so ExifTool ships alongside the handler.",
                };
            }

            ExifToolResult result;
            try
            {
                result = Run(exe, ExifToolCommand.BuildWriteArguments(filePath, metadata));
            }
            catch (Exception ex)
            {
                return new WriteResult { Success = false, Message = $"Could not run ExifTool:\n{ex.Message}" };
            }

            if (result.ExitCode == 0)
            {
                var note = (result.StdOut.Length > 0 ? result.StdOut.Trim() : "1 image files updated");
                return new WriteResult
                {
                    Success = true,
                    Message = note + "\n\nThe original was kept as \""
                        + Path.GetFileName(filePath) + "_original\".",
                };
            }

            var err = result.StdErr.Length > 0 ? result.StdErr.Trim()
                : (result.StdOut.Length > 0 ? result.StdOut.Trim() : "ExifTool reported an error.");
            return new WriteResult { Success = false, Message = err };
        }

        private sealed class ExifToolResult
        {
            public int ExitCode { get; set; }
            public string StdOut { get; set; } = "";
            public string StdErr { get; set; } = "";
        }

        private static ExifToolResult Run(string exePath, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
            };

            using var process = new Process { StartInfo = psi };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(TimeoutMs))
            {
                try { process.Kill(); } catch { /* best-effort */ }
                throw new TimeoutException("ExifTool did not finish in time.");
            }
            // Ensure the async readers have flushed after exit.
            process.WaitForExit();

            return new ExifToolResult
            {
                ExitCode = process.ExitCode,
                StdOut = stdout.ToString(),
                StdErr = stderr.ToString(),
            };
        }
    }
}
