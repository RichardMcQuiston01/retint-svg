using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ImageTools.Core
{
    /// <summary>
    /// Computes the proposed new names for a Power Rename pass (search/replace,
    /// regex or literal, with a counter token). Pure and UI-agnostic: it works on
    /// bare file names and returns proposals; the caller (dialog) shows the preview
    /// and performs the actual file moves with collision handling.
    /// </summary>
    public static class RenameEngine
    {
        private const string CounterToken = "${n}";

        /// <summary>
        /// Plans the rename for each name in <paramref name="fileNames"/> (order
        /// preserved). The counter starts at <see cref="RenameOptions.CounterStart"/>
        /// and advances only for files that actually change. Throws
        /// <see cref="ArgumentException"/> if regex mode is on and the pattern is
        /// invalid.
        /// </summary>
        public static IReadOnlyList<RenameResult> Plan(IReadOnlyList<string> fileNames, RenameOptions options)
        {
            if (fileNames is null) throw new ArgumentNullException(nameof(fileNames));
            if (options is null) throw new ArgumentNullException(nameof(options));

            Regex? regex = null;
            if (options.UseRegex && options.Search.Length > 0)
            {
                try
                {
                    var opts = options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    regex = new Regex(options.Search, opts);
                }
                catch (ArgumentException ex)
                {
                    throw new ArgumentException($"Invalid regular expression: {ex.Message}", ex);
                }
            }

            var results = new List<RenameResult>(fileNames.Count);
            var counter = options.CounterStart;

            foreach (var name in fileNames)
            {
                var replacement = ExpandCounter(options.Replace, counter, options.CounterPadding);
                var proposed = Transform(name, replacement, options, regex);
                var changed = !string.Equals(proposed, name, StringComparison.Ordinal);
                if (changed) counter++;
                results.Add(new RenameResult(name, proposed, changed));
            }

            return results;
        }

        private static string Transform(string name, string replacement, RenameOptions o, Regex? regex)
        {
            switch (o.Target)
            {
                case RenameTarget.ExtensionOnly:
                {
                    var stem = Path.GetFileNameWithoutExtension(name);
                    var ext = TrimDot(Path.GetExtension(name));
                    var newExt = Apply(ext, replacement, o, regex);
                    return newExt.Length > 0 ? stem + "." + newExt : stem;
                }
                case RenameTarget.NameAndExtension:
                    return Apply(name, replacement, o, regex);

                default: // NameOnly
                {
                    var stem = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name); // includes dot or empty
                    return Apply(stem, replacement, o, regex) + ext;
                }
            }
        }

        private static string Apply(string input, string replacement, RenameOptions o, Regex? regex)
        {
            if (o.Search.Length == 0) return input; // nothing to find

            if (o.UseRegex)
            {
                if (regex is null) return input;
                return o.MatchAllOccurrences
                    ? regex.Replace(input, replacement)
                    : regex.Replace(input, replacement, 1);
            }

            return LiteralReplace(input, o.Search, replacement, o.CaseSensitive, o.MatchAllOccurrences);
        }

        private static string LiteralReplace(string input, string search, string replacement, bool caseSensitive, bool all)
        {
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var sb = new System.Text.StringBuilder();
            var start = 0;
            while (true)
            {
                var idx = input.IndexOf(search, start, comparison);
                if (idx < 0)
                {
                    sb.Append(input, start, input.Length - start);
                    break;
                }
                sb.Append(input, start, idx - start);
                sb.Append(replacement);
                start = idx + search.Length;
                if (!all)
                {
                    sb.Append(input, start, input.Length - start);
                    break;
                }
            }
            return sb.ToString();
        }

        private static string ExpandCounter(string template, int counter, int padding)
        {
            if (template.IndexOf(CounterToken, StringComparison.Ordinal) < 0)
                return template;

            var number = counter.ToString(CultureInfo.InvariantCulture);
            if (padding > 0) number = number.PadLeft(padding, '0');
            return template.Replace(CounterToken, number);
        }

        private static string TrimDot(string ext) =>
            ext.StartsWith(".", StringComparison.Ordinal) ? ext.Substring(1) : ext;
    }
}
