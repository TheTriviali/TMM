using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

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

        // Optional Steam AppId for verify/install shortcuts (e.g. "12210" for GTA IV)
        // If empty/null, Steam integration buttons are hidden/disabled
        public string? SteamAppId { get; set; }

        // Comma-separated file types: ".rar, .zip, .7z"
        public string ModFileTypes { get; set; } = ".rar, .zip, .7z";

        // Static mapping: filetype → output subdirectory (relative to game root)
        // e.g., { ".asi": ".", ".ini": "config" }
        public Dictionary<string, string> OutputDirectories { get; set; } = new();

        // Conditional routing rules evaluated at deploy-time.
        // These take priority over OutputDirectories for the same extension.
        // e.g. "put .asi in plugins\ if that folder exists, else in ."
        public List<ConditionalRoute> ConditionalRoutes { get; set; } = new();

        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Parses ModFileTypes string into a list of extensions (lowercase).</summary>
        public List<string> GetFileTypes() =>
            ModFileTypes
                .Split(',')
                .Select(ft => ft.Trim().ToLowerInvariant())
                .Where(ft => !string.IsNullOrEmpty(ft))
                .ToList();

        /// <summary>
        /// Gets output directory for a given file extension using only the static
        /// OutputDirectories map.  A "*" key acts as catch-all.  Falls back to "." (root).
        /// </summary>
        public string GetOutputDirectory(string fileExtension)
        {
            string lower = fileExtension.ToLowerInvariant();
            if (OutputDirectories.TryGetValue(lower, out var dir)) return dir;
            if (OutputDirectories.TryGetValue("*", out var wildcard)) return wildcard;
            return ".";
        }

        /// <summary>
        /// Gets the output directory for a file, applying ConditionalRoutes first
        /// (they can check whether a sub-folder actually exists), then falling back
        /// to the static OutputDirectories map.
        /// </summary>
        /// <param name="fileExtension">Extension of the file being deployed, e.g. ".asi".</param>
        /// <param name="gameDir">Absolute path to the game installation directory.</param>
        public string ResolveOutputDirectory(string fileExtension, string gameDir)
        {
            string lower = fileExtension.ToLowerInvariant();
            foreach (var cond in ConditionalRoutes)
            {
                if (!cond.Extension.Equals(lower, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                string checkPath = Path.Combine(gameDir, cond.CheckSubdir);
                return Directory.Exists(checkPath) ? cond.RouteIfExists : cond.RouteIfMissing;
            }
            return GetOutputDirectory(lower);
        }
    }
}
