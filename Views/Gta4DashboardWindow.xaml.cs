using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Three-column dashboard for GTA IV, TLaD, and TBoGT.
    /// Each column manages its own mod list, search filter, and per-column deploy/rollback/launch.
    /// The toolbar has Deploy All, Rollback Active, and Play buttons for each episode.
    /// ASI routing is handled transparently by BackendCore via ConditionalRoutes on each GameProfile.
    /// </summary>
    public partial class Gta4DashboardWindow : TmmWindow
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly BackendCore _core;
        private readonly (GameProfile Profile, ObservableCollection<ModItem> Mods)[] _episodes;

        private Point    _startPoint;
        private ModItem? _draggedItem;
        private ListView? _activeList;          // tracks which column has focus
        private CancellationTokenSource? _deployCts;

        // Per-episode deploy pending flags (IV=0, TLaD=1, TBoGT=2)
        private readonly bool[] _pendingByEpisode = { true, true, true };

        // ── Construction ──────────────────────────────────────────────────────────

        public Gta4DashboardWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            _episodes = new[]
            {
                (GameProfile.IV,    core.Mods["IV"]),
                (GameProfile.TLaD,  core.Mods["TLaD"]),
                (GameProfile.TBoGT, core.Mods["TBoGT"])
            };

            listIV.ItemsSource    = _episodes[0].Mods;
            listTLaD.ItemsSource  = _episodes[1].Mods;
            listTBoGT.ItemsSource = _episodes[2].Mods;

            _activeList = listIV;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            await _core.RefreshAllModListsAsync();
            UpdatePathLabels();
            UpdateDeployButtons();
            UpdateStatusDots();
            UpdateLaunchButtons();
            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        private void UpdatePathLabels()
        {
            txtPathIV.Text    = _core.GetVanillaPath(GameProfile.IV)    ?? "Path not set";
            txtPathTLaD.Text  = _core.GetVanillaPath(GameProfile.TLaD)  ?? "Path not set";
            txtPathTBoGT.Text = _core.GetVanillaPath(GameProfile.TBoGT) ?? "Path not set";
        }

        private void UpdateDeployButtons()
        {
            UpdateEpisodeDeployButton(btnDeployIV,    GameProfile.IV,    0);
            UpdateEpisodeDeployButton(btnDeployTLaD,  GameProfile.TLaD,  1);
            UpdateEpisodeDeployButton(btnDeployTBoGT, GameProfile.TBoGT, 2);
        }

        private void UpdateEpisodeDeployButton(Button btn, GameProfile profile, int episodeIdx)
        {
            bool ready    = _core.IsGameReady(profile);
            bool pending  = _pendingByEpisode[episodeIdx];
            bool hasMods  = _core.Mods[profile.Key].Any(m => m.IsEnabled);

            btn.IsEnabled = ready;

            if (!ready || !hasMods)
                btn.Background = UiColors.DisabledBrush;
            else if (pending)
                btn.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
            else
                btn.Background = UiColors.DisabledBrush;
        }

        private int EpisodeIndex(string key) => key switch { "IV" => 0, "TLaD" => 1, "TBoGT" => 2, _ => 0 };

        private void UpdateStatusDots()
        {
            SetDotColor(dotIV,    _core.IsGameReady(GameProfile.IV));
            SetDotColor(dotTLaD,  _core.IsGameReady(GameProfile.TLaD));
            SetDotColor(dotTBoGT, _core.IsGameReady(GameProfile.TBoGT));
        }

        private static void SetDotColor(System.Windows.Shapes.Ellipse dot, bool ready)
        {
            dot.Fill = new SolidColorBrush(ready ? UiColors.ReadyGreen : UiColors.NotReadyRed);
        }

        private void UpdateLaunchButtons()
        {
            UpdateLaunchButton(btnLaunchIV,    btnPlayIV,    GameProfile.IV);
            UpdateLaunchButton(btnLaunchTLaD,  btnPlayTLaD,  GameProfile.TLaD);
            UpdateLaunchButton(btnLaunchTBoGT, btnPlayTBoGT, GameProfile.TBoGT);
        }

        private void UpdateLaunchButton(Button colBtn, Button toolbarBtn, GameProfile profile)
        {
            string? dir = _core.GetVanillaPath(profile);
            bool canLaunch = !string.IsNullOrEmpty(dir)
                          && Directory.Exists(dir)
                          && File.Exists(Path.Combine(dir, profile.ExeName));

            // Column footer launch button: show/hide
            colBtn.Visibility = canLaunch ? Visibility.Visible : Visibility.Collapsed;

            // Toolbar play button: tint green/red based on readiness
            toolbarBtn.Background = new SolidColorBrush(canLaunch ? UiColors.ReadyGreen : UiColors.NotReadyRed);
        }

        // ── Window chrome ─────────────────────────────────────────────────────────

        private new void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                BtnMaximize_Click(sender, e);
            else if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            MainWindowBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
        }

        // ── Toolbar ───────────────────────────────────────────────────────────────

        private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            SidebarBorder.Visibility = SidebarBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "For help and documentation, visit the TMM GitHub repository.\n\nOpen in browser?",
                "Help & Resources", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
                ShellHelper.OpenUrl("https://github.com/noahd179/tgtamm");
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            new AboutWindow(_core) { Owner = this }.ShowDialog();
        }

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
                ShellHelper.OpenUrl(url);
        }

        private async void BtnInstallMod_Click(object sender, RoutedEventArgs e)
        {
            // If a specific list has focus, default to that episode; otherwise ask.
            var defaultProfile = ResolveProfileFromList(_activeList);
            GameProfile? profile = defaultProfile;

            var picker = new EpisodePicker(
                defaultProfile != null
                    ? $"Install mod for (default: {defaultProfile.ShortName}):"
                    : "Install mod for:",
                _episodes.Select(ep => ep.Profile).ToArray());

            if (picker.ShowDialog() != true || picker.SelectedProfile == null) return;
            profile = picker.SelectedProfile;

            string rawFolder = Path.Combine(_core.AppDataPath, profile.RawFolderName);

            var dlg = new OpenFileDialog
            {
                Title       = $"Install Mod for {profile.DisplayName}",
                Filter      = "Mod Archives & Files|*.zip;*.rar;*.7z;*.asi;*.dll;*.ini;*.dat|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var file in dlg.FileNames)
                await InstallModFileAsync(file, rawFolder, profile);

            await RefreshAsync();
        }

        private async Task InstallModFileAsync(string filePath, string rawFolder, GameProfile profile)
        {
            string ext      = Path.GetExtension(filePath).ToLowerInvariant();
            string modName  = Path.GetFileNameWithoutExtension(filePath);
            string destDir  = Path.Combine(rawFolder, modName);
            Directory.CreateDirectory(destDir);

            try
            {
                if (ext is ".zip" or ".rar" or ".7z")
                    await BackendCore.ExtractArchiveSafeAsync(filePath, destDir, CancellationToken.None);
                else
                    File.Copy(filePath, Path.Combine(destDir, Path.GetFileName(filePath)), overwrite: true);

                var item = new ModItem
                {
                    Name          = modName,
                    IsEnabled     = true,
                    LoadOrder     = _core.Mods[profile.Key].Count,
                    RawFolderPath = destDir
                };
                SyncModInfoToFolder(item);
                NotificationService.ShowSuccess($"Installed '{modName}' for {profile.ShortName}.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to install '{modName}': {ex.Message}");
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void BtnRescanPaths_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => _core.QuickScan());
            await RefreshAsync();
            NotificationService.ShowInfo("Paths rescanned.");
        }

        private async void BtnDeployAll_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            foreach (var (profile, _) in _episodes)
            {
                if (!_core.IsGameReady(profile)) continue;
                await RunDeployAsync(profile, null);
                count++;
            }
            if (count == 0)
                MessageBox.Show("No IV episodes are configured. Set game paths via the sidebar browse buttons.",
                    "Nothing to Deploy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnRollbackActive_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetActiveProfile();
            var manifests = _core.GetRollbackManifests(profile.Key);
            if (manifests.Count == 0)
            {
                NotificationService.ShowInfo($"No rollback snapshots exist for {profile.DisplayName}.");
                return;
            }

            var latest = manifests[0];
            string info = $"Rollback {profile.DisplayName} to snapshot from {latest.Timestamp}?\n\n" +
                          $"Mods: {string.Join(", ", latest.ModNames.Take(5))}" +
                          (latest.ModNames.Count > 5 ? $" (+{latest.ModNames.Count - 5} more)" : "");

            if (MessageBox.Show(info, "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var btn = (Button)sender;
            btn.IsEnabled = false;

            try
            {
                var progress = new Progress<DeploymentProgress>(_ => { });
                await _core.RollbackDeployAsync(latest, progress);
                NotificationService.ShowSuccess($"{profile.DisplayName} rolled back.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Rollback failed: {ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void BtnOpenAppData_Click(object sender, RoutedEventArgs e) => _core.OpenAppData();

        private async void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow(_core) { Owner = this }.ShowDialog();
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
            await RefreshAsync();
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            new ThemeManagerWindow(_core) { Owner = this }.ShowDialog();
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
        }

        private void BtnRollTheme_Click(object sender, RoutedEventArgs e)
        {
            var presets = ThemeManagerWindow.BuiltInPresets;
            if (presets.Count == 0) return;
            var preset = presets[new Random().Next(presets.Count)];

            _core.Settings.AccentColor    = preset.AccentColor;
            _core.Settings.BgColor        = preset.BgColor;
            _core.Settings.ColorMode      = preset.ColorMode;
            _core.Settings.TitlebarTheme  = preset.TitlebarTheme;
            _core.Settings.FontFamily     = preset.FontFamily;
            _core.Settings.MicaEnabled    = preset.MicaEnabled;
            _core.Settings.LastPresetName = preset.Name;
            _core.SaveSettings();

            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
        }

        // ── Notification toasts ───────────────────────────────────────────────────

        private void Toast_Close(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        // ── Per-column browse (sidebar) ───────────────────────────────────────────

        private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            var dlg = new OpenFolderDialog
            {
                Title = $"Select {profile.DisplayName} directory"
            };

            string? current = _core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                dlg.InitialDirectory = current;

            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FolderName)) return;

            _core.SetVanillaPath(profile, dlg.FolderName);
            _core.SaveSettings();
            await RefreshAsync();
        }

        // ── Deploy / Rollback / Launch ────────────────────────────────────────────

        private async void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            await RunDeployAsync(profile, btn);
        }

        private async Task RunDeployAsync(GameProfile profile, Button? btn)
        {
            string? gameDir = _core.GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show(
                    $"Game directory for {profile.DisplayName} is not set or missing.\n\n" +
                    "Use the browse button in the sidebar to set it.",
                    "Directory Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _deployCts?.Cancel();
            _deployCts = new CancellationTokenSource();
            if (btn != null) btn.IsEnabled = false;

            // Show overlay
            DialogOverlay.Visibility      = Visibility.Visible;
            DeployProgressPanel.Visibility = Visibility.Visible;
            txtDeployStage.Text            = $"Deploying {profile.ShortName}...";
            pbDeploy.IsIndeterminate       = true;
            txtDeployCount.Text            = "";

            try
            {
                var progress = new Progress<DeploymentProgress>(p =>
                {
                    txtDeployStage.Text = $"[{profile.ShortName}] {p.Stage}";
                    if (p.Total > 0)
                    {
                        pbDeploy.IsIndeterminate = false;
                        pbDeploy.Maximum = p.Total;
                        pbDeploy.Value   = p.Current;
                        txtDeployCount.Text = $"{p.Current} / {p.Total}";
                    }
                });

                await _core.DeployModsAsync(profile, _core.Mods[profile.Key], progress, _deployCts.Token);
                _pendingByEpisode[EpisodeIndex(profile.Key)] = false;
                UpdateDeployButtons();
                NotificationService.ShowSuccess($"{profile.DisplayName} deployed successfully.");
            }
            catch (OperationCanceledException)
            {
                NotificationService.ShowWarning("Deploy cancelled.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Deploy failed for {profile.DisplayName}: {ex.Message}");
            }
            finally
            {
                DialogOverlay.Visibility      = Visibility.Collapsed;
                DeployProgressPanel.Visibility = Visibility.Collapsed;
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private async void BtnRollback_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            var manifests = _core.GetRollbackManifests(profile.Key);
            if (manifests.Count == 0)
            {
                NotificationService.ShowInfo($"No rollback snapshots for {profile.DisplayName}.");
                return;
            }

            var latest = manifests[0];
            string info = $"Rollback {profile.DisplayName} to snapshot from {latest.Timestamp}?\n\n" +
                          $"Mods: {string.Join(", ", latest.ModNames.Take(5))}" +
                          (latest.ModNames.Count > 5 ? $" (+{latest.ModNames.Count - 5} more)" : "");

            if (MessageBox.Show(info, "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            btn.IsEnabled = false;

            try
            {
                var progress = new Progress<DeploymentProgress>(_ => { });
                await _core.RollbackDeployAsync(latest, progress);
                NotificationService.ShowSuccess($"{profile.DisplayName} rolled back.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Rollback failed: {ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            string? gameDir = _core.GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show("Game directory is not set or missing.", "Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string exe = Path.Combine(gameDir, profile.ExeName);
            if (!File.Exists(exe))
            {
                MessageBox.Show($"{profile.ExeName} was not found in the game directory.", "Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gameDir });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch: {ex.Message}", "Launch Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────────────

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F2)          { MenuRename_Click(null, null!); e.Handled = true; }
            else if (e.Key == Key.Space)  { MenuToggle_Click(null, null!); e.Handled = true; }
            else if (e.Key == Key.Delete) { MenuDelete_Click(null, null!); e.Handled = true; }
            else if (e.Key == Key.F5)     { BtnDeployAll_Click(null!, null!); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up)        { MenuMoveUp_Click(null, null!);   e.Handled = true; }
                else if (e.Key == Key.Down) { MenuMoveDown_Click(null, null!); e.Handled = true; }
            }
            base.OnKeyDown(e);
        }

        private void List_KeyDown(object sender, KeyEventArgs e) => OnKeyDown(e);

        // ── Search / filter ───────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb || tb.Tag is not string key) return;
            ApplySearchFilter(key, tb.Text);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            TextBox? tb = key switch
            {
                "IV"    => txtSearchIV,
                "TLaD"  => txtSearchTLaD,
                "TBoGT" => txtSearchTBoGT,
                _       => null
            };
            if (tb != null) tb.Text = "";
        }

        private void ApplySearchFilter(string key, string text)
        {
            var v = CollectionViewSource.GetDefaultView(_core.Mods[key]);
            v.Filter = string.IsNullOrWhiteSpace(text)
                ? null
                : i => ((ModItem)i).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        // ── Mod list interaction ──────────────────────────────────────────────────

        private void List_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ListView lv) _activeList = lv;
        }

        private void ModCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: ModItem item })
            {
                SyncModInfoToFolder(item);
                // Flag whichever episode contains this mod as pending
                for (int i = 0; i < _episodes.Length; i++)
                {
                    if (_episodes[i].Mods.Contains(item))
                    {
                        _pendingByEpisode[i] = true;
                        UpdateDeployButtons();
                        break;
                    }
                }
            }
        }

        // ── Context menu ──────────────────────────────────────────────────────────

        private void MenuRename_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            var win = new RenameWindow(mod.Name) { Owner = this };
            if (win.ShowDialog() == true)
            {
                mod.Name = win.NewName;
                SyncModInfoToFolder(mod);
            }
        }

        private void MenuToggle_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            FlagEpisodePending();
        }

        private void MenuDelete_Click(object? sender, RoutedEventArgs e)
        {
            if (_activeList == null || _activeList.Tag is not string key) return;

            var mods     = _core.Mods[key];
            var selected = _activeList.SelectedItems.Cast<ModItem>().ToList();

            if (selected.Count == 0) return;
            if (MessageBox.Show($"Delete {selected.Count} mod(s)?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            foreach (var m in selected)
            {
                try
                {
                    if (Directory.Exists(m.RawFolderPath))
                        BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                    mods.Remove(m);
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error deleting '{m.Name}': {ex.Message}");
                }
            }

            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        private void MenuMoveUp_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            var list = GetActiveModList();
            int idx = list.IndexOf(mod);
            if (idx <= 0) return;
            var other = list[idx - 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            list.Move(idx, idx - 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            FlagEpisodePending();
        }

        private void MenuMoveDown_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            var list = GetActiveModList();
            int idx = list.IndexOf(mod);
            if (idx < 0 || idx >= list.Count - 1) return;
            var other = list[idx + 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            list.Move(idx, idx + 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            FlagEpisodePending();
        }

        private void FlagEpisodePending()
        {
            int idx = EpisodeIndex(GetActiveProfile().Key);
            _pendingByEpisode[idx] = true;
            UpdateDeployButtons();
        }

        private void MenuSetLoadOrder_Click(object sender, RoutedEventArgs e)
        {
            var mod  = GetSelectedMod();
            var list = GetActiveModList();
            if (mod == null || list.Count == 0) return;

            int maxOrder = list.Count - 1;
            var win = new RenameWindow(mod.LoadOrder.ToString())
            {
                Title = $"Set Load Order (0–{maxOrder})",
                Owner = this
            };
            if (win.ShowDialog() != true || !int.TryParse(win.NewName, out int newOrder)) return;

            newOrder = Math.Clamp(newOrder, 0, maxOrder);
            list.Remove(mod);
            list.Insert(newOrder, mod);
            for (int i = 0; i < list.Count; i++) list[i].LoadOrder = i;

            var sorted = list.OrderBy(x => x.LoadOrder).ToList();
            list.Clear();
            foreach (var m in sorted) list.Add(m);
            foreach (var m in list) SyncModInfoToFolder(m);
            FlagEpisodePending();
        }

        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null && Directory.Exists(mod.RawFolderPath))
                ShellHelper.OpenFolder(mod.RawFolderPath);
        }

        private void MenuOpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetActiveProfile();
            string? path = _core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                ShellHelper.OpenFolder(path);
            else
                MessageBox.Show($"Game folder for {profile.DisplayName} is not set or missing.",
                    "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetActiveProfile();
            string path = Path.Combine(_core.BackupsPath, profile.Key);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuProperties_Click(object sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            new ModPropertiesWindow(mod) { Owner = this }.ShowDialog();
        }

        // ── Drag-and-drop reorder ─────────────────────────────────────────────────

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            if (sender is ListView lv)
            {
                _activeList = lv;
                if (e.OriginalSource is FrameworkElement el && el.DataContext is ModItem m)
                    _draggedItem = m;
            }
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _startPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _startPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            if (sender is ListView lv)
                DragDrop.DoDragDrop(lv, _draggedItem, DragDropEffects.Move);
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            if (sender is not ListView lv || _draggedItem == null) { e.Effects = DragDropEffects.None; return; }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            var line = GetDropLine(lv);
            if (line == null) return;
            double y = GetInsertionLineY(lv, e.GetPosition(lv).Y);
            Canvas.SetTop(line, y - 1);
            line.Width = Math.Max(lv.ActualWidth - 4, 0);
            Canvas.SetLeft(line, 2);
            line.Visibility = Visibility.Visible;
        }

        private void List_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListView lv) HideDropLine(lv);
            e.Handled = true;
        }

        private void List_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListView lv) HideDropLine(lv);
            if (_draggedItem == null || sender is not ListView targetList) return;
            if (targetList.Tag is not string key) return;

            var (_, mods) = _episodes.FirstOrDefault(ep => ep.Profile.Key == key);
            if (mods == null || !mods.Contains(_draggedItem)) { _draggedItem = null; return; }

            int fromIdx   = mods.IndexOf(_draggedItem);
            int insertIdx = GetInsertionIndex(targetList, e.GetPosition(targetList).Y);
            if (insertIdx > fromIdx) insertIdx--;
            int toIdx = Math.Clamp(insertIdx, 0, mods.Count - 1);

            if (fromIdx == toIdx) { _draggedItem = null; return; }
            mods.Move(fromIdx, toIdx);

            for (int i = 0; i < mods.Count; i++)
            {
                mods[i].LoadOrder = i;
                SyncModInfoToFolder(mods[i]);
            }
            _pendingByEpisode[EpisodeIndex(key)] = true;
            UpdateDeployButtons();
            _draggedItem = null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private System.Windows.Shapes.Rectangle? GetDropLine(ListView lv) => lv.Name switch
        {
            "listIV"    => DropLineIV,
            "listTLaD"  => DropLineTLaD,
            "listTBoGT" => DropLineTBoGT,
            _           => null
        };

        private void HideDropLine(ListView lv)
        {
            var l = GetDropLine(lv);
            if (l != null) l.Visibility = Visibility.Collapsed;
        }

        private static double GetInsertionLineY(ListView lv, double mouseY)
        {
            for (int i = 0; i < lv.Items.Count; i++)
            {
                if (lv.ItemContainerGenerator.ContainerFromIndex(i) is not ListViewItem item) continue;
                var pos = item.TranslatePoint(new Point(0, 0), lv);
                if (mouseY < pos.Y + item.ActualHeight / 2.0) return pos.Y;
            }
            if (lv.Items.Count > 0 &&
                lv.ItemContainerGenerator.ContainerFromIndex(lv.Items.Count - 1) is ListViewItem last)
            {
                var pos = last.TranslatePoint(new Point(0, 0), lv);
                return pos.Y + last.ActualHeight;
            }
            return 0;
        }

        private static int GetInsertionIndex(ListView lv, double mouseY)
        {
            for (int i = 0; i < lv.Items.Count; i++)
            {
                if (lv.ItemContainerGenerator.ContainerFromIndex(i) is not ListViewItem item) continue;
                var pos = item.TranslatePoint(new Point(0, 0), lv);
                if (mouseY < pos.Y + item.ActualHeight / 2.0) return i;
            }
            return lv.Items.Count;
        }

        private ModItem? GetSelectedMod() => _activeList?.SelectedItem as ModItem;

        private ObservableCollection<ModItem> GetActiveModList()
        {
            if (_activeList?.Tag is string key) return _core.Mods[key];
            return _episodes[0].Mods; // fallback: IV
        }

        private GameProfile GetActiveProfile()
        {
            if (_activeList?.Tag is string key)
            {
                var ep = _episodes.FirstOrDefault(ep => ep.Profile.Key == key);
                if (ep.Profile != null) return ep.Profile;
            }
            return GameProfile.IV;
        }

        private static GameProfile? ResolveProfileFromList(ListView? lv) =>
            lv?.Tag is string key ? GameProfile.ByKey(key) : null;

        private static void SyncModInfoToFolder(ModItem mod)
        {
            try
            {
                if (Directory.Exists(mod.RawFolderPath))
                    File.WriteAllText(
                        Path.Combine(mod.RawFolderPath, "modinfo.txt"),
                        JsonSerializer.Serialize(mod, JsonHelper.PrettyOptions));
            }
            catch { /* best effort */ }
        }
    }

    // ── Small helper dialog for picking which IV episode to target ────────────────

    internal class EpisodePicker : Window
    {
        public GameProfile? SelectedProfile { get; private set; }

        public EpisodePicker(string prompt, GameProfile[] profiles)
        {
            Title = "Select Episode";
            Width = 280; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.ToolWindow;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;

            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = prompt,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            foreach (var p in profiles)
            {
                var btn = new Button
                {
                    Content = p.DisplayName,
                    Margin = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(10, 6, 10, 6),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = p
                };
                btn.Click += (_, _) => { SelectedProfile = (GameProfile)btn.Tag; DialogResult = true; Close(); };
                sp.Children.Add(btn);
            }

            Content = sp;
        }
    }
}
