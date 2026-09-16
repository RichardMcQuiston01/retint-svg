namespace ImageTools.Core
{
    /// <summary>Which part of the filename a rename applies to.</summary>
    public enum RenameTarget
    {
        /// <summary>The name without its extension (default).</summary>
        NameOnly,
        /// <summary>Only the extension (without the leading dot).</summary>
        ExtensionOnly,
        /// <summary>The whole filename including the extension.</summary>
        NameAndExtension,
    }

    /// <summary>
    /// Options for a Power Rename pass — a search/replace over a set of filenames,
    /// PowerToys-style. Pure data; the matching logic lives in
    /// <see cref="RenameEngine"/> so it is unit-testable without any UI.
    /// </summary>
    public sealed class RenameOptions
    {
        /// <summary>Text (or regex pattern) to find. Empty means "no change".</summary>
        public string Search { get; set; } = "";

        /// <summary>
        /// Replacement text. May contain the counter token <c>${n}</c>, which
        /// expands to an incrementing number (see <see cref="CounterStart"/> /
        /// <see cref="CounterPadding"/>). In regex mode, standard substitutions
        /// like <c>$1</c> also apply.
        /// </summary>
        public string Replace { get; set; } = "";

        /// <summary>Treat <see cref="Search"/> as a .NET regular expression.</summary>
        public bool UseRegex { get; set; }

        /// <summary>Case-sensitive matching (default: case-insensitive).</summary>
        public bool CaseSensitive { get; set; }

        /// <summary>Replace every occurrence (default) or just the first.</summary>
        public bool MatchAllOccurrences { get; set; } = true;

        /// <summary>Which part of the filename to transform.</summary>
        public RenameTarget Target { get; set; } = RenameTarget.NameOnly;

        /// <summary>First value substituted for the <c>${n}</c> counter token.</summary>
        public int CounterStart { get; set; } = 1;

        /// <summary>Zero-pad the counter to this many digits (0 = no padding).</summary>
        public int CounterPadding { get; set; }
    }

    /// <summary>The planned rename for one file: its original and proposed names.</summary>
    public sealed class RenameResult
    {
        public string Original { get; }
        public string Proposed { get; }

        /// <summary>True when <see cref="Proposed"/> differs from <see cref="Original"/>.</summary>
        public bool Changed { get; }

        public RenameResult(string original, string proposed, bool changed)
        {
            Original = original;
            Proposed = proposed;
            Changed = changed;
        }
    }
}
