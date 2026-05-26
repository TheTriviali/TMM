using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    public partial class Step1_GameDetailsPage : UserControl, IWizardStep
    {
        public event EventHandler? ValidationChanged;

        public bool IsValid { get; private set; }

        public Step1_GameDetailsPage()
        {
            InitializeComponent();
        }

        public void LoadProfile(CustomGameProfile profile)
        {
            txtGameName.Text   = profile.GameName;
            txtInstallDir.Text = profile.GameDirectory;
            txtExePath.Text    = profile.ExePath ?? "";
            txtSteamAppId.Text = profile.SteamAppId ?? "";
            ValidateInputs(null, null);
        }

        public void SaveProfile(CustomGameProfile profile)
        {
            profile.GameName     = txtGameName.Text.Trim();
            profile.GameDirectory = txtInstallDir.Text.Trim();
            profile.ExePath      = NullIfBlank(txtExePath.Text);
            profile.SteamAppId   = NullIfBlank(txtSteamAppId.Text);
        }

        private void ValidateInputs(object? sender, TextChangedEventArgs? e)
        {
            bool nameOk = !string.IsNullOrWhiteSpace(txtGameName?.Text);
            string dir  = txtInstallDir?.Text?.Trim() ?? "";
            bool dirOk  = !string.IsNullOrEmpty(dir) && Directory.Exists(dir);

            bool steamOk = true;
            string steamText = txtSteamAppId?.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(steamText) && !uint.TryParse(steamText, out _))
            {
                steamOk = false;
                if (txtSteamIdError != null) txtSteamIdError.Visibility = Visibility.Visible;
            }
            else
            {
                if (txtSteamIdError != null) txtSteamIdError.Visibility = Visibility.Collapsed;
            }

            if (txtDirStatus != null)
            {
                if (string.IsNullOrEmpty(dir))
                {
                    txtDirStatus.Text = "";
                }
                else if (dirOk)
                {
                    txtDirStatus.Text       = "✓ Directory found";
                    txtDirStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                }
                else
                {
                    txtDirStatus.Text       = "✗ Directory not found";
                    txtDirStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x70));
                }
            }

            bool wasValid = IsValid;
            IsValid = nameOk && dirOk && steamOk;
            if (IsValid != wasValid) ValidationChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Game Install Directory",
                InitialDirectory = string.IsNullOrEmpty(txtInstallDir.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    : txtInstallDir.Text
            };
            if (dlg.ShowDialog() != true) return;

            txtInstallDir.Text = dlg.FolderName;
            if (string.IsNullOrWhiteSpace(txtGameName.Text))
                txtGameName.Text = Path.GetFileName(dlg.FolderName);
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = txtInstallDir.Text.Trim();
            var dlg = new OpenFileDialog
            {
                Title  = "Select Game Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = Directory.Exists(gameDir)
                    ? gameDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            if (dlg.ShowDialog() != true) return;

            string exePath = dlg.FileName;
            if (!string.IsNullOrEmpty(gameDir) &&
                exePath.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                exePath = exePath[gameDir.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            txtExePath.Text = exePath;
        }

        private static string? NullIfBlank(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
