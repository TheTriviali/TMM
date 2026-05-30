using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    public partial class SettingsPage : UserControl
    {
        private readonly BackendCore _core;
        private bool _isUpdating = false;

        public SettingsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
            InitializeAccentPresets();
            chkVerboseNotifications.IsChecked = _core.Settings.VerboseNotifications;

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null) lblVersion.Text = $"v{ver.Major}.{ver.Minor}.{ver.Build}";
        }

        private void InitializeAccentPresets()
        {
            _isUpdating = true;

            // Populate preset combo
            cmbAccentPreset.Items.Add("- Custom -");
            foreach (var preset in AccentPresets.All)
                cmbAccentPreset.Items.Add(preset.Name);

            // Load current colors
            txtPrimaryColor.Text = _core.Settings.AccentColor;
            txtSecondaryColor.Text = _core.Settings.AccentColor2;

            // Select preset if it matches
            int presetIdx = AccentPresets.All.FindIndex(p => p.Name == _core.Settings.ActiveAccentPreset);
            cmbAccentPreset.SelectedIndex = presetIdx >= 0 ? presetIdx + 1 : 0;

            _isUpdating = false;
            UpdateColorPreviews();
        }

        private void CmbAccentPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (cmbAccentPreset.SelectedIndex <= 0) return;

            var preset = AccentPresets.All[cmbAccentPreset.SelectedIndex - 1];
            txtPrimaryColor.Text = preset.PrimaryHex;
            txtSecondaryColor.Text = preset.SecondaryHex;
            _core.Settings.ActiveAccentPreset = preset.Name;
            ApplyAccentColors();
        }

        private void TxtPrimaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
            cmbAccentPreset.SelectedIndex = 0; // Mark as custom
        }

        private void TxtSecondaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
            cmbAccentPreset.SelectedIndex = 0; // Mark as custom
        }

        private void UpdateColorPreviews()
        {
            try
            {
                if (txtPrimaryColor != null)
                {
                    var primary = (Color)ColorConverter.ConvertFromString(txtPrimaryColor.Text);
                    primaryColorPreview.Background = new SolidColorBrush(primary);
                }
            }
            catch { }

            try
            {
                if (txtSecondaryColor != null)
                {
                    var secondary = (Color)ColorConverter.ConvertFromString(txtSecondaryColor.Text);
                    secondaryColorPreview.Background = new SolidColorBrush(secondary);
                }
            }
            catch { }
        }

        private void BtnApplyAccent_Click(object sender, RoutedEventArgs e)
        {
            ApplyAccentColors();
        }

        private void ApplyAccentColors()
        {
            try
            {
                // Validate hex colors
                var primary = (Color)ColorConverter.ConvertFromString(txtPrimaryColor.Text);
                var secondary = (Color)ColorConverter.ConvertFromString(txtSecondaryColor.Text);

                _core.Settings.AccentColor = txtPrimaryColor.Text;
                _core.Settings.AccentColor2 = txtSecondaryColor.Text;
                _core.SaveSettings();

                // Apply immediately to UI
                ThemeEngine.ApplyTheme(_core.Settings);

                NotificationService.ShowSuccess("Accent colors updated");
            }
            catch (Exception ex)
            {
                NotificationService.ShowWarning($"Invalid color format: {ex.Message}");
            }
        }

        private void ChkVerboseNotifications_Click(object sender, RoutedEventArgs e)
        {
            _core.Settings.VerboseNotifications = chkVerboseNotifications.IsChecked == true;
            _core.SaveSettings();
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            string logPath = Path.Combine(_core.AppDataPath, "TMM.log");
            if (File.Exists(logPath))
                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
            else
                NotificationService.ShowInfo("Log file does not exist yet.", "Settings");
        }

        private void BtnWipeCache_Click(object sender, RoutedEventArgs e)
        {
            _core.Log("User initiated manual download cache wipe.");
            try
            {
                _core.WipeDownloadCache();
                Directory.CreateDirectory(_core.DownloadCachePath);
                NotificationService.ShowSuccess("Temporary cache wiped successfully");
            }
            catch (Exception ex)
            {
                _core.Log($"Cache wipe failed: {ex.Message}");
                NotificationService.ShowWarning("Cache wipe failed — close any open mod folders and try again");
            }
        }

        private void BtnFactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will wipe all settings and game paths. The app will relaunch.\n\nContinue?",
                "Factory Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _core.Log("Initiating Factory Reset...");
            try { _core.FactoryReset(); }
            catch (Exception ex) { _core.Log($"FactoryReset error (non-fatal): {ex.Message}"); }

            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
                catch (Exception ex) { _core.Log($"Restart failed: {ex.Message}"); }
            }
            Environment.Exit(0);
        }
    }
}
