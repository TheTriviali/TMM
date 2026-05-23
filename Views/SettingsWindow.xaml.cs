using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TMM
{
    public partial class SettingsWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public SettingsWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();
        }

        private void BtnRerunSetup_Click(object sender, RoutedEventArgs e)
        {
            new InitialSetupWindow(_core) { Owner = Owner }.ShowDialog();
            _ = rowIII.RefreshAsync();
            _ = rowVC.RefreshAsync();
            _ = rowSA.RefreshAsync();
            _ = rowIV.RefreshAsync();
            _ = rowTLaD.RefreshAsync();
            _ = rowTBoGT.RefreshAsync();
        }

        private void BtnSteamAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (cmbSteamGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;
            SteamLauncher.Invoke(btn.Tag.ToString()!, profile.SteamAppId, _core.Log);
        }

        private async void BtnMd5Check_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSteamGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;

            string result = await _core.GetMd5DiagnosticsAsync(profile);
            MessageBox.Show(result, $"MD5 Check - {profile.DisplayName}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            string logPath = Path.Combine(_core.AppDataPath, "TMM.log");
            if (File.Exists(logPath))
                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
            else
                MessageBox.Show("Log file does not exist yet.", "No Logs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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
                NotificationService.ShowWarning("Cache wipe failed - close any open mod folders and try again");
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

            // Start the new instance first, then shut down this one.
            // Environment.Exit bypasses all WPF shutdown hooks, avoiding the
            // Application.Current null race that occurs when Shutdown() is called
            // while resources are already in a torn-down state.
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
