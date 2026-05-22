using System.Collections.Generic;
using System.Linq;

namespace TMM
{
    /// <summary>
    /// Represents a user-defined custom game configuration.
    /// Serialized to/from JSON in AppData.
    /// </summary>
    public class CustomGameProfile
    {
        public string GameName { get; set; } = "";
        public string GameDirectory { get; set; } = "";

        // Optional path to game exe, relative to GameDirectory (e.g. "hl2.exe")
        public string? ExePath { get; set; }

        // Comma-separated file types: ".rar, .zip, .7z"
        public string ModFileTypes { get; set; } = ".rar, .zip, .7z";

        // Mapping of filetype to output directory (relative to game root)
        // e.g., { ".asi": ".", ".ini": "config" }
        public Dictionary<string, string> OutputDirectories { get; set; } = new();

        /// <summary>Parses ModFileTypes string into a list of extensions (lowercase).</summary>
        public List<string> GetFileTypes() =>
            ModFileTypes
                .Split(',')
                .Select(ft => ft.Trim().ToLowerInvariant())
                .Where(ft => !string.IsNullOrEmpty(ft))
                .ToList();

        /// <summary>Gets output directory for a given file extension, or "." (root) as fallback.
        /// A "*" key in OutputDirectories acts as a catch-all for unmatched extensions.</summary>
        public string GetOutputDirectory(string fileExtension)
        {
            string lower = fileExtension.ToLowerInvariant();
            if (OutputDirectories.TryGetValue(lower, out var dir)) return dir;
            if (OutputDirectories.TryGetValue("*", out var wildcard)) return wildcard;
            return ".";
        }
    }
}
