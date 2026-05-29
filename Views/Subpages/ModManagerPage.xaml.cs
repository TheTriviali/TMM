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
                ? "Directory not set"
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
                IntegrityState.SizeMismatch  => ("⚠ Size mismatch",      new SolidColorBrush(Color.FromRgb(216, 163, 26))),
                IntegrityState.Md5Mismatch   => ("⚠ MD5 mismatch",       new SolidColorBrush(Color.FromRgb(216, 163, 26))),
                IntegrityState.FileMissing   => ("⚠ Exe missing",        new SolidColorBrush(Color.FromRgb(224, 112, 112))),
                _                            => ("",                     Brushes.Gray),
            };
            Cust_txtIntegrityState.Text = label;
            Cust_txtIntegrityState.Foreground = color;
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
                await _core.OnModAddedAsync(_customProfile.Key, modName);
                NotificationService.ShowSuccess($"Installed '{modName}'.");
                _core.Activity.Record(ActivityKind.ModAdded, _customProfile.Key, _customConfig.GameName, $"Installed '{modName}'");

                var proxies = ProxyDllDetector.Scan(destFolder);
                if (proxies.Count > 0)
                {
                    string proxyList = string.Join(", ", proxies.Select(p => p.FileName));
                    NotificationService.ShowInfo($"'{modName}' contains proxy loader(s): {proxyList}");
                    Logger.Info($"Proxy DLL detected in '{modName}': {string.Join("; ", proxies.Select(p => $"{p.FileName} ({p.Reason})"))}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to install '{modName}'", ex);
                NotificationService.ShowError($"Failed to install '{modName}': {ex.Message}");
            }
        }

        private async void BtnImportFromGame_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_customConfig.GameDirectory) || !Directory.Exists(_customConfig.GameDirectory))
            {
                MessageBox.Show("Set the game directory first, then try import again.",
                    "Import Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowDeployOverlay("Scanning existing install...");
            List<ModImportCandidate> candidates;
            try
            {
                candidates = await new ModImporter().ScanAsync(_customConfig.GameDirectory, _customConfig, CancellationToken.None);
            }
            catch (Exception ex)
            {
                HideDeployOverlay();
                NotificationService.ShowError($"Import scan failed: {ex.Message}");
                return;
            }
            finally
            {
                HideDeployOverlay();
            }

            if (candidates.Count == 0)
            {
                MessageBox.Show("No obvious mod candidates were found in the current game folder.",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var review = new ImportReviewWindow(candidates) { Owner = Window.GetWindow(this) };
            if (review.ShowDialog() != true)
                return;

            var selected = review.GetSelectedCandidates();
            if (selected.Count == 0)
                return;

            if (MessageBox.Show(
                    $"Move {selected.Count} detected mod(s) into TMM and redeploy them to preserve the current install?",
                    "Import Existing Install",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            ShowDeployOverlay("Importing existing install...");
            try
            {
                var importedMods = await new ModImporter().ImportAsync(
                    _core, _customProfile.Key, _customConfig.GameDirectory, _customConfig, selected, CancellationToken.None);

                foreach (var mod in importedMods)
                {
                    SyncModInfoToFolder(mod);
                    _modsCustom.Add(mod);
                }

                for (int i = 0; i < _modsCustom.Count; i++)
                    _modsCustom[i].LoadOrder = i;

                SaveModsCustom();
                await _core.DeployCustomGameModsAsync(_customProfile, _customConfig, _modsCustom);
                NotificationService.ShowSuccess($"Imported {importedMods.Count} mod(s).");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Import failed: {ex.Message}");
            }
            finally
            {
                HideDeployOverlay();
                await RefreshCustomAsync();
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
                plans = new List<(ModItem, DeploymentPlan)>(enabled.Count);
                foreach (var mod in enabled)
                {
                    if (!Directory.Exists(mod.RawFolderPath)) continue;
                    plans.Add((mod, await _core.GetDeploymentPlanAsync(_customProfile.Key, mod, _customConfig)));
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
            var previousConfig = CloneProfile(_customConfig);
            var wizard = new CustomGameSetupWizard(_customConfig) { Owner = Window.GetWindow(this) };
            if (wizard.ShowDialog() != true || wizard.Result is null) return;
            var updatedConfig = wizard.Result;
            bool routingChanged = RoutingRulesChanged(previousConfig, updatedConfig);
            _customConfig = updatedConfig;
            await GameRegistry.Instance.UpdateCustomGameAsync(_customProfile.Key, _customConfig);

            if (routingChanged)
            {
                var affectedMods = _modsCustom.Where(m => Directory.Exists(m.RawFolderPath)).ToList();
                if (affectedMods.Count > 0)
                {
                    var confirm = MessageBox.Show(
                        $"{affectedMods.Count} existing mods have stale plans. Replan all now?",
                        "Routing Rules Changed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        ShowDeployOverlay("Replanning deployment plans...");
                        try
                        {
                            foreach (var mod in affectedMods)
                                await _core.OnModAddedAsync(_customProfile.Key, mod.Name);
                        }
                        finally
                        {
                            HideDeployOverlay();
                        }
                    }
                }
            }

            await RefreshCustomAsync();
        }

        private async void BtnRefreshCustom_Click(object sender, RoutedEventArgs e)
        {
            await _core.RefreshAllModListsAsync();
            await RefreshCustomAsync();
        }

        private void TxtSearchCustom_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshCustomView();
        }

        private void BtnClearSearchCustom_Click(object sender, RoutedEventArgs e)
        {
            Cust_txtSearch.Text = "";
            RefreshCustomView();
        }

        private void Cust_ShowGroupsChanged(object sender, RoutedEventArgs e)
        {
            _showGroups = Cust_chkShowGroups.IsChecked == true;
            RefreshCustomView();
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
        //  LOADOUTS  (Block D)
        // ====================================================================

        private void BtnLoadouts_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var saveItem = new MenuItem { Header = "Save Current Loadout..." };
            saveItem.Click += async (_, _) => await SaveLoadoutFlow();
            menu.Items.Add(saveItem);

            var loadouts = _core.ListLoadouts(_customProfile.Key).OrderBy(n => n).ToList();
            if (loadouts.Count >= 2)
            {
                var diffItem = new MenuItem { Header = "Compare Loadouts..." };
                diffItem.Click += (_, _) =>
                    new LoadoutDiffWindow(_core, _customProfile.Key, loadouts) { Owner = Window.GetWindow(this) }.ShowDialog();
                menu.Items.Add(diffItem);
            }

            if (loadouts.Count > 0)
            {
                menu.Items.Add(new Separator());
                foreach (var name in loadouts)
                {
                    var item = new MenuItem { Header = name };

                    var applyItem = new MenuItem { Header = "Apply" };
                    applyItem.Click += async (_, _) =>
                    {
                        await _core.ApplyLoadoutAsync(_customProfile.Key, name, _modsCustom);
                        SaveModsCustom();
                        _core.Activity.Record(ActivityKind.LoadoutApplied, _customProfile.Key, _customConfig.GameName, $"Applied '{name}'");
                        NotificationService.ShowSuccess($"Applied loadout '{name}'");
                    };
                    item.Items.Add(applyItem);

                    var renameItem = new MenuItem { Header = "Rename..." };
                    renameItem.Click += (_, _) => RenameLoadoutFlow(name);
                    item.Items.Add(renameItem);

                    var exportItem = new MenuItem { Header = "Export as .tmmpack..." };
                    exportItem.Click += async (_, _) => await ExportLoadoutFlow(name);
                    item.Items.Add(exportItem);

                    item.Items.Add(new Separator());

                    var deleteItem = new MenuItem { Header = "Delete", Foreground = Brushes.IndianRed };
                    deleteItem.Click += (_, _) => DeleteLoadoutFlow(name);
                    item.Items.Add(deleteItem);

                    menu.Items.Add(item);
                }
            }

            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }

        private async Task SaveLoadoutFlow()
        {
            var dlg = new RenameWindow("", "Save Loadout", "Name:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName)) return;

            string name = dlg.NewName.Trim();
            if (_core.LoadoutExists(_customProfile.Key, name))
            {
                var result = MessageBox.Show($"A loadout named '{name}' already exists. Overwrite?",
                    "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            await _core.SaveLoadoutAsync(_customProfile.Key, name, _modsCustom);
            _core.Activity.Record(ActivityKind.LoadoutSaved, _customProfile.Key, _customConfig.GameName, $"Saved '{name}'", _modsCustom.Count);
            NotificationService.ShowSuccess($"Saved loadout '{name}'");
        }

        private void RenameLoadoutFlow(string oldName)
        {
            var dlg = new RenameWindow(oldName, "Rename Loadout", "New name:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName) || dlg.NewName == oldName) return;

            string newName = dlg.NewName.Trim();
            if (_core.LoadoutExists(_customProfile.Key, newName))
            {
                NotificationService.ShowWarning($"A loadout named '{newName}' already exists.");
                return;
            }

            if (_core.RenameLoadout(_customProfile.Key, oldName, newName))
                NotificationService.ShowSuccess($"Renamed to '{newName}'");
            else
                NotificationService.ShowError("Rename failed.");
        }

        private void DeleteLoadoutFlow(string name)
        {
            var result = MessageBox.Show($"Delete loadout '{name}'? This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            if (_core.DeleteLoadout(_customProfile.Key, name))
                NotificationService.ShowSuccess($"Deleted '{name}'");
            else
                NotificationService.ShowError("Delete failed.");
        }

        private async Task ExportLoadoutFlow(string loadoutName)
        {
            var sfd = new SaveFileDialog
            {
                Filter   = "TMM Pack|*.tmmpack",
                FileName = $"{loadoutName}.tmmpack",
                Title    = "Export Loadout",
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                int modCount = await TmmPackBuilder.ExportAsync(
                    _core, _customProfile.Key, _customConfig.GameName, loadoutName, sfd.FileName);
                NotificationService.ShowSuccess($"Exported '{loadoutName}' ({modCount} mods)");
            }
            catch (Exception ex)
            {
                Logger.Error($"Export failed for loadout '{loadoutName}'", ex);
                NotificationService.ShowError($"Export failed: {ex.Message}");
            }
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
