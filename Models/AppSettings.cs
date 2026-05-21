// TABLE OF CONTENTS
// ─────────────────────────────────────────────────────────────────
//   AppSettings CLASS
//     Game paths (per-key) ........................................ ~7
//     Launch & debug flags ........................................ ~12
//     Colors (accent, background, mode, text-contrast) ........... ~15
//     Titlebar (theme, alignment, personalize, opacity) ........... ~21
//     Typography .................................................. ~27
//     Mica / backdrop ............................................. ~30
//     Toolbar (labels toggle) ..................................... ~34
//     Deploy overrides (per-game force-deploy flags) .............. ~38
//     Theme preset state .......................................... ~43
// ─────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace TGTAMM
{
    public class AppSettings
    {
        public Dictionary<string, string?> GamePaths { get; set; } = new()
        {
            { "III", null }, { "VC", null }, { "SA", null }
        };

        public bool FirstLaunch   { get; set; } = true;
        public bool DebugStaging  { get; set; } = false;

        // ── Colors ──────────────────────────────────────────────────────────
        public string AccentColor   { get; set; } = "#0883FF";
        public string BgColor       { get; set; } = "#1C1C1E";
        public string ColorMode     { get; set; } = "Dark";
        public string TextColorMode { get; set; } = "WCAG";

        // ── Titlebar ─────────────────────────────────────────────────────────
        public string TitlebarTheme     { get; set; } = "Compact";
        public string TitlebarAlignment { get; set; } = "Left";
        public bool   TitlebarPersonalize { get; set; } = true;
        public double TitlebarOpacity   { get; set; } = 0.88;

        // ── Typography ───────────────────────────────────────────────────────
        public string FontFamily { get; set; } = "Segoe UI Light";

        // ── Mica / Backdrop ──────────────────────────────────────────────────
        public bool   MicaEnabled   { get; set; } = false;
        public double MicaIntensity { get; set; } = 0.55;

        // ── Toolbar ──────────────────────────────────────────────────────────
        public bool ToolbarShowLabels { get; set; } = true;

        // ── Accent border ────────────────────────────────────────────────────
        // When true, the window outer border uses AccentBrush instead of HeaderBrush.
        public bool AccentBorderEnabled { get; set; } = false;

        // ── Per-game deploy overrides ─────────────────────────────────────────
        // When true for a game, deployment proceeds even if the exe is Vanilla
        // (Steam build). Toggled via right-click on the play buttons in the toolbar.
        public Dictionary<string, bool> DeployOverrides { get; set; } = new()
        {
            { "III", false }, { "VC", false }, { "SA", false }
        };

        public string LastPresetName { get; set; } = "macOS Dark";
    }
}
