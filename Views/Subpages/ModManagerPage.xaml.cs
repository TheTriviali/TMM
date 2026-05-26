using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
using TMM.Services;

namespace TMM
{
    public partial class ModManagerPage : UserControl
    {
        // ── Shared state ──────────────────────────────────────────────────────────

        public event Action? BackRequested;

        private BackendCore _core = null!;
        private LibraryEntry _entry = null!;

        // ── Game state ────────────────────────────────────────────────────────────

        private GameProfile _customProfile = null!;
        private CustomGameProfile _customConfig = null!;
        private ObservableCollection<ModItem> _modsCustom = new();
        private bool _pendingCustom;
        private Point _startCustom;
        private ModItem? _draggedCustom;
        private CancellationTokenSource? _deployCts;

        // ── Constructor ───────────────────────────────────────────────────────────

        public ModManagerPage()
        {
            InitializeComponent();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void LoadEntry(LibraryEntry entry, BackendCore core)
        {
            _core  = core;
            _entry = entry;

            panelCustom.Visibility      = Visibility.Collapsed;
            panelPlaceholder.Visibility = Visibility.Collapsed;

            if (entry.IsPlaceholder)
            {
                Placeholder_txtName.Text = entry.DisplayName;
                panelPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            InitCustomGame();
            panelCustom.Visibility = Visibility.Visible;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GAME PANEL  (all games — built-in and custom — use the same panel)
        // ══════════════════════════════════════════════════════════════════════════

        private void InitCustomGame()
        {
            _customConfig = GameRegistry.Instance.GetCustomGameConfig(_entry.Key)
                         ?? new CustomGameProfile { GameName = _entry.DisplayName };

            _customProfile = GameRegistry.Instance.GetGameProfile(_entry.Key)
                          ?? new GameProfile(_entry.Key, _entry.DisplayName, _entry.Key,
                                             _entry.Key + ".exe", "", "");

            _modsCustom = _core.Mods.TryGetValue(_entry.Key, out var existing)
                ? existing
                : new ObservableCollection<ModItem>();

            Cust_ModList.ItemsSource = _modsCustom;
            LoadModsFromJsonCustom();
            _ = RefreshCustomAsync();
        }

        private async Task RefreshCustomAsync()
        {
            await _core.RefreshAllModListsAsync();
            UpdateDeployButtonCustom();
            UpdateSidebarCustom();
            Cust_txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        private void UpdateSidebarCustom()
        {
            Cust_txtSidebarGameName.Text = _customConfig.GameName;
            Cust_txtSidebarDir.Text = string.IsNullOrEmpty(_customConfig.GameDirectory)
                ? "Directory not set"
                : _customConfig.GameDirectory;
        }

        private void UpdateDeployButtonCustom()
        {
            bool ready     = !string.IsNullOrEmpty(_customConfig.GameDirectory) &&
                             Directory.Exists(_customConfig.GameDirectory);
            bool hasEnabled = _modsCustom.Any(m => m.IsEnabled);
            _pendingCustom = ready && hasEnabled;

            if (!_pendingCustom)
                Cust_btnDeploy.Background = UiColors.DisabledBrush;
            else
                Cust_btnDeploy.SetResourceReference(Button.BackgroundProperty, "AccentBrush");

            Cust_pnlLaunch.Visibility = string.IsNullOrEmpty(_customConfig.ExePath)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void LoadModsFromJsonCustom()
        {
            string jsonPath = Path.Combine(_core.AppDataPath, _customProfile.RawFolderName, "modlist.json");
            if (!File.Exists(jsonPath)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<List<ModItem>>(
                    File.ReadAllText(jsonPath), JsonHelper.PrettyOptions);
                if (saved == null) return;
                _modsCustom.Clear();
                foreach (var m in saved.OrderBy(x => x.LoadOrder))
                    if (Directory.Exists(m.RawFolderPath)) _modsCustom.Add(m);
            }
            catch { }
        }

        private void SaveModsCustom()
        {
            string folder = Path.Combine(_core.AppDataPath, _customProfile.RawFolderName);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "modlist.json"),
                JsonSerializer.Serialize(_modsCustom.ToList(), JsonHelper.PrettyOptions));
        }

        // ── Toolbar handlers ──────────────────────────────────────────────────────

        private void BtnToggleSidebarCustom_Click(object sender, RoutedEventArgs e)
            => Cust_SidebarBorder.Visibility = Cust_SidebarBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;

        private async void BtnInstallModCustom_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Title       = "Select Mod Archive(s)",
                Filter      = "Archive Files|*.zip;*.rar;*.7z|All Files|*.*",
                Multiselect = true
            };
            if (ofd.ShowDialog() != true) return;
            foreach (string path in ofd.FileNames)
                await InstallModFileCustomAsync(path);
            await RefreshCustomAsync();
            SaveModsCustom();
        }

        private async Task InstallModFileCustomAsync(string archivePath)
        {
            string ext        = Path.GetExtension(archivePath).ToLowerInvariant();
            string modName    = Path.GetFileNameWithoutExtension(archivePath);
            string destFolder = Path.Combine(_core.AppDataPath, _customProfile.RawFolderName, modName);

            if (Directory.Exists(destFolder))
            {
                if (MessageBox.Show($"A mod named '{modName}' already exists. Overwrite?",
                        "Mod Exists", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                BackendCore.ForceDeleteDirectory(destFolder);
            }
            Directory.CreateDirectory(destFolder);

            try
            {
                if (ext is ".zip" or ".rar" or ".7z")
                    await BackendCore.ExtractArchiveSafeAsync(archivePath, destFolder, CancellationToken.None);
                else
                    File.Copy(archivePath, Path.Combine(destFolder, Path.GetFileName(archivePath)), overwrite: true);

                var item = new ModItem
                {
                    Name          = modName,
                    IsEnabled     = true,
                    LoadOrder     = _modsCustom.Count,
                    RawFolderPath = destFolder
                };
                SyncModInfoToFolder(item);
                _modsCustom.Add(item);
                NotificationService.ShowSuccess($"Installed '{modName}'.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to install '{modName}': {ex.Message}");
            }
        }

        private async void BtnDeployCustom_Click(object sender, RoutedEventArgs e)
        {
            if (!_pendingCustom)
            {
                MessageBox.Show("Game directory not configured or no enabled mods.",
                    "Cannot Deploy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var allMods = _modsCustom.ToList();
            new LoadOrderResolver().ResolveFinalLoadOrders(allMods, _customConfig);
            var enabled = allMods
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.FinalLoadOrder)
                .ToList();

            if (enabled.Count == 0) return;

            ShowDeployOverlay("Planning deployment...");
            List<(ModItem Mod, DeploymentPlan Plan)> plans;
            try
            {
                var planner = new DeploymentPlanner();
                plans = new List<(ModItem, DeploymentPlan)>(enabled.Count);
                foreach (var mod in enabled)
                {
                    if (!Directory.Exists(mod.RawFolderPath)) continue;
                    plans.Add((mod, await planner.PlanDeploymentAsync(mod, _customConfig)));
                }
            }
            finally
            {
                HideDeployOverlay();
            }

            var preview = new DeployPreviewWindow(plans, _customConfig.GameDirectory)
                { Owner = Window.GetWindow(this) };
            if (preview.ShowDialog() != true) return;

            _deployCts?.Cancel();
            _deployCts = new CancellationTokenSource();
            Cust_btnDeploy.IsEnabled = false;
            ShowDeployOverlay($"Deploying {_customConfig.GameName}...");
            try
            {
                var fileMap  = preview.BuildFileMap();
                var modNames = enabled.Select(m => m.Name).ToList();
                await _core.DeployFilesToGameDirAsync(
                    _customProfile.Key, _customConfig.GameDirectory,
                    fileMap, modNames, MakeProgress(), _deployCts.Token);
                NotificationService.ShowSuccess($"{_customConfig.GameName} deployed.");
            }
            catch (OperationCanceledException)
            {
                NotificationService.ShowWarning("Deploy cancelled.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Deploy failed: {ex.Message}");
            }
            finally
            {
                HideDeployOverlay();
                Cust_btnDeploy.IsEnabled = true;
            }
            await RefreshCustomAsync();
        }

        private async void BtnRollbackCustom_Click(object sender, RoutedEventArgs e)
            => await RunRollbackAsync(_customProfile);

        private void BtnLaunchCustom_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_customConfig.SteamAppId))
            {
                SteamLauncher.Invoke("rungameid", _customConfig.SteamAppId);
                return;
            }

            if (string.IsNullOrEmpty(_customConfig.ExePath)) return;
            string exeFull = Path.IsPathRooted(_customConfig.ExePath)
                ? _customConfig.ExePath
                : Path.Combine(_customConfig.GameDirectory, _customConfig.ExePath);

            if (!File.Exists(exeFull))
            {
                MessageBox.Show($"Executable not found:\n{exeFull}", "Launch Failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(exeFull) { UseShellExecute = true });
        }

        private async void BtnEditConfigCustom_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new CustomGameSetupWizard(_customConfig) { Owner = Window.GetWindow(this) };
            if (wizard.ShowDialog() != true || wizard.Result is null) return;
            _customConfig = wizard.Result;
            await GameRegistry.Instance.UpdateCustomGameAsync(_customProfile.Key, _customConfig);
            await RefreshCustomAsync();
        }

        private async void BtnRefreshCustom_Click(object sender, RoutedEventArgs e)
        {
            await _core.RefreshAllModListsAsync();
            await RefreshCustomAsync();
        }

        private void TxtSearchCustom_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = Cust_txtSearch.Text.Trim().ToLowerInvariant();
            CollectionViewSource.GetDefaultView(_modsCustom).Filter = string.IsNullOrEmpty(q)
                ? null
                : obj => obj is ModItem m && m.Name.ToLowerInvariant().Contains(q);
        }

        private void BtnClearSearchCustom_Click(object sender, RoutedEventArgs e)
        {
            Cust_txtSearch.Text = "";
            CollectionViewSource.GetDefaultView(_modsCustom).Filter = null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SHARED HANDLERS
        // ══════════════════════════════════════════════════════════════════════════

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => BackRequested?.Invoke();

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            var r = MessageBox.Show(
                "For help and documentation, visit the TMM GitHub repository.\n\nOpen in browser?",
                "Help & Resources", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r == MessageBoxResult.Yes)
                ShellHelper.OpenUrl("https://github.com/noahd179/tgtamm");
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
            => new AboutWindow(_core) { Owner = Window.GetWindow(this) }.ShowDialog();

        private void BtnOpenAppData_Click(object sender, RoutedEventArgs e)
            => _core.OpenAppData();

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
                ShellHelper.OpenUrl(url);
        }

        // ── Deploy / Rollback helpers ─────────────────────────────────────────────

        private async Task RunRollbackAsync(GameProfile profile)
        {
            var manifests = _core.GetRollbackManifests(profile.Key);
            if (manifests.Count == 0)
            {
                MessageBox.Show($"No rollback snapshots found for {profile.DisplayName}.",
                    "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var latest = manifests[0];
            string info = $"Rollback {profile.DisplayName} to snapshot from {latest.Timestamp}?\n\n" +
                          $"Mods: {string.Join(", ", latest.ModNames.Take(5))}" +
                          (latest.ModNames.Count > 5 ? $" (+{latest.ModNames.Count - 5} more)" : "");

            if (MessageBox.Show(info, "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            ShowDeployOverlay("Rolling back...");
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
                HideDeployOverlay();
            }
        }

        private void ShowDeployOverlay(string stage)
        {
            deployOverlay.Visibility       = Visibility.Visible;
            deployProgressPanel.Visibility = Visibility.Visible;
            pbDeploy.IsIndeterminate       = true;
            txtDeployStage.Text            = stage;
            txtDeployCount.Text            = "";
        }

        private void HideDeployOverlay()
        {
            deployOverlay.Visibility       = Visibility.Collapsed;
            deployProgressPanel.Visibility = Visibility.Collapsed;
            pbDeploy.IsIndeterminate       = true;
            pbDeploy.Value                 = 0;
        }

        private Progress<DeploymentProgress> MakeProgress() =>
            new(p =>
            {
                txtDeployStage.Text = p.Stage;
                if (p.Total > 0)
                {
                    pbDeploy.IsIndeterminate = false;
                    pbDeploy.Value           = (double)p.Current / p.Total * 100;
                    txtDeployCount.Text      = $"{p.Current} / {p.Total}";
                }
                else
                {
                    pbDeploy.IsIndeterminate = true;
                    txtDeployCount.Text      = "";
                }
            });

        // ══════════════════════════════════════════════════════════════════════════
        // MOD LIST INTERACTION
        // ══════════════════════════════════════════════════════════════════════════

        private void ModCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: ModItem item })
            {
                SyncModInfoToFolder(item);
                _pendingCustom = true;
                UpdateDeployButtonCustom();
                SaveModsCustom();
            }
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────────────

        private void Cust_List_KeyDown(object sender, KeyEventArgs e)
        {
            var selected = Cust_ModList.SelectedItem as ModItem;
            if (selected == null) return;
            switch (e.Key)
            {
                case Key.F2:     CustStartRename(selected);           break;
                case Key.Space:  CustToggleMod(selected);             break;
                case Key.Delete: CustDeleteSelected();                 break;
                case Key.F5:     BtnDeployCustom_Click(null!, null!);  break;
                case Key.Up   when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    CustMoveUp(selected);   break;
                case Key.Down when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    CustMoveDown(selected); break;
            }
        }

        // ── Context menu ──────────────────────────────────────────────────────────

        private void MenuRename_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            var win = new RenameWindow(mod.Name) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true) return;
            mod.Name = win.NewName;
            SyncModInfoToFolder(mod);
            SaveModsCustom();
        }

