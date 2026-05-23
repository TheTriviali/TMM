// TABLE OF CONTENTS
// -----------------------------------------------------------------
//   STATE  (_core, _activeProfile, drag state, flags) ............ ~23
//   INIT  (constructor, Window_Loaded) ........................... ~42
//   UI REFRESH  (RefreshUIAsync) ................................. ~76
//   TITLEBAR THEMING
//     ApplyTitlebarStyle() ........................................ ~134
//     UpdateControlLayout() (static helper) ...................... ~236
//   INSTALLATION & DOWNLOADS
//     ProcessDownloadsBatchAsync() ................................ ~270
//     ProcessExeInstallation() .................................... ~315
//     ProcessModInstallationAsync() ............................... ~351
//     ProcessDxvkArchiveAsync() ................................... ~464
//     CleanModloaderFolder() (static) ............................ ~545
//   JSON PERSISTENCE
//     LoadModsFromJson() .......................................... ~582
//     SaveMods() .................................................. ~604
//     SyncModInfoToFolder() (static) ............................. ~621
//     ShowExtractionDebug() ....................................... ~633
//   DRAG & DROP / REORDERING
//     List_Drop() ................................................. ~662
//     List_PreviewMouseLeftButtonDown() / List_MouseMove() ........ ~745
//     List_DragOver() / List_DragLeave() ......................... ~765
//     GetDropLine() / HideDropLine() ............................. ~783
//     GetInsertionLineY() / GetInsertionIndex() .................. ~797
//   INSTALL / DEPLOY HANDLERS
//     BtnInstallMod_Click() ....................................... ~831
//     Web auto-installers (Widescreen/SilentPatch/ASI/2DFX/CLEO) .. ~872
//     BtnRefresh_Click() / BtnDeploy_Click() ..................... ~973
//   SIMPLE BUTTON HANDLERS  (Help, About, Settings, ...) ........... ~1103
//     BtnToggleOverride_Click() ................................... ~1147
//   CONTEXT MENU  (Rename/Toggle/Delete/MoveUp/MoveDown/...) ...... ~1218
//   STANDARD BINDINGS  (ModCheckBox, Search, Sort, Keys) ......... ~1409
//   HELPERS  (GetActiveList, ResolveProfile, FlagDeploy) ......... ~1475
// -----------------------------------------------------------------

