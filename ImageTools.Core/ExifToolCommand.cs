using System;
using System.Collections.Generic;
using System.Text;

namespace ImageTools.Core
{
    /// <summary>
    /// Builds the ExifTool command lines the handler uses to read and write image
    /// metadata, and defines the ordered field ⇄ tag mapping every other part of
    /// the metadata feature relies on. Pure and dependency-free so it can be
    /// unit-tested; the actual process launch lives in the handler.
    ///
    /// <para><b>Reading</b> uses <c>-csv</c>: ExifTool emits a two-row CSV (header +
    /// one data row) whose data columns come back in exactly the order the tags were
    /// requested. <see cref="ReadValues"/> therefore maps strictly by column index,
    /// not by header name, which sidesteps group-prefix/name ambiguity entirely.</para>
    ///
    /// <para><b>Writing</b> assigns each tag with a group-qualified <c>-Group:Tag=value</c>
    /// so, e.g., Copyright always lands in EXIF and never in a random other group.
    /// <c>-overwrite_original</c> is deliberately NOT passed, so ExifTool keeps a
    /// <c>&lt;name&gt;_original</c> backup of the untouched file next to the edited one.</para>
    /// </summary>
    public static class ExifToolCommand
    {
        /// <summary>One editable field: its position in the CSV/assignment order, the
        /// group-qualified ExifTool tag, and whether it is a list-valued tag.</summary>
        public sealed class Field
        {
            public Field(string name, string tag, bool isList)
            {
                Name = name;
                Tag = tag;
                IsList = isList;
            }

            /// <summary>Stable field key (matches an <see cref="ImageMetadata"/> property).</summary>
            public string Name { get; }

            /// <summary>Group-qualified ExifTool tag, e.g. "EXIF:Artist".</summary>
            public string Tag { get; }

            /// <summary>True for list tags (Keywords) written/read with a separator.</summary>
            public bool IsList { get; }
        }

        /// <summary>
        /// The single source of truth for field order and tag mapping. Read (CSV
        /// column order), write (assignment order), and <see cref="ImageMetadata"/>
        /// all follow this list, so they can never drift out of sync.
        /// </summary>
        public static readonly IReadOnlyList<Field> Fields = new[]
        {
            new Field("Artist",      "EXIF:Artist",                        false),
            new Field("Copyright",   "EXIF:Copyright",                     false),
            new Field("Description", "EXIF:ImageDescription",              false),
            new Field("DateTaken",   "EXIF:DateTimeOriginal",             false),
            new Field("Title",       "IPTC:ObjectName",                    false),
            new Field("Caption",     "IPTC:Caption-Abstract",             false),
            new Field("Keywords",    "IPTC:Keywords",                      true),
            new Field("Creator",     "IPTC:By-line",                       false),
            new Field("City",        "IPTC:City",                          false),
            new Field("Country",     "IPTC:Country-PrimaryLocationName",  false),
        };

        /// <summary>The ", " separator used for the list-valued Keywords field, both
        /// when asking ExifTool to join values (read) and split them (write).</summary>
        public const string ListSeparator = ", ";

        /// <summary>
        /// The command line that reads the mapped fields from <paramref name="filePath"/>
        /// as CSV. UTF-8 charsets are declared so non-ASCII values round-trip.
        /// </summary>
        public static string BuildReadArguments(string filePath)
        {
            RequirePath(filePath);

            var sb = new StringBuilder();
            AppendRaw(sb, "-charset", "filename=UTF8", "-charset", "iptc=UTF8", "-charset", "exif=UTF8");
            AppendRaw(sb, "-csv");
            AppendRaw(sb, "-sep");
            Append(sb, ListSeparator); // has a space, so it must be quoted
            foreach (var f in Fields)
                AppendRaw(sb, "-" + f.Tag);
            Append(sb, filePath);
            return sb.ToString();
        }

        /// <summary>
        /// The command line that writes every mapped field of <paramref name="metadata"/>
        /// back to <paramref name="filePath"/>. Empty values clear their tag. No
        /// <c>-overwrite_original</c>, so a <c>_original</c> backup is kept; <c>-P</c>
        /// preserves the file's modification date and the IPTC charset is set to UTF-8.
        /// </summary>
        public static string BuildWriteArguments(string filePath, ImageMetadata metadata)
        {
            RequirePath(filePath);
            if (metadata is null) throw new ArgumentNullException(nameof(metadata));

            var sb = new StringBuilder();
            AppendRaw(sb, "-charset", "filename=UTF8", "-charset", "iptc=UTF8", "-charset", "exif=UTF8");
            AppendRaw(sb, "-sep");
            Append(sb, ListSeparator); // has a space, so it must be quoted
            AppendRaw(sb, "-P");
            AppendRaw(sb, "-codedcharacterset=utf8");
            foreach (var f in Fields)
                Append(sb, "-" + f.Tag + "=" + GetValue(metadata, f.Name));
            Append(sb, filePath);
            return sb.ToString();
        }

