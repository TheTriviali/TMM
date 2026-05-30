using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Lightweight gradient picker for library card colors. Offers preset two-tone
    /// swatches plus custom start/end hex entry with a live preview. Returns the chosen
    /// <see cref="StartHex"/>/<see cref="EndHex"/> when <c>DialogResult == true</c>.
    /// </summary>
    public partial class ColorPickerWindow : TmmWindow
    {
        public string StartHex { get; private set; }
        public string EndHex   { get; private set; }

        // Curated two-tone presets (start, end). Kept tasteful and on the darker side so
        // white card text stays legible.
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

        public ColorPickerWindow(string startHex, string endHex)
        {
            InitializeComponent();
            StartHex = startHex;
            EndHex   = endHex;
            txtStart.Text = startHex;
            txtEnd.Text   = endHex;
            BuildPresets();
            UpdatePreview();
        }

        private void BuildPresets()
        {
            foreach (var (start, end) in Presets)
            {
                var swatch = new Border
                {
                    Width = 54, Height = 34,
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    ToolTip = $"{start} → {end}",
                    Background = new LinearGradientBrush(
                        ParseOr(start, Colors.Black),
                        ParseOr(end, Colors.Black),
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

        private void Hex_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (TryParse(txtStart.Text, out var c0)) previewStart.Color = c0;
            if (TryParse(txtEnd.Text,   out var c1)) previewEnd.Color   = c1;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParse(txtStart.Text, out _) || !TryParse(txtEnd.Text, out _))
            {
                NotificationService.ShowWarning("Enter valid hex colors (e.g. #1B3A1B).");
                return;
            }
            StartHex = txtStart.Text.Trim();
            EndHex   = txtEnd.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

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
