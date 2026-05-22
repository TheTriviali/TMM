using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class DxvkSettingsWindow : Window
    {
        private readonly BackendCore _core;

        public DxvkSettingsWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
            cmbGame.SelectedIndex = 0;
        }

        private void CmbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // chkHud may be null during initial XAML parse; guard against that.
            if (chkHud == null) return;
            chkHud.IsChecked = chkFramerate.IsChecked = chkVsync.IsChecked = false;
            txtRes.Text = "";

            if (cmbGame.SelectedItem is not ComboBoxItem item) return;
            string tag = item.Tag.ToString()!;
            if (tag == "ALL") return;

            var profile = GameProfile.ByKey(tag);
            if (profile == null) return;

            string? gameDir = _core.GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir)) return;

            string confPath = Path.Combine(gameDir, "dxvk.conf");
            if (!File.Exists(confPath)) return;

            string text = File.ReadAllText(confPath);
            chkHud.IsChecked = text.Contains("dxvk.hud = full");
            chkFramerate.IsChecked = text.Contains("dxvk.framerate = 60");
            chkVsync.IsChecked = text.Contains("d3d9.presentInterval = 1");
        }

        private void BtnClose_Click_Header(object sender, RoutedEventArgs e) => Close();

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var lines = new List<string>();
            if (chkHud.IsChecked == true) lines.Add("dxvk.hud = full");
            if (chkFramerate.IsChecked == true) lines.Add("dxvk.framerate = 60");
            if (chkVsync.IsChecked == true) lines.Add("d3d9.presentInterval = 1");
            if (!string.IsNullOrWhiteSpace(txtRes.Text)) lines.Add($"d3d9.customRes = {txtRes.Text}");

            string conf = string.Join("\n", lines);
            string tag = ((ComboBoxItem)cmbGame.SelectedItem).Tag.ToString()!;

            if (tag == "ALL")
            {
                foreach (var p in GameProfile.All)
                {
                    string? dir = _core.GetVanillaPath(p);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        File.WriteAllText(Path.Combine(dir, "dxvk.conf"), conf);
                }
            }
            else
            {
                var profile = GameProfile.ByKey(tag);
                string? dir = profile != null ? _core.GetVanillaPath(profile) : null;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    File.WriteAllText(Path.Combine(dir, "dxvk.conf"), conf);
            }

            Close();
        }
    }
}