using SharpCompress.Readers;
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
    public partial class MainDashboardWindow : DashboardWindowBase
    {
        // ==========================================================
        // STATE
        // ==========================================================

        private readonly BackendCore _core;

        // The currently focused game (drives keyboard shortcuts and "active list" lookups).
        private GameProfile _activeProfile = GameProfile.III;

        private bool _isSortedByLoadOrder = true;
        private Point _startPoint;
        private ModItem? _draggedItem;
        private bool _hasPendingChanges = true; // true on startup: game dir may not reflect current mod list
        private bool _needsDowngradeHelp = false;
        private bool _deployReady = false;

        // ==========================================================
        // INIT
        // ==========================================================

        public MainDashboardWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            // Bind each list directly to the per-profile ObservableCollection.
            PListIII.ItemsSource = _core.Mods[GameProfile.III.Key];
            PListVC.ItemsSource = _core.Mods[GameProfile.VC.Key];
            PListSA.ItemsSource = _core.Mods[GameProfile.SA.Key];

            LoadModsFromJson();
        }

        private async void Window_Loaded(object s, RoutedEventArgs e)
        {
            // Initialize GameRegistry and custom games
            await _core.InitializeAsync();

            // Application.Current is guaranteed non-null from here onward.
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);

            // Restore window position and size
            if (_core.Settings.WindowLeft > 0 && _core.Settings.WindowTop > 0)
            {
                Left = _core.Settings.WindowLeft;
                Top = _core.Settings.WindowTop;
            }
            if (_core.Settings.WindowWidth > 800 && _core.Settings.WindowHeight > 600)
            {
                Width = _core.Settings.WindowWidth;
                Height = _core.Settings.WindowHeight;
            }

            if (_core.Settings.FirstLaunch)
            {
                var setupWin = new InitialSetupWindow(_core) { Owner = this };
                setupWin.ShowDialog();
            }

            await RefreshUIAsync();
        }

        // ==========================================================
        // UI REFRESH
        // ==========================================================

        public async Task RefreshUIAsync()
        {
            ApplyTitlebarStyle();
            await _core.RefreshAllModListsAsync();
            txtDiskSpace.Text = _core.GetDriveSpaceInfo();

            bool allMapped = GameProfile.All.All(_core.IsGameReady);
            bool anyMapped = GameProfile.All.Any(_core.IsGameReady);

            // Install button needs at least one game mapped.
            btnInstallMod.IsEnabled = anyMapped;

            // Warning shows when ANY game is unmapped (clearer than the old wording).
            txtInstallWarning.Visibility = allMapped ? Visibility.Collapsed : Visibility.Visible;

            bool needsDowngradeHelp = false;

            foreach (var profile in GameProfile.All)
            {
                bool isReady    = _core.IsGameReady(profile);
                var  status     = await _core.VerifyGameStatusAsync(profile);
                bool hasOverride = _core.HasExeModOverride(profile);

                // Only flag as needing help if vanilla AND no override active
                if (status == ExeStatus.Vanilla && !hasOverride) needsDowngradeHelp = true;

                if (FindName($"OverlayCol{profile.Key}") is Button col)
                    col.Visibility = isReady ? Visibility.Collapsed : Visibility.Visible;

                if (FindName($"btnPlay{profile.Key}") is Button playBtn)
                {
                    if (!isReady)
                        playBtn.Background = UiColors.DisabledBrush;
                    else if (status == ExeStatus.Vanilla && !hasOverride)
                        // Red = vanilla exe, no override - deploy works but game won't launch
                        playBtn.Background = new SolidColorBrush(Color.FromRgb(180, 45, 45));
                    else if (status == ExeStatus.Vanilla && hasOverride)
                        // Orange = vanilla but override enabled - can deploy, game launch will still need 1.0 exe
                        playBtn.Background = UiColors.PendingBrush;
                    else
                        playBtn.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "AccentBrush");
                }
            }

            _needsDowngradeHelp = needsDowngradeHelp;
            HelpNotificationDot.Visibility = needsDowngradeHelp ? Visibility.Visible : Visibility.Collapsed;

            bool anyReady = GameProfile.All.Any(_core.IsGameReady);
            _deployReady = anyReady && _hasPendingChanges;
            if (_deployReady)
                btnDeploy.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "AccentBrush");
            else
                btnDeploy.Background = UiColors.DisabledBrush;

            btnDeploy.ToolTip = _deployReady ? "Deploy Mods (F5)" : "No pending changes to deploy";
        }

        // ==========================================================
        // TITLEBAR THEMING
        // ==========================================================

        /// <summary>Public so ThemeManagerWindow can invoke it after settings change.</summary>
        public void ApplyTitlebarStyle()
        {
            // Guard: Application.Current can be null during very early startup
            // (e.g., immediately after factory reset + process restart).
            if (Application.Current == null) return;

            // Maximized windows use square corners.
            bool squarify = WindowState == WindowState.Maximized;
            MainWindowBorder.CornerRadius = squarify ? new CornerRadius(0) : new CornerRadius(10);
            TitleBarBorder.CornerRadius   = squarify ? new CornerRadius(0) : new CornerRadius(10, 10, 0, 0);
            if (MainWindowBorder.Child is Border innerBorder)
            {
                var vb = innerBorder.OpacityMask as VisualBrush;
                if (vb?.Visual is Border maskBorder)
                    maskBorder.CornerRadius = squarify ? new CornerRadius(0) : new CornerRadius(11);
            }

            // Hide both control groups, then show the right one.
            VanillaControls.Visibility  = Visibility.Collapsed;
            CompactControls.Visibility  = Visibility.Collapsed;

            if (_core.Settings.TitlebarTheme == "Compact")
            {
                TitleBarBorder.Visibility  = Visibility.Collapsed;
                CompactControls.Visibility = Visibility.Visible;
            }
            else
            {
                // Default: W11 Vanilla (also handles any legacy/unknown theme value)
                TitleBarBorder.Visibility  = Visibility.Visible;
                TitleBarBorder.Background  = Brushes.Transparent;
                TitleBarBorder.Opacity     = 1.0;
                VanillaControls.Visibility = Visibility.Visible;
            }

            // Apply font (cascades to all child controls in the main window)
            ThemeEngine.ApplyFont(this, _core.Settings);
        }

        // ==========================================================
        // INSTALLATION & DOWNLOADS
        // ==========================================================

        private async Task ProcessDownloadsBatchAsync(List<(string url, string hintKey, string expectedName)> batch)
        {
            // Duplicate detection: if any target already exists, prompt before redownloading.
            bool anyDuplicates = batch.Any(item =>
            {
                var keys = GameProfile.All.Any(p => p.Key == item.hintKey)
                    ? new[] { item.hintKey }
                    : GameProfile.All.Select(p => p.Key).ToArray();
                return keys.Any(k => Directory.Exists(Path.Combine(_core.AppDataPath, $"ModsRaw{k}", item.expectedName)));
            });

            if (anyDuplicates &&
                MessageBox.Show("Duplicate mods detected. Download anyway?", "Warning", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            this.IsEnabled = false;
            DialogOverlay.Visibility = Visibility.Visible;

            var downloaded = new List<(string path, string hint)>();
            try
            {
                foreach (var item in batch)
                {
                    string path = Path.Combine(_core.DownloadCachePath, Path.GetFileName(new Uri(item.url).LocalPath));
                    await _core.DownloadFileAsync(item.url, path);
                    downloaded.Add((path, item.hintKey));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed:\n{ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
                DialogOverlay.Visibility = Visibility.Collapsed;
            }

            foreach (var file in downloaded)
                await ProcessModInstallationAsync(file.path, file.hint);

            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        /// <summary>Drag-and-dropped exe goes straight in as a load-order-0 mod (downgrade).</summary>
        private void ProcessExeInstallation(string filePath, GameProfile target)
        {
            string fileName = Path.GetFileName(filePath).ToLowerInvariant();
            string modName = Path.GetFileNameWithoutExtension(filePath);
            bool isDowngrade = fileName == target.ExeName.ToLowerInvariant();

            if (isDowngrade) modName = $"GTA {target.Key} 1.0 Downgrade";

            string targetFolder = Path.Combine(_core.AppDataPath, target.RawFolderName, modName);
            Directory.CreateDirectory(targetFolder);
            File.Copy(filePath, Path.Combine(targetFolder, Path.GetFileName(filePath)), true);

            var list = _core.Mods[target.Key];
            int loadOrder = isDowngrade ? 0 : (list.Count > 0 ? list.Max(x => x.LoadOrder) + 1 : 0);
            if (isDowngrade) foreach (var m in list) m.LoadOrder++;

            var item = new ModItem
            {
                Name = modName,
                RawFolderPath = targetFolder,
                PackedFilePath = filePath,
                IsEnabled = true,
                LoadOrder = loadOrder
            };
            list.Add(item);

            // Visual reorder by LoadOrder.
            var sorted = list.OrderBy(x => x.LoadOrder).ToList();
            list.Clear();
            foreach (var i in sorted) list.Add(i);

            SyncModInfoToFolder(item);
            SaveMods();
            FlagDeploy();
        }

        private async Task ProcessModInstallationAsync(string filePath, string hintKey = "")
        {
            string staging = Path.Combine(
                _core.DownloadCachePath,
                Path.GetFileNameWithoutExtension(filePath).Replace(".tar", "", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(staging);

            try
            {
                // DXVK gets special handling - we extract just the d3d8/d3d9 DLLs.
                if (filePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) &&
                    filePath.Contains("dxvk", StringComparison.OrdinalIgnoreCase))
                {
                    await ProcessDxvkArchiveAsync(filePath, staging, hintKey);
                    return;
                }

                if (filePath.EndsWith(".zip") || filePath.EndsWith(".7z") ||
                    filePath.EndsWith(".rar") || filePath.EndsWith(".tar.gz"))
                {
                    await BackendCore.ExtractArchiveSafeAsync(filePath, staging);

                    // Single-subdirectory unwrap: many archives wrap their content in one
                    // top-level folder (e.g. "GTA VC 1.0 Downgrade/gta-vc.exe"). If that
                    // pattern is detected, promote the subdirectory to the staging root so
                    // the rest of the pipeline sees files at the expected depth.
                    var rootEntries = Directory.GetFileSystemEntries(staging);
                    if (rootEntries.Length == 1 && Directory.Exists(rootEntries[0]))
                    {
                        // Move everything from the single subdir up to staging root.
                        string singleSub = rootEntries[0];
                        foreach (var entry in Directory.GetFileSystemEntries(singleSub))
                        {
                            string dest = Path.Combine(staging, Path.GetFileName(entry));
                            if (Directory.Exists(entry))
                            {
                                if (!Directory.Exists(dest))
                                    Directory.Move(entry, dest);
                            }
                            else
                            {
                                // Rename ReadMe.txt alongside a game exe to VC1.0ReadMe.txt etc.
                                string fn = Path.GetFileName(entry);
                                if (fn.Equals("ReadMe.txt", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Detect which game this archive is for from hintKey or context
                                    string prefix = !string.IsNullOrEmpty(hintKey) && hintKey != "ALL"
                                        ? $"{hintKey}1.0" : "Mod";
                                    dest = Path.Combine(staging, $"{prefix}ReadMe.txt");
                                }
                                if (!File.Exists(dest))
                                    File.Move(entry, dest);
                            }
                        }
                        try { Directory.Delete(singleSub, false); } catch { }
                    }
                }
                else
                {
                    await Task.Run(() => File.Copy(filePath, Path.Combine(staging, Path.GetFileName(filePath)), true));
                }

                var wizard = new ArchiveExtractionWindow(_core, filePath, staging, hintKey) { Owner = this };
                if (wizard.ShowDialog() != true || wizard.SelectedTargets.Count == 0) return;

                foreach (var k in wizard.SelectedTargets)
                {
                    var profile = GameProfile.ByKey(k);
                    if (profile == null) continue;

                    string target = Path.Combine(_core.AppDataPath, profile.RawFolderName, new DirectoryInfo(staging).Name);
                    if (!Directory.Exists(target))
                    {
                        Directory.CreateDirectory(target);
                        _core.CopyDirectory(staging, target);
                    }

                    if (target.Contains("modloader", StringComparison.OrdinalIgnoreCase))
                        CleanModloaderFolder(target, profile.Key);

                    var list = _core.Mods[profile.Key];
                    string modName = new DirectoryInfo(staging).Name;
                    if (!list.Any(m => m.Name == modName))
                    {
                        var item = new ModItem
                        {
                            Name = modName,
                            RawFolderPath = target,
                            PackedFilePath = filePath,
                            IsEnabled = true,
                            LoadOrder = list.Count > 0 ? list.Max(x => x.LoadOrder) + 1 : 0
                        };
                        list.Add(item);
                        SyncModInfoToFolder(item);
                    }
                }
                SaveMods();
                FlagDeploy();
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Extraction failed: {ex.Message}");
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    try { Directory.Delete(staging, true); }
                    catch { /* locked - cleanup happens on next launch */ }
                }
            }
        }

        private async Task ProcessDxvkArchiveAsync(string filePath, string staging, string hintKey)
        {
            string d3d9 = Path.Combine(staging, "d3d9.dll");
            string d3d8 = Path.Combine(staging, "d3d8.dll");

            using (Stream s = File.OpenRead(filePath))
            await using (var reader = await ReaderFactory.OpenAsyncReader(s))
            {
                while (await reader.MoveToNextEntryAsync())
                {
                    if (reader.Entry.IsDirectory ||
                        !reader.Entry.Key!.Contains("x32", StringComparison.OrdinalIgnoreCase)) continue;

                    if (reader.Entry.Key.EndsWith("d3d9.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        await using var es = await reader.OpenEntryStreamAsync();
                        await using var fs = File.Create(d3d9);
                        await es.CopyToAsync(fs);
                    }
                    else if (reader.Entry.Key.EndsWith("d3d8.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        await using var es = await reader.OpenEntryStreamAsync();
                        await using var fs = File.Create(d3d8);
                        await es.CopyToAsync(fs);
                    }
                }
            }

            if (!File.Exists(d3d9))
                throw new Exception("Could not find x32/d3d9.dll in the DXVK archive.");

            MessageBox.Show(
                "DXVK Installation Note:\n\n" +
                "* GTA SA: Uses d3d9.dll (Vulkan translation)\n" +
                "* GTA III/VC: Uses d3d8.dll (DXVK utilizes a proxy for legacy titles)",
                "DXVK Deployment", MessageBoxButton.OK, MessageBoxImage.Information);

            var wizard = new ArchiveExtractionWindow(_core, filePath, staging, hintKey) { Owner = this };
            if (wizard.ShowDialog() != true || wizard.SelectedTargets.Count == 0) return;

            foreach (string k in wizard.SelectedTargets)
            {
                var profile = GameProfile.ByKey(k);
                if (profile == null) continue;

                string target = Path.Combine(_core.AppDataPath, profile.RawFolderName, "dxvk");
                Directory.CreateDirectory(target);
                File.Copy(d3d9, Path.Combine(target, "d3d9.dll"), true);

                // III and VC need d3d8 instead of d3d9 (they're DX8-era titles).
                if ((profile.Key == "III" || profile.Key == "VC") && File.Exists(d3d8))
                {
                    File.Copy(d3d8, Path.Combine(target, "d3d8.dll"), true);
                    File.Delete(Path.Combine(target, "d3d9.dll"));
                }

                var list = _core.Mods[profile.Key];
                if (!list.Any(m => m.Name == "dxvk"))
                {
                    var item = new ModItem
                    {
                        Name = "dxvk",
                        RawFolderPath = target,
                        PackedFilePath = filePath,
                        IsEnabled = true,
                        LoadOrder = list.Count > 0 ? list.Max(x => x.LoadOrder) + 1 : 0
                    };
                    list.Add(item);
                    SyncModInfoToFolder(item);
                }
            }
            SaveMods();
            FlagDeploy();
        }

        /// <summary>
        /// Post-install fixup for Modloader archives.
        /// SA: modloader.asi stays at the mod root (game root on deploy).
        /// III/VC: modloader.asi must be inside a scripts/ subdirectory - Ultimate ASI
        ///         Loader picks it up from there. The modloader/ data folder stays at root.
        /// </summary>
        private static void CleanModloaderFolder(string target, string profileKey)
        {
            // III/VC require modloader.asi in scripts/ (picked up by Ultimate ASI Loader).
            if (profileKey == "III" || profileKey == "VC")
            {
                string scriptsDir = Path.Combine(target, "scripts");
                Directory.CreateDirectory(scriptsDir);
                string asiSrc = Path.Combine(target, "modloader.asi");
                if (File.Exists(asiSrc))
                    File.Move(asiSrc, Path.Combine(scriptsDir, "modloader.asi"), true);
            }

            // Clean out the modloader/ data subfolder so the user starts fresh.
            string nested = Path.Combine(target, "modloader");
            if (Directory.Exists(nested))
            {
                foreach (var d in Directory.GetDirectories(nested)) Directory.Delete(d, true);
                foreach (var f in Directory.GetFiles(nested)) File.Delete(f);
            }
            else
            {
                Directory.CreateDirectory(nested);
            }

            string leiaMe = Path.Combine(target, "Leia-me.txt");
            if (File.Exists(leiaMe))
                File.Move(leiaMe, Path.Combine(target, "ReadMeModloaderES.txt"), true);

            string readMe = Path.Combine(target, "Readme.txt");
            if (File.Exists(readMe))
                File.Move(readMe, Path.Combine(target, "ReadmeModloader.txt"), true);
        }

        // ==========================================================
        // JSON PERSISTENCE
        // ==========================================================

        private void LoadModsFromJson()
        {
            string p = Path.Combine(_core.AppDataPath, "modlist.json");
            if (!File.Exists(p)) return;

            try
            {
                var d = JsonDocument.Parse(File.ReadAllText(p));
                foreach (var profile in GameProfile.All)
                {
                    if (!d.RootElement.TryGetProperty(profile.Key, out var elem)) continue;

                    var items = JsonSerializer.Deserialize<ObservableCollection<ModItem>>(elem.GetRawText(), JsonHelper.PrettyOptions);
                    if (items == null) continue;

                    var list = _core.Mods[profile.Key];
                    foreach (var m in items.OrderBy(x => x.LoadOrder)) list.Add(m);
                }
            }
            catch (Exception ex) { _core.Log($"LoadModsFromJson failed: {ex.Message}"); }
        }

        private void SaveMods()
        {
            try
            {
                var snapshot = new
                {
                    III = _core.Mods[GameProfile.III.Key],
                    VC = _core.Mods[GameProfile.VC.Key],
                    SA = _core.Mods[GameProfile.SA.Key]
                };
                File.WriteAllText(
                    Path.Combine(_core.AppDataPath, "modlist.json"),
                    JsonSerializer.Serialize(snapshot, JsonHelper.PrettyOptions));
            }
            catch (Exception ex) { _core.Log($"SaveMods failed: {ex.Message}"); }
        }

        private static void SyncModInfoToFolder(ModItem mod)
        {
            try
            {
                if (Directory.Exists(mod.RawFolderPath))
                    File.WriteAllText(
                        Path.Combine(mod.RawFolderPath, "modinfo.txt"),
                        JsonSerializer.Serialize(mod, JsonHelper.PrettyOptions));
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        // ==========================================================
        // DRAG & DROP / REORDERING
        // ==========================================================

        private async void List_Drop(object s, DragEventArgs e)
        {
            if (s is ListView dropList) HideDropLine(dropList);

            // External file drop (real OS files coming in).
            if (e.Data.GetDataPresent(DataFormats.FileDrop) &&
                e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            {
                string path = files[0];
                var targetProfile = ResolveProfileFromList(s) ?? _activeProfile;

                if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    ProcessExeInstallation(path, targetProfile);
                else
                    await ProcessModInstallationAsync(path, targetProfile.Key);

                txtDiskSpace.Text = _core.GetDriveSpaceInfo();
                return;
            }

            // Internal drag - either cross-list copy or in-list reorder.
            if (_draggedItem == null || s is not ListView targetList) return;

            var targetKey = (targetList.Tag?.ToString()) ?? _activeProfile.Key;
            var sourceKey = ResolveProfileFromMod(_draggedItem)?.Key ?? _activeProfile.Key;

            if (sourceKey != targetKey)
            {
                if (MessageBox.Show($"Copy '{_draggedItem.Name}' from GTA {sourceKey} to GTA {targetKey}?",
                    "Copy Mod", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var targetProfile = GameProfile.ByKey(targetKey);
                    if (targetProfile == null) { _draggedItem = null; return; }

                    string targetDir = Path.Combine(_core.AppDataPath, targetProfile.RawFolderName, _draggedItem.Name);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                        _core.CopyDirectory(_draggedItem.RawFolderPath, targetDir);
                    }

                    var list = _core.Mods[targetKey];
                    if (!list.Any(m => m.Name == _draggedItem.Name))
                    {
                        var item = new ModItem
                        {
                            Name = _draggedItem.Name,
                            RawFolderPath = targetDir,
                            PackedFilePath = _draggedItem.PackedFilePath,
                            IsEnabled = true,
                            LoadOrder = list.Count > 0 ? list.Max(x => x.LoadOrder) + 1 : 0
                        };
                        list.Add(item);
                        SyncModInfoToFolder(item);
                    }
                    SaveMods();
                    FlagDeploy();
                }
            }
            else if (s is ListView sameList)
            {
                var l = _core.Mods[(sameList.Tag?.ToString()) ?? _activeProfile.Key];
                int oldIdx = l.IndexOf(_draggedItem);
                int insertIdx = GetInsertionIndex(sameList, e.GetPosition(sameList).Y);
                // When moving down, the remove shifts remaining indices by -1
                if (insertIdx > oldIdx) insertIdx--;
                int newIdx = Math.Clamp(insertIdx, 0, l.Count - 1);
                if (oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx)
                {
                    l.Move(oldIdx, newIdx);
                    for (int i = 0; i < l.Count; i++)
                    {
                        l[i].LoadOrder = i;
                        SyncModInfoToFolder(l[i]);
                    }
                    FlagDeploy();
                    SaveMods();
                }
            }

            _draggedItem = null;
        }

        private void List_PreviewMouseLeftButtonDown(object s, MouseButtonEventArgs e)
        {
            _startPoint = e.GetPosition(null);
            if (e.OriginalSource is FrameworkElement el && el.DataContext is ModItem m)
                _draggedItem = m;

            // Track which list is "active" for shortcut keys.
            if (s is ListView lv)
                _activeProfile = ResolveProfileFromList(lv) ?? _activeProfile;
        }

        private void List_MouseMove(object s, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null &&
                Math.Abs(e.GetPosition(null).X - _startPoint.X) > SystemParameters.MinimumHorizontalDragDistance)
            {
                DragDrop.DoDragDrop((DependencyObject)s, _draggedItem, DragDropEffects.Move);
            }
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            if (sender is not ListView lv || _draggedItem == null) return;
            var line = GetDropLine(lv);
            if (line == null) return;
            var pt = e.GetPosition(lv);
            double y = GetInsertionLineY(lv, pt.Y);
            Canvas.SetTop(line, y - 1);
            line.Width = Math.Max(lv.ActualWidth - 4, 0);
            Canvas.SetLeft(line, 2);
            line.Visibility = Visibility.Visible;
        }

        private void List_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListView lv) HideDropLine(lv);
        }

        private System.Windows.Shapes.Rectangle? GetDropLine(ListView lv) =>
            lv.Name switch
            {
                "PListIII" => DropLineIII,
                "PListVC"  => DropLineVC,
                _          => DropLineSA
            };

        private void HideDropLine(ListView lv)
        {
            var line = GetDropLine(lv);
            if (line != null) line.Visibility = Visibility.Collapsed;
        }

        private static double GetInsertionLineY(ListView lv, double mouseY)
        {
            for (int i = 0; i < lv.Items.Count; i++)
            {
                if (lv.ItemContainerGenerator.ContainerFromIndex(i) is not ListViewItem item) continue;
                var pos = item.TranslatePoint(new Point(0, 0), lv);
                if (mouseY < pos.Y + item.ActualHeight / 2.0)
                    return pos.Y;
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
                if (mouseY < pos.Y + item.ActualHeight / 2.0)
                    return i;
            }
            return lv.Items.Count;
        }

        // ==========================================================
        // INSTALL / DEPLOY HANDLERS
        // ==========================================================

        private async void BtnInstallMod_Click(object s, RoutedEventArgs e)
        {
            var d = new Microsoft.Win32.OpenFileDialog
            {
                Title  = "Install Mod or Game Executable",
                Filter = "All Mod Files|*.zip;*.7z;*.rar;*.tar.gz;*.dll;*.asi;*.cs;*.cm;*.exe" +
                         "|Archives|*.zip;*.7z;*.rar;*.tar.gz" +
                         "|Executables (Downgrade)|*.exe" +
                         "|All Files|*.*"
            };
            if (d.ShowDialog() != true) return;

            string path = d.FileName;

            // .exe gets special treatment: install as a "downgraded exe" mod
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                string exeName = Path.GetFileName(path).ToLowerInvariant();
                // Try to auto-detect which game this exe belongs to
                var matchedProfile = GameProfile.All.FirstOrDefault(p =>
                    p.ExeName.ToLowerInvariant() == exeName);

                if (matchedProfile == null)
                {
                    MessageBox.Show(
                        "Could not match this exe to a known game (gta3.exe / gta-vc.exe / gta-sa.exe).\n\n" +
                        "Drag and drop the exe directly onto the correct game list instead.",
                        "Unrecognised Executable", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ProcessExeInstallation(path, matchedProfile);
            }
            else
            {
                await ProcessModInstallationAsync(path, _activeProfile.Key);
            }
        }

        // ----- Web auto-installers -----

        private async void BtnWidescreenFix_Click(object s, RoutedEventArgs e)
        {
            bool installFrontend = MessageBox.Show(
                "Also install the Widescreen Frontend interface? (Overwrites LOADSCS.txd)",
                "Frontend?", MessageBoxButton.YesNo) == MessageBoxResult.Yes;

            var batch = new List<(string url, string hintKey, string expectedName)>();
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string repoTag = profile.Key switch { "III" => "gta3", "VC" => "gtavc", _ => "gtasa" };
                string assetPrefix = profile.Key switch { "III" => "GTA3", "VC" => "GTAVC", _ => "GTASA" };

                batch.Add(($"https://github.com/ThirteenAG/WidescreenFixesPack/releases/download/{repoTag}/{assetPrefix}.WidescreenFix.zip",
                    profile.Key, $"{assetPrefix}.WidescreenFix"));

                if (installFrontend)
                    batch.Add(($"https://github.com/ThirteenAG/WidescreenFixesPack/releases/download/{repoTag}/{assetPrefix}.WidescreenFrontend.zip",
                        profile.Key, $"{assetPrefix}.WidescreenFrontend"));
            }

            if (batch.Count > 0) await ProcessDownloadsBatchAsync(batch);
        }

        private async void BtnSilentPatch_Click(object s, RoutedEventArgs e)
        {
            var batch = GameProfile.All
                .Where(_core.IsGameReady)
                .Select(p => (
                    url: $"https://github.com/CookiePLMonster/SilentPatch/releases/latest/download/SilentPatch{p.Key}.zip",
                    hintKey: p.Key,
                    expectedName: $"SilentPatch{p.Key}"))
                .ToList();

            if (batch.Count > 0) await ProcessDownloadsBatchAsync(batch);
            else MessageBox.Show("Please deploy or prepare at least one game before installing SilentPatch.");
        }

        private async void BtnProject2DFX_Click(object s, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string releaseTag = profile.Key switch
                {
                    "III" => "gta3",
                    "VC" => "gtavc",
                    "SA" => "gtasa",
                    _ => profile.Key.ToLowerInvariant()
                };
                await DownloadAndInstallDirectAsync(
                    $"https://github.com/ThirteenAG/III.VC.SA.IV.Project2DFX/releases/download/{releaseTag}/{profile.Key}.Project2DFX.zip",
                    profile.Key);
            }
        }

        private async void BtnASILoader_Click(object s, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string asset = profile.Key == "SA" ? "dinput8" : "dsound";
                await DownloadAndInstallDirectAsync(
                    $"https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases/download/Win32-latest/{asset}-Win32.zip",
                    profile.Key);
            }
        }

        private async Task DownloadAndInstallDirectAsync(string url, string hintKey)
        {
            try
            {
                string p = Path.Combine(_core.DownloadCachePath, Path.GetFileName(new Uri(url).LocalPath));
                await _core.DownloadFileAsync(url, p);
                await ProcessModInstallationAsync(p, hintKey);
                txtDiskSpace.Text = _core.GetDriveSpaceInfo();
            }
            catch (Exception ex) { MessageBox.Show($"Failed: {ex.Message}"); }
        }

        // ----- Refresh / deploy -----

        private async void BtnRefresh_Click(object s, RoutedEventArgs e)
        {
            if (MessageBox.Show("Rebuild mod list from folders?", "Sync", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            foreach (var profile in GameProfile.All) _core.Mods[profile.Key].Clear();

            foreach (var profile in GameProfile.All)
            {
                string root = Path.Combine(_core.AppDataPath, profile.RawFolderName);
                if (!Directory.Exists(root)) continue;

                foreach (var d in Directory.GetDirectories(root))
                {
                    string info = Path.Combine(d, "modinfo.txt");
                    var list = _core.Mods[profile.Key];
                    list.Add(File.Exists(info)
                        ? JsonSerializer.Deserialize<ModItem>(File.ReadAllText(info), JsonHelper.PrettyOptions)!
                        : new ModItem { Name = Path.GetFileName(d), RawFolderPath = d, IsEnabled = true, LoadOrder = list.Count });
                }
            }

            SaveMods();
            await RefreshUIAsync();
        }

        private async void BtnDeploy_Click(object s, RoutedEventArgs e)
        {
            if (!_deployReady)
            {
                if (_needsDowngradeHelp)
                    MessageBox.Show(
                        "This game requires a 1.0 downgraded executable to run with mods.\n\n" +
                        "Download links can be found on the TMM GitHub page:\n" +
                        "https://github.com/triviali/tgtamm",
                        "Downgrade Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_isSortedByLoadOrder)
            {
                var result = MessageBox.Show(
                    "Note: Your list is currently sorted by Name.\n\n" +
                    "Mods will still be deployed according to their assigned Load Order priority (#).\n\n" +
                    "Continue with deployment?",
                    "Deployment Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel) return;
            }

            this.IsEnabled = false;
            DialogOverlay.Visibility     = Visibility.Visible;
            DeployProgressPanel.Visibility = Visibility.Visible;
            pbDeploy.IsIndeterminate     = true;
            txtDeployStage.Text          = "Preparing...";
            txtDeployCount.Text          = "";

            var deployed = new List<string>();
            var vanillaExeWarnings = new List<string>();

            var progress = new Progress<DeploymentProgress>(p =>
            {
                txtDeployStage.Text = p.Stage;
                if (p.Total > 0)
                {
                    pbDeploy.IsIndeterminate = false;
                    pbDeploy.Value           = (double)p.Current / p.Total * 100;
                    txtDeployCount.Text      = $"{p.Current} / {p.Total} files";
                }
                else
                {
                    pbDeploy.IsIndeterminate = true;
                    txtDeployCount.Text      = "";
                }
            });

            try
            {
                foreach (var profile in GameProfile.All)
                {
                    if (string.IsNullOrEmpty(_core.GetVanillaPath(profile))) continue;

                    txtDeployStage.Text = $"Deploying {profile.Key}...";
                    await _core.DeployModsAsync(profile, _core.Mods[profile.Key], progress);
                    deployed.Add(profile.Key);

                    // Warn (don't block) if exe is still vanilla — mods deploy fine but game won't launch.
                    var exeStatus = await _core.VerifyGameStatusAsync(profile);
                    if (exeStatus == ExeStatus.Vanilla)
                        vanillaExeWarnings.Add(profile.Key);
                }

                if (deployed.Count > 0) _hasPendingChanges = false;

                string summary = "";
                if (deployed.Count > 0) summary += $"[OK] Deployed: {string.Join(", ", deployed)}\n";
                if (vanillaExeWarnings.Count > 0)
                    summary += $"\nWarning - {string.Join(", ", vanillaExeWarnings)}:\n" +
                               "Mods deployed, but the game exe is still a Steam/Vanilla build.\n" +
                               "The game will fail to launch (Application Load Error 5:0000065434).\n" +
                               "Install a 1.0 downgraded exe as a mod to fix this.";
                if (!string.IsNullOrEmpty(summary.Trim()))
                    MessageBox.Show(summary.Trim(), "Deployment Complete");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Deployment Error: {ex.Message}");
            }
            finally
            {
                this.IsEnabled = true;
                DeployProgressPanel.Visibility = Visibility.Collapsed;
                DialogOverlay.Visibility       = Visibility.Collapsed;
                pbDeploy.IsIndeterminate       = true;
                pbDeploy.Value                 = 0;
                await RefreshUIAsync();
            }
        }

        private async void BtnRollback_Click(object s, RoutedEventArgs e)
        {
            var manifests = _core.GetRollbackManifests(_activeProfile.Key);
            if (manifests.Count == 0)
            {
                MessageBox.Show($"No rollback points found for {_activeProfile.DisplayName}.\n\nDeploy mods first to create a backup.",
                    "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var latest = manifests[0];
            string modList = latest.ModNames.Count > 0
                ? string.Join(", ", latest.ModNames)
                : "(no mods)";

            var choice = MessageBox.Show(
                $"Rollback {_activeProfile.DisplayName} to its state before the last deploy?\n\n" +
                $"Restore point: {latest.Timestamp}\n" +
                $"Mods that were applied: {modList}\n\n" +
                $"Files changed: {latest.Entries.Count}",
                "Confirm Rollback", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (choice != MessageBoxResult.Yes) return;

            this.IsEnabled = false;
            DialogOverlay.Visibility = Visibility.Visible;
            DeployProgressPanel.Visibility = Visibility.Visible;
            pbDeploy.IsIndeterminate = true;
            txtDeployStage.Text = "Rolling back...";
            txtDeployCount.Text = "";

            try
            {
                var progress = new Progress<DeploymentProgress>(p =>
                {
                    txtDeployStage.Text = p.Stage;
                    if (p.Total > 0)
                    {
                        pbDeploy.IsIndeterminate = false;
                        pbDeploy.Value = (double)p.Current / p.Total * 100;
                        txtDeployCount.Text = $"{p.Current} / {p.Total}";
                    }
                });

                await _core.RollbackDeployAsync(latest, progress);
                MessageBox.Show($"Rollback complete for {_activeProfile.DisplayName}.",
                    "Rollback", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rollback failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.IsEnabled = true;
                DeployProgressPanel.Visibility = Visibility.Collapsed;
                DialogOverlay.Visibility = Visibility.Collapsed;
                pbDeploy.IsIndeterminate = true;
                pbDeploy.Value = 0;
            }
        }

        private async void BtnCleo_Click(object s, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string url = profile.Key switch
                {
                    "III" => "https://github.com/cleolibrary/III.VC.CLEO/releases/download/2.1.1/CLEO.III_v2.1.1.zip",
                    "VC"  => "https://github.com/cleolibrary/III.VC.CLEO/releases/download/2.1.1/CLEO.VC_v2.1.1.zip",
                    "SA"  => "https://github.com/cleolibrary/CLEO5/releases/download/v5.4.0/SA.CLEO-v5.4.0.zip",
                    _     => ""
                };
                if (!string.IsNullOrEmpty(url))
                    await DownloadAndInstallDirectAsync(url, profile.Key);
            }
        }

        // ==========================================================
        // SIMPLE BUTTON HANDLERS
        // ==========================================================

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url)
            {
                try { ShellHelper.OpenUrl(url); }
                catch (Exception ex) { MessageBox.Show($"Could not open link: {ex.Message}"); }
            }
        }

        private void BtnLaunchModded_Click(object s, RoutedEventArgs e)
        {
            if (s is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            string? gameDir = _core.GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show("Game directory is not set or missing.",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string exe = Path.Combine(gameDir, profile.ExeName);
            if (!File.Exists(exe))
            {
                MessageBox.Show($"{profile.ExeName} not found in the game directory.",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gameDir });
        }

        private void BtnHelp_Click(object s, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo("https://github.com/triviali/tgtamm") { UseShellExecute = true });

        private async void MenuToggleOverride_Click(object s, RoutedEventArgs e)
        {
            if (s is not MenuItem mi || mi.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            bool isNowOn = _core.ToggleDeployOverride(profile);

            string state = isNowOn ? "ENABLED" : "DISABLED";
            string msg = isNowOn
                ? $"Force Deploy Override ENABLED for GTA {key}.\n\n" +
                  "Mods will deploy even though the exe is Vanilla/Steam.\n" +
                  "Right-click the play button again to turn this off."
                : $"Force Deploy Override DISABLED for GTA {key}.\n\n" +
                  "A downgraded 1.0 exe is now required to deploy mods.";

            MessageBox.Show(msg, $"Override {state}", MessageBoxButton.OK,
                isNowOn ? MessageBoxImage.Warning : MessageBoxImage.Information);

            await RefreshUIAsync();
        }

        private async void MenuToggleOverrideList_Click(object s, RoutedEventArgs e)
        {
            var profile = ResolveProfileFromContextMenu(e);
            bool isNowOn = _core.ToggleDeployOverride(profile);
            string state = isNowOn ? "ENABLED" : "DISABLED";
            string msg = isNowOn
                ? $"Force Deploy Override ENABLED for GTA {profile.Key}.\n\n" +
                  "Mods will deploy even though the exe is Vanilla/Steam.\n" +
                  "Note: the game will still fail to launch without a 1.0 downgraded exe."
                : $"Force Deploy Override DISABLED for GTA {profile.Key}.\n\n" +
                  "A downgraded 1.0 exe is now required to deploy mods.";
            MessageBox.Show(msg, $"Override {state}", MessageBoxButton.OK,
                isNowOn ? MessageBoxImage.Warning : MessageBoxImage.Information);
            await RefreshUIAsync();
        }

        private void BtnAbout_Click(object s, RoutedEventArgs e)
            => new AboutWindow(_core) { Owner = this }.ShowDialog();

        private void Window_StateChanged(object? s, EventArgs e)
        {
            bool maximized = WindowState == WindowState.Maximized;
            MainWindowBorder.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(10);
            TitleBarBorder.CornerRadius   = maximized ? new CornerRadius(0) : new CornerRadius(10, 10, 0, 0);
            if (MainWindowBorder.Child is Border inner)
            {
                var vb = inner.OpacityMask as System.Windows.Media.VisualBrush;
                if (vb?.Visual is Border mask)
                    mask.CornerRadius = maximized ? new CornerRadius(0) : new CornerRadius(11);
            }
        }

        private void BtnToggleSidebar_Click(object s, RoutedEventArgs e)
            => SidebarBorder.Visibility = SidebarBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

        private async void BtnSettings_Click(object s, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Visible;
            new SettingsWindow(_core) { Owner = this }.ShowDialog();
            DialogOverlay.Visibility = Visibility.Collapsed;
            await RefreshUIAsync();
        }

        private void BtnTheme_Click(object s, RoutedEventArgs e)
        {
            DialogOverlay.Visibility = Visibility.Visible;
            new ThemeManagerWindow(_core) { Owner = this }.ShowDialog();
            DialogOverlay.Visibility = Visibility.Collapsed;
        }


        private void BtnOpenAppData_Click(object s, RoutedEventArgs e) => _core.OpenAppData();


        private async void BtnOpenWizardOverlay_Click(object s, RoutedEventArgs e)
        {
            new InitialSetupWindow(_core) { Owner = this }.ShowDialog();
            await RefreshUIAsync();
        }

        private void BtnBackToLauncher_Click(object s, RoutedEventArgs e) => Close();
        private void BtnCloseApp_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();

        // ==========================================================
        // CONTEXT MENU
        // ==========================================================

        private void MenuRename_Click(object? s, RoutedEventArgs? e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            var renameWin = new RenameWindow(mod.Name) { Owner = this };
            if (renameWin.ShowDialog() == true)
            {
                mod.Name = renameWin.NewName; // INotifyPropertyChanged on ModItem updates UI automatically.
                SyncModInfoToFolder(mod);
                SaveMods();
            }
        }

        private void MenuToggle_Click(object? s, RoutedEventArgs? e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            FlagDeploy();
            SaveMods();
        }

        private void MenuDelete_Click(object? s, RoutedEventArgs? e)
        {
            var lv = GetActiveListV();
            var profile = ResolveProfileFromList(lv) ?? _activeProfile;
            var list = _core.Mods[profile.Key];
            var selected = lv.SelectedItems.Cast<ModItem>().ToList();

            if (selected.Count == 0 ||
                MessageBox.Show($"Delete {selected.Count} mod(s)?", "Confirm", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            foreach (var m in selected)
            {
                try
                {
                    if (Directory.Exists(m.RawFolderPath))
                        BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                    list.Remove(m);
                }
                catch (Exception ex) { MessageBox.Show($"Error deleting {m.Name}: {ex.Message}"); }
            }

            SaveMods();
            FlagDeploy();
            txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        private void MenuMoveUp_Click(object? s, RoutedEventArgs? e)
        {
            var mod = GetSelectedMod();
            var list = GetActiveList();
            if (mod == null) return;

            int idx = list.IndexOf(mod);
            if (idx > 0)
            {
                var other = list[idx - 1];
                (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
                list.Move(idx, idx - 1);
                SyncModInfoToFolder(mod);
                SyncModInfoToFolder(other);
                FlagDeploy();
                SaveMods();
            }
        }

        private void MenuMoveDown_Click(object? s, RoutedEventArgs? e)
        {
            var mod = GetSelectedMod();
            var list = GetActiveList();
            if (mod == null) return;

            int idx = list.IndexOf(mod);
            if (idx >= 0 && idx < list.Count - 1)
            {
                var other = list[idx + 1];
                (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
                list.Move(idx, idx + 1);
                SyncModInfoToFolder(mod);
                SyncModInfoToFolder(other);
                FlagDeploy();
                SaveMods();
            }
        }

        private void MenuSetLoadOrder_Click(object s, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            var list = GetActiveList();
            if (mod == null || list.Count == 0) return;

            int maxOrder = list.Count - 1;
            var inputWin = new RenameWindow(mod.LoadOrder.ToString())
            {
                Title = $"Set Load Order (0 to {maxOrder})",
                Owner = this
            };

            if (inputWin.ShowDialog() != true || !int.TryParse(inputWin.NewName, out int newOrder)) return;

            newOrder = Math.Clamp(newOrder, 0, maxOrder);
            list.Remove(mod);
            list.Insert(newOrder, mod);

            for (int i = 0; i < list.Count; i++) list[i].LoadOrder = i;

            var sorted = list.OrderBy(x => x.LoadOrder).ToList();
            list.Clear();
            foreach (var m in sorted) list.Add(m);

            foreach (var m in list) SyncModInfoToFolder(m);
            FlagDeploy();
            SaveMods();
        }

        private async void MenuInstallToAll_Click(object s, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null && File.Exists(mod.PackedFilePath))
                await ProcessModInstallationAsync(mod.PackedFilePath, "ALL");
            else
                MessageBox.Show("Original archive not found. Cannot re-install to all games.", "Source Missing");
        }

        private void MenuOpenFolder_Click(object s, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null && Directory.Exists(mod.RawFolderPath))
                ShellHelper.OpenFolder(mod.RawFolderPath);
        }

        private GameProfile ResolveProfileFromContextMenu(RoutedEventArgs e)
        {
            var target = ((e.Source as MenuItem)?.Parent as ContextMenu)?.PlacementTarget;
            if (target is ListView lv)
                return ResolveProfileFromList(lv) ?? _activeProfile;
            return _activeProfile;
        }

        private void MenuOpenBaseFolder_Click(object s, RoutedEventArgs e)
        {
            var profile = ResolveProfileFromContextMenu(e);
            string? path = _core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                ShellHelper.OpenFolder(path);
            else
                MessageBox.Show($"Base game folder for {profile.DisplayName} is not set or doesn't exist.",
                    "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenVirtualFolder_Click(object s, RoutedEventArgs e)
        {
            var profile = ResolveProfileFromContextMenu(e);
            string backupDir = Path.Combine(_core.BackupsPath, profile.Key);
            Directory.CreateDirectory(backupDir);
            ShellHelper.OpenFolder(backupDir);
        }

        private void MenuOpenModsFolder_Click(object s, RoutedEventArgs e)
        {
            var profile = ResolveProfileFromContextMenu(e);
            string path = Path.Combine(_core.AppDataPath, profile.RawFolderName);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }


        private void MenuProperties_Click(object s, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;

            DialogOverlay.Visibility = Visibility.Visible;
            new ModPropertiesWindow(mod) { Owner = this }.ShowDialog();
            DialogOverlay.Visibility = Visibility.Collapsed;
        }

        // ==========================================================
        // STANDARD BINDINGS
        // ==========================================================

        private void ModCheckBox_Changed(object s, RoutedEventArgs e)
        {
            // Persist the new IsEnabled to disk BEFORE FlagDeploy triggers
            // RefreshAllModListsAsync, which re-reads modinfo.txt from disk.
            // Without this sync the refresh overwrites the toggle.
            if (s is CheckBox cb && cb.DataContext is ModItem mod)
                SyncModInfoToFolder(mod);
            FlagDeploy();
            SaveMods();
        }

        private void TxtSearchIII_TextChanged(object s, TextChangedEventArgs e) =>
            ApplySearchFilter(GameProfile.III.Key, txtSearchIII.Text);

        private void TxtSearchVC_TextChanged(object s, TextChangedEventArgs e) =>
            ApplySearchFilter(GameProfile.VC.Key, txtSearchVC.Text);

        private void TxtSearchSA_TextChanged(object s, TextChangedEventArgs e) =>
            ApplySearchFilter(GameProfile.SA.Key, txtSearchSA.Text);

        private void ApplySearchFilter(string key, string text)
        {
            var v = CollectionViewSource.GetDefaultView(_core.Mods[key]);
            v.Filter = string.IsNullOrWhiteSpace(text)
                ? null
                : i => ((ModItem)i).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
        }

        private void GridViewColumnHeaderClicked(object s, RoutedEventArgs e)
        {
            if (s is not ListView lv ||
                lv.ItemsSource is not ObservableCollection<ModItem> l ||
                e.OriginalSource is not GridViewColumnHeader h ||
                h.Column == null) return;

            _isSortedByLoadOrder = h.Column.Header?.ToString() == "#";
            var sorted = _isSortedByLoadOrder
                ? l.OrderBy(m => m.LoadOrder).ToList()
                : l.OrderBy(m => m.Name).ToList();
            l.Clear();
            foreach (var i in sorted) l.Add(i);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Save window position and size (only if not maximized/minimized)
            if (WindowState == WindowState.Normal)
            {
                _core.Settings.WindowLeft  = Left;
                _core.Settings.WindowTop   = Top;
                _core.Settings.WindowWidth  = Width;
                _core.Settings.WindowHeight = Height;
            }

            base.OnClosing(e);
        }

        private void Toast_Close(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement elem && elem.DataContext is NotificationItem notif)
            {
                NotificationService.Queue.Remove(notif);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NotificationItem notif)
            {
                NotificationService.Queue.Remove(notif);
            }
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.F2) MenuRename_Click(null, null);
            else if (e.Key == Key.Space) MenuToggle_Click(null, null);
            else if (e.Key == Key.Delete) MenuDelete_Click(null, null);
            else if (e.Key == Key.F5) BtnDeploy_Click(null!, null!);
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up) { MenuMoveUp_Click(null, null); e.Handled = true; }
                else if (e.Key == Key.Down) { MenuMoveDown_Click(null, null); e.Handled = true; }
            }
            base.OnKeyDown(e);
        }

        // ==========================================================
        // HELPERS
        // ==========================================================

        private ObservableCollection<ModItem> GetActiveList() => _core.Mods[_activeProfile.Key];

        private ListView GetActiveListV() =>
            PListIII.IsKeyboardFocusWithin ? PListIII :
            PListVC.IsKeyboardFocusWithin ? PListVC : PListSA;

        private ModItem? GetSelectedMod() => GetActiveListV().SelectedItem as ModItem;

        private static GameProfile? ResolveProfileFromList(object source) =>
            source is ListView lv && lv.Tag is string key ? GameProfile.ByKey(key) : null;

        private GameProfile? ResolveProfileFromMod(ModItem mod)
        {
            foreach (var profile in GameProfile.All)
                if (_core.Mods[profile.Key].Contains(mod)) return profile;
            return null;
        }

        /// <summary>Async-safe deploy flag: refreshes UI without blocking the caller.</summary>
        private void FlagDeploy()
        {
            _hasPendingChanges = true;
            _ = RefreshUIAsync();
        }
    }
}
