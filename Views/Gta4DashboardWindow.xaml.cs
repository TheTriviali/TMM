using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TMM
{
    /// <summary>
    /// Three-column dashboard for GTA IV, TLaD, and TBoGT.
    /// Each column manages its own mod list and deploy/rollback pipeline.
    /// ASI routing (plugins\ folder check) is handled transparently by BackendCore
    /// via the ConditionalRoutes on each GameProfile.
    /// </summary>
    public partial class Gta4DashboardWindow : Window
    {
        private readonly BackendCore _core;

        // The three profiles and their mod collections.
        private readonly (GameProfile Profile, ObservableCollection<ModItem> Mods)[] _episodes;

        private Point _startPoint;
        private ModItem? _draggedItem;
        private ListView? _dragSource;
        private CancellationTokenSource? _deployCts;

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
            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
            txtStatus.Text = $"IV: {_episodes[0].Mods.Count(m => m.IsEnabled)} enabled  |  " +
                             $"TLaD: {_episodes[1].Mods.Count(m => m.IsEnabled)} enabled  |  " +
                             $"TBoGT: {_episodes[2].Mods.Count(m => m.IsEnabled)} enabled";
        }

        private void UpdatePathLabels()
        {
            txtPathIV.Text    = _core.GetVanillaPath(GameProfile.IV)    ?? "Path not set";
            txtPathTLaD.Text  = _core.GetVanillaPath(GameProfile.TLaD)  ?? "Path not set";
            txtPathTBoGT.Text = _core.GetVanillaPath(GameProfile.TBoGT) ?? "Path not set";
        }

        private void UpdateDeployButtons()
        {
            btnDeployIV.IsEnabled    = _core.IsGameReady(GameProfile.IV);
            btnDeployTLaD.IsEnabled  = _core.IsGameReady(GameProfile.TLaD);
            btnDeployTBoGT.IsEnabled = _core.IsGameReady(GameProfile.TBoGT);
        }

        // ── Window chrome ─────────────────────────────────────────────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
                BtnMaxRestore_Click(sender, e);
            else if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)  => WindowState = WindowState.Minimized;
        private void BtnMaxRestore_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_StateChanged(object sender, EventArgs e)
        {
            btnMaxRestore.Content = WindowState == WindowState.Maximized ? "" : "";
        }

        // ── Toolbar ───────────────────────────────────────────────────────────────

        private async void BtnInstallMod_Click(object sender, RoutedEventArgs e)
        {
            // Show a picker for which episode to install into.
            var picker = new EpisodePicker("Install mod for:", _episodes.Select(ep => ep.Profile).ToArray());
            if (picker.ShowDialog() != true || picker.SelectedProfile == null) return;

            var profile = picker.SelectedProfile;
            string rawFolder = Path.Combine(_core.AppDataPath, profile.RawFolderName);

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Install Mod for {profile.DisplayName}",
                Filter = "Mod Archives & Folders|*.zip;*.rar;*.7z;*.asi;*.dll;*.ini;*.dat|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;

            foreach (var file in dlg.FileNames)
                await InstallModFileAsync(file, rawFolder, profile);

            await RefreshAsync();
        }

        private async Task InstallModFileAsync(string filePath, string rawFolder, GameProfile profile)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string modName = Path.GetFileNameWithoutExtension(filePath);
            string destDir = Path.Combine(rawFolder, modName);
            Directory.CreateDirectory(destDir);

            try
            {
                if (ext is ".zip" or ".rar" or ".7z")
                {
                    await BackendCore.ExtractArchiveSafeAsync(filePath, destDir, CancellationToken.None);
                }
                else
                {
                    File.Copy(filePath, Path.Combine(destDir, Path.GetFileName(filePath)), overwrite: true);
                }

                // Write modinfo
                var item = new ModItem
                {
                    Name = modName,
                    IsEnabled = true,
                    LoadOrder = _core.Mods[profile.Key].Count,
                    RawFolderPath = destDir
                };
                System.IO.File.WriteAllText(
                    Path.Combine(destDir, "modinfo.txt"),
                    System.Text.Json.JsonSerializer.Serialize(item, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                txtStatus.Text = $"Installed '{modName}' for {profile.ShortName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to install '{modName}':\n{ex.Message}", "Install Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void BtnRescanPaths_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Scanning...";
            await Task.Run(() => _core.QuickScan());
            await RefreshAsync();
        }

        // ── Deploy / Rollback ─────────────────────────────────────────────────────

        private async void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            string? gameDir = _core.GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show($"Game directory for {profile.DisplayName} is not set or missing.\n\nUse 'Rescan Paths' or open Setup to configure it.",
                    "Directory Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _deployCts?.Cancel();
            _deployCts = new CancellationTokenSource();
            btn.IsEnabled = false;
            txtStatus.Text = $"Deploying {profile.ShortName}...";

            try
            {
                var progress = new Progress<DeploymentProgress>(p =>
                    txtStatus.Text = $"[{profile.ShortName}] {p.Stage} ({p.Current}/{p.Total})");

                await _core.DeployModsAsync(profile, _core.Mods[profile.Key], progress, _deployCts.Token);
                txtStatus.Text = $"{profile.DisplayName} deployed successfully.";
            }
            catch (OperationCanceledException)
            {
                txtStatus.Text = "Deploy cancelled.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deploy failed for {profile.DisplayName}:\n{ex.Message}", "Deploy Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Deploy failed.";
            }
            finally
            {
                btn.IsEnabled = true;
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
                MessageBox.Show($"No rollback snapshots exist for {profile.DisplayName}.",
                    "Nothing to Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var latest = manifests[0];
            string info = $"Rollback {profile.DisplayName} to snapshot from {latest.Timestamp}?\n\n" +
                          $"Mods: {string.Join(", ", latest.ModNames.Take(5))}" +
                          (latest.ModNames.Count > 5 ? $" (+{latest.ModNames.Count - 5} more)" : "");

            if (MessageBox.Show(info, "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            btn.IsEnabled = false;
            txtStatus.Text = $"Rolling back {profile.ShortName}...";

            try
            {
                var progress = new Progress<DeploymentProgress>(p =>
                    txtStatus.Text = $"[{profile.ShortName}] {p.Stage}");
                await _core.RollbackDeployAsync(latest, progress);
                txtStatus.Text = $"{profile.DisplayName} rolled back.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rollback failed:\n{ex.Message}", "Rollback Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Rollback failed.";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }

        // ── Mod list interaction ──────────────────────────────────────────────────

        private void ModCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: ModItem item })
                SaveModList(item);
        }

        private void SaveModList(ModItem item)
        {
            // Find which profile owns this item by checking all three mod lists.
            foreach (var (profile, mods) in _episodes)
            {
                if (!mods.Contains(item)) continue;
                string infoPath = Path.Combine(item.RawFolderPath, "modinfo.txt");
                try
                {
                    System.IO.File.WriteAllText(infoPath,
                        System.Text.Json.JsonSerializer.Serialize(item,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
                catch { /* best effort */ }
                break;
            }
        }

        // ── Drag-and-drop reorder ─────────────────────────────────────────────────

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            _dragSource = sender as ListView;
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSource == null) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _startPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _startPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            if (_dragSource.SelectedItem is ModItem item)
            {
                _draggedItem = item;
                DragDrop.DoDragDrop(_dragSource, item, DragDropEffects.Move);
            }
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void List_DragLeave(object sender, DragEventArgs e) => e.Handled = true;

        private void List_Drop(object sender, DragEventArgs e)
        {
            if (_draggedItem == null || sender is not ListView list) return;
            if (list.Tag is not string key) return;

            var (_, mods) = _episodes.FirstOrDefault(ep => ep.Profile.Key == key);
            if (mods == null || !mods.Contains(_draggedItem)) return;

            int fromIdx = mods.IndexOf(_draggedItem);
            int toIdx   = 0;

            // Find drop target index
            var target = (e.OriginalSource as FrameworkElement)?.DataContext as ModItem;
            if (target != null) toIdx = mods.IndexOf(target);

            if (fromIdx == toIdx) { _draggedItem = null; return; }
            mods.Move(fromIdx, toIdx);

            // Re-number load orders
            for (int i = 0; i < mods.Count; i++)
            {
                mods[i].LoadOrder = i;
                SaveModList(mods[i]);
            }
            _draggedItem = null;
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
            sp.Children.Add(new TextBlock { Text = prompt, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });

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
