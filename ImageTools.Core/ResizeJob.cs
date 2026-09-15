using System.Collections.Generic;

namespace ImageTools.Core
{
    /// <summary>
    /// The unit of work handed from the context-menu handler to the resize
    /// worker process. Serialized to a temp JSON file (System.Text.Json) so the
    /// selected file list is never subject to command-line length limits, and so
    /// no pixel work happens inside Explorer.
    ///
    /// This type is a plain DTO — no imaging dependency. Kept in Core so both the
    /// handler (which writes it) and the worker (which reads it) share one schema.
    /// </summary>
    public sealed class ResizeJob
    {
        /// <summary>The target size to apply to every file.</summary>
        public SizeSpec Size { get; set; } = new SizeSpec();

        /// <summary>JPEG encode quality (1–100) for lossy outputs.</summary>
        public int JpegQuality { get; set; } = 85;

        /// <summary>When false, images are never enlarged beyond their source size.</summary>
        public bool AllowUpscale { get; set; } = true;

        /// <summary>Where outputs go — "sibling" (next to the source) for now.</summary>
        public string OutputLocation { get; set; } = "sibling";

        /// <summary>Absolute paths of the images to resize.</summary>
        public List<string> Files { get; set; } = new List<string>();
    }
}
