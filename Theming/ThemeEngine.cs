using System;
using System.Windows;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Simplified theme engine: applies WinUI dark mode + 2-tone custom accent colors.
    /// All UI uses Windows 11 dark theme colors except for the custom accent.
    /// </summary>
    public static class ThemeEngine
    {
        public static void ApplyTheme(AppSettings settings)
        {
            if (Application.Current == null) return;

            try
            {
                // Parse accent colors from settings
                var accentPrimary   = (Color)ColorConverter.ConvertFromString(settings.AccentColor);
                var accentSecondary = (Color)ColorConverter.ConvertFromString(settings.AccentColor2);

                // ── WinUI Dark Mode Base Colors (Windows 11 style) ──────────────────
                // These are static and match the Windows 11 dark theme
                Application.Current.Resources["BgBrush"]        = new SolidColorBrush(Color.FromRgb(32, 32, 32));      // #202020
                Application.Current.Resources["TextBrush"]      = new SolidColorBrush(Color.FromRgb(229, 229, 229));  // #E5E5E5
                Application.Current.Resources["SubTextBrush"]   = new SolidColorBrush(Color.FromRgb(155, 155, 155));  // #9B9B9B
                Application.Current.Resources["PanelBrush"]     = new SolidColorBrush(Color.FromRgb(45, 45, 48));     // #2D2D30
                Application.Current.Resources["ControlBgBrush"] = new SolidColorBrush(Color.FromRgb(60, 60, 67));     // #3C3C43
                Application.Current.Resources["HeaderBrush"]    = new SolidColorBrush(Color.FromRgb(50, 50, 54));     // #323236

                // ── 2-Tone Accent ──────────────────────────────────────────────────
                Application.Current.Resources["AccentBrush"]     = new SolidColorBrush(accentPrimary);
                Application.Current.Resources["AccentBrush2"]    = new SolidColorBrush(accentSecondary);

                // For accent text (white always works well with bright accent colors)
                Application.Current.Resources["AccentTextBrush"] = new SolidColorBrush(Colors.White);

                // ── Gradient Brushes (for library cards, etc.) ──────────────────────
                // Linear gradient from primary to secondary accent
                var gradientBrush = new LinearGradientBrush(accentPrimary, accentSecondary, 45.0);
                Application.Current.Resources["AccentGradientBrush"] = gradientBrush;

                // ── Window border + accent label/soft fills ─────────────────────────
                Application.Current.Resources["WindowBorderBrush"] = new SolidColorBrush(accentPrimary);
                Application.Current.Resources["AccentLabelBrush"]  = new SolidColorBrush(accentPrimary);
                Application.Current.Resources["AccentSoftBrush"]   = new SolidColorBrush(Color.FromArgb(0x22, accentPrimary.R, accentPrimary.G, accentPrimary.B));
            }
            catch { /* invalid hex - keep previous theme */ }
        }

        /// <summary>Generate a gradient from primary to secondary accent at a given angle.</summary>
        public static LinearGradientBrush GetAccentGradient(Color primary, Color secondary, double angle = 45.0)
        {
            return new LinearGradientBrush(primary, secondary, angle);
        }

    }
}
