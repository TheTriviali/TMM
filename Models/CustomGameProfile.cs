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

    }
}
