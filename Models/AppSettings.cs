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

        // ── Theme state ──────────────────────────────────────────────────────
        // -- Toolbar
        public bool ToolbarShowLabels { get; set; } = true;

        public string LastPresetName { get; set; } = "macOS Dark";
    }
}
