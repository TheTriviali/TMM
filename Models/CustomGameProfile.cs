using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// Represents a user-defined custom game configuration.
    /// Serialized to/from JSON in AppData/CustomGames/.
    /// </summary>
    public class CustomGameProfile
    {
        public string GameName { get; set; } = "";
        public string GameDirectory { get; set; } = "";
        public string? ExePath { get; set; }
        public string? SteamAppId { get; set; }

        /// <summary>
        /// Ordered list of routing rules (first match wins).
        /// Replaces the old ModFileTypes + OutputDirectories + ConditionalRoutes trio.
        /// </summary>
        public List<RoutingRule> RoutingRules { get; set; } = new();

        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }

        /// <summary>Library card gradient start color, hex e.g. "#1B3A1B". Null = use theme default.</summary>
        public string? GradientStartHex { get; set; }

        /// <summary>Library card gradient end color, hex e.g. "#0C1E0C". Null = use theme default.</summary>
        public string? GradientEndHex { get; set; }

        /// <summary>Maturity/release status shown as a chip on the library card.</summary>
        public ReleaseStatus LibraryStatus { get; set; } = ReleaseStatus.Release;

        /// <summary>
        /// Optional custom artwork filename (basename only, e.g. "my_art.png").
        /// Full path resolved by BackendCore.GetLibraryArtPath(gameKey).
        /// Null = use gradient banner.
        /// </summary>
        public string? CustomArtFileName { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }

        // ── File acceptance ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if this game config accepts the given file extension for install.
        /// Archives (.zip/.rar/.7z) are always accepted.
        /// A "*" / "any" catch-all rule accepts all extensions.
        /// If no rules are defined, all files are accepted (open policy).
        /// </summary>
        public bool AcceptsFileType(string ext)
        {
            string lower = ext.ToLowerInvariant();
            if (lower is ".zip" or ".rar" or ".7z") return true;
            if (RoutingRules.Count == 0) return true;
            foreach (var rule in RoutingRules)
            {
                if (rule.ExtensionPattern is "*" or ".*" or "any") return true;
                if (rule.ExtensionPattern.Equals(lower, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// Returns distinct accepted extensions for the file-open dialog filter.
        /// Returns empty list when a catch-all rule exists (means "all files").
        /// Always includes archives.
        /// </summary>
        public List<string> GetFileTypes()
        {
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".zip", ".rar", ".7z" };
            foreach (var rule in RoutingRules)
            {
                if (rule.ExtensionPattern is "*" or ".*" or "any")
                    return new List<string>(); // catch-all → show "All Files" in dialog
                if (!string.IsNullOrWhiteSpace(rule.ExtensionPattern) &&
                    rule.ExtensionPattern.StartsWith('.'))
                    results.Add(rule.ExtensionPattern.ToLowerInvariant());
            }
            return results.ToList();
        }

        // ── Routing resolution ────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the output sub-directory for a file being deployed.
        /// Rules are evaluated in order; first match wins.
        /// </summary>
        /// <param name="fileExtension">Lowercase extension of the file, e.g. ".dll"</param>
        /// <param name="fileName">Filename only (no directory), for NameContains matching.</param>
        /// <param name="gameDir">Absolute game directory path, for IF condition checking.</param>
        public string ResolveOutputDirectory(string fileExtension, string fileName, string gameDir)
        {
            string lowerExt = fileExtension.ToLowerInvariant();
            foreach (var rule in RoutingRules)
            {
                if (!MatchesExtension(rule.ExtensionPattern, lowerExt)) continue;
                if (!string.IsNullOrWhiteSpace(rule.NameContains) &&
                    !fileName.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase)) continue;

                if (rule.HasCondition)
                {
                    bool exists = !string.IsNullOrEmpty(gameDir) &&
                                  Directory.Exists(Path.Combine(gameDir, rule.CheckSubdir!));
                    return exists ? rule.Destination : (rule.FallbackDestination ?? ".");
                }
                return rule.Destination;
            }
            return ".";
        }

        private static bool MatchesExtension(string pattern, string ext)
        {
            if (pattern is "*" or ".*" or "any") return true;
            return pattern.Equals(ext, StringComparison.OrdinalIgnoreCase);
        }
    }
}
