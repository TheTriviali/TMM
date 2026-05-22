using System;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class DebugConsoleWindow : Window
    {
        private readonly BackendCore _core;

        public DebugConsoleWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();
            txtLog.AppendText($"[Alpha Diagnostics - {DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n");
            cmbGame.SelectedIndex = 0;
        }

        private void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            if (cmbGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;
            txtLog.AppendText($"Invoking Steam Protocol for AppID {profile.SteamAppId}...\n");
            SteamLauncher.Validate(profile, msg => txtLog.AppendText(msg + "\n"));
        }

        private async void BtnMd5_Click(object sender, RoutedEventArgs e)
        {
            if (cmbGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;
            txtLog.AppendText($"\n[MD5 Check - {profile.DisplayName}]\n");
            string result = await _core.GetMd5DiagnosticsAsync(profile);
            txtLog.AppendText(result + "\n");
            txtLog.ScrollToEnd();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
