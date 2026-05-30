using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TMM
{
    public partial class AppearanceDialog : TmmWindow
    {
        private readonly LibraryEntry _entry;
        private readonly BackendCore  _core;

        // Tracks a newly-chosen art path during the session (null = no change / removed)
        private string? _pendingArtPath;
        private bool    _artRemoved;

        private static readonly (string Start, string End)[] Presets =
        {
            ("#1B3A1B", "#0C1E0C"), // forest green
            ("#B5179E", "#3A0CA3"), // vice magenta → purple
            ("#C56A1A", "#3A1E08"), // desert orange
            ("#0C1A2E", "#060F1C"), // steel blue
            ("#7A1414", "#2A0808"), // crimson
            ("#B8860B", "#2E2206"), // gold
            ("#0E4D4D", "#06201F"), // teal
            ("#3A0CA3", "#10082E"), // indigo
            ("#444A52", "#1A1D21"), // graphite
            ("#7A2E5E", "#2A0F22"), // plum
        };

        public AppearanceDialog(LibraryEntry entry, BackendCore core)
        {
            InitializeComponent();
            _entry = entry;
            _core  = core;
            previewName.Text = entry.DisplayName;

            // Seed color from current effective value (override else shipped gradient)
            var ov = core.GetCardColor(entry.Key);
            txtStart.Text = ov?.Start ?? entry.GradientStartHex;
            txtEnd.Text   = ov?.End   ?? entry.GradientEndHex;

            // Seed artwork
            var artPath = core.GetLibraryArtPath(entry.Key);
            if (artPath != null) ShowArtPreview(artPath);

            BuildPresets();
            UpdateGradientPreview();
        }

        // ── Presets ───────────────────────────────────────────────────────────────

        private void BuildPresets()
        {
            foreach (var (start, end) in Presets)
            {
                var swatch = new Border
                {
                    Width  = 54, Height = 34,
                    CornerRadius    = new CornerRadius(6),
                    Margin          = new Thickness(0, 0, 6, 6),
                    Cursor          = Cursors.Hand,
                    BorderBrush     = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    ToolTip         = $"{start} → {end}",
                    Background      = new LinearGradientBrush(
                        ParseOr(start, Colors.Black),
                        ParseOr(end,   Colors.Black),
                        new Point(0, 0), new Point(1, 1)),
                };
                swatch.MouseLeftButtonUp += (_, _) =>
                {
                    txtStart.Text = start;
                    txtEnd.Text   = end;
                };
                presetPanel.Children.Add(swatch);
            }
        }

        // ── Color inputs ──────────────────────────────────────────────────────────

        private void Hex_TextChanged(object sender, TextChangedEventArgs e) => UpdateGradientPreview();

        private void UpdateGradientPreview()
        {
            if (TryParse(txtStart.Text, out var c0)) previewStart.Color = c0;
            if (TryParse(txtEnd.Text,   out var c1)) previewEnd.Color   = c1;
        }

        // ── Artwork ───────────────────────────────────────────────────────────────

        private void BtnChooseArt_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = $"Select artwork for {_entry.DisplayName}",
                Filter = "PNG Image (*.png)|*.png",
            };
            if (dlg.ShowDialog() != true) return;
            _pendingArtPath = dlg.FileName;
            _artRemoved     = false;
            ShowArtPreview(dlg.FileName);
        }

        private void BtnRemoveArt_Click(object sender, RoutedEventArgs e)
        {
            _pendingArtPath = null;
            _artRemoved     = true;
            previewArtOverlay.Visibility = Visibility.Collapsed;
            btnRemoveArt.Visibility      = Visibility.Collapsed;
        }

        private void ShowArtPreview(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                previewArtOverlay.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                previewArtOverlay.Visibility = Visibility.Visible;
                btnRemoveArt.Visibility      = Visibility.Visible;
            }
            catch { /* invalid image — ignore */ }
        }

        // ── Footer buttons ────────────────────────────────────────────────────────

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParse(txtStart.Text, out _) || !TryParse(txtEnd.Text, out _))
            {
                NotificationService.ShowWarning("Enter valid hex colors (e.g. #1B3A1B).");
                return;
            }
            _core.SetCardColor(_entry.Key, txtStart.Text.Trim(), txtEnd.Text.Trim());

            if (_artRemoved)
                _core.DeleteLibraryArt(_entry.Key);
            else if (_pendingArtPath != null)
            {
                try { _core.SaveLibraryArt(_entry.Key, _pendingArtPath); }
                catch (ArgumentException ex) { NotificationService.ShowWarning(ex.Message); return; }
            }

            DialogResult = true;
            Close();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _core.ClearCardColor(_entry.Key);
            _core.DeleteLibraryArt(_entry.Key);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Color ParseOr(string hex, Color fallback)
            => TryParse(hex, out var c) ? c : fallback;

        private static bool TryParse(string? hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try { color = (Color)ColorConverter.ConvertFromString(hex.Trim()); return true; }
            catch { return false; }
        }
    }
}
