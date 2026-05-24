using System.Collections.Generic;

namespace TMM
{
    public class AppSettings
    {
        // Game paths: dynamically populated from GameRegistry.
        // Initially populated with built-in games; custom games added as created.
        public Dictionary<string, string?> GamePaths { get; set; } = new()
        {
            { "III", null }, { "VC", null }, { "SA", null }
        };

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

        // ── Window state ──────────────────────────────────────────────────────
        public double WindowLeft   { get; set; } = -1;
        public double WindowTop    { get; set; } = -1;
        public double WindowWidth  { get; set; } = 1280;
        public double WindowHeight { get; set; } = 672;

        // ── Per-game deploy overrides ─────────────────────────────────────────
        // When true for a game, deployment proceeds even if the exe is Vanilla (Steam build).
        // Toggled via right-click on the play buttons in the toolbar.
        public Dictionary<string, bool> DeployOverrides { get; set; } = new()
        {
            { "III", false }, { "VC", false }, { "SA", false }
        };

        // ── Multi-game (TMM) ───────────────────────────────────────────────────
        // Track the last selected game for quick restoration on app launch.
        public string? LastSelectedGameKey { get; set; } = null;

    }
}
