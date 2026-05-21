using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TGTAMM
{
    public partial class ThemeManagerWindow : Window
    {
        // ── State ─────────────────────────────────────────────────────────────
        private readonly BackendCore _core;
        private bool _isUpdating = false;

        // HSV state for each picker (hue 0-360, s/v 0-1)
        private double _accentH = 174, _accentS = 0.62, _accentV = 0.79;
        private double _bgH     = 0,   _bgS     = 0,    _bgV     = 0.12;

        private bool _accentSpecCapture = false;
        private bool _accentHueCapture  = false;
        private bool _bgSpecCapture     = false;
        private bool _bgHueCapture      = false;

        // ── Built-in presets ──────────────────────────────────────────────────
        internal static readonly List<ThemePreset> BuiltInPresets = new()
        {
            // ─ Dark themes ─────────────────────────────────────────────────
            new() { Name = "Dark Teal (Default)",   AccentColor = "#4EC9B0", BgColor = "#1E1E1E",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Vice City Neon",        AccentColor = "#FF6EC7", BgColor = "#0E0018",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "GTA III Era",           AccentColor = "#D43030", BgColor = "#161616",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Trebuchet MS" },
            new() { Name = "San Andreas Grove",     AccentColor = "#5BBF4A", BgColor = "#101510",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "macOS Dark",            AccentColor = "#0A84FF", BgColor = "#1C1C1E",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Segoe UI Light" },
            new() { Name = "Midnight Purple",       AccentColor = "#B085FF", BgColor = "#120D1E",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Ember",                 AccentColor = "#FF7043", BgColor = "#1A1209",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "Stealth",               AccentColor = "#778899", BgColor = "#111214",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Consolas" },
            // ─ GTA-inspired ────────────────────────────────────────────────
            new() { Name = "Los Santos Sunset",     AccentColor = "#FF8C00", BgColor = "#1A1208",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "Liberty City Fog",      AccentColor = "#7BA7BC", BgColor = "#1A1F25",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Midnight Flamingo",      AccentColor = "#FF2D87", BgColor = "#12000C",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "GTA Online",            AccentColor = "#00CFDD", BgColor = "#0D1117",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            // ─ Popular editor themes ────────────────────────────────────────
            new() { Name = "Dracula",               AccentColor = "#BD93F9", BgColor = "#282A36",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = true },
            new() { Name = "Nord",                  AccentColor = "#88C0D0", BgColor = "#2E3440",
                    ColorMode = "Dark",  TitlebarTheme = "Vanilla",   FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Gruvbox Dark",          AccentColor = "#FABD2F", BgColor = "#282828",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Catppuccin Mocha",      AccentColor = "#CBA6F7", BgColor = "#1E1E2E",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "One Dark",              AccentColor = "#61AFEF", BgColor = "#282C34",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            // ─ Other dark themes ────────────────────────────────────────────
            new() { Name = "Phosphor",              AccentColor = "#00FF41", BgColor = "#0D0D0D",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Consolas" },
            new() { Name = "Golden Hour",           AccentColor = "#FFD700", BgColor = "#1A1500",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Slate",                 AccentColor = "#7289DA", BgColor = "#2C2F33",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = true },
            new() { Name = "Crimson Night",         AccentColor = "#E0115F", BgColor = "#120008",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Deep Ocean",            AccentColor = "#00B4D8", BgColor = "#03060F",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            // ─ Light themes ────────────────────────────────────────────────
            new() { Name = "Light Sky",             AccentColor = "#0078D4", BgColor = "#EFF3F8",
                    ColorMode = "Light", TitlebarTheme = "macOSLight",FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Light Warm",            AccentColor = "#C0392B", BgColor = "#FAF6F1",
                    ColorMode = "Light", TitlebarTheme = "Win8",      FontFamily = "Calibri",
                    TitlebarPersonalize = true },
            new() { Name = "Light Mint",            AccentColor = "#2E8B57", BgColor = "#F2FAF5",
                    ColorMode = "Light", TitlebarTheme = "Vanilla",   FontFamily = "Bahnschrift",
                    TitlebarPersonalize = false },
            new() { Name = "Solarized Light",       AccentColor = "#268BD2", BgColor = "#FDF6E3",
                    ColorMode = "Light", TitlebarTheme = "macOSLight",FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Rose Quartz",           AccentColor = "#B5446E", BgColor = "#FDF0F5",
                    ColorMode = "Light", TitlebarTheme = "macOSLight",FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Nord Light",            AccentColor = "#5E81AC", BgColor = "#ECEFF4",
                    ColorMode = "Light", TitlebarTheme = "Vanilla",   FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = false },
            // ─ GTA-era classic themes ────────────────────────────────────────
            new() { Name = "Vice City Pink",        AccentColor = "#FF3CAC", BgColor = "#0A0012",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "San Andreas Dusk",      AccentColor = "#FF6B35", BgColor = "#13060A",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Liberty City Rain",     AccentColor = "#4A9EBF", BgColor = "#090E14",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Grove Street",          AccentColor = "#4BDA3A", BgColor = "#0A1208",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            // ─ Extra dark themes ─────────────────────────────────────────────
            new() { Name = "Cyberpunk",             AccentColor = "#FFE600", BgColor = "#0A0015",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Vaporwave",             AccentColor = "#FF71CE", BgColor = "#09001F",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "Terminal Green",        AccentColor = "#00FF88", BgColor = "#050F05",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Blood Moon",            AccentColor = "#FF1744", BgColor = "#0E0003",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Obsidian",              AccentColor = "#6C8EBF", BgColor = "#080A0C",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Neon Coral",            AccentColor = "#FF6B9D", BgColor = "#0F0810",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Ocean Depths",          AccentColor = "#0BC5EA", BgColor = "#020B18",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Toxic",                 AccentColor = "#A8FF3E", BgColor = "#080E02",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Retro Amber",           AccentColor = "#FFB300", BgColor = "#0E0900",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Consolas",
                    TitlebarPersonalize = false },
            new() { Name = "Sapphire",              AccentColor = "#1E90FF", BgColor = "#05080F",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            // ─ Extra light themes ─────────────────────────────────────────────
            new() { Name = "Light Lavender",        AccentColor = "#7C3AED", BgColor = "#F8F5FF",
                    ColorMode = "Light", TitlebarTheme = "macOSLight",FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Light Slate",           AccentColor = "#475569", BgColor = "#F1F5F9",
                    ColorMode = "Light", TitlebarTheme = "Vanilla",   FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Parchment",             AccentColor = "#8B5E3C", BgColor = "#FBF5E6",
                    ColorMode = "Light", TitlebarTheme = "Win7",      FontFamily = "Calibri",
                    TitlebarPersonalize = true },
            // ─ Retro/Synthwave themes ──────────────────────────────────────────
            new() { Name = "Synthwave Sunset",       AccentColor = "#FF10F0", BgColor = "#0F0620",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Outrun",                 AccentColor = "#00FFFF", BgColor = "#110033",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "Retrowave",              AccentColor = "#FF006E", BgColor = "#0D001F",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "80s Neon",               AccentColor = "#39FF14", BgColor = "#0A0A1A",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            // ─ Minimalist themes ───────────────────────────────────────────────
            new() { Name = "Stark",                  AccentColor = "#FFFFFF", BgColor = "#1A1A1A",
                    ColorMode = "Dark",  TitlebarTheme = "Vanilla",   FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Minimal Gray",           AccentColor = "#888888", BgColor = "#212121",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = false },
            new() { Name = "Pure Black",             AccentColor = "#00FFFF", BgColor = "#000000",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Consolas",
                    TitlebarPersonalize = false },
            // ─ Nature-inspired themes ──────────────────────────────────────────
            new() { Name = "Forest",                 AccentColor = "#2D5016", BgColor = "#0E1410",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Sunset Blaze",           AccentColor = "#FF6B5B", BgColor = "#1A0A05",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Midnight Lake",          AccentColor = "#1E90FF", BgColor = "#0A0D15",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Segoe UI",
                    TitlebarPersonalize = true },
            new() { Name = "Aurora",                 AccentColor = "#00FF7F", BgColor = "#0B1428",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            // ─ Editor themes ───────────────────────────────────────────────────
            new() { Name = "Monokai",                AccentColor = "#F92672", BgColor = "#272822",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Solarized Dark",         AccentColor = "#268BD2", BgColor = "#002B36",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "Material",               AccentColor = "#BB86FC", BgColor = "#121212",
                    ColorMode = "Dark",  TitlebarTheme = "Vanilla",   FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "GitHub Dark",            AccentColor = "#58A6FF", BgColor = "#0D1117",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            // ─ Vibrant/Colorful themes ────────────────────────────────────────
            new() { Name = "Electric Violet",        AccentColor = "#BC13FE", BgColor = "#0F0318",
                    ColorMode = "Dark",  TitlebarTheme = "macOS",     FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Magenta Dream",          AccentColor = "#FF00FF", BgColor = "#1A001A",
                    ColorMode = "Dark",  TitlebarTheme = "Win9x",     FontFamily = "Trebuchet MS",
                    TitlebarPersonalize = true },
            new() { Name = "Cyan Pulse",             AccentColor = "#00FFFF", BgColor = "#001515",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            new() { Name = "Sunset Orange",          AccentColor = "#FF9500", BgColor = "#1A0800",
                    ColorMode = "Dark",  TitlebarTheme = "Win7",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Lime Spark",             AccentColor = "#CCFF00", BgColor = "#0A0E00",
                    ColorMode = "Dark",  TitlebarTheme = "Win8",      FontFamily = "Consolas",
                    TitlebarPersonalize = true },
            // ─ Additional light themes ─────────────────────────────────────────
            new() { Name = "Light Coral",            AccentColor = "#FF6B6B", BgColor = "#FDF8F8",
                    ColorMode = "Light", TitlebarTheme = "Win7",      FontFamily = "Bahnschrift",
                    TitlebarPersonalize = true },
            new() { Name = "Light Teal",             AccentColor = "#20B2AA", BgColor = "#F0FFFE",
                    ColorMode = "Light", TitlebarTheme = "macOSLight",FontFamily = "Segoe UI Light",
                    TitlebarPersonalize = true },
            new() { Name = "Light Peach",            AccentColor = "#FFDAB9", BgColor = "#FFF8F5",
                    ColorMode = "Light", TitlebarTheme = "Vanilla",   FontFamily = "Calibri",
                    TitlebarPersonalize = true },
            new() { Name = "Light Forest",           AccentColor = "#228B22", BgColor = "#F5FFF5",
                    ColorMode = "Light", TitlebarTheme = "Win8",      FontFamily = "Segoe UI",
                    TitlebarPersonalize = true },
            // ─ Special retro OS themes ────────────────────────────────────────
            // ─ Unique Themes (retro OS) ───────────────────────────────────────
            new() { Name = "═══ UNIQUE THEMES ═══", AccentColor = "#000000", BgColor = "#1E1E1E",
                    ColorMode = "Dark",  TitlebarTheme = "Compact",   FontFamily = "Segoe UI",
                    TitlebarPersonalize = false },
            new() { Name = "★ Windows 3.1",          AccentColor = "#000080", BgColor = "#C0C0C0",
                    ColorMode = "Dark",  TitlebarTheme = "Win31",     FontFamily = "MS Sans Serif",
                    TitlebarPersonalize = true },
            new() { Name = "★ Classic Mac (9.0)",    AccentColor = "#000000", BgColor = "#F0F0F0",
                    ColorMode = "Light", TitlebarTheme = "MacOS9",    FontFamily = "Chicago",
                    TitlebarPersonalize = true },
        };

        // ── Constructor ───────────────────────────────────────────────────────
        public ThemeManagerWindow(BackendCore core)
        {
            // Both _core AND _isUpdating must be set BEFORE InitializeComponent().
            // XAML init fires SelectionChanged / ValueChanged on every ComboBox and Slider
            // as they are created. If _isUpdating is false at that point, the handlers
            // call TriggerThemeUpdate() before txtAccent / txtBg exist → NullRef.
            _core = core;
            _isUpdating = true;
            InitializeComponent();

            // Populate presets combo
            cmbPresets.Items.Add("— Built-in Presets —");
            foreach (var p in BuiltInPresets) cmbPresets.Items.Add(p.Name);
            cmbPresets.SelectedIndex = 0;

            // Initialise pickers from saved settings
            InitPickerFromHex(txtAccent.Text = _core.Settings.AccentColor, isAccent: true);
            InitPickerFromHex(txtBg.Text     = _core.Settings.BgColor,     isAccent: false);

            // Titlebar combos
            SelectByTag(cmbTheme,     _core.Settings.TitlebarTheme);
            SelectByTag(cmbAlign,     _core.Settings.TitlebarAlignment);
            SelectByTag(cmbColor,     _core.Settings.ColorMode);
            SelectByTag(cmbFont,      _core.Settings.FontFamily);
            SelectByTag(cmbTextColor, _core.Settings.TextColorMode);

            chkPersonalize.IsChecked    = _core.Settings.TitlebarPersonalize;
            chkMica.IsChecked           = _core.Settings.MicaEnabled;
            chkAccentBorder.IsChecked   = _core.Settings.AccentBorderEnabled;

            // Restore last selected preset (without firing ApplyPreset — settings already loaded)
            if (!string.IsNullOrEmpty(_core.Settings.LastPresetName))
            {
                int idx = BuiltInPresets.FindIndex(p => p.Name == _core.Settings.LastPresetName);
                if (idx >= 0)
                    cmbPresets.SelectedIndex = idx + 1; // +1 for header item
            }

            _isUpdating = false;

            // Generate .mmtheme files for all built-in presets into AppData\Themes\
            // Do this after init so _core is fully ready; runs quickly (JSON writes).
            GenerateBuiltInThemeFiles();

            // ContentRendered fires after the first layout + render pass, ensuring
            // ActualWidth/Height are non-zero when we place the picker cursors.
            // (Loaded fires before layout measurement completes — ActualWidth is 0 there.)
            ContentRendered += (_, _) =>
            {
                UpdateAccentCursorFromHsv();
                UpdateBgCursorFromHsv();
            };
            // Also update when the window is resized (spectrum canvases change size)
            SizeChanged += (_, _) =>
            {
                UpdateAccentCursorFromHsv();
                UpdateBgCursorFromHsv();
            };
        }

        // ── HSV ↔ RGB — delegates to ThemeEngine (no duplication) ──────────────

        private static Color HsvToRgb(double h, double s, double v) =>
            ThemeEngine.HsvToRgb(h, s, v);

        private static (double h, double s, double v) RgbToHsv(Color c) =>
            ThemeEngine.RgbToHsv(c);

        private void InitPickerFromHex(string hex, bool isAccent)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var (h, s, v) = RgbToHsv(color);
                if (isAccent) { _accentH = h; _accentS = s; _accentV = v; }
                else          { _bgH     = h; _bgS     = s; _bgV     = v; }
            }
            catch { }
        }

        // ── Cursor position updaters ──────────────────────────────────────────

        private void UpdateAccentCursorFromHsv()
        {
            double w = accentSpecCanvas.ActualWidth, h = accentSpecCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            Canvas.SetLeft(accentCursor, _accentS * w - 6);
            Canvas.SetTop(accentCursor,  (1 - _accentV) * h - 6);
            Canvas.SetLeft(accentHueCursor, _accentH / 360.0 * accentHueCanvas.ActualWidth - 2);

            var pureHue = HsvToRgb(_accentH, 1, 1);
            accentHueStop.Color = pureHue;

            var finalColor = HsvToRgb(_accentH, _accentS, _accentV);
            accentPreview.Fill = new SolidColorBrush(finalColor);

            _isUpdating = true;
            txtAccent.Text = $"#{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";
            _isUpdating = false;
        }

        private void UpdateBgCursorFromHsv()
        {
            double w = bgSpecCanvas.ActualWidth, h = bgSpecCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;
            Canvas.SetLeft(bgCursor, _bgS * w - 6);
            Canvas.SetTop(bgCursor,  (1 - _bgV) * h - 6);
            Canvas.SetLeft(bgHueCursor, _bgH / 360.0 * bgHueCanvas.ActualWidth - 2);

            var pureHue = HsvToRgb(_bgH, 1, 1);
            bgHueStop.Color = pureHue;

            var finalColor = HsvToRgb(_bgH, _bgS, _bgV);
            bgPreview.Fill = new SolidColorBrush(finalColor);

            _isUpdating = true;
            txtBg.Text = $"#{finalColor.R:X2}{finalColor.G:X2}{finalColor.B:X2}";
            _isUpdating = false;
        }

        // ── Accent spectrum mouse ─────────────────────────────────────────────

        private void AccentSpec_MouseDown(object s, MouseButtonEventArgs e)
        {
            _accentSpecCapture = true;
            accentSpecCanvas.CaptureMouse();
            UpdateAccentSVFromMouse(e.GetPosition(accentSpecCanvas));
        }
        private void AccentSpec_MouseMove(object s, MouseEventArgs e)
        {
            if (!_accentSpecCapture) return;
            UpdateAccentSVFromMouse(e.GetPosition(accentSpecCanvas));
        }
        private void AccentSpec_MouseUp(object s, MouseButtonEventArgs e)
        {
            _accentSpecCapture = false;
            accentSpecCanvas.ReleaseMouseCapture();
        }
        private void UpdateAccentSVFromMouse(Point p)
        {
            double w = accentSpecCanvas.ActualWidth, h = accentSpecCanvas.ActualHeight;
            _accentS = Math.Clamp(p.X / w, 0, 1);
            _accentV = Math.Clamp(1 - p.Y / h, 0, 1);
            UpdateAccentCursorFromHsv();
            ClearPresetSelection();
            TriggerThemeUpdate();
        }

        // ── Accent hue mouse ──────────────────────────────────────────────────

        private void AccentHue_MouseDown(object s, MouseButtonEventArgs e)
        {
            _accentHueCapture = true;
            accentHueCanvas.CaptureMouse();
            UpdateAccentHueFromMouse(e.GetPosition(accentHueCanvas));
        }
        private void AccentHue_MouseMove(object s, MouseEventArgs e)
        {
            if (!_accentHueCapture) return;
            UpdateAccentHueFromMouse(e.GetPosition(accentHueCanvas));
        }
        private void AccentHue_MouseUp(object s, MouseButtonEventArgs e)
        {
            _accentHueCapture = false;
            accentHueCanvas.ReleaseMouseCapture();
        }
        private void UpdateAccentHueFromMouse(Point p)
        {
            _accentH = Math.Clamp(p.X / accentHueCanvas.ActualWidth, 0, 1) * 360;
            UpdateAccentCursorFromHsv();
            ClearPresetSelection();
            TriggerThemeUpdate();
        }

        // ── BG spectrum mouse ─────────────────────────────────────────────────

        private void BgSpec_MouseDown(object s, MouseButtonEventArgs e)
        {
            _bgSpecCapture = true;
            bgSpecCanvas.CaptureMouse();
            UpdateBgSVFromMouse(e.GetPosition(bgSpecCanvas));
        }
        private void BgSpec_MouseMove(object s, MouseEventArgs e)
        {
            if (!_bgSpecCapture) return;
            UpdateBgSVFromMouse(e.GetPosition(bgSpecCanvas));
        }
        private void BgSpec_MouseUp(object s, MouseButtonEventArgs e)
        {
            _bgSpecCapture = false;
            bgSpecCanvas.ReleaseMouseCapture();
        }
        private void UpdateBgSVFromMouse(Point p)
        {
            double w = bgSpecCanvas.ActualWidth, h = bgSpecCanvas.ActualHeight;
            _bgS = Math.Clamp(p.X / w, 0, 1);
            _bgV = Math.Clamp(1 - p.Y / h, 0, 1);
            UpdateBgCursorFromHsv();
            ClearPresetSelection();
            TriggerThemeUpdate();
        }

        // ── BG hue mouse ──────────────────────────────────────────────────────

        private void BgHue_MouseDown(object s, MouseButtonEventArgs e)
        {
            _bgHueCapture = true;
            bgHueCanvas.CaptureMouse();
            UpdateBgHueFromMouse(e.GetPosition(bgHueCanvas));
        }
        private void BgHue_MouseMove(object s, MouseEventArgs e)
        {
            if (!_bgHueCapture) return;
            UpdateBgHueFromMouse(e.GetPosition(bgHueCanvas));
        }
        private void BgHue_MouseUp(object s, MouseButtonEventArgs e)
        {
            _bgHueCapture = false;
            bgHueCanvas.ReleaseMouseCapture();
        }
        private void UpdateBgHueFromMouse(Point p)
        {
            _bgH = Math.Clamp(p.X / bgHueCanvas.ActualWidth, 0, 1) * 360;
            UpdateBgCursorFromHsv();
            ClearPresetSelection();
            TriggerThemeUpdate();
        }

        // ── Manual hex input ──────────────────────────────────────────────────

        private void TxtAccentHex_Changed(object s, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(txtAccent.Text);
                var (h, sv, v) = RgbToHsv(c);
                _accentH = h; _accentS = sv; _accentV = v;
                UpdateAccentCursorFromHsv();
                ClearPresetSelection();
                TriggerThemeUpdate();
            }
            catch { }
        }

        private void TxtBgHex_Changed(object s, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(txtBg.Text);
                var (h, sv, v) = RgbToHsv(c);
                _bgH = h; _bgS = sv; _bgV = v;
                UpdateBgCursorFromHsv();
                ClearPresetSelection();
                TriggerThemeUpdate();
            }
            catch { }
        }

        // ── Titlebar / general settings ───────────────────────────────────────

        private void ModularSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            if (cmbTheme     is { SelectedItem: ComboBoxItem t }) _core.Settings.TitlebarTheme     = t.Tag.ToString()!;
            if (cmbAlign     is { SelectedItem: ComboBoxItem a }) _core.Settings.TitlebarAlignment  = a.Tag.ToString()!;
            if (cmbColor     is { SelectedItem: ComboBoxItem c }) _core.Settings.ColorMode           = c.Tag.ToString()!;
            if (cmbFont      is { SelectedItem: ComboBoxItem f }) _core.Settings.FontFamily          = f.Tag.ToString()!;
            if (cmbTextColor is { SelectedItem: ComboBoxItem x }) _core.Settings.TextColorMode       = x.Tag.ToString()!;

            _core.Settings.TitlebarPersonalize  = chkPersonalize.IsChecked == true;
            _core.Settings.MicaEnabled          = chkMica.IsChecked == true;
            _core.Settings.AccentBorderEnabled  = chkAccentBorder.IsChecked == true;

            TriggerThemeUpdate();
        }

        // ── Presets ───────────────────────────────────────────────────────────

        private void CmbPresets_Changed(object s, SelectionChangedEventArgs e)
        {
            if (_isUpdating || cmbPresets.SelectedIndex <= 0) return;

            int idx = cmbPresets.SelectedIndex - 1; // offset by header item
            if (idx >= BuiltInPresets.Count) return;

            _core.Settings.LastPresetName = BuiltInPresets[idx].Name;
            ApplyPreset(BuiltInPresets[idx]);
        }

        private void ClearPresetSelection()
        {
            if (cmbPresets.SelectedIndex != 0)
            {
                _isUpdating = true;
                cmbPresets.SelectedIndex = 0;
                _isUpdating = false;
            }
            _core.Settings.LastPresetName = "";
        }

        private void ApplyPreset(ThemePreset preset)
        {
            _core.Settings.AccentColor          = preset.AccentColor;
            _core.Settings.BgColor              = preset.BgColor;
            _core.Settings.ColorMode            = preset.ColorMode;
            _core.Settings.TitlebarTheme        = preset.TitlebarTheme;
            _core.Settings.TitlebarAlignment    = preset.TitlebarAlignment;
            _core.Settings.TitlebarPersonalize  = preset.TitlebarPersonalize;
            _core.Settings.TitlebarOpacity      = preset.TitlebarOpacity;
            _core.Settings.FontFamily           = preset.FontFamily;
            _core.Settings.TextColorMode        = preset.TextColorMode;
            _core.Settings.MicaEnabled          = preset.MicaEnabled;
            _core.Settings.MicaIntensity        = preset.MicaIntensity;

            // Resync all UI controls
            _isUpdating = true;
            InitPickerFromHex(txtAccent.Text = preset.AccentColor, isAccent: true);
            InitPickerFromHex(txtBg.Text     = preset.BgColor,     isAccent: false);
            SelectByTag(cmbTheme,     preset.TitlebarTheme);
            SelectByTag(cmbAlign,     preset.TitlebarAlignment);
            SelectByTag(cmbColor,     preset.ColorMode);
            SelectByTag(cmbFont,      preset.FontFamily);
            SelectByTag(cmbTextColor, preset.TextColorMode);
            chkPersonalize.IsChecked   = preset.TitlebarPersonalize;
            chkMica.IsChecked          = preset.MicaEnabled;
            chkAccentBorder.IsChecked  = false; // presets don't force-enable the accent border
            _isUpdating = false;

            UpdateAccentCursorFromHsv();
            UpdateBgCursorFromHsv();
            TriggerThemeUpdate();
        }

        private void BtnImportPreset_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Theme Preset",
                Filter = "TGTAMM Theme|*.mmtheme|All Files|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var preset = JsonSerializer.Deserialize<ThemePreset>(json);
                if (preset == null) throw new Exception("Empty or invalid preset file.");
                if (preset.Version < 2)
                    MessageBox.Show(
                        $"This preset was saved with an older version of TGTAMM (v{preset.Version})." +
                        "It will still be applied — some newer fields will use defaults.",
                        "Older Preset Format", MessageBoxButton.OK, MessageBoxImage.Information);
                ApplyPreset(preset);
                MessageBox.Show($"Loaded preset: {preset.Name}", "Import OK",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not import preset:\n{ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void GenerateBuiltInThemeFiles()
        {
            try
            {
                string dir = Path.Combine(_core.AppDataPath, "Themes");
                Directory.CreateDirectory(dir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                foreach (var preset in BuiltInPresets)
                {
                    // Sanitize name to a safe filename
                    var safeName = string.Concat(preset.Name.Select(
                        c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                    string dest = Path.Combine(dir, $"{safeName}.mmtheme");
                    File.WriteAllText(dest, JsonSerializer.Serialize(preset, opts));
                }
            }
            catch { /* non-fatal — theme files are optional convenience exports */ }
        }

        private void BtnExportPreset_Click(object s, RoutedEventArgs e)
        {
            string themesDir = Path.Combine(_core.AppDataPath, "Themes");
            Directory.CreateDirectory(themesDir);
            var dlg = new SaveFileDialog
            {
                Title            = "Export Theme Preset",
                Filter           = "TGTAMM Theme|*.mmtheme|All Files|*.*",
                FileName         = "MyTheme",
                DefaultExt       = ".mmtheme",
                InitialDirectory = themesDir
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                // Snapshot current complement swatches (if any were generated)
                var currentComplements = new List<string>();
                if (lstComplement != null)
                    foreach (var child in lstComplement.Children)
                        if (child is System.Windows.Controls.Button sb && sb.Tag is string hex)
                            currentComplements.Add(hex);

                var preset = new ThemePreset
                {
                    Version             = 2,
                    Name                = Path.GetFileNameWithoutExtension(dlg.FileName),
                    AccentColor         = _core.Settings.AccentColor,
                    BgColor             = _core.Settings.BgColor,
                    ColorMode           = _core.Settings.ColorMode,
                    TitlebarTheme       = _core.Settings.TitlebarTheme,
                    TitlebarAlignment   = _core.Settings.TitlebarAlignment,
                    TitlebarPersonalize = _core.Settings.TitlebarPersonalize,
                    TitlebarOpacity     = _core.Settings.TitlebarOpacity,
                    FontFamily          = _core.Settings.FontFamily,
                    TextColorMode       = _core.Settings.TextColorMode,
                    MicaEnabled         = _core.Settings.MicaEnabled,
                    MicaIntensity       = _core.Settings.MicaIntensity,
                    ComplementColors    = currentComplements
                };
                var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show($"Preset saved to:\n{dlg.FileName}", "Export OK",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not export preset:\n{ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Core helpers ──────────────────────────────────────────────────────

        private void TriggerThemeUpdate()
        {
            // Guard: called from event handlers that can fire before controls exist.
            if (txtAccent == null || txtBg == null || _core == null) return;

            _core.Settings.AccentColor = txtAccent.Text;
            _core.Settings.BgColor     = txtBg.Text;
            _core.SaveSettings();

            ThemeEngine.ApplyTheme(_core.Settings);

            if (Owner is MainDashboardWindow main)
            {
                main.ApplyTitlebarStyle();
                ThemeEngine.ApplyFont(main, _core.Settings);
                ThemeEngine.TryApplyMica(main, _core.Settings.MicaEnabled);
            }
        }

        private static void SelectByTag(ComboBox cmb, string tag)
        {
            foreach (ComboBoxItem item in cmb.Items)
                if (item.Tag?.ToString() == tag) { cmb.SelectedItem = item; return; }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        // ── Complementary colour suggestions ─────────────────────────────────

        private void BtnSuggestAccent_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null) return;
            try
            {
                var bg = (Color)ColorConverter.ConvertFromString(txtBg.Text);
                string suggested = ThemeEngine.SuggestAccentForBg(bg);
                txtAccent.Text = suggested;   // TxtAccentHex_Changed will propagate
            }
            catch { }
        }

        private void BtnComplementPalette_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || lstComplement == null) return;
            try
            {
                var baseColor = (Color)ColorConverter.ConvertFromString(txtAccent.Text);
                string mode = (cmbComplementMode?.SelectedItem as ComboBoxItem)?.Tag?.ToString()
                              ?? "Complementary";
                var palette = ThemeEngine.GetComplementPalette(baseColor, mode);

                lstComplement.Children.Clear();
                foreach (var col in palette)
                {
                    string hex = $"#{col.R:X2}{col.G:X2}{col.B:X2}";
                    var btn = new System.Windows.Controls.Button
                    {
                        Width      = 38,
                        Height     = 38,
                        Margin     = new Thickness(4, 0, 4, 0),
                        Cursor     = System.Windows.Input.Cursors.Hand,
                        ToolTip    = hex,
                        Tag        = hex,
                        Background = new SolidColorBrush(col),
                        BorderThickness = new Thickness(2),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    };
                    btn.Template = BuildSwatchTemplate();
                    btn.Click += (s, _) => { if (s is System.Windows.Controls.Button b) txtAccent.Text = b.Tag?.ToString() ?? ""; };
                    lstComplement.Children.Add(btn);
                }
            }
            catch { }
        }

        private static ControlTemplate BuildSwatchTemplate()
        {
            var tpl = new ControlTemplate(typeof(System.Windows.Controls.Button));
            var fac = new FrameworkElementFactory(typeof(Border));
            fac.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            fac.SetValue(Border.BackgroundProperty,
                new TemplateBindingExtension(System.Windows.Controls.Control.BackgroundProperty));
            fac.SetValue(Border.BorderBrushProperty,
                new TemplateBindingExtension(System.Windows.Controls.Control.BorderBrushProperty));
            fac.SetValue(Border.BorderThicknessProperty,
                new TemplateBindingExtension(System.Windows.Controls.Control.BorderThicknessProperty));
            tpl.VisualTree = fac;
            return tpl;
        }

        private void BtnAdvanced_Click(object sender, RoutedEventArgs e)
        {
            bool showing = RightPickerPanel.Visibility == Visibility.Visible;
            RightPickerPanel.Visibility = showing ? Visibility.Collapsed : Visibility.Visible;
            RightGapBorder.Visibility   = RightPickerPanel.Visibility;
            btnAdvanced.Content = showing ? "🎨  Custom Colors  ▾" : "🎨  Custom Colors  ▴";
            // Resize window: compact (430) when closed, expanded (800) when open
            this.Width = showing ? 430 : 800;
            if (!showing)
            {
                UpdateAccentCursorFromHsv();
                UpdateBgCursorFromHsv();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }

    // ── Preset DTO ────────────────────────────────────────────────────────────

    /// <summary>
    /// Serialized to/from .mmtheme (JSON) files for sharing.
    /// </summary>
    public class ThemePreset
    {
        /// <summary>File format version. Bump when fields are added so importers can warn on mismatch.</summary>
        public int    Version             { get; set; } = 2;

        public string Name                { get; set; } = "My Theme";
        public string AccentColor         { get; set; } = "#4EC9B0";
        public string BgColor             { get; set; } = "#1E1E1E";
        public string ColorMode           { get; set; } = "Dark";
        public string TitlebarTheme       { get; set; } = "Win7";
        public string TitlebarAlignment   { get; set; } = "Right";
        public bool   TitlebarPersonalize { get; set; } = true;
        public double TitlebarOpacity     { get; set; } = 0.88;
        public string FontFamily          { get; set; } = "Bahnschrift";
        public string TextColorMode       { get; set; } = "WCAG";
        public bool   MicaEnabled         { get; set; } = false;
        public double MicaIntensity       { get; set; } = 0.55;

        /// <summary>
        /// Optional: hex colours algorithmically paired with the accent.
        /// Populated by the complement picker; not required for the theme to apply.
        /// </summary>
        public List<string> ComplementColors { get; set; } = new();
    }
}
