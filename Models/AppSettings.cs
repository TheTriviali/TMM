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
        /// Key of the game set as "active" (pinned to first in library, used for ModManager
        /// shortcut nav on relaunch). E.g. "GTA_III_SERIES". Null = no active game set.
        /// </summary>
        public string? ActiveGameKey { get; set; }

        /// <summary>
        /// Library view mode. One of: "home" | "list".
        /// Default is "home". Legacy "grid"/"showcase" values migrate to "home" on launch.
        /// </summary>
        public string LibraryViewMode { get; set; } = "home";

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

        // ── Home stats cache ──────────────────────────────────────────────────────
        /// <summary>
        /// Cached total size (bytes) of all installed mods across every ModsRaw_* folder.
        /// Recomputed off the render path (on init + after install/deploy) so the Home
        /// stats strip can show a size without walking disk on every render. See
        /// BackendCore.RecomputeModsInstalledSizeAsync.
        /// </summary>
        public long CachedModsInstalledBytes { get; set; } = 0;

        // ── Notifications ─────────────────────────────────────────────────────────
        /// <summary>
        /// When true, low-level/verbose operations (folder creation, plan freeze, baseline
        /// capture, backup prune, import steps, etc.) raise a toast in addition to being
        /// recorded in the notification history. When false (default) those operations are
        /// still recorded to history but stay silent. See NotificationService.ShowVerbose.
        /// </summary>
        public bool VerboseNotifications { get; set; } = false;

        /// <summary>
        /// Set to true the first time the built-in WebView2 download interceptor successfully
        /// saves a mod archive. Gates visibility of the Downloads drawer in ModManagerPage so
        /// users who never use built-in downloads see no new UI.
        /// </summary>
        public bool HasUsedBuiltInDownloads { get; set; } = false;

        // ── Per-game card color overrides ─────────────────────────────────────────
        /// <summary>
        /// User-chosen library card gradient per game key. Value is "startHex|endHex"
        /// (e.g. "#B5179E|#3A0CA3"). When present it overrides the gradient that ships
        /// with the game's profile, for both built-in and custom games. Set via the card's
        /// "Set card color" menu; cleared by "Reset color".
        /// </summary>
        public Dictionary<string, string> CardColorOverrides { get; set; } = new();
    }
}
