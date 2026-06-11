using System.Collections.Generic;
using System.Drawing;

namespace SVGToolsShell
{
    /// <summary>
    /// Defines the preset color options shown in all context menu flyouts.
    /// Add, remove, or reorder entries here to update every submenu at once.
    /// A null Hex value signals "Custom..." — the handler will open a ColorDialog.
    /// </summary>
    public static class ColorPresets
    {
        public static readonly IReadOnlyList<(string Label, string? Hex, Color Swatch)> All =
            new List<(string, string?, Color)>
            {
                ("Black",    "#000000", Color.Black),
                ("White",    "#FFFFFF", Color.White),
                ("Red",      "#FF0000", Color.Red),
                ("Green",    "#00AA00", Color.FromArgb(0, 170, 0)),
                ("Blue",     "#0000FF", Color.Blue),
                ("Yellow",   "#FFD700", Color.Gold),
                ("Orange",   "#FF8000", Color.Orange),
                ("Purple",   "#800080", Color.Purple),
                ("Gold",     "#EFBF04", Color.FromArgb(239, 191, 4)),
                ("Silver",   "#C0C0C0", Color.Silver),
                ("Custom…",  null,      Color.Transparent),
            };
    }
}
