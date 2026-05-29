using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    public partial class Step1_GameDetailsPage : UserControl, IWizardStep
    {
        public event EventHandler? ValidationChanged;

        public bool IsValid { get; private set; }

        private readonly ObservableCollection<string> _md5s = new();

        public Step1_GameDetailsPage()
        {
            InitializeComponent();
            icMd5List.ItemsSource = _md5s;
        }

        public void LoadProfile(CustomGameProfile profile)
        {
            txtGameName.Text   = profile.GameName;
            txtInstallDir.Text = profile.GameDirectory;
            txtExePath.Text    = profile.ExePath ?? "";
            txtSteamAppId.Text = profile.SteamAppId ?? "";
            txtNexusSlug.Text  = profile.NexusSlug ?? "";
            txtOverlayFolders.Text = string.Join(", ", profile.OverlayFolders);
            txtCompanionSiblings.Text = string.Join(Environment.NewLine,
                profile.CompanionSiblings.Select(kvp =>
                    $"{kvp.Key} = {string.Join(", ", kvp.Value)}"));
            txtSearchHints.Text = string.Join(Environment.NewLine, profile.SearchHints);

            txtExpectedBytes.Text = profile.ExpectedExeBytes?.ToString() ?? "";
            _md5s.Clear();
            foreach (var h in profile.AcceptedExeMd5s) _md5s.Add(h);

            ValidateInputs(null, null);
        }

        public void SaveProfile(CustomGameProfile profile)
        {
            profile.GameName      = txtGameName.Text.Trim();
            profile.GameDirectory = txtInstallDir.Text.Trim();
            profile.ExePath       = NullIfBlank(txtExePath.Text);
            profile.SteamAppId    = NullIfBlank(txtSteamAppId.Text);
            profile.NexusSlug     = NullIfBlank(txtNexusSlug.Text);
            profile.OverlayFolders = ParseCsvList(txtOverlayFolders.Text);
            profile.CompanionSiblings = ParseCompanionMap(txtCompanionSiblings.Text);
            profile.SearchHints = ParseLineList(txtSearchHints.Text);

            string sizeText = txtExpectedBytes.Text.Trim();
            profile.ExpectedExeBytes = long.TryParse(sizeText, out long bytes) && bytes > 0
                ? bytes : null;

            profile.AcceptedExeMd5s = new System.Collections.Generic.List<string>(_md5s);
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

        private static System.Collections.Generic.List<string> ParseCsvList(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? new System.Collections.Generic.List<string>()
                : text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .Where(x => !string.IsNullOrWhiteSpace(x))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();

        // Newline-only split — search-hint paths may legitimately contain commas/spaces.
        private static System.Collections.Generic.List<string> ParseLineList(string? text) =>
            string.IsNullOrWhiteSpace(text)
                ? new System.Collections.Generic.List<string>()
                : text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                      .Select(x => x.Trim())
                      .Where(x => !string.IsNullOrWhiteSpace(x))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList();

        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> ParseCompanionMap(string? text)
        {
            var map = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text)) return map;

            foreach (string line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
                if (parts.Length != 2) continue;

                string key = parts[0].Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;

                var siblings = parts[1]
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (siblings.Count > 0)
                    map[key] = siblings;
            }

            return map;
        }

        // ── Integrity handlers ────────────────────────────────────────────────

        private async void BtnAutoDetect_Click(object sender, RoutedEventArgs e)
        {
            string? resolved = ResolveExePath();
            if (resolved is null || !File.Exists(resolved))
            {
                NotificationService.ShowWarning(
                    "Set a valid install directory and executable path first.");
                return;
            }

            btnAutoDetect.IsEnabled = false;
            try
            {
                long size = new FileInfo(resolved).Length;
                txtExpectedBytes.Text = size.ToString();

                string md5 = await IntegrityChecker.ComputeMd5Async(resolved);
                if (!_md5s.Contains(md5)) _md5s.Add(md5);

                NotificationService.ShowSuccess($"Detected: {size:N0} bytes, MD5 {md5[..8]}…");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Could not read exe: {ex.Message}");
            }
            finally
            {
                btnAutoDetect.IsEnabled = true;
            }
        }

        private void BtnAddMd5_Click(object sender, RoutedEventArgs e) => AddMd5FromInput();

        private void TxtNewMd5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) AddMd5FromInput();
        }

        private void AddMd5FromInput()
        {
            string val = txtNewMd5.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(val)) return;
            // Strip whitespace/dashes that some hash generators include
            val = val.Replace(" ", "").Replace("-", "");
            if (val.Length != 32 || !IsHex(val))
            {
                NotificationService.ShowWarning("MD5 must be 32 hex characters.");
                return;
            }
            if (!_md5s.Contains(val)) _md5s.Add(val);
            txtNewMd5.Clear();
        }

        private void BtnRemoveMd5_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string hash }) _md5s.Remove(hash);
        }

        private static bool IsHex(string s)
        {
            foreach (char c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }

        /// <summary>Resolves the absolute path to the exe from current form state.</summary>
        private string? ResolveExePath()
        {
            string dir = txtInstallDir.Text.Trim();
            string exe = txtExePath.Text.Trim();
            if (string.IsNullOrEmpty(exe)) return null;
            return Path.IsPathRooted(exe)
                ? exe
                : string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, exe);
        }
    }
}
