using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using ImageTools.Core;

namespace ImageResizer.Worker
{
    /// <summary>
    /// Entry point. Usage: <c>ImageResizer.Worker.exe &lt;job-file.json&gt;</c>.
    /// The job file is a serialized <see cref="ResizeJob"/> written by the
    /// context-menu handler; the file list travels in it (not on the command
    /// line) so large selections aren't capped by command-line length limits.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                ShowError("No job file was supplied.");
                return 2;
            }

            var jobPath = args[0];
            ResizeJob? job;
            try
            {
                var json = File.ReadAllText(jobPath);
                job = JsonSerializer.Deserialize<ResizeJob>(json);
            }
            catch (Exception ex)
            {
                ShowError($"Could not read the resize job:\n{ex.Message}");
                return 2;
            }

            // System.Text.Json can set Files to null (an explicit "Files": null in
            // the job) despite the initializer, so guard it before dereferencing.
            if (job is null || job.Files is null || job.Files.Count == 0)
            {
                ShowError("The resize job contained no files.");
                TryDelete(jobPath);
                return 2;
            }

            var outputs = new List<string>();
            var errors = new List<string>();

            foreach (var file in job.Files)
            {
                try
                {
                    outputs.Add(ResizeEngine.ResizeFile(file, job));
                }
                catch (Exception ex)
                {
                    errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }

            TryDelete(jobPath); // the job file is single-use scratch
            ShowSummary(outputs, errors);
            return errors.Count > 0 ? 1 : 0;
        }

        private static void ShowSummary(IReadOnlyList<string> outputs, IReadOnlyList<string> errors)
        {
            if (errors.Count > 0)
            {
                var body = $"Resized {outputs.Count} file(s).\n\nThe following error(s) occurred:\n"
                    + string.Join("\n", errors);
                MessageBox.Show(body, "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (outputs.Count > 0)
            {
                MessageBox.Show(
                    $"Done! Created {outputs.Count} file(s).",
                    "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static void ShowError(string message) =>
            MessageBox.Show(message, "Image Resizer", MessageBoxButtons.OK, MessageBoxIcon.Error);

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }
}
