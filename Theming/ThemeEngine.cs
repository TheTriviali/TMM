// TABLE OF CONTENTS
// ─────────────────────────────────────────────────────────────────
//   DWM P/INVOKE  (DwmSetWindowAttribute) ........................ ~13
//   PUBLIC ENTRY POINTS
//     ApplyTheme()  — updates all dynamic brushes ................ ~18
//       · Accent + AccentTextBrush + AccentLabelBrush ............. ~30
//       · TextBrush + SubTextBrush ................................ ~39
//       · BgBrush (with Mica alpha) ............................... ~60
//       · PanelBrush + HeaderBrush + ControlBgBrush ............... ~64
//       · Titlebar brushes (Win8, macOS, Aero7, Win9x) ........... ~137
//     ApplyFont() ................................................. ~189
//     TryApplyMica()  — DWM Mica/Acrylic backdrop ............... ~199
//   COMPLEMENTARY COLOUR GENERATION
//     GetComplementPalette()  (Complementary/Triadic/...) ........ ~241
//     SuggestAccentForBg()  — WCAG-optimised hue sweep ........... ~272
//   CONTRAST COLOUR ALGORITHMS
//     GetContrastColor()  (WCAG | YIQ | Invert) ................. ~312
//   HSV HELPERS  (public — shared with ThemeManagerWindow)
//     HsvToRgb() / RgbToHsv() ................................... ~337
//   INTERNAL HELPERS
//     RelativeLuminance() / BlendColors() ........................ ~368
// ─────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace TGTAMM
{
    public static class ThemeEngine
    {
        // ── DWM P/Invoke ─────────────────────────────────────────────────────
        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int pvAttr, int cbAttr);

        // ── Public entry points ───────────────────────────────────────────────

        public static void ApplyTheme(AppSettings settings)
        {
            if (Application.Current == null) return;

            try
            {
                var accent = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
                var bg     = (Color)ColorConverter.ConvertFromString(settings.BgColor);
                bool isDark    = settings.ColorMode == "Dark";
                bool mica      = settings.MicaEnabled;
                double micaAmt = 0.42; // fixed moderate intensity — not user-adjustable

                // ── Accent ────────────────────────────────────────────────────
                Application.Current.Resources["AccentBrush"] = new SolidColorBrush(accent);

                // ── Smart text color for accent ───────────────────────────────
                // Calculate text color that contrasts with the accent for buttons/highlights
                Color accentTextColor = GetContrastColor(accent, settings.TextColorMode);
                Application.Current.Resources["AccentTextBrush"] = new SolidColorBrush(accentTextColor);
                Application.Current.Resources["HighlightTextBrush"] = new SolidColorBrush(accentTextColor);

                // ── Smart text color ─────────────────────────────────────────
                Color textColor    = GetContrastColor(bg, settings.TextColorMode);
                Color subTextColor = BlendColors(textColor, bg, isDark ? 0.50 : 0.42);
                Application.Current.Resources["TextBrush"]    = new SolidColorBrush(textColor);
                Application.Current.Resources["SubTextBrush"] = new SolidColorBrush(subTextColor);

                // ── Accent label color ────────────────────────────────────────
                // A readable version of accent that maintains hue but ensures contrast with panel background
                // Convert to HSV, reduce saturation/value for better readability, then convert back
                var (h, s, v) = RgbToHsv(accent);
                // Reduce saturation and value to make it readable on dark/light backgrounds
                // while maintaining the hue for visual identity
                double newSat = Math.Min(s * 0.65, 0.8);  // Desaturate to max 80%
                double newVal = isDark
                    ? Math.Max(Math.Min(v * 1.15, 1.0), 0.65)  // always at least 65% bright on dark bg
                    : Math.Min(Math.Max(v * 0.70, 0.0), 0.45); // always at most 45% bright on light bg
                Color accentLabelColor = HsvToRgb(h, newSat, newVal);
                Application.Current.Resources["AccentLabelBrush"] = new SolidColorBrush(accentLabelColor);

                // ── Background ───────────────────────────────────────────────
                // When Mica is enabled, make the main bg semi-transparent so the
                // DWM backdrop (Mica/Acrylic) bleeds through the window border.
                byte bgAlpha = mica ? (byte)Math.Round(255 * (0.55 - micaAmt * 0.40)) : (byte)255;
                Application.Current.Resources["BgBrush"] =
                    new SolidColorBrush(Color.FromArgb(bgAlpha, bg.R, bg.G, bg.B));

                // ── Panels ────────────────────────────────────────────────────
                if (isDark)
                {
                    if (mica)
                    {
                        // Strong Mica: highly translucent panels, subtle accent tint
                        // micaAmt drives opacity — higher = more see-through
                        byte pa = (byte)Math.Round(255 * (0.50 - micaAmt * 0.42));
                        byte pr = (byte)Math.Round(bg.R * 0.92 + accent.R * 0.08);
                        byte pg = (byte)Math.Round(bg.G * 0.92 + accent.G * 0.08);
                        byte pb = (byte)Math.Round(bg.B * 0.92 + accent.B * 0.08);
                        Application.Current.Resources["PanelBrush"] =
                            new SolidColorBrush(Color.FromArgb(pa, pr, pg, pb));

                        byte ha = (byte)Math.Round(255 * (0.45 - micaAmt * 0.38));
                        byte hr = (byte)Math.Round(28 + accent.R * 0.06);
                        byte hg = (byte)Math.Round(29 + accent.G * 0.06);
                        byte hb = (byte)Math.Round(33 + accent.B * 0.06);
                        Application.Current.Resources["HeaderBrush"] =
                            new SolidColorBrush(Color.FromArgb(ha,
                                Math.Min(hr, (byte)255), Math.Min(hg, (byte)255), Math.Min(hb, (byte)255)));
                    }
                    else
                    {
                        // Crisp dark panels with subtle elevation via alpha
                        byte elev = (byte)Math.Round(Math.Min(255.0, bg.R + 14));
                        Application.Current.Resources["PanelBrush"] =
                            new SolidColorBrush(Color.FromArgb(230, elev, elev,
                                (byte)Math.Round(Math.Min(255.0, bg.B + 16))));
                        Application.Current.Resources["HeaderBrush"] =
                            new SolidColorBrush(Color.FromArgb(255,
                                (byte)Math.Round(Math.Min(255.0, bg.R + 20)),
                                (byte)Math.Round(Math.Min(255.0, bg.G + 20)),
                                (byte)Math.Round(Math.Min(255.0, bg.B + 24))));
                    }

                    Application.Current.Resources["ControlBgBrush"] =
                        new SolidColorBrush(Color.FromArgb(mica ? (byte)160 : (byte)255,
                            (byte)Math.Round(Math.Min(255.0, bg.R + 28)),
                            (byte)Math.Round(Math.Min(255.0, bg.G + 28)),
                            (byte)Math.Round(Math.Min(255.0, bg.B + 32))));
                    Application.Current.Resources["CheckeredRowBrush"] =
                        new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
                }
                else
                {
                    if (mica)
                    {
                        byte pa = (byte)Math.Round(255 * (0.55 - micaAmt * 0.45));
                        Application.Current.Resources["PanelBrush"] =
                            new SolidColorBrush(Color.FromArgb(pa, 242, 244, 250));

                        byte ha = (byte)Math.Round(255 * (0.50 - micaAmt * 0.40));
                        Application.Current.Resources["HeaderBrush"] =
                            new SolidColorBrush(Color.FromArgb(ha, 215, 220, 232));
                    }
                    else
                    {
                        Application.Current.Resources["PanelBrush"] =
                            new SolidColorBrush(Color.FromArgb(245, 242, 244, 250));
                        Application.Current.Resources["HeaderBrush"] =
                            new SolidColorBrush(Color.FromRgb(215, 220, 232));
                    }

                    Application.Current.Resources["ControlBgBrush"] =
                        new SolidColorBrush(Color.FromRgb(225, 229, 240));
                    Application.Current.Resources["CheckeredRowBrush"] =
                        new SolidColorBrush(Color.FromArgb(38, 0, 0, 0));
                    Application.Current.Resources["SubTextBrush"] =
                        new SolidColorBrush(Color.FromRgb(72, 82, 100));
                }

                // ── Titlebar brushes ──────────────────────────────────────────
                if (settings.TitlebarPersonalize)
                {
                    // Win8: accent-tinted, raised opacity for visibility
                    Application.Current.Resources["Win8TitleBrush"] =
                        new SolidColorBrush(Color.FromArgb(200, accent.R, accent.G, accent.B));
                    // macOS Dark: very deep accent shadow
                    Application.Current.Resources["MacTitleBrush"] =
                        new SolidColorBrush(Color.FromArgb(238,
                            (byte)Math.Round(accent.R * 0.10),
                            (byte)Math.Round(accent.G * 0.10),
                            (byte)Math.Round(accent.B * 0.10)));
                }
                else
                {
                    Application.Current.Resources["Win8TitleBrush"] =
                        new SolidColorBrush(isDark
                            ? Color.FromArgb(190, 22, 22, 24)
                            : Color.FromArgb(190, 215, 220, 232));
                    Application.Current.Resources["MacTitleBrush"] =
                        new SolidColorBrush(isDark
                            ? Color.FromArgb(238, 26, 26, 28)
                            : Color.FromArgb(238, 238, 238, 242));
                }

                // Win7 Aero — gradient-like frosted glass. If personalize is on,
                // tint with accent at low saturation for a subtle coloured glass look.
                if (settings.TitlebarPersonalize)
                {
                    byte ar = (byte)Math.Round(accent.R * 0.25 + 100 * 0.75);
                    byte ag = (byte)Math.Round(accent.G * 0.25 + 160 * 0.75);
                    byte ab = (byte)Math.Round(accent.B * 0.25 + 200 * 0.75);
                    Application.Current.Resources["Aero7TitleBrush"] =
                        new SolidColorBrush(Color.FromArgb(90, ar, ag, ab));
                }
                else
                {
                    Application.Current.Resources["Aero7TitleBrush"] =
                        new SolidColorBrush(Color.FromArgb(82, 120, 190, 240));
                }

                // macOS Light
                Application.Current.Resources["MacLightTitleBrush"] =
                    new SolidColorBrush(Color.FromArgb(255, 236, 236, 238));

                // Win9x
                Application.Current.Resources["Win9xTitleStartColor"] = Color.FromRgb(10, 36, 106);
                Application.Current.Resources["Win9xTitleEndColor"]   = Color.FromRgb(166, 202, 240);
            }
            catch { /* invalid hex — keep previous theme */ }
        }

        public static void ApplyFont(Window window, AppSettings settings)
        {
            try { window.FontFamily = new FontFamily(settings.FontFamily); }
            catch { }
        }

        /// <summary>
        /// Applies or removes DWM Mica/Acrylic backdrop.
        /// Must be called both when enabling AND disabling so DWM state stays in sync.
        /// </summary>
        public static void TryApplyMica(Window window, bool enable)
        {
            IntPtr hwnd;
            try { hwnd = new WindowInteropHelper(window).EnsureHandle(); }
            catch { return; }

            if (!enable)
            {
                // Reset backdrop to none and remove dark-mode flag
                try { int v = 1; DwmSetWindowAttribute(hwnd, 38, ref v, sizeof(int)); } catch { }   // DWMSBT_NONE
                try { int v = 0; DwmSetWindowAttribute(hwnd, 1029, ref v, sizeof(int)); } catch { } // legacy mica off
                return;
            }

            // Dark mode title bar text — makes Win11 caption text white
            try { int v = 1; DwmSetWindowAttribute(hwnd, 20, ref v, sizeof(int)); } catch { }

            // Method 1: DWMWA_SYSTEMBACKDROP_TYPE (38) — Win11 22H2+
            // 2 = Mica, 3 = Acrylic, 4 = MicaAlt (more saturated, best with coloured themes)
            bool method1 = false;
            try
            {
                int backdrop = 4; // MicaAlt
                DwmSetWindowAttribute(hwnd, 38, ref backdrop, sizeof(int));
                method1 = true;
            }
            catch { }

            if (!method1)
            {
                // Method 2: undocumented DWMWA_MICA_EFFECT (1029) — Win11 RTM
                try { int v = 1; DwmSetWindowAttribute(hwnd, 1029, ref v, sizeof(int)); } catch { }
            }
        }

        // ── Complementary colour generation ───────────────────────────────────

        /// <summary>
        /// Returns a palette of algorithmically derived companion colours
        /// for use in the UI (e.g. suggest accent pairings for a given bg).
        /// mode = "Complementary" | "Triadic" | "Analogous" | "SplitComp" | "Tetradic"
        /// </summary>
        public static IReadOnlyList<Color> GetComplementPalette(Color baseColor, string mode)
        {
            var (h, s, v) = RgbToHsv(baseColor);

            return mode switch
            {
                "Triadic"    => new[] { HsvToRgb((h + 120) % 360, s, v),
                                        HsvToRgb((h + 240) % 360, s, v) },

                "Analogous"  => new[] { HsvToRgb((h + 30)  % 360, s, v),
                                        HsvToRgb((h - 30 + 360) % 360, s, v),
                                        HsvToRgb((h + 60)  % 360, s, v) },

                "SplitComp"  => new[] { HsvToRgb((h + 150) % 360, s, v),
                                        HsvToRgb((h + 210) % 360, s, v) },

                "Tetradic"   => new[] { HsvToRgb((h + 90)  % 360, s, v),
                                        HsvToRgb((h + 180) % 360, s, v),
                                        HsvToRgb((h + 270) % 360, s, v) },

                // Default: true complementary (opposite on wheel) + a muted variant
                _ => new[] { HsvToRgb((h + 180) % 360, s, v),
                             HsvToRgb((h + 180) % 360, s * 0.6, Math.Min(v + 0.15, 1.0)) }
            };
        }

        /// <summary>
        /// Picks the 'best' accent colour for a given background:
        /// maximises WCAG contrast while keeping the hue close to preferredHue.
        /// Returns the suggested colour as a #RRGGBB hex string.
        /// </summary>
        public static string SuggestAccentForBg(Color bg, double preferredHueDeg = -1)
        {
            double bgLum = RelativeLuminance(bg);
            double bestScore = -1;
            Color best = Colors.Cyan;

            // Sweep 36 hues × 3 saturation levels × 3 value levels
            for (int hi = 0; hi < 36; hi++)
            {
                double hue = hi * 10.0;
                foreach (var sat in new[] { 0.65, 0.80, 0.95 })
                foreach (var val in new[] { 0.70, 0.85, 1.00 })
                {
                    var candidate = HsvToRgb(hue, sat, val);
                    double candLum = RelativeLuminance(candidate);

                    // WCAG contrast ratio
                    double lighter = Math.Max(bgLum, candLum);
                    double darker  = Math.Min(bgLum, candLum);
                    double ratio   = (lighter + 0.05) / (darker + 0.05);

                    // Prefer hues close to the preferred hue if one was given
                    double huePenalty = 0;
                    if (preferredHueDeg >= 0)
                    {
                        double hueDiff = Math.Abs(hue - preferredHueDeg);
                        if (hueDiff > 180) hueDiff = 360 - hueDiff;
                        huePenalty = hueDiff / 360.0 * 2.0; // 0-2 penalty
                    }

                    double score = ratio - huePenalty;
                    if (score > bestScore) { bestScore = score; best = candidate; }
                }
            }

            return $"#{best.R:X2}{best.G:X2}{best.B:X2}";
        }

        // ── Contrast colour algorithms ────────────────────────────────────────

        public static Color GetContrastColor(Color bg, string mode)
        {
            switch (mode)
            {
                case "YIQ":
                {
                    double yiq = (bg.R * 299.0 + bg.G * 587.0 + bg.B * 114.0) / 1000.0;
                    return yiq >= 128 ? Colors.Black : Colors.White;
                }
                case "Invert":
                {
                    return RelativeLuminance(bg) > 0.22 ? Colors.Black : Colors.White;
                }
                default: // WCAG
                {
                    double lBg = RelativeLuminance(bg);
                    double ratioWithWhite = 1.05 / (lBg + 0.05);
                    double ratioWithBlack = (lBg + 0.05) / 0.05;
                    return ratioWithWhite >= ratioWithBlack ? Colors.White : Colors.Black;
                }
            }
        }

        // ── HSV helpers (public so ThemeManagerWindow can share them) ─────────

        public static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
            double r, g, b;
            if      (h < 60)  { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else               { r = c; g = 0; b = x; }
            return Color.FromRgb((byte)((r+m)*255), (byte)((g+m)*255), (byte)((b+m)*255));
        }

        public static (double h, double s, double v) RgbToHsv(Color c)
        {
            double r = c.R/255.0, g = c.G/255.0, b = c.B/255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double delta = max - min, h = 0, s = max == 0 ? 0 : delta/max, v = max;
            if (delta > 0)
            {
                if      (max == r) h = 60 * (((g-b)/delta) % 6);
                else if (max == g) h = 60 * (((b-r)/delta) + 2);
                else               h = 60 * (((r-g)/delta) + 4);
                if (h < 0) h += 360;
            }
            return (h, s, v);
        }

        // ── Internal helpers ──────────────────────────────────────────────────

        public static double RelativeLuminance(Color c)
        {
            static double Lin(double v)
            {
                v /= 255.0;
                return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return 0.2126*Lin(c.R) + 0.7152*Lin(c.G) + 0.0722*Lin(c.B);
        }

        private static Color BlendColors(Color fg, Color bg, double alpha) =>
            Color.FromRgb(
                (byte)(fg.R*(1-alpha) + bg.R*alpha),
                (byte)(fg.G*(1-alpha) + bg.G*alpha),
                (byte)(fg.B*(1-alpha) + bg.B*alpha));
    }
}
