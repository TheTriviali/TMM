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
        private bool _showGroups;
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
                                             _entry.Key + ".exe", "");

            // Built-in profiles keep their resolved path in Settings.GamePaths (set by Quick
            // Scan / setup), not in config.GameDirectory. Sync it so the unified panel's deploy
            // button, sidebar path, and integrity check all resolve for built-in games too.
            if (string.IsNullOrEmpty(_customConfig.GameDirectory))
            {
                var resolved = _core.GetVanillaPath(_customProfile);
                if (!string.IsNullOrEmpty(resolved))
                    _customConfig.GameDirectory = resolved;
            }

            _modsCustom = _core.Mods.TryGetValue(_entry.Key, out var existing)
                ? existing
                : new ObservableCollection<ModItem>();

            Cust_ModList.ItemsSource = _modsCustom;
            LoadModsFromJsonCustom();
            InitializeDownloadsDrawer();
            _ = RefreshCustomAsync();
        }

        private async Task RefreshCustomAsync()
        {
            await _core.RefreshAllModListsAsync();
            RefreshCustomView();
            await EnsureDeploymentPlansAsync();
            UpdateDeployButtonCustom();
            UpdateSidebarCustom();
            Cust_txtDiskSpace.Text = _core.GetDriveSpaceInfo();
            await RefreshIntegrityAsync();
        }

        private void UpdateSidebarCustom()
        {
            Cust_txtSidebarGameName.Text = _customConfig.GameName;
            Cust_txtSidebarDir.Text = string.IsNullOrEmpty(_customConfig.GameDirectory)
                ? TMM.Services.LocalizationService.Instance["ModManager_DirectoryNotSet"]
                : _customConfig.GameDirectory;

            if (!string.IsNullOrEmpty(_customConfig.NexusSlug))
            {
                Cust_btnNexus.Content = "NexusMods";
                Cust_btnNexus.Tag    = $"https://www.nexusmods.com/{_customConfig.NexusSlug}/mods";
            }
            else
            {
                Cust_btnNexus.Content = "Find Mods";
                Cust_btnNexus.Tag    = $"https://duckduckgo.com/?q={Uri.EscapeDataString(_customConfig.GameName + " mods")}";
            }
        }

        private async Task RefreshIntegrityAsync()
        {
            bool configured = _customConfig.ExpectedExeBytes.HasValue
                              || _customConfig.AcceptedExeMd5s.Count > 0;
            if (!configured)
            {
                Cust_IntegrityBorder.Visibility = Visibility.Collapsed;
                return;
            }

            string? exePath = ResolveExePath();
            if (exePath is null)
            {
                Cust_IntegrityBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var result = await IntegrityChecker.CheckAsync(exePath, _customConfig);
            Cust_IntegrityBorder.Visibility = Visibility.Visible;

            (string label, Brush color) = result.State switch
            {
                IntegrityState.Ok            => ("✓ Integrity verified", Brushes.LightGreen),
                IntegrityState.SizeMismatch  => ("ℹ Executable differs from this profile's expected version", new SolidColorBrush(Color.FromRgb(64, 156, 255))),
                IntegrityState.Md5Mismatch   => ("ℹ Executable differs from this profile's expected version", new SolidColorBrush(Color.FromRgb(64, 156, 255))),
                IntegrityState.FileMissing   => ("⚠ Exe missing",        new SolidColorBrush(Color.FromRgb(224, 112, 112))),
                _                            => ("",                     Brushes.Gray),
            };
            Cust_txtIntegrityState.Text = label;
            Cust_txtIntegrityState.Foreground = color;

            // Override detail message for mismatch cases with reassuring text
            if (result.State == IntegrityState.SizeMismatch || result.State == IntegrityState.Md5Mismatch)
                Cust_txtIntegrityDetail.Text = "Your game .exe doesn't match what this profile was built for. Mods may still work — this is just informational.";
            else
                Cust_txtIntegrityDetail.Text = result.Message;
        }

        private string? ResolveExePath()
        {
            string? exe = _customConfig.ExePath;
            string dir = _customConfig.GameDirectory ?? "";
            if (string.IsNullOrEmpty(exe))
            {
                // Fall back to GameProfile.ExeName (built-in games use just the dir + profile.ExeName)
                if (string.IsNullOrEmpty(_customProfile.ExeName) || string.IsNullOrEmpty(dir))
                    return null;
                return Path.Combine(dir, _customProfile.ExeName);
            }
            return Path.IsPathRooted(exe) ? exe : Path.Combine(dir, exe);
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
                RefreshCustomView();
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

        private void RefreshCustomView()
        {
            var view = CollectionViewSource.GetDefaultView(_modsCustom);
            if (view is null) return;

            string q = Cust_txtSearch.Text.Trim().ToLowerInvariant();
            view.Filter = string.IsNullOrEmpty(q)
                ? null
                : obj => obj is ModItem m && m.Name.ToLowerInvariant().Contains(q);

            using (view.DeferRefresh())
            {
                view.GroupDescriptions.Clear();
                if (_showGroups)
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ModItem.GroupName)));
            }
        }

        private async Task EnsureDeploymentPlansAsync()
        {
            foreach (var mod in _modsCustom.Where(m => Directory.Exists(m.RawFolderPath)))
            {
                string planPath = Path.Combine(mod.RawFolderPath, "_tmm", "deployplan.json");
                if (File.Exists(planPath)) continue;
                await _core.OnModAddedAsync(_customProfile.Key, mod.Name);
            }
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
                ShellHelper.OpenUrl("https://github.com/TheTriviali/TMM");
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
            => new AboutWindow(_core) { Owner = Window.GetWindow(this) }.ShowDialog();

        private void BtnOpenAppData_Click(object sender, RoutedEventArgs e)
            => ShellHelper.OpenOwnedFolder(_core.AppDataPath);

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
                ShellHelper.OpenUrl(url);
        }

        private void BtnIntegrityLearnMore_Click(object sender, RoutedEventArgs e) =>
            ShellHelper.OpenUrl("https://github.com/TheTriviali/TMM/blob/master/docs/FAQ.md#integrity-checks");

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

        private void MenuSetGroup_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            var win = new RenameWindow(mod.GroupName ?? "", "Set Group", "Group name:")
            {
                Owner = Window.GetWindow(this),
            };
            if (win.ShowDialog() != true) return;

            mod.GroupName = string.IsNullOrWhiteSpace(win.NewName) ? null : win.NewName.Trim();
            SyncModInfoToFolder(mod);
            SaveModsCustom();
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            RefreshCustomView();
            _ = ReplanGroupedModAsync(mod);
        }

        private void MenuClearGroup_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            if (string.IsNullOrWhiteSpace(mod.GroupName)) return;

            mod.GroupName = null;
            SyncModInfoToFolder(mod);
            SaveModsCustom();
            _pendingCustom = true;
            UpdateDeployButtonCustom();
            RefreshCustomView();
            _ = ReplanGroupedModAsync(mod);
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

        private async Task ReplanGroupedModAsync(ModItem mod)
        {
            if (!Directory.Exists(mod.RawFolderPath))
                return;

            try
            {
                ShowDeployOverlay("Updating deployment plan...");
                await _core.OnModAddedAsync(_customProfile.Key, mod.Name);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to update plan for '{mod.Name}': {ex.Message}");
            }
            finally
            {
                HideDeployOverlay();
            }
        }

        private void MenuOpenFolder_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null)
            {
                if (Directory.Exists(mod.RawFolderPath))
                    ShellHelper.OpenFolder(mod.RawFolderPath);
                else if (_customProfile != null)
                {
                    // Mod folder is missing; fall back to parent mods folder
                    ShellHelper.OpenOwnedFolder(Path.Combine(_core.AppDataPath, _customProfile.RawFolderName));
                }
            }
        }

        private void MenuOpenGameFolder_Click(object? sender, RoutedEventArgs e)
        {
            string? path = _customConfig?.GameDirectory;
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                ShellHelper.OpenFolder(path);
            else
                MessageBox.Show(TMM.Services.LocalizationService.Instance["ModManager_FolderNotSet"], "Folder Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenBackupFolder_Click(object? sender, RoutedEventArgs e)
        {
            string path = Path.Combine(_core.BackupsPath, _customProfile?.Key ?? "");
            ShellHelper.OpenOwnedFolder(path);
        }

        private void MenuOpenModsFolder_Click(object? sender, RoutedEventArgs e)
        {
            if (_customProfile == null) return;
            string path = Path.Combine(_core.AppDataPath, _customProfile.RawFolderName);
            ShellHelper.OpenOwnedFolder(path);
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
                {
                    string legacyPath = Path.Combine(mod.RawFolderPath, "modinfo.txt");
                    File.WriteAllText(legacyPath, JsonSerializer.Serialize(mod, JsonHelper.PrettyOptions));

                    string sidecarDir = Path.Combine(mod.RawFolderPath, "_tmm");
                    Directory.CreateDirectory(sidecarDir);
                    File.WriteAllText(
                        Path.Combine(sidecarDir, "modinfo.json"),
                        JsonSerializer.Serialize(mod, JsonHelper.PrettyOptions));
                }
            }
            catch { /* best effort */ }
        }

        private static CustomGameProfile CloneProfile(CustomGameProfile src) => new()
        {
            GameName           = src.GameName,
            ShortName          = src.ShortName,
            GameDirectory      = src.GameDirectory,
            ExePath            = src.ExePath,
            SteamAppId         = src.SteamAppId,
            ModTypes           = new(src.ModTypes),
            RoutingRules       = new(src.RoutingRules),
            OverlayFolders     = new(src.OverlayFolders),
            CompanionSiblings  = src.CompanionSiblings.ToDictionary(
                kvp => kvp.Key,
                kvp => new List<string>(kvp.Value)),
            InstallerHints     = src.InstallerHints,
            LauncherCard       = src.LauncherCard,
            Description        = src.Description,
            Author             = src.Author,
            Version            = src.Version,
            ReleaseTag         = src.ReleaseTag,
            CustomTag          = src.CustomTag,
            Robustness         = src.Robustness,
            IsNative           = src.IsNative,
            ExpectedExeBytes   = src.ExpectedExeBytes,
            AcceptedExeMd5s    = new(src.AcceptedExeMd5s),
            GradientStartHex   = src.GradientStartHex,
            GradientEndHex     = src.GradientEndHex,
            LibraryStatus      = src.LibraryStatus,
            CustomArtFileName   = src.CustomArtFileName,
            NexusSlug          = src.NexusSlug,
        };

        private static bool RoutingRulesChanged(CustomGameProfile before, CustomGameProfile after) =>
            SerializePlanRelevantProfile(before) != SerializePlanRelevantProfile(after);

        private static string SerializePlanRelevantProfile(CustomGameProfile profile)
        {
            var parts = new List<string>
            {
                JsonSerializer.Serialize(profile.RoutingRules, JsonHelper.PrettyOptions),
                JsonSerializer.Serialize(profile.OverlayFolders, JsonHelper.PrettyOptions),
                JsonSerializer.Serialize(profile.CompanionSiblings, JsonHelper.PrettyOptions),
            };
            parts.AddRange(profile.ModTypes.Select(mt => $"{mt.Name}:{JsonSerializer.Serialize(mt.RoutingRules, JsonHelper.PrettyOptions)}"));
            return string.Join("\n", parts);
        }


        // ====================================================================
        //  FAVORITES  (Block F polish)
        // ====================================================================

        private void StarButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModItem mod)
            {
                mod.IsFavorite = !mod.IsFavorite;
                SaveModsCustom();
            }
        }

        private void MenuToggleFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (Cust_ModList.SelectedItem is ModItem mod)
            {
                mod.IsFavorite = !mod.IsFavorite;
                SaveModsCustom();
                NotificationService.ShowInfo(mod.IsFavorite ? $"Starred '{mod.Name}'" : $"Unstarred '{mod.Name}'");
            }
        }
    }
}