        private void MenuToggle_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void MenuDelete_Click(object? sender, RoutedEventArgs e)
        {
            var selected = Cust_ModList.SelectedItems.Cast<ModItem>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show($"Delete {selected.Count} mod(s)?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            foreach (var m in selected)
            {
                try
                {
                    if (Directory.Exists(m.RawFolderPath))
                        BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                    _modsCustom.Remove(m);
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error deleting '{m.Name}': {ex.Message}");
                }
            }
            SaveModsCustom();
        }

        private void MenuMoveUp_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            int idx = _modsCustom.IndexOf(mod);
            if (idx <= 0) return;
            var other = _modsCustom[idx - 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            _modsCustom.Move(idx, idx - 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void MenuMoveDown_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            int idx = _modsCustom.IndexOf(mod);
            if (idx < 0 || idx >= _modsCustom.Count - 1) return;
            var other = _modsCustom[idx + 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            _modsCustom.Move(idx, idx + 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void MenuSetLoadOrder_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            int max = _modsCustom.Count - 1;
            var win = new RenameWindow(mod.LoadOrder.ToString())
            {
                Title = $"Set Load Order (0–{max})",
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true || !int.TryParse(win.NewName, out int newOrder)) return;
            newOrder = Math.Clamp(newOrder, 0, max);
            _modsCustom.Remove(mod);
            _modsCustom.Insert(newOrder, mod);
            for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
            foreach (var m in _modsCustom) SyncModInfoToFolder(m);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void MenuOpenFolder_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null && Directory.Exists(mod.RawFolderPath))
                ShellHelper.OpenFolder(mod.RawFolderPath);
        }

        private void MenuOpenGameFolder_Click(object? sender, RoutedEventArgs e)
        {
            string? path = _customConfig?.GameDirectory;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                ShellHelper.OpenFolder(path);
            else
                MessageBox.Show("Game folder is not set or missing.", "Folder Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenBackupFolder_Click(object? sender, RoutedEventArgs e)
        {
            string path = Path.Combine(_core.BackupsPath, _customProfile?.Key ?? "");
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuOpenModsFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (_customProfile == null) return;
            string path = Path.Combine(_core.AppDataPath, _customProfile.RawFolderName);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuProperties_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null)
                new ModPropertiesWindow(mod) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        // ── Active list helper ────────────────────────────────────────────────────

        private ModItem? GetSelectedMod() => Cust_ModList.SelectedItem as ModItem;

        // ── Custom context menu helpers ───────────────────────────────────────────

        private void CustStartRename(ModItem mod)
        {
            var dlg = new RenameWindow(mod.Name) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName)) return;
            mod.Name = dlg.NewName;
            SyncModInfoToFolder(mod);
            SaveModsCustom();
        }

        private void CustToggleMod(ModItem mod)
        {
            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void CustMoveUp(ModItem mod)
        {
            int idx = _modsCustom.IndexOf(mod);
            if (idx <= 0) return;
            _modsCustom.Move(idx, idx - 1);
            for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
            foreach (var m in _modsCustom) SyncModInfoToFolder(m);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void CustMoveDown(ModItem mod)
        {
            int idx = _modsCustom.IndexOf(mod);
            if (idx < 0 || idx >= _modsCustom.Count - 1) return;
            _modsCustom.Move(idx, idx + 1);
            for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
            foreach (var m in _modsCustom) SyncModInfoToFolder(m);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        private void CustDeleteSelected()
        {
            var selected = Cust_ModList.SelectedItems.Cast<ModItem>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show($"Delete {selected.Count} mod(s)?", "Delete Mods",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            foreach (var m in selected)
            {
                if (Directory.Exists(m.RawFolderPath))
                    BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                _modsCustom.Remove(m);
            }
            for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
        }

        // ══════════════════════════════════════════════════════════════════════════
        // DRAG & DROP
        // ══════════════════════════════════════════════════════════════════════════

        private void Cust_List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _startCustom = e.GetPosition(null);
            if (e.OriginalSource is FrameworkElement el && el.DataContext is ModItem m)
                _draggedCustom = m;
        }

        private void Cust_List_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedCustom == null) return;
            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _startCustom.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _startCustom.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            DragDrop.DoDragDrop(Cust_ModList, _draggedCustom, DragDropEffects.Move);
            _draggedCustom = null;
        }

        private void Cust_List_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(ModItem)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
            double y = GetInsertionLineY(Cust_ModList, e.GetPosition(Cust_ModList).Y);
            System.Windows.Controls.Canvas.SetTop(Cust_DropLine, y - 1);
            Cust_DropLine.Width = Cust_ModList.ActualWidth - 4;
            Cust_DropLine.Visibility = Visibility.Visible;
        }

        private void Cust_List_DragLeave(object sender, DragEventArgs e)
            => Cust_DropLine.Visibility = Visibility.Collapsed;

        private void Cust_List_Drop(object sender, DragEventArgs e)
        {
            Cust_DropLine.Visibility = Visibility.Collapsed;
            if (_draggedCustom == null || !e.Data.GetDataPresent(typeof(ModItem))) return;

            int insertIdx = GetInsertionIndex(Cust_ModList, e.GetPosition(Cust_ModList).Y);
            _modsCustom.Remove(_draggedCustom);
            if (insertIdx > _modsCustom.Count) insertIdx = _modsCustom.Count;
            _modsCustom.Insert(insertIdx, _draggedCustom);
            for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
            foreach (var m in _modsCustom) SyncModInfoToFolder(m);
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
            _draggedCustom = null;
        }

        // ── Drop geometry helpers ─────────────────────────────────────────────────

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

        // ── Toast helpers ─────────────────────────────────────────────────────────

        private void Toast_Close(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        private void Toast_CloseBtn(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item)
                NotificationService.Queue.Remove(item);
        }

        // ── Static sync helper ────────────────────────────────────────────────────

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
}
