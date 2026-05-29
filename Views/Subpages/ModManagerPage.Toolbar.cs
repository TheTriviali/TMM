using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// ModManagerPage — toolbar action handlers: install, import-from-game, deploy,
    /// rollback, launch, edit-config, refresh, and search/grouping.
    /// </summary>
    public partial class ModManagerPage
    {
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

            var review = new ImportReviewWindow(candidates, _customConfig.GameDirectory) { Owner = Window.GetWindow(this) };
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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exeFull) { UseShellExecute = true });
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
    }
}