        /// <summary>
        /// Maps the data row of ExifTool's CSV output onto an <see cref="ImageMetadata"/>.
        /// The row is the columns after the leading <c>SourceFile</c> column, i.e. one
        /// value per <see cref="Fields"/> entry, in order. Short rows (missing trailing
        /// columns) leave the remaining fields empty rather than throwing.
        /// </summary>
        public static ImageMetadata ReadValues(IReadOnlyList<string> dataColumnsAfterSourceFile)
        {
            var m = new ImageMetadata();
            if (dataColumnsAfterSourceFile is null) return m;
            for (var i = 0; i < Fields.Count && i < dataColumnsAfterSourceFile.Count; i++)
                SetValue(m, Fields[i].Name, dataColumnsAfterSourceFile[i] ?? "");
            return m;
        }

        /// <summary>Parses ExifTool's full CSV output into an <see cref="ImageMetadata"/>,
        /// or an all-empty instance when there is no data row.</summary>
        public static ImageMetadata ReadFromCsv(string csv)
        {
            var rows = Csv.Parse(csv);
            if (rows.Count < 2) return new ImageMetadata();

            // Row 0 is the header (SourceFile, then the requested tags). Row 1 is the
            // single file's data. Drop the SourceFile column and map the rest by index.
            var data = rows[1];
            var afterSourceFile = new List<string>(data.Count > 0 ? data.Count - 1 : 0);
            for (var i = 1; i < data.Count; i++) afterSourceFile.Add(data[i]);
            return ReadValues(afterSourceFile);
        }

        private static string GetValue(ImageMetadata m, string name)
        {
            switch (name)
            {
                case "Artist":      return m.Artist ?? "";
                case "Copyright":   return m.Copyright ?? "";
                case "Description": return m.Description ?? "";
                case "DateTaken":   return m.DateTaken ?? "";
                case "Title":       return m.Title ?? "";
                case "Caption":     return m.Caption ?? "";
                case "Keywords":    return m.Keywords ?? "";
                case "Creator":     return m.Creator ?? "";
                case "City":        return m.City ?? "";
                case "Country":     return m.Country ?? "";
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown field.");
            }
        }

        private static void SetValue(ImageMetadata m, string name, string value)
        {
            switch (name)
            {
                case "Artist":      m.Artist = value; break;
                case "Copyright":   m.Copyright = value; break;
                case "Description": m.Description = value; break;
                case "DateTaken":   m.DateTaken = value; break;
                case "Title":       m.Title = value; break;
                case "Caption":     m.Caption = value; break;
                case "Keywords":    m.Keywords = value; break;
                case "Creator":     m.Creator = value; break;
                case "City":        m.City = value; break;
                case "Country":     m.Country = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown field.");
            }
        }

        private static void RequirePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path is required.", nameof(filePath));
        }

        /// <summary>Appends one or more already-safe literal tokens (options with no
        /// user-supplied text), space-separated, without quoting.</summary>
        private static void AppendRaw(StringBuilder sb, params string[] tokens)
        {
            foreach (var t in tokens)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(t);
            }
        }

        /// <summary>Appends one token, Windows-quoting it if it contains spaces,
        /// quotes, or is empty, so it survives CommandLineToArgvW as a single arg.</summary>
        private static void Append(StringBuilder sb, string token)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(QuoteArgument(token));
        }

        /// <summary>
        /// Quotes a single argument per the Windows CommandLineToArgvW rules so an
        /// arbitrary value (spaces, embedded quotes, trailing backslashes, even an
        /// empty string) is passed to ExifTool intact. Newlines inside a quoted
        /// argument are preserved (only spaces/tabs delimit arguments), so multi-line
        /// captions round-trip.
        /// </summary>
        public static string QuoteArgument(string? arg)
        {
            arg = arg ?? "";

            // No quoting needed for a non-empty token free of whitespace and quotes.
            if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\r', '"' }) < 0)
                return arg;

            var sb = new StringBuilder();
            sb.Append('"');
            for (var i = 0; i < arg.Length; i++)
            {
                var backslashes = 0;
                while (i < arg.Length && arg[i] == '\\')
                {
                    backslashes++;
                    i++;
                }

                if (i == arg.Length)
                {
                    // Escape all trailing backslashes so they don't escape the
                    // closing quote.
                    sb.Append('\\', backslashes * 2);
                    break;
                }

                if (arg[i] == '"')
                {
                    // Escape the run of backslashes AND the quote.
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    // Backslashes not followed by a quote are literal.
                    sb.Append('\\', backslashes);
                    sb.Append(arg[i]);
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
