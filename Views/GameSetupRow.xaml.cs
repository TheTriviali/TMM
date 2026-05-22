using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// One-stop UI block for displaying and configuring the path / status of
    /// a single game. Used by both InitialSetupWindow (full action set) and
    /// SettingsWindow (display-only via ShowActions=false).
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
                btnSteam.Visibility = vis;
            }
        }

        /// <summary>Fired after the user changes the path (browse / scan).</summary>
        public event EventHandler? PathChanged;

        /// <summary>Refreshes the displayed path, status text, and clone button visibility.</summary>
        public async Task RefreshAsync()
        {
            if (_core == null || _profile == null) return;

            string path = _core.GetVanillaPath(_profile) ?? "";
            txtPath.Text = path;

            if (string.IsNullOrEmpty(path))
            {
                lblStatus.Text = "Not Found / Not Installed";
                lblStatus.Foreground = Brushes.Gray;
                return;
            }

            if (!Directory.Exists(path) || Directory.GetFileSystemEntries(path).Length == 0)
            {
                lblStatus.Text = "âš ï¸ Ghost Install: Folder is missing or empty!";
                lblStatus.Foreground = Brushes.Red;
                return;
            }

            var state = await _core.VerifyGameStatusAsync(_profile);
            if (state == ExeStatus.Vanilla)
            {
                lblStatus.Text = "Steam API Detected (1.0 Downgrade Required for Virtual Mode)";
                lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(216, 163, 26));
            }
            else
            {
                lblStatus.Text = "Ready for Modding";
                lblStatus.Foreground = new SolidColorBrush(Color.FromRgb(78, 201, 176));
            }

        }

        // ---------- Action handlers ----------

        private async void BtnQuickScan_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || _profile == null) return;

            lblStatus.Text = "Running Quick Scan...";
            lblStatus.Foreground = Brushes.Cyan;

            await Task.Run(() => _core.QuickScan());
            await RefreshAsync();
            PathChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || _profile == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = $"Executable|{_profile.ExeName}",
                Title = $"Locate {_profile.ExeName}"
            };
            if (dialog.ShowDialog() != true) return;

            string? folder = Path.GetDirectoryName(dialog.FileName);
            if (string.IsNullOrEmpty(folder)) return;

            _core.SetVanillaPath(_profile, folder);
            await RefreshAsync();
            PathChanged?.Invoke(this, EventArgs.Empty);
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
