using System.Collections.Generic;

namespace TMM
{
    public class AppSettings
    {
        // Game paths: populated lazily by BackendCore.LoadSettings (built-ins) and
        // GameRegistry sync in InitializeAsync (custom games).
        public Dictionary<string, string?> GamePaths { get; set; } = new();

        // Track which custom games exist (for cleanup and initialization)
        public List<string> CustomGameKeys { get; set; } = new();

        public bool FirstLaunch { get; set; } = true;

        // ── Accent Colors (2-tone) ──────────────────────────────────────────
        // Primary accent color (main UI highlights, nav icons, etc.)
        public string AccentColor { get; set; } = "#0883FF";
        // Secondary accent color (for gradients, optional decorative use)
        public string AccentColor2 { get; set; } = "#00D9FF";
        // Active preset name (e.g. "Blue-Cyan", "Purple-Pink")
        public string ActiveAccentPreset { get; set; } = "Blue-Cyan";

        // ── Library state (Design-required) ──────────────────────────────────────────

        /// <summary>
        /// Keys of games the user has archived (hidden from main grid by default).
        /// Persisted across sessions. Users can unarchive from the archive chip panel.
        /// </summary>
        public List<string> ArchivedGameKeys { get; set; } = new();

        /// <summary>
        /// Key of the game set as "default" (highlighted in library, used for ModManager
        /// shortcut nav item). E.g. "GTA_III_SERIES". Null = no default set.
        /// </summary>
        public string? DefaultGameKey { get; set; }

        /// <summary>
        /// Library view mode. One of: "grid" | "list" | "showcase".
        /// Default is "grid".
        /// </summary>
        public string LibraryViewMode { get; set; } = "grid";

        /// <summary>
        /// User-defined display order for library cards. List of game keys in order.
        /// Cards not in this list appear after the listed ones.
        /// </summary>
        public List<string> GameOrder { get; set; } = new();

        // ── Window state ──────────────────────────────────────────────────────
        public double WindowLeft   { get; set; } = -1;
        public double WindowTop    { get; set; } = -1;
        public double WindowWidth  { get; set; } = 1100;
        public double WindowHeight { get; set; } = 720;

        // ── Localization ────────────────────────────────────────────────────────
        // Current language code (e.g., "en-US", "es-ES"). Loaded by LocalizationService at startup.
        public string CurrentLanguage { get; set; } = "en-US";

        // ── Recent activity feed ────────────────────────────────────────────────
        /// <summary>Rolling log of recent user actions (capped at 20 entries, newest first).</summary>
        public List<ActivityEntry> RecentActivity { get; set; } = new();

        // ── Backup quota ────────────────────────────────────────────────────────
        /// <summary>Warn user when total backup folder size exceeds this many bytes (default 5 GB).</summary>
        public long BackupSizeWarnBytes { get; set; } = 5L * 1024 * 1024 * 1024;
    }
}
