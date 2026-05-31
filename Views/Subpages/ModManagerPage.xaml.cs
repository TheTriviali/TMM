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
            WireHeader();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens <paramref name="entry"/> in the workspace. <paramref name="tab"/> selects
        /// the initial sub-tab (Mods · Conflicts · Backups · Downloads · Config); null keeps
        /// the default ("Mods").
        /// </summary>
        public void LoadEntry(LibraryEntry entry, BackendCore core, string? tab = null)
        {
            _core  = core;
            _entry = entry;

            panelCustom.Visibility      = Visibility.Collapsed;
            panelPlaceholder.Visibility = Visibility.Collapsed;

            if (entry.IsPlaceholder)
            {
                Placeholder_txtName.Text = entry.DisplayName;
                Cust_Header.LoadPlaceholder(entry);
                panelPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            InitCustomGame();
            ShowTab(tab ?? "Mods");
            panelCustom.Visibility = Visibility.Visible;
        }

        /// <summary>The sub-tab currently shown, so the shell can restore it on return.</summary>
        public string CurrentTab => _currentTab;

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
            InitBackupsTab();
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
            // Kick off background conflict analysis to populate inline badges.
            ScheduleConflictAnalysis();
        }

        private void UpdateSidebarCustom()
        {
            Cust_txtSidebarGameName.Text = _customConfig.GameName;
            Cust_txtSidebarDir.Text = string.IsNullOrEmpty(_customConfig.GameDirectory)
                ? TMM.Services.LocalizationService.Instance["ModManager_DirectoryNotSet"]
                : _customConfig.GameDirectory;

            // Refresh the workspace header identity/meta/loadout switcher.
            UpdateHeader();

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

            // Update folder-set banner visibility
            UpdatePathAffordances();
        }

        private void UpdatePathAffordances()
        {
            bool isPathSet = !string.IsNullOrEmpty(_customConfig.GameDirectory) &&
                             Directory.Exists(_customConfig.GameDirectory);
            Cust_SetFolderBanner.Visibility = isPathSet ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void BtnBrowseGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_customProfile == null) return;

            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"Select {_customConfig.GameName} Folder" };
            if (dlg.ShowDialog() != true) return;

            // Update the in-memory config so the sidebar, banner, and deploy button
            // re-evaluate immediately (RefreshCustomAsync does not re-read GamePaths).
            _customConfig.GameDirectory = dlg.FolderName;

            // Reflect the new path immediately — don't wait for the full async refresh.
            UpdatePathAffordances();
            UpdateSidebarCustom();

            // Distinguish between built-in and custom games for persistence.
            var builtInProfile = GameProfile.All.FirstOrDefault(p => p.Key == _customProfile.Key);
            if (builtInProfile != null)
                _core.SetVanillaPath(builtInProfile, dlg.FolderName); // built-in: Settings.GamePaths
            else
                GameRegistry.Instance.SaveCustomGameSync(_customProfile.Key, _customConfig); // custom: GameRegistry

            await RefreshCustomAsync();
            NotificationService.ShowSuccess($"Game folder set to {Path.GetFileName(dlg.FolderName)}");
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

            Cust_Header.SetDeployEnabled(_pendingCustom);
            UpdateHeaderPending();
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
            // Keep chip counts in sync after any mod list mutation.
            if (Cust_FilterChips?.IsLoaded == true) RefreshFilterChips();
        }

        private void RefreshCustomView()
        {
            var view = CollectionViewSource.GetDefaultView(_modsCustom);
            if (view is null) return;

            // Auto-group only when at least one mod has a group assigned.
            bool hasGroups = _modsCustom.Any(m => !string.IsNullOrWhiteSpace(m.GroupName));
            using (view.DeferRefresh())
            {
                view.GroupDescriptions.Clear();
                if (hasGroups)
                    view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ModItem.GroupName)));
            }

            // Apply chip + search filter together.
            ApplyChipFilter();

            string q = Cust_txtSearch?.Text?.Trim() ?? "";
            Cust_EmptyState.Visibility = _modsCustom.Count == 0 && string.IsNullOrEmpty(q)
                ? Visibility.Visible
                : Visibility.Collapsed;
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
                NotificationService.ShowWarning($"No rollback snapshots found for {profile.DisplayName}.", "Rollback");
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
                NotificationService.ShowSuccess($"{profile.DisplayName} rolled back.", "Rollback");
            }
            catch (IOException ex)
            {
                Logger.Error("Rollback failed (IO)", ex);
                NotificationService.ShowError($"Rollback failed: {ex.Message}", "Rollback", "TMM-E002");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Error("Rollback failed (access denied)", ex);
                NotificationService.ShowError($"Rollback failed — access denied: {ex.Message}", "Rollback", "TMM-E002");
            }
            catch (Exception ex)
            {
                Logger.Error("Rollback failed", ex);
                NotificationService.ShowError($"Rollback failed: {ex.Message}", "Rollback", "TMM-E002");
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
            // Supports both context-menu (uses selected mod) and hover-button (uses Tag).
            var mod = (sender is FrameworkElement fe && fe.Tag is ModItem tagged)
                ? tagged : GetSelectedMod();
            if (mod != null)
            {
                if (Directory.Exists(mod.RawFolderPath))
                    ShellHelper.OpenFolder(mod.RawFolderPath);
                else if (_customProfile != null)
                    ShellHelper.OpenOwnedFolder(Path.Combine(_core.AppDataPath, _customProfile.RawFolderName));
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
            var mod = (sender is FrameworkElement fe && fe.Tag is ModItem tagged)
                ? tagged : GetSelectedMod();
            if (mod != null)
                new ModPropertiesWindow(mod) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        // ── Active list helper ────────────────────────────────────────────────────

        private ModItem? GetSelectedMod() => Cust_ModList.SelectedItem as ModItem;

        // ── M2: Order inline edit ────────────────────────────────────────────────

        private void OrderDisplay_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2) return;
            e.Handled = true;
            if (sender is not TextBlock display) return;
            var parent = VisualTreeHelper.GetParent(display) as System.Windows.Controls.Grid;
            var edit = parent?.Children.OfType<TextBox>().FirstOrDefault();
            if (edit is null) return;
            edit.Text = display.Text;
            display.Visibility = Visibility.Collapsed;
            edit.Visibility = Visibility.Visible;
            edit.SelectAll();
            edit.Focus();
        }

        private void OrderEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox edit) return;
            CommitOrderEdit(edit);
        }

        private void OrderEdit_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (sender is not TextBox edit) return;
            if (e.Key == System.Windows.Input.Key.Enter) { CommitOrderEdit(edit); e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Escape) { CancelOrderEdit(edit); e.Handled = true; }
        }

        private void CommitOrderEdit(TextBox edit)
        {
            var parent = VisualTreeHelper.GetParent(edit) as System.Windows.Controls.Grid;
            var display = parent?.Children.OfType<TextBlock>().FirstOrDefault();
            if (display is null) return;

            if (edit.DataContext is ModItem mod && int.TryParse(edit.Text, out int newOrder))
            {
                newOrder = Math.Clamp(newOrder, 0, _modsCustom.Count - 1);
                _modsCustom.Remove(mod);
                _modsCustom.Insert(newOrder, mod);
                for (int i = 0; i < _modsCustom.Count; i++) _modsCustom[i].LoadOrder = i;
                foreach (var m in _modsCustom) SyncModInfoToFolder(m);
                _pendingCustom = true;
                UpdateDeployButtonCustom();
                SaveModsCustom();
            }
            edit.Visibility = Visibility.Collapsed;
            display.Visibility = Visibility.Visible;
        }

        private static void CancelOrderEdit(TextBox edit)
        {
            var parent = VisualTreeHelper.GetParent(edit) as System.Windows.Controls.Grid;
            var display = parent?.Children.OfType<TextBlock>().FirstOrDefault();
            if (display is null) return;
            edit.Visibility = Visibility.Collapsed;
            display.Visibility = Visibility.Visible;
        }

        // ── M2: Filter chips ──────────────────────────────────────────────────────

        private enum ModFilter { All, Enabled, Conflicts, Favorites }
        private ModFilter _activeFilter = ModFilter.All;

        private void RefreshFilterChips()
        {
            int total     = _modsCustom.Count;
            int enabled   = _modsCustom.Count(m => m.IsEnabled);
            int conflicts = _modsCustom.Count(m => m.ConflictSummary is { } s &&
                                                   (s.OverwritesCount > 0 || s.OverwrittenByCount > 0));
            int favorites = _modsCustom.Count(m => m.IsFavorite);

            Cust_ChipAllText.Text       = $"All  {total}";
            Cust_ChipEnabledText.Text   = $"Enabled  {enabled}";
            Cust_ChipConflictsText.Text = $"Conflicts  {conflicts}";
            Cust_ChipFavoritesText.Text = $"★ Favorites  {favorites}";

            // Active chip = AccentBrush, inactive = ControlBgBrush
            var accent   = (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"];
            var inactive = (System.Windows.Media.Brush)Application.Current.Resources["ControlBgBrush"];
            Cust_ChipAll.Background       = _activeFilter == ModFilter.All       ? accent : inactive;
            Cust_ChipEnabled.Background   = _activeFilter == ModFilter.Enabled   ? accent : inactive;
            Cust_ChipConflicts.Background = _activeFilter == ModFilter.Conflicts ? accent : inactive;
            Cust_ChipFavorites.Background = _activeFilter == ModFilter.Favorites ? accent : inactive;

            var white = System.Windows.Media.Brushes.White;
            var text  = (System.Windows.Media.Brush)Application.Current.Resources["TextBrush"];
            Cust_ChipAllText.Foreground       = _activeFilter == ModFilter.All       ? white : text;
            Cust_ChipEnabledText.Foreground   = _activeFilter == ModFilter.Enabled   ? white : text;
            Cust_ChipConflictsText.Foreground = _activeFilter == ModFilter.Conflicts ? white : text;
            Cust_ChipFavoritesText.Foreground = _activeFilter == ModFilter.Favorites ? white : text;
        }

        private void ApplyChipFilter()
        {
            var view = CollectionViewSource.GetDefaultView(_modsCustom);
            if (view == null) return;

            // Combine chip filter with existing search filter (already applied in RefreshCustomView).
            // Re-apply the full filter here so both work together.
            string q = Cust_txtSearch?.Text?.Trim().ToLowerInvariant() ?? "";
            bool hasGroups = _modsCustom.Any(m => !string.IsNullOrWhiteSpace(m.GroupName));

            view.Filter = obj =>
            {
                if (obj is not ModItem m) return false;

                // Search
                if (!string.IsNullOrEmpty(q) &&
                    !m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) &&
                    !(m.GroupName?.Contains(q, StringComparison.OrdinalIgnoreCase) == true))
                    return false;

                // Chip
                return _activeFilter switch
                {
                    ModFilter.Enabled   => m.IsEnabled,
                    ModFilter.Conflicts => m.ConflictSummary is { } s && (s.OverwritesCount > 0 || s.OverwrittenByCount > 0),
                    ModFilter.Favorites => m.IsFavorite,
                    _                   => true,
                };
            };

            // Group view mirrors existing logic
            if (view is System.Windows.Data.ListCollectionView lcv)
            {
                if (hasGroups && _activeFilter == ModFilter.All && string.IsNullOrEmpty(q))
                    lcv.GroupDescriptions.Clear();
                // Groups are managed by RefreshCustomView; just ensure filter is applied.
            }

            view.Refresh();
            RefreshFilterChips();
        }

        private void ChipAll_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _activeFilter = ModFilter.All;
            ApplyChipFilter();
        }

        private void ChipEnabled_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _activeFilter = ModFilter.Enabled;
            ApplyChipFilter();
        }

        private void ChipConflicts_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _activeFilter = ModFilter.Conflicts;
            ApplyChipFilter();
        }

        private void ChipFavorites_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _activeFilter = ModFilter.Favorites;
            ApplyChipFilter();
        }

        // ── M2: Bulk-action bar ───────────────────────────────────────────────────

        private void Cust_ModList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int count = Cust_ModList.SelectedItems.Count;
            if (count > 1)
            {
                Cust_BulkBar.Visibility = Visibility.Visible;
                Cust_BulkSelLabel.Text  = $"{count} mods selected";
            }
            else
            {
                Cust_BulkBar.Visibility = Visibility.Collapsed;
            }
        }

        private IEnumerable<ModItem> GetBulkSelection() =>
            Cust_ModList.SelectedItems.Cast<ModItem>();

        private void BulkEnable_Click(object sender, RoutedEventArgs e)
        {
            BatchEnable(GetBulkSelection());
            RefreshFilterChips();
        }

        private void BulkDisable_Click(object sender, RoutedEventArgs e)
        {
            BatchDisable(GetBulkSelection());
            RefreshFilterChips();
        }

        private void BulkSetGroup_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new RenameWindow("", "Set Group", "Group name:")
            {
                Owner = Window.GetWindow(this),
            };
            if (dlg.ShowDialog() != true) return;
            BatchSetGroup(GetBulkSelection(), dlg.NewName);
            RefreshFilterChips();
        }

        private void BulkRemove_Click(object sender, RoutedEventArgs e)
        {
            BatchRemove(GetBulkSelection().ToList());
            RefreshFilterChips();
        }

        // ── M2: Conflict badge toggle ─────────────────────────────────────────────

        private void ConflictBadge_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is ModItem mod)
            {
                mod.IsConflictExpanded = !mod.IsConflictExpanded;
                e.Handled = true;
            }
        }

        // ── Entry control buttons ────────────────────────────────────────────────

        private void EntryMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: ModItem mod }) CustMoveUp(mod);
        }

        private void EntryMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: ModItem mod }) CustMoveDown(mod);
        }

        // ── M2: Background conflict analysis ─────────────────────────────────────

        private readonly TMM.Services.ConflictAnalyzer _conflictAnalyzer = new();

        private void ScheduleConflictAnalysis()
        {
            if (_customProfile is null) return;
            // Run off-thread; update UI on return.
            var profile = _customProfile;
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var plans = _modsCustom
                        .Where(m => m.IsEnabled)
                        .Select(m =>
                        {
                            string planPath = System.IO.Path.Combine(m.RawFolderPath, "_tmm", "deployplan.json");
                            TMM.Services.DeploymentPlan? plan = null;
                            if (System.IO.File.Exists(planPath))
                            {
                                try
                                {
                                    plan = System.Text.Json.JsonSerializer.Deserialize<TMM.Services.DeploymentPlan>(
                                        System.IO.File.ReadAllText(planPath), JsonHelper.PrettyOptions);
                                }
                                catch { /* corrupt plan — skip */ }
                            }
                            return (m, plan);
                        })
                        .Where(t => t.plan is not null)
                        .Select(t => (t.m, t.plan!))
                        .ToList();

                    var summaries = _conflictAnalyzer.AnalyzeByMod(plans);
                    int clashCount = _conflictAnalyzer.Analyze(plans).Count
                                   + _conflictAnalyzer.AnalyzeProxyConflicts(plans).Count;

                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        foreach (var mod in _modsCustom)
                        {
                            mod.ConflictSummary = summaries.TryGetValue(mod.Name, out var s) ? s : null;
                        }
                        RefreshFilterChips();
                        UpdateConflictsTabBadge(clashCount);
                        if (_currentTab == "Conflicts") RenderConflictsTab();
                    });
                }
                catch { /* analysis is best-effort */ }
            });
        }

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
            ModCategories      = new(src.ModCategories),
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
