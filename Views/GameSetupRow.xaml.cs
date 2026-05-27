using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// One-stop UI block for displaying and configuring the path / status of
    /// a single game. Used by InitialSetupWindow, SettingsWindow, and setup dialogs.
    /// </summary>
    public partial class GameSetupRow : UserControl
    {
        private BackendCore? _core;
        private GameProfile? _profile;
        private bool _showActions = true;

        public GameSetupRow()
        {
            InitializeComponent();
        }

        /// <summary>Bind this row to a game profile + backend. Required.</summary>
        public void Bind(BackendCore core, GameProfile profile)
        {
            _core = core;
            _profile = profile;
            lblTitle.Text = profile.DisplayName;

            // Hide the Steam button for games that have no Steam AppId
            // (e.g. TLaD and TBoGT are part of the IV install, no separate app).
            if (string.IsNullOrEmpty(profile.SteamAppId))
                btnSteam.Visibility = Visibility.Collapsed;

            _ = RefreshAsync();
        }

        /// <summary>
        /// When false, the row displays only the path + status (used by SettingsWindow).
        /// </summary>
        public bool ShowActions
        {
            get => _showActions;
            set
            {
                _showActions = value;
                var vis = value ? Visibility.Visible : Visibility.Collapsed;
                btnQuickScan.Visibility = vis;
                btnBrowse.Visibility = vis;
                // Steam button only visible when actions are shown AND profile has a Steam AppId.
                btnSteam.Visibility = (value && !string.IsNullOrEmpty(_profile?.SteamAppId))
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>Fired after the user changes the path (browse / scan).</summary>
        public event EventHandler? PathChanged;

        /// <summary>
        /// Fired when this row's path change caused sibling game paths to be
        /// auto-derived (IV browse → TLaD / TBoGT). Parent windows can use this
        /// to refresh adjacent rows without a full re-scan.
        /// </summary>
        public event EventHandler? LinkedPathsChanged;

        /// <summary>Refreshes the displayed path, status text, and status colour.</summary>
        public Task RefreshAsync()
        {
            if (_core == null || _profile == null) return Task.CompletedTask;

            string path = _core.GetVanillaPath(_profile) ?? "";
            txtPath.Text = path;

            var loc = LocalizationService.Instance;

            if (string.IsNullOrEmpty(path))
            {
                lblStatus.Text = loc["GameSetupRow_NotFound"];
                lblStatus.Foreground = Brushes.Gray;
            }
            else if (!Directory.Exists(path) || Directory.GetFileSystemEntries(path).Length == 0)
            {
                lblStatus.Text = loc["GameSetupRow_GhostInstall"];
                lblStatus.Foreground = Brushes.Red;
            }
            else
            {
                lblStatus.Text = loc["GameSetupRow_Ready"];
                lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
            }

            return Task.CompletedTask;
        }

        // ── Action handlers ───────────────────────────────────────────────────

        private async void BtnQuickScan_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || _profile == null) return;

            lblStatus.Text = LocalizationService.Instance["GameSetupRow_Scanning"];
            lblStatus.Foreground = Brushes.Cyan;

            await Task.Run(() => _core.QuickScan());
            await RefreshAsync();
            PathChanged?.Invoke(this, EventArgs.Empty);

            // IV quick-scan may have auto-derived TLaD/TBoGT
            if (GameProfile.IvFamilyKeys.Contains(_profile.Key))
                LinkedPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || _profile == null) return;

            string filter = string.IsNullOrEmpty(_profile.ExeName)
                ? "Executable|*.exe"
                : $"Executable|{_profile.ExeName}";

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                Title = $"Locate {_profile.DisplayName} — select the game executable"
            };
            if (dialog.ShowDialog() != true) return;

            string? folder = Path.GetDirectoryName(dialog.FileName);
            if (string.IsNullOrEmpty(folder)) return;

            // SetVanillaPath auto-derives TLaD/TBoGT when profile is IV.
            _core.SetVanillaPath(_profile, folder);
            await RefreshAsync();
            PathChanged?.Invoke(this, EventArgs.Empty);

            if (_profile.Key == "IV")
                LinkedPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BtnSteam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            var menu = (ContextMenu)Resources["SteamMenu"];
            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void MenuSteamAction_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || _profile == null) return;
            if (sender is not MenuItem item || item.Tag is not string action) return;

            SteamLauncher.Invoke(action, _profile.SteamAppId, _core.Log);
        }
    }
}
