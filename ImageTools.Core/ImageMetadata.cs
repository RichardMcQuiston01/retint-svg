namespace ImageTools.Core
{
    /// <summary>
    /// The editable metadata fields shown in the "Edit metadata…" dialog: a small,
    /// curated mix of common EXIF and IPTC values. Every field is a plain string so
    /// this type stays a dependency-free DTO shared by the handler (which reads the
    /// values from, and writes them back with, ExifTool) and the unit tests.
    ///
    /// <para><b>Keywords</b> is the one list-valued field; it is carried here as a
    /// single ", "-separated string (what the dialog shows and edits), and split /
    /// joined by ExifTool's <c>-sep</c> option on the way in and out.</para>
    ///
    /// The exact ExifTool tag each field maps to — and the order fields are read and
    /// written in — lives in <see cref="ExifToolCommand.Fields"/>, the single source
    /// of truth that keeps the read (CSV columns), write (assignments), and this DTO
    /// in lockstep.
    /// </summary>
    public sealed class ImageMetadata
    {
        // ── EXIF ──────────────────────────────────────────────────────────────
        /// <summary>EXIF:Artist — the person who created the image.</summary>
        public string Artist { get; set; } = "";

        /// <summary>EXIF:Copyright — the copyright notice.</summary>
        public string Copyright { get; set; } = "";

        /// <summary>EXIF:ImageDescription — a free-text description.</summary>
        public string Description { get; set; } = "";

        /// <summary>EXIF:DateTimeOriginal — when the photo was taken ("YYYY:MM:DD HH:MM:SS").</summary>
        public string DateTaken { get; set; } = "";

        // ── IPTC ──────────────────────────────────────────────────────────────
        /// <summary>IPTC:ObjectName — a short document title.</summary>
        public string Title { get; set; } = "";

        /// <summary>IPTC:Caption-Abstract — a caption / longer description.</summary>
        public string Caption { get; set; } = "";

        /// <summary>IPTC:Keywords — a ", "-separated list of keywords.</summary>
        public string Keywords { get; set; } = "";

        /// <summary>IPTC:By-line — the creator / photographer.</summary>
        public string Creator { get; set; } = "";

        /// <summary>IPTC:City — the city shown in or associated with the image.</summary>
        public string City { get; set; } = "";

        /// <summary>IPTC:Country-PrimaryLocationName — the country name.</summary>
        public string Country { get; set; } = "";
    }
}
