using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>Release/maturity tag for a game configuration.</summary>
    public enum ReleaseTag
    {
        /// <summary>Production-ready release.</summary>
        Release,

        /// <summary>Pre-release with known issues being addressed.</summary>
        Beta,

        /// <summary>Early release; stability not guaranteed.</summary>
        Alpha,

        /// <summary>User-defined custom tag.</summary>
        Custom,
    }

    /// <summary>Robustness/maturity level of a game configuration.</summary>
    public enum RobustnessLevel
    {
        /// <summary>Early stage, frequent changes expected.</summary>
        Experimental,

        /// <summary>Tested and reliable for most use cases.</summary>
        Stable,

        /// <summary>Production-ready, rarely changes.</summary>
        Mature,
    }

    /// <summary>
    /// Represents a user-defined or built-in game configuration.
    /// Serialized to/from JSON in AppData/CustomGames/ or bundled as .tmmgame profiles.
    /// All games (built-in and custom) are treated identically through this structure.
    /// </summary>
    public class CustomGameProfile
    {
        public string GameName { get; set; } = "";
        /// <summary>Abbreviated display name for game cards (≤10 chars). Derived from GameName if null.</summary>
        public string? ShortName { get; set; }
        public string GameDirectory { get; set; } = "";
        public string? ExePath { get; set; }
        public string? SteamAppId { get; set; }

        /// <summary>
        /// Ordered list of mod types supported by this game (e.g., "ASI Plugin", "CLEO Script").
        /// Each mod type has associated file extensions and routing rules.
        /// </summary>
        public List<ModType> ModTypes { get; set; } = new();

        /// <summary>
        /// Ordered list of routing rules (first match wins).
        /// Game-wide rules that apply across all mod types.
        /// Type-specific rules are evaluated first, then game-wide rules.
        /// </summary>
        public List<RoutingRule> RoutingRules { get; set; } = new();

        /// <summary>
        /// Top-level folder names that should be treated as overlays when found in a mod root.
        /// Files beneath those folders preserve their relative path on deploy.
        /// </summary>
        public List<string> OverlayFolders { get; set; } = new();

        /// <summary>
        /// Known sibling folders that should be scanned for companion files during import.
        /// Key = folder name that owns the main file type; value = allowed companion folders.
        /// </summary>
        public Dictionary<string, List<string>> CompanionSiblings { get; set; } = new();

        /// <summary>
        /// Default install locations to probe during Quick Scan, relative to each fixed drive
        /// root (e.g. "Steam/steamapps/common/My Game"). Lets a shared profile auto-detect the
        /// game on another person's system. The folder that contains (or whose immediate
        /// subfolder contains) the executable named by <see cref="ExePath"/> is treated as a match.
        /// </summary>
        public List<string> SearchHints { get; set; } = new();

        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }

        /// <summary>Game configuration version (semantic versioning, e.g., 1.0.0).</summary>
        public System.Version? Version { get; set; }

        /// <summary>Release/maturity tag for this game configuration.</summary>
        public ReleaseTag ReleaseTag { get; set; } = ReleaseTag.Release;

        /// <summary>Custom tag when ReleaseTag is set to Custom (e.g., "+gta3-optimized").</summary>
        public string? CustomTag { get; set; }

        /// <summary>Robustness level indicating how stable and tested this configuration is.</summary>
        public RobustnessLevel Robustness { get; set; } = RobustnessLevel.Stable;

        /// <summary>True if this is a built-in game profile shipped with TMM; false if user-created.</summary>
        public bool IsNative { get; set; } = false;

        /// <summary>
        /// Optional expected file size of the game executable, in bytes.
        /// Null = no size check performed. Cheap to verify (no hashing).
        /// </summary>
        public long? ExpectedExeBytes { get; set; }

        /// <summary>
        /// Accepted MD5 hashes (lowercase hex) for the game executable.
        /// Empty = no hash check. Multiple entries support downgrader variants
        /// (a single profile can validate several known-good binaries).
        /// </summary>
        public List<string> AcceptedExeMd5s { get; set; } = new();

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

        /// <summary>
        /// NexusMods game slug (e.g. "grandtheftauto3").
        /// When set, the Mod Manager sidebar links directly to https://www.nexusmods.com/{NexusSlug}/mods.
        /// Null = sidebar falls back to a DuckDuckGo search for "{GameName} mods".
        /// </summary>
        public string? NexusSlug { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }

    }
}
