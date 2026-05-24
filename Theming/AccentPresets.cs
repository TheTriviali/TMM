using System.Collections.Generic;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Defines 2-tone accent color presets for the UI.
    /// Each preset has a primary and secondary accent; UI generates gradients from these.
    /// </summary>
    public static class AccentPresets
    {
        public record AccentPreset(string Name, string PrimaryHex, string SecondaryHex)
        {
            public Color Primary   => (Color)ColorConverter.ConvertFromString(PrimaryHex);
            public Color Secondary => (Color)ColorConverter.ConvertFromString(SecondaryHex);
        }

        public static readonly List<AccentPreset> All = new()
        {
            // Cool tones
            new("Blue-Cyan",       "#0883FF", "#00D9FF"),
            new("Deep Blue",       "#0066CC", "#0099FF"),
            new("Slate-Blue",      "#4A7BA7", "#7BB8D4"),
            new("Teal-Green",      "#17A2B8", "#20C997"),

            // Warm tones
            new("Orange-Gold",     "#FF7F00", "#FFB84D"),
            new("Coral-Pink",      "#FF6B6B", "#FF9A9E"),
            new("Purple-Pink",     "#9B59B6", "#E74C3C"),
            new("Magenta-Purple",  "#E91E63", "#9C27B0"),
        };

        /// <summary>Get preset by name, or return Blue-Cyan (default).</summary>
        public static AccentPreset GetByName(string name)
        {
            return All.Find(p => p.Name == name) ?? All[0];
        }
    }
}
