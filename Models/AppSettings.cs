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

        // ── Colors ──────────────────────────────────────────────────────────
        public string AccentColor { get; set; } = "#0883FF";
        public string BgColor     { get; set; } = "#1C1C1E";
        public string ColorMode   { get; set; } = "Dark";

        // ── Titlebar ─────────────────────────────────────────────────────────
        public string TitlebarTheme { get; set; } = "Vanilla";

        // ── Typography ───────────────────────────────────────────────────────
        public string FontFamily { get; set; } = "Segoe UI Light";

        // ── Mica / Backdrop ──────────────────────────────────────────────────
        public bool MicaEnabled { get; set; } = false;

        // ── Accent border ────────────────────────────────────────────────────
        // When true, the window outer border uses AccentBrush instead of HeaderBrush.
        public bool AccentBorderEnabled { get; set; } = false;

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

        public string LastPresetName { get; set; } = "Dark Teal (Default)";

        // ── Multi-game (TMM) ───────────────────────────────────────────────────
        // Track the last selected game for quick restoration on app launch.
        public string? LastSelectedGameKey { get; set; } = null;
    }
}
