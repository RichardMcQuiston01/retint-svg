using System.Collections.Generic;
using System.Text;

namespace ImageTools.Core
{
    /// <summary>
    /// A minimal RFC 4180 CSV reader — just enough to parse ExifTool's <c>-csv</c>
    /// output, whose values can contain commas, embedded double quotes (doubled),
    /// and newlines (a multi-line caption) once quoted. Quote-aware so those never
    /// corrupt the column split, unlike a naive line/comma split.
    /// </summary>
    public static class Csv
    {
        /// <summary>
        /// Parses CSV text into rows of fields. Handles quoted fields, doubled
        /// quotes (<c>""</c> → <c>"</c>) inside them, and CR/LF or LF record
        /// separators (a newline inside quotes stays part of the field). A trailing
        /// newline does not produce a spurious empty row.
        /// </summary>
        public static IReadOnlyList<IReadOnlyList<string>> Parse(string? text)
        {
            var rows = new List<IReadOnlyList<string>>();
            if (string.IsNullOrEmpty(text)) return rows;

            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var sawAnyField = false; // did the current row have content worth emitting?

            void EndField()
            {
                row.Add(field.ToString());
                field.Clear();
                sawAnyField = true;
            }

            void EndRow()
            {
                EndField();
                rows.Add(row.ToArray());
                row = new List<string>();
                sawAnyField = false;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"'); // escaped quote
                            i++;
                        }
                        else
                        {
                            inQuotes = false; // closing quote
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        EndField();
                        break;
                    case '\r':
                        // Treat CRLF and lone CR as one record separator.
                        EndRow();
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        break;
                    case '\n':
                        EndRow();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            // Flush a final row that wasn't terminated by a newline. Only emit it if
            // there's actual pending content, so trailing newlines don't add a blank row.
            if (field.Length > 0 || row.Count > 0 || sawAnyField)
                EndRow();

            return rows;
        }
    }
}
