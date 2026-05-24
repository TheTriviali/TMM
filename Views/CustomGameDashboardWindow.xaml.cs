using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    public partial class CustomGameDashboardWindow : TmmWindow
    {
        private readonly BackendCore _core;
        private readonly GameProfile _profile;
        private CustomGameProfile _config;

        private readonly ObservableCollection<ModItem> _mods;
        private bool _hasPendingChanges = true;
        private Point _startPoint;
        private ModItem? _draggedItem;

        public CustomGameDashboardWindow(BackendCore core, GameProfile profile, CustomGameProfile config)
        {
            _core    = core;
            _profile = profile;
            _config  = config;

            if (!core.Mods.ContainsKey(profile.Key))
                throw new InvalidOperationException($"Mod list for '{profile.Key}' not initialized.");
            _mods = core.Mods[profile.Key];

            InitializeComponent();

            Title = "TMM — " + config.GameName;
            txtGameTitle.Text = " — " + config.GameName;

            pnlLaunch.Visibility = string.IsNullOrEmpty(config.ExePath)
                ? Visibility.Collapsed
                : Visibility.Visible;

            ModList.ItemsSource = _mods;
            LoadModsFromJson();
        }

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
            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
            UpdateDeployButton();
            UpdateSidebar();
        }

        private void UpdateSidebar()
        {
            txtSidebarGameName.Text = _config.GameName;
            txtSidebarDir.Text = string.IsNullOrEmpty(_config.GameDirectory)
                ? "Directory not set"
                : _config.GameDirectory;
        }

        private void UpdateDeployButton()
        {
            bool ready = !string.IsNullOrEmpty(_config.GameDirectory) &&
                         Directory.Exists(_config.GameDirectory);
            bool hasEnabled = _mods.Any(m => m.IsEnabled);
            bool hasOverride = _core.Settings.DeployOverrides.TryGetValue(_profile.Key, out bool ov) && ov;

            _hasPendingChanges = ready && hasEnabled;

            if (!_hasPendingChanges)
                btnDeploy.Background = UiColors.DisabledBrush;
            else if (hasOverride)
                btnDeploy.Background = UiColors.PendingBrush;
            else
                btnDeploy.SetResourceReference(Button.BackgroundProperty, "AccentBrush");

            btnDeploy.ToolTip = !ready
                ? "Game directory not configured or missing"
                : !hasEnabled ? "No enabled mods to deploy" : "Deploy Mods to Game Directory (F5)";
        }

        // ── Window chrome ──────────────────────────────────────────────────────

        private new void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && e.ClickCount == 2)
                BtnMaximize_Click(sender, e);
            else if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                DragMove();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            MainWindowBorder.CornerRadius = WindowState == WindowState.Maximized
                ? new CornerRadius(0) : new CornerRadius(10);
        }

        // ── Toolbar ────────────────────────────────────────────────────────────

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

        private void BtnOpenAppData_Click(object sender, RoutedEventArgs e) => _core.OpenAppData();

        // ── Notification toasts ────────────────────────────────────────────────

        private void Toast_Close(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        // ── Install mod ────────────────────────────────────────────────────────

        private async void BtnInstallMod_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Mod Archive(s)",
                Filter = BuildFileFilter(),
                Multiselect = true
            };
            if (ofd.ShowDialog() != true) return;

            foreach (string path in ofd.FileNames)
                await InstallModAsync(path);

            await RefreshAsync();
            SaveMods();
        }

        private string BuildFileFilter()
        {
            var types = _config.GetFileTypes();
            if (types.Count == 0)
                return "Archive Files|*.zip;*.rar;*.7z|All Files|*.*";

            string extensions = string.Join(";", types.Select(t => $"*{t}"));
            return $"Mod Files|{extensions}|All Files|*.*";
        }

        private async Task InstallModAsync(string archivePath)
        {
            string ext = Path.GetExtension(archivePath).ToLowerInvariant();
            string modName = Path.GetFileNameWithoutExtension(archivePath);
            string destFolder = Path.Combine(_core.AppDataPath, _profile.RawFolderName, modName);

            if (Directory.Exists(destFolder))
            {
                var r = MessageBox.Show(
                    $"A mod named '{modName}' already exists. Overwrite?",
                    "Mod Exists", MessageBoxButton.YesNo);
                if (r != MessageBoxResult.Yes) return;
                BackendCore.ForceDeleteDirectory(destFolder);
            }

            Directory.CreateDirectory(destFolder);

            bool isArchive = new[] { ".zip", ".rar", ".7z", ".tar", ".gz" }.Contains(ext);
            if (isArchive)
            {
                try
                {
                    await BackendCore.ExtractArchiveSafeAsync(archivePath, destFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Failed to extract '{modName}':\n{ex.Message}",
                        "Install Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    BackendCore.ForceDeleteDirectory(destFolder);
                    return;
                }
            }
            else
            {
                File.Copy(archivePath, Path.Combine(destFolder, Path.GetFileName(archivePath)), overwrite: true);
            }

            var item = new ModItem
            {
                Name = modName,
                RawFolderPath = destFolder,
                PackedFilePath = archivePath,
                IsEnabled = true,
                LoadOrder = _mods.Count
            };
            SyncModInfoToFolder(item);
            _mods.Add(item);
            NotificationService.ShowSuccess($"Installed: {modName}");
        }

        // ── Refresh ────────────────────────────────────────────────────────────

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            _mods.Clear();
            string root = Path.Combine(_core.AppDataPath, _profile.RawFolderName);
            if (Directory.Exists(root))
            {
                int order = 0;
                foreach (var d in Directory.GetDirectories(root))
                {
                    string info = Path.Combine(d, "modinfo.txt");
                    _mods.Add(File.Exists(info)
                        ? JsonSerializer.Deserialize<ModItem>(File.ReadAllText(info), JsonHelper.PrettyOptions) ?? NewMod(d, order)
                        : NewMod(d, order));
                    order++;
                }
            }

            await RefreshAsync();
            SaveMods();
            NotificationService.ShowInfo("Mod list refreshed from disk.");
        }

        private static ModItem NewMod(string folder, int order) => new()
        {
            Name = Path.GetFileName(folder),
            RawFolderPath = folder,
            IsEnabled = true,
            LoadOrder = order
        };

        // ── Deploy ─────────────────────────────────────────────────────────────

        private async void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            if (!_hasPendingChanges) return;

            if (!Directory.Exists(_config.GameDirectory))
            {
                MessageBox.Show("Game directory not found. Please check your game configuration.",
                    "Deploy Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Deploy mods to:\n{_config.GameDirectory}\n\nEnabled mods will be copied to your game directory.",
                "Confirm Deploy", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (confirm != MessageBoxResult.Yes) return;

            btnDeploy.IsEnabled = false;

            // Show overlay
            DialogOverlay.Visibility      = Visibility.Visible;
            DeployProgressPanel.Visibility = Visibility.Visible;
            txtDeployStage.Text            = "Deploying...";
            pbDeploy.IsIndeterminate       = true;
            txtDeployCount.Text            = "";

            try
            {
                var progress = new Progress<DeploymentProgress>(p =>
                {
                    txtDeployStage.Text = string.IsNullOrEmpty(p.Stage) ? "Deploying..." : p.Stage;
                    if (p.Total > 0)
                    {
                        pbDeploy.IsIndeterminate = false;
                        pbDeploy.Maximum = p.Total;
                        pbDeploy.Value   = p.Current;
                        txtDeployCount.Text = $"{p.Current} / {p.Total}";
                    }
                });

                await _core.DeployCustomGameModsAsync(_profile, _config, _mods, progress);
                _hasPendingChanges = false;
                UpdateDeployButton();
                NotificationService.ShowSuccess($"Deploy complete — {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Deploy failed: {ex.Message}");
            }
            finally
            {
                DialogOverlay.Visibility      = Visibility.Collapsed;
                DeployProgressPanel.Visibility = Visibility.Collapsed;
                btnDeploy.IsEnabled = true;
            }
        }

        // ── Rollback ───────────────────────────────────────────────────────────

        private async void BtnRollback_Click(object sender, RoutedEventArgs e)
        {
            var manifests = _core.GetRollbackManifests(_profile.Key);
            if (manifests.Count == 0)
            {
                NotificationService.ShowInfo("No rollback points found. Deploy mods first to create a backup.");
                return;
            }

            var latest = manifests[0];
            string modList = latest.ModNames.Count > 0
                ? string.Join(", ", latest.ModNames)
                : "(no mods)";

            var choice = MessageBox.Show(
                $"Rollback {_config.GameName} to its state before the last deploy?\n\n" +
                $"Restore point: {latest.Timestamp}\n" +
                $"Mods applied: {modList}\n" +
                $"Files changed: {latest.Entries.Count}",
                "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes) return;

            btnRollback.IsEnabled = false;

            try
            {
                var progress = new Progress<DeploymentProgress>(_ => { });
                await _core.RollbackDeployAsync(latest, progress);
                NotificationService.ShowSuccess($"Rollback complete — {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Rollback failed: {ex.Message}");
            }
            finally
            {
                btnRollback.IsEnabled = true;
            }
        }

        // ── Launch game ────────────────────────────────────────────────────────

        private void BtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_config.SteamAppId))
            {
                SteamLauncher.Invoke("rungameid", _config.SteamAppId);
                return;
            }

            if (string.IsNullOrEmpty(_config.ExePath)) return;
            string exeFull = Path.IsPathRooted(_config.ExePath)
                ? _config.ExePath
                : Path.Combine(_config.GameDirectory, _config.ExePath);

            if (!File.Exists(exeFull))
            {
                MessageBox.Show($"Executable not found:\n{exeFull}", "Launch Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(exeFull) { UseShellExecute = true });
        }

        // ── Edit config ────────────────────────────────────────────────────────

        private async void BtnEditConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new CustomGameConfigWindow(_config) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            await GameRegistry.Instance.UpdateCustomGameAsync(_profile.Key, dlg.Result);
            _config = dlg.Result;

            Title = "TMM — " + _config.GameName;
            txtGameTitle.Text = " — " + _config.GameName;
            pnlLaunch.Visibility = string.IsNullOrEmpty(_config.ExePath)
                ? Visibility.Collapsed : Visibility.Visible;

            await RefreshAsync();
        }

        // ── Settings / Theme ──────────────────────────────────────────────────

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            new SettingsWindow(_core) { Owner = this }.ShowDialog();
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            new ThemeManagerWindow(_core) { Owner = this }.ShowDialog();
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);
        }

        // ── JSON persistence ───────────────────────────────────────────────────

        private void LoadModsFromJson()
        {
            string json = Path.Combine(_core.AppDataPath, _profile.RawFolderName, "modlist.json");
            if (!File.Exists(json)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<List<ModItem>>(File.ReadAllText(json), JsonHelper.PrettyOptions);
                if (saved == null) return;
                _mods.Clear();
                foreach (var m in saved.OrderBy(x => x.LoadOrder))
                    if (Directory.Exists(m.RawFolderPath)) _mods.Add(m);
            }
            catch { }
        }

        private void SaveMods()
        {
            string folder = Path.Combine(_core.AppDataPath, _profile.RawFolderName);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "modlist.json"),
                JsonSerializer.Serialize(_mods.ToList(), JsonHelper.PrettyOptions));
        }

        private static void SyncModInfoToFolder(ModItem mod)
        {
            if (!Directory.Exists(mod.RawFolderPath)) return;
            File.WriteAllText(
                Path.Combine(mod.RawFolderPath, "modinfo.txt"),
                JsonSerializer.Serialize(mod, JsonHelper.PrettyOptions));
        }

        // ── Drag & Drop reorder ────────────────────────────────────────────────

        private void List_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            _startPoint = e.GetPosition(null);

        private void List_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(null);
            var diff = _startPoint - pos;
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var item = GetHoveredMod(e.OriginalSource as DependencyObject);
            if (item == null) return;

            _draggedItem = item;
            DragDrop.DoDragDrop(ModList, item, DragDropEffects.Move);
            _draggedItem = null;
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(ModItem)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            UpdateDropLine(e.GetPosition(ModList).Y);
        }

        private void List_DragLeave(object sender, DragEventArgs e) => HideDropLine();

        private void List_Drop(object sender, DragEventArgs e)
        {
            HideDropLine();
            if (_draggedItem == null || !e.Data.GetDataPresent(typeof(ModItem))) return;

            int insertIdx = GetInsertionIndex(e.GetPosition(ModList).Y);
            _mods.Remove(_draggedItem);
            if (insertIdx > _mods.Count) insertIdx = _mods.Count;
            _mods.Insert(insertIdx, _draggedItem);

            for (int i = 0; i < _mods.Count; i++) _mods[i].LoadOrder = i;
            foreach (var m in _mods) SyncModInfoToFolder(m);
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void UpdateDropLine(double mouseY)
        {
            Canvas.SetTop(DropLine, GetInsertionLineY(mouseY) - 1);
            DropLine.Width = ModList.ActualWidth - 4;
            DropLine.Visibility = Visibility.Visible;
        }

        private void HideDropLine() => DropLine.Visibility = Visibility.Collapsed;

        private double GetInsertionLineY(double mouseY)
        {
            for (int i = 0; i < _mods.Count; i++)
            {
                var container = ModList.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                if (container == null) continue;
                var pt = container.TransformToAncestor(ModList).Transform(new Point(0, 0));
                if (mouseY < pt.Y + container.ActualHeight / 2) return pt.Y;
            }
            return ModList.ActualHeight - 2;
        }

        private int GetInsertionIndex(double mouseY)
        {
            for (int i = 0; i < _mods.Count; i++)
            {
                var container = ModList.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                if (container == null) continue;
                var pt = container.TransformToAncestor(ModList).Transform(new Point(0, 0));
                if (mouseY < pt.Y + container.ActualHeight / 2) return i;
            }
            return _mods.Count;
        }

        private ModItem? GetHoveredMod(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is ListViewItem lvi && lvi.DataContext is ModItem m) return m;
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
        }

        // ── Keyboard ───────────────────────────────────────────────────────────

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            var selected = ModList.SelectedItem as ModItem;
            if (selected == null) return;
            switch (e.Key)
            {
                case Key.F2:   StartRename(selected); break;
                case Key.Space: ToggleMod(selected); break;
                case Key.Delete: DeleteSelected(); break;
                case Key.Up   when e.KeyboardDevice.Modifiers == ModifierKeys.Control: MoveUp(selected); break;
                case Key.Down when e.KeyboardDevice.Modifiers == ModifierKeys.Control: MoveDown(selected); break;
                case Key.F5:  BtnDeploy_Click(sender, e); break;
            }
        }

        // ── Search ─────────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = txtSearch.Text.Trim().ToLowerInvariant();
            CollectionViewSource.GetDefaultView(_mods).Filter = string.IsNullOrEmpty(q)
                ? null
                : obj => obj is ModItem m && m.Name.ToLowerInvariant().Contains(q);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            CollectionViewSource.GetDefaultView(_mods).Filter = null;
        }

        // ── Context menu ───────────────────────────────────────────────────────

        private void MenuRename_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m) StartRename(m);
        }

        private void MenuSetLoadOrder_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is not ModItem mod) return;
            var dlg = new RenameWindow(mod.LoadOrder.ToString())
            {
                Title = $"Set Load Order (0 to {_mods.Count - 1})",
                Owner = this
            };
            if (dlg.ShowDialog() != true || !int.TryParse(dlg.NewName, out int order)) return;
            order = Math.Clamp(order, 0, _mods.Count - 1);
            _mods.Remove(mod);
            _mods.Insert(order, mod);
            for (int i = 0; i < _mods.Count; i++) _mods[i].LoadOrder = i;
            foreach (var m in _mods) SyncModInfoToFolder(m);
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void MenuToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m) ToggleMod(m);
        }

        private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m && Directory.Exists(m.RawFolderPath))
                ShellHelper.OpenFolder(m.RawFolderPath);
        }

        private void MenuOpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_config.GameDirectory) && Directory.Exists(_config.GameDirectory))
                ShellHelper.OpenFolder(_config.GameDirectory);
            else
                MessageBox.Show("Game directory not set or does not exist.", "Not Found");
        }

        private void MenuOpenBackupFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(_core.BackupsPath, _profile.Key);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuOpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(_core.AppDataPath, _profile.RawFolderName);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m) MoveUp(m);
        }

        private void MenuMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m) MoveDown(m);
        }

        private void MenuProperties_Click(object sender, RoutedEventArgs e)
        {
            if (ModList.SelectedItem is ModItem m)
                new ModPropertiesWindow(m) { Owner = this }.ShowDialog();
        }

        private void MenuDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

        // ── Mod helpers ────────────────────────────────────────────────────────

        private void StartRename(ModItem mod)
        {
            var dlg = new RenameWindow(mod.Name) { Owner = this };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName)) return;
            mod.Name = dlg.NewName;
            SyncModInfoToFolder(mod);
            SaveMods();
        }

        private void ToggleMod(ModItem mod)
        {
            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void MoveUp(ModItem mod)
        {
            int idx = _mods.IndexOf(mod);
            if (idx <= 0) return;
            _mods.Move(idx, idx - 1);
            for (int i = 0; i < _mods.Count; i++) _mods[i].LoadOrder = i;
            foreach (var m in _mods) SyncModInfoToFolder(m);
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void MoveDown(ModItem mod)
        {
            int idx = _mods.IndexOf(mod);
            if (idx < 0 || idx >= _mods.Count - 1) return;
            _mods.Move(idx, idx + 1);
            for (int i = 0; i < _mods.Count; i++) _mods[i].LoadOrder = i;
            foreach (var m in _mods) SyncModInfoToFolder(m);
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void DeleteSelected()
        {
            var selected = ModList.SelectedItems.Cast<ModItem>().ToList();
            if (selected.Count == 0) return;
            var r = MessageBox.Show(
                $"Delete {selected.Count} mod(s)? This removes the mod files from TMM storage.",
                "Delete Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            foreach (var m in selected)
            {
                if (Directory.Exists(m.RawFolderPath))
                    BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                _mods.Remove(m);
            }

            for (int i = 0; i < _mods.Count; i++) _mods[i].LoadOrder = i;
            _hasPendingChanges = true;
            UpdateDeployButton();
            SaveMods();
        }

        private void ModCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ModItem m)
            {
                SyncModInfoToFolder(m);
                _hasPendingChanges = true;
                UpdateDeployButton();
                SaveMods();
            }
        }
    }
}
