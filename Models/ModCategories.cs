using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// The fixed-preset category taxonomy for mods. Each mod carries a single
    /// <see cref="ModItem.Category"/> drawn from a game's available set; that set
    /// defaults to <see cref="DefaultCategories"/> but a custom game can override it
    /// via the wizard (<see cref="CustomGameProfile.ModCategories"/>).
    ///
    /// Categories are organizational only — they drive the list colour spine and the
    /// filter chips, never routing or deployment.
    /// </summary>
    public static class ModCategories
    {
        /// <summary>The built-in preset shared by every game that doesn't define its own.</summary>
        public static readonly IReadOnlyList<string> DefaultCategories =
            new[] { "Gameplay", "Visual", "Audio", "Map", "Other" };

        // Stable colours for the built-in preset (dark-theme friendly, distinct hues).
        private static readonly Dictionary<string, Color> KnownColors =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Gameplay"] = Color.FromRgb(0x4C, 0xAF, 0x50), // green
                ["Visual"]   = Color.FromRgb(0x5B, 0x8D, 0xEF), // blue
                ["Audio"]    = Color.FromRgb(0xC7, 0x92, 0xEA), // purple
                ["Map"]      = Color.FromRgb(0xE0, 0xA4, 0x58), // amber
                ["Other"]    = Color.FromRgb(0x8A, 0x8F, 0x98), // grey
            };

        // Fallback palette for user-defined categories not in the known map. Indexed
        // by a stable hash of the category name so a given name always reads the same.
        private static readonly Color[] FallbackPalette =
        {
            Color.FromRgb(0xEF, 0x6F, 0x6F), // red
            Color.FromRgb(0x4F, 0xC3, 0xC9), // teal
            Color.FromRgb(0xF2, 0xC4, 0x4D), // yellow
            Color.FromRgb(0x9C, 0xCC, 0x65), // lime
            Color.FromRgb(0xCE, 0x6D, 0xB8), // magenta
            Color.FromRgb(0x7E, 0x9C, 0xF0), // periwinkle
        };

        /// <summary>Neutral spine colour for uncategorized mods.</summary>
        public static readonly Color UncategorizedColor = Color.FromRgb(0x3A, 0x3D, 0x44);

        /// <summary>
        /// Resolves the available category set for a game: the profile's own list if it
        /// defines one, otherwise the built-in <see cref="DefaultCategories"/>.
        /// </summary>
        public static IReadOnlyList<string> ForGame(CustomGameProfile? profile)
        {
            var custom = profile?.ModCategories;
            return custom is { Count: > 0 } ? custom : DefaultCategories;
        }

        /// <summary>
        /// The colour spine for a category. Known presets get fixed hues; unknown
        /// user-defined names get a stable colour from the fallback palette;
        /// null/empty (uncategorized) gets the neutral colour.
        /// </summary>
        public static Color ColorFor(string? category)
        {
            if (string.IsNullOrWhiteSpace(category)) return UncategorizedColor;
            if (KnownColors.TryGetValue(category, out var known)) return known;

            // Stable, non-negative index from the name so colours don't shuffle.
            int hash = 0;
            foreach (char c in category.Trim().ToLowerInvariant())
                hash = unchecked(hash * 31 + c);
            int index = (hash & 0x7FFFFFFF) % FallbackPalette.Length;
            return FallbackPalette[index];
        }

        /// <summary>A reusable brush for the category spine (frozen, so it's shareable).</summary>
        public static SolidColorBrush BrushFor(string? category)
        {
            var brush = new SolidColorBrush(ColorFor(category));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Normalizes a raw category string against the game's available set: trims it,
        /// matches case-insensitively to a known entry (returning that entry's casing),
        /// or returns null when blank or not in the set.
        /// </summary>
        public static string? Normalize(string? raw, IReadOnlyList<string> available)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string trimmed = raw.Trim();
            return available.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
        }
    }
}
