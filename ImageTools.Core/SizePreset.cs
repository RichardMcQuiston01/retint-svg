using System.Collections.Generic;

namespace ImageTools.Core
{
    /// <summary>
    /// A named resize option shown in the context menu. The raster analogue of
    /// SVGToolsShell's ColorPresets: edit <see cref="Defaults"/> (or the user's
    /// config) and every menu built from it updates.
    /// </summary>
    public sealed class SizePreset
    {
        public string Label { get; }
        public SizeSpec Spec { get; }

        public SizePreset(string label, SizeSpec spec)
        {
            Label = label;
            Spec = spec;
        }

        /// <summary>The built-in presets used when the user has no custom config.</summary>
        public static IReadOnlyList<SizePreset> Defaults { get; } = new[]
        {
            new SizePreset("25%",  SizeSpec.FromPercent(25)),
            new SizePreset("50%",  SizeSpec.FromPercent(50)),
            new SizePreset("75%",  SizeSpec.FromPercent(75)),
            new SizePreset("200%", SizeSpec.FromPercent(200)),
            new SizePreset("Longest edge 1024 px", SizeSpec.FromLongestEdge(1024)),
            new SizePreset("Longest edge 1920 px", SizeSpec.FromLongestEdge(1920)),
        };
    }
}
