using Microsoft.Win32;
using SharpCompress.Readers;
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

namespace TMM
{
    public partial class ModManagerPage : UserControl
    {
        // ── Shared state ──────────────────────────────────────────────────────────

        public event Action? BackRequested;

        private BackendCore _core = null!;
        private LibraryEntry _entry = null!;

        private enum DashMode { None, IIISeries, IVSeries, Custom, Placeholder }
        private DashMode _mode;

        // Active focused list (shared across modes)
        private ListView? _activeList;

        // ── GTA III state ─────────────────────────────────────────────────────────

        private Point _startIII;
        private ModItem? _draggedIII;

        // ── GTA IV state ──────────────────────────────────────────────────────────

        private (GameProfile Profile, ObservableCollection<ModItem> Mods)[] _episodesIV = [];
        private bool[] _pendingIV = [false, false, false];
        private Point _startIV;
        private ModItem? _draggedIV;
        private CancellationTokenSource? _deployCts;

        // ── Custom game state ─────────────────────────────────────────────────────

        private GameProfile _customProfile = null!;
        private CustomGameProfile _customConfig = null!;
        private ObservableCollection<ModItem> _modsCustom = new();
        private bool _pendingCustom;
        private Point _startCustom;
        private ModItem? _draggedCustom;

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

            panelIIISeries.Visibility   = Visibility.Collapsed;
            panelIVSeries.Visibility    = Visibility.Collapsed;
            panelCustom.Visibility      = Visibility.Collapsed;
            panelPlaceholder.Visibility = Visibility.Collapsed;

            if (entry.IsPlaceholder)
            {
                _mode = DashMode.Placeholder;
                Placeholder_txtName.Text = entry.DisplayName;
                panelPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            bool isIII = entry.GameKeys.Any(k => k is "III" or "VC" or "SA");
            bool isIV  = entry.GameKeys.Any(k => k is "IV" or "TLaD" or "TBoGT");

            if (isIII)
            {
                _mode = DashMode.IIISeries;
                InitIIISeries();
                panelIIISeries.Visibility = Visibility.Visible;
            }
            else if (isIV)
            {
                _mode = DashMode.IVSeries;
                InitIVSeries();
                panelIVSeries.Visibility = Visibility.Visible;
            }
            else
            {
                _mode = DashMode.Custom;
                InitCustomGame();
                panelCustom.Visibility = Visibility.Visible;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // GTA III SERIES
        // ═════════════════════════════════════════════════════════════════════════

        private void InitIIISeries()
        {
            III_listIII.ItemsSource = _core.Mods[GameProfile.III.Key];
            III_listVC.ItemsSource  = _core.Mods[GameProfile.VC.Key];
            III_listSA.ItemsSource  = _core.Mods[GameProfile.SA.Key];

            LoadModsFromJsonIII();
            _ = RefreshIIIAsync();
        }

        private async Task RefreshIIIAsync()
        {
            await _core.RefreshAllModListsAsync();
            UpdateGamePathsIII();
            UpdateDeployButtonsIII();
            III_txtDiskSpace.Text = _core.GetDriveSpaceInfo();
        }

        private void UpdateGamePathsIII()
        {
            UpdatePathRow(GameProfile.III, III_dotIII, III_txtPathIII);
            UpdatePathRow(GameProfile.VC,  III_dotVC,  III_txtPathVC);
            UpdatePathRow(GameProfile.SA,  III_dotSA,  III_txtPathSA);
        }

        private void UpdatePathRow(GameProfile profile,
            System.Windows.Shapes.Ellipse dot, TextBlock txt)
        {
            string? path = _core.GetVanillaPath(profile);
            bool isSet = !string.IsNullOrEmpty(path) && Directory.Exists(path);
            dot.Fill = new SolidColorBrush(isSet ? UiColors.ReadyGreen : UiColors.NotReadyRed);
            txt.Text = isSet ? path! : "—";
        }

        private void UpdateDeployButtonsIII()
        {
            bool anyReady = GameProfile.All.Any(p => !string.IsNullOrEmpty(_core.GetVanillaPath(p)));
            III_btnDeploy.Background = anyReady
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : UiColors.DisabledBrush;

            UpdateColDeployButton(III_btnDeployIII, "III");
            UpdateColDeployButton(III_btnDeployVC,  "VC");
            UpdateColDeployButton(III_btnDeploySA,  "SA");
        }

        private void UpdateColDeployButton(Button btn, string key)
        {
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            bool ready = !string.IsNullOrEmpty(_core.GetVanillaPath(profile));
            bool hasMods = _core.Mods[key].Any(m => m.IsEnabled);
            btn.Background = (ready && hasMods)
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : UiColors.DisabledBrush;
        }

        private void LoadModsFromJsonIII()
        {
            foreach (var profile in GameProfile.All)
                LoadModsFromJsonForProfile(profile.Key);
        }

        private void LoadModsFromJsonForProfile(string key)
        {
            string jsonPath = Path.Combine(_core.AppDataPath, "Mods", key, "modlist.json");
            if (!File.Exists(jsonPath)) return;
            try
            {
                var saved = JsonSerializer.Deserialize<List<ModItem>>(
                    File.ReadAllText(jsonPath), JsonHelper.PrettyOptions);
                if (saved == null) return;
                var list = _core.Mods[key];
                list.Clear();
                foreach (var m in saved.OrderBy(x => x.LoadOrder))
                    if (Directory.Exists(m.RawFolderPath)) list.Add(m);
            }
            catch { }
        }

        private void SaveModsIII()
        {
            foreach (var profile in GameProfile.All)
                SaveModsForProfile(profile.Key, _core.Mods[profile.Key].ToList());
        }

        private void SaveModsForProfile(string key, List<ModItem> mods)
        {
            string folder = Path.Combine(_core.AppDataPath, "Mods", key);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "modlist.json"),
                JsonSerializer.Serialize(mods, JsonHelper.PrettyOptions));
        }

        // ── III toolbar handlers ──────────────────────────────────────────────────

        private void BtnToggleSidebarIII_Click(object sender, RoutedEventArgs e)
            => III_SidebarBorder.Visibility = III_SidebarBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;

        private async void BtnBrowseIII_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            var dlg = new OpenFolderDialog { Title = $"Select {profile.DisplayName} directory" };
            string? current = _core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                dlg.InitialDirectory = current;
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FolderName)) return;

            _core.SetVanillaPath(profile, dlg.FolderName);
            _core.SaveSettings();
            await RefreshIIIAsync();
        }

        private async void BtnDeployIII_Click(object sender, RoutedEventArgs e)
        {
            var toProcess = GameProfile.All
                .Where(p => !string.IsNullOrEmpty(_core.GetVanillaPath(p)))
                .ToList();

            if (toProcess.Count == 0)
            {
                MessageBox.Show("No game paths are configured.\n\nSet paths via the sidebar browse buttons.",
                    "Nothing to Deploy", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowDeployOverlay("Preparing...");
            var deployed = new List<string>();
            try
            {
                var progress = MakeProgress();
                foreach (var profile in toProcess)
                {
                    txtDeployStage.Text = $"Deploying {profile.Key}...";
                    await _core.DeployModsAsync(profile, _core.Mods[profile.Key], progress);
                    deployed.Add(profile.Key);
                }
                if (deployed.Count > 0)
                    NotificationService.ShowSuccess($"Deployed: {string.Join(", ", deployed)}");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Deploy error: {ex.Message}");
            }
            finally
            {
                HideDeployOverlay();
                await RefreshIIIAsync();
            }
        }

        private async void BtnDeployCol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            await RunDeployAsync(profile, btn);

            if (_mode == DashMode.IIISeries) UpdateDeployButtonsIII();
            else if (_mode == DashMode.IVSeries) UpdateDeployButtonsIV();
        }

        private async void BtnRollbackIII_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetActiveProfileIII() ?? GameProfile.III;
            await RunRollbackAsync(profile);
        }

        private async void BtnRollbackCol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            await RunRollbackAsync(profile);
        }

        private void BtnLaunchIII_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            LaunchGame(profile.Key, _core.GetVanillaPath(profile), profile.ExeName);
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Rebuild mod list from folders?", "Sync", MessageBoxButton.YesNo)
                != MessageBoxResult.Yes) return;

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
            SaveModsIII();
            await RefreshIIIAsync();
        }

        private GameProfile? GetActiveProfileIII()
        {
            if (_activeList?.Tag is string key) return GameProfile.ByKey(key);
            return null;
        }

        // ── III web installer handlers ────────────────────────────────────────────

        private async void BtnWidescreen_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string url = profile.Key switch
                {
                    "III" => "https://github.com/ThirteenAG/WidescreenFixesPack/releases/download/gtaiii/III.WidescreenFix.zip",
                    "VC"  => "https://github.com/ThirteenAG/WidescreenFixesPack/releases/download/gtavc/VC.WidescreenFix.zip",
                    "SA"  => "https://github.com/ThirteenAG/WidescreenFixesPack/releases/download/gtasa/SA.WidescreenFix.zip",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(url)) await DownloadAndInstallIIIAsync(url, profile.Key);
            }
        }

        private async void BtnSilentPatch_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string url = profile.Key switch
                {
                    "III" => "https://github.com/CookiePLMonster/SilentPatch/releases/download/SilentPatchIII/SilentPatchIII.zip",
                    "VC"  => "https://github.com/CookiePLMonster/SilentPatch/releases/download/SilentPatchVC/SilentPatchVC.zip",
                    "SA"  => "https://github.com/CookiePLMonster/SilentPatch/releases/download/SilentPatchSA/SilentPatchSA.zip",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(url)) await DownloadAndInstallIIIAsync(url, profile.Key);
            }
        }

        private async void BtnASILoader_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string asset = profile.Key == "SA" ? "dinput8" : "dsound";
                await DownloadAndInstallIIIAsync(
                    $"https://github.com/ThirteenAG/Ultimate-ASI-Loader/releases/download/Win32-latest/{asset}-Win32.zip",
                    profile.Key);
            }
        }

        private async void Btn2DFX_Click(object sender, RoutedEventArgs e)
        {
            const string releaseTag = "v4.5";
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
                await DownloadAndInstallIIIAsync(
                    $"https://github.com/ThirteenAG/III.VC.SA.IV.Project2DFX/releases/download/{releaseTag}/{profile.Key}.Project2DFX.zip",
                    profile.Key);
        }

        private async void BtnCleo_Click(object sender, RoutedEventArgs e)
        {
            foreach (var profile in GameProfile.All.Where(_core.IsGameReady))
            {
                string url = profile.Key switch
                {
                    "III" => "https://github.com/cleolibrary/III.VC.CLEO/releases/download/2.1.1/CLEO.III_v2.1.1.zip",
                    "VC"  => "https://github.com/cleolibrary/III.VC.CLEO/releases/download/2.1.1/CLEO.VC_v2.1.1.zip",
                    "SA"  => "https://github.com/cleolibrary/CLEO5/releases/download/v5.4.0/SA.CLEO-v5.4.0.zip",
                    _ => ""
                };
                if (!string.IsNullOrEmpty(url)) await DownloadAndInstallIIIAsync(url, profile.Key);
            }
        }

        private async Task DownloadAndInstallIIIAsync(string url, string hintKey)
        {
            try
            {
                string p = Path.Combine(_core.DownloadCachePath, Path.GetFileName(new Uri(url).LocalPath));
                await _core.DownloadFileAsync(url, p);
                await InstallModFileForProfileAsync(p, hintKey);
                III_txtDiskSpace.Text = _core.GetDriveSpaceInfo();
            }
            catch (Exception ex) { NotificationService.ShowError($"Download failed: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════════════════
        // GTA IV SERIES
        // ══════════════════════════════════════════════════════════════════════════

        private void InitIVSeries()
        {
            _episodesIV =
            [
                (GameProfile.IV,    _core.Mods[GameProfile.IV.Key]),
                (GameProfile.TLaD,  _core.Mods[GameProfile.TLaD.Key]),
                (GameProfile.TBoGT, _core.Mods[GameProfile.TBoGT.Key]),
            ];
            _pendingIV = [false, false, false];

            IV_listIV.ItemsSource    = _episodesIV[0].Mods;
            IV_listTLaD.ItemsSource  = _episodesIV[1].Mods;
            IV_listTBoGT.ItemsSource = _episodesIV[2].Mods;

            _ = RefreshIVAsync();
        }

        private async Task RefreshIVAsync()
        {
            await _core.RefreshAllModListsAsync();
            UpdateStatusDotsIV();
            UpdateDeployButtonsIV();
            IV_txtDiskSpace.Text = _core.GetDriveSpaceInfo();

            IV_txtPathIV.Text    = _core.GetVanillaPath(GameProfile.IV)    ?? "—";
            IV_txtPathTLaD.Text  = _core.GetVanillaPath(GameProfile.TLaD)  ?? "—";
            IV_txtPathTBoGT.Text = _core.GetVanillaPath(GameProfile.TBoGT) ?? "—";
        }

        private void UpdateStatusDotsIV()
        {
            SetDotColor(IV_dotIV,    _core.IsGameReady(GameProfile.IV));
            SetDotColor(IV_dotTLaD,  _core.IsGameReady(GameProfile.TLaD));
            SetDotColor(IV_dotTBoGT, _core.IsGameReady(GameProfile.TBoGT));
        }

        private static void SetDotColor(System.Windows.Shapes.Ellipse dot, bool ready)
            => dot.Fill = new SolidColorBrush(ready ? UiColors.ReadyGreen : UiColors.NotReadyRed);

        private void UpdateDeployButtonsIV()
        {
            bool anyReady = _episodesIV.Any(ep => _core.IsGameReady(ep.Profile));
            IV_btnDeployAll.Background = anyReady
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : UiColors.DisabledBrush;

            UpdateColDeployButtonIV(IV_btnDeployIV,    "IV");
            UpdateColDeployButtonIV(IV_btnDeployTLaD,  "TLaD");
            UpdateColDeployButtonIV(IV_btnDeployTBoGT, "TBoGT");
        }

        private void UpdateColDeployButtonIV(Button btn, string key)
        {
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            bool ready   = _core.IsGameReady(profile);
            bool hasMods = _core.Mods[key].Any(m => m.IsEnabled);
            int idx = IVEpisodeIndex(key);
            bool pending = idx >= 0 && _pendingIV[idx];

            if (!ready || !hasMods)
                btn.Background = UiColors.DisabledBrush;
            else if (pending)
                btn.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
            else
                btn.Background = UiColors.DisabledBrush;
        }

        private static int IVEpisodeIndex(string key) => key switch
        {
            "IV" => 0, "TLaD" => 1, "TBoGT" => 2, _ => -1
        };

        // ── IV toolbar handlers ───────────────────────────────────────────────────

        private void BtnToggleSidebarIV_Click(object sender, RoutedEventArgs e)
            => IV_SidebarBorder.Visibility = IV_SidebarBorder.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;

        private async void BtnBrowseIV_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            var dlg = new OpenFolderDialog { Title = $"Select {profile.DisplayName} directory" };
            string? current = _core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(current) && Directory.Exists(current))
                dlg.InitialDirectory = current;
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.FolderName)) return;

            _core.SetVanillaPath(profile, dlg.FolderName);
            _core.SaveSettings();
            await RefreshIVAsync();
        }

        private async void BtnDeployAllIV_Click(object sender, RoutedEventArgs e)
        {
            int count = 0;
            foreach (var (profile, _) in _episodesIV)
            {
                if (!_core.IsGameReady(profile)) continue;
                await RunDeployAsync(profile, null);
                count++;
            }
            if (count == 0)
                MessageBox.Show("No IV episodes are configured. Set game paths via the sidebar browse buttons.",
                    "Nothing to Deploy", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnRollbackActiveIV_Click(object sender, RoutedEventArgs e)
        {
            var profile = GetActiveProfileIV() ?? GameProfile.IV;
            await RunRollbackAsync(profile);
        }

        private void BtnLaunchIV_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string key) return;
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;
            LaunchGame(profile.Key, _core.GetVanillaPath(profile), profile.ExeName);
        }

        private async void BtnRefreshIV_Click(object sender, RoutedEventArgs e)
        {
            await _core.RefreshAllModListsAsync();
            await RefreshIVAsync();
        }

        private async void BtnInstallModIV_Click(object sender, RoutedEventArgs e)
        {
            var defaultProfile = _activeList?.Tag is string k ? GameProfile.ByKey(k) : null;
            var picker = new EpisodePicker(
                defaultProfile != null
                    ? $"Install mod for (default: {defaultProfile.ShortName}):"
                    : "Install mod for:",
                _episodesIV.Select(ep => ep.Profile).ToArray())
            {
                Owner = Window.GetWindow(this)
            };
            if (picker.ShowDialog() != true || picker.SelectedProfile == null) return;

            var profile = picker.SelectedProfile;
            string rawFolder = Path.Combine(_core.AppDataPath, profile.RawFolderName);

            var dlg = new OpenFileDialog
            {
                Title       = $"Install Mod for {profile.DisplayName}",
                Filter      = "Mod Archives & Files|*.zip;*.rar;*.7z;*.asi;*.dll;*.ini;*.dat|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;
            foreach (var file in dlg.FileNames)
                await InstallModFileForProfileAsync(file, profile.Key);

            await RefreshIVAsync();
        }

        private GameProfile? GetActiveProfileIV()
        {
            if (_activeList?.Tag is string key)
                return _episodesIV.FirstOrDefault(ep => ep.Profile.Key == key).Profile;
            return null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // CUSTOM GAME
        // ══════════════════════════════════════════════════════════════════════════

        private void InitCustomGame()
        {
            // Find the CustomGameProfile for this entry's key
            _customConfig = GameRegistry.Instance.GetCustomGameConfig(_entry.Key)
                         ?? new CustomGameProfile { GameName = _entry.DisplayName };

            // Resolve GameProfile for this custom game
            _customProfile = GameRegistry.Instance.GetGameProfile(_entry.Key)
                          ?? GameProfile.ByKey(_entry.Key)
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
            bool ready    = !string.IsNullOrEmpty(_customConfig.GameDirectory) &&
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

        // ── Custom toolbar handlers ───────────────────────────────────────────────

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
            string ext      = Path.GetExtension(archivePath).ToLowerInvariant();
            string modName  = Path.GetFileNameWithoutExtension(archivePath);
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
            await RunDeployAsync(_customProfile, Cust_btnDeploy);
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
            var dlg = new CustomGameConfigWindow(_customConfig) { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;
            _customConfig = dlg.Result;
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

        private void BtnInstallMod_Click(object sender, RoutedEventArgs e)
        {
            // GTA III install — pick active or ask for game
            if (_mode == DashMode.IIISeries)
                _ = InstallModIIIAsync();
        }

        private async Task InstallModIIIAsync()
        {
            var profiles = GameProfile.All.ToArray();
            var picker = new EpisodePicker(
                "Install mod for:",
                profiles)
            {
                Owner = Window.GetWindow(this)
            };
            if (picker.ShowDialog() != true || picker.SelectedProfile == null) return;
            var profile = picker.SelectedProfile;

            var dlg = new OpenFileDialog
            {
                Title       = $"Install Mod for {profile.DisplayName}",
                Filter      = "Mod Archives & Files|*.zip;*.rar;*.7z;*.asi;*.dll;*.ini;*.dat|All files|*.*",
                Multiselect = true
            };
            if (dlg.ShowDialog() != true) return;
            foreach (var file in dlg.FileNames)
                await InstallModFileForProfileAsync(file, profile.Key);

            await RefreshIIIAsync();
        }

        private async Task InstallModFileForProfileAsync(string filePath, string key)
        {
            var profile = GameProfile.ByKey(key);
            if (profile == null) return;

            string ext     = Path.GetExtension(filePath).ToLowerInvariant();
            string modName = Path.GetFileNameWithoutExtension(filePath);
            string destDir = Path.Combine(_core.AppDataPath, profile.RawFolderName, modName);
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
                    LoadOrder     = _core.Mods[key].Count,
                    RawFolderPath = destDir
                };
                SyncModInfoToFolder(item);
                _core.Mods[key].Add(item);
                NotificationService.ShowSuccess($"Installed '{modName}' for {profile.ShortName}.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Failed to install '{modName}': {ex.Message}");
            }
        }

        // ── Deploy / Rollback helpers ─────────────────────────────────────────────

        private async Task RunDeployAsync(GameProfile profile, Button? btn)
        {
            string? gameDir = _mode == DashMode.Custom
                ? _customConfig.GameDirectory
                : _core.GetVanillaPath(profile);

            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show($"Game directory for {profile.DisplayName} is not set or missing.",
                    "Directory Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _deployCts?.Cancel();
            _deployCts = new CancellationTokenSource();
            if (btn != null) btn.IsEnabled = false;

            ShowDeployOverlay($"Deploying {profile.ShortName}...");

            try
            {
                var progress = MakeProgress();
                var mods = _mode == DashMode.Custom
                    ? _modsCustom
                    : _core.Mods[profile.Key];

                await _core.DeployModsAsync(profile, mods, progress, _deployCts.Token);
                NotificationService.ShowSuccess($"{profile.DisplayName} deployed.");
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
                if (btn != null) btn.IsEnabled = true;
            }
        }

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
            deployOverlay.Visibility      = Visibility.Visible;
            deployProgressPanel.Visibility = Visibility.Visible;
            pbDeploy.IsIndeterminate      = true;
            txtDeployStage.Text           = stage;
            txtDeployCount.Text           = "";
        }

        private void HideDeployOverlay()
        {
            deployOverlay.Visibility      = Visibility.Collapsed;
            deployProgressPanel.Visibility = Visibility.Collapsed;
            pbDeploy.IsIndeterminate      = true;
            pbDeploy.Value                = 0;
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

        // ── Launch helper ─────────────────────────────────────────────────────────

        private static void LaunchGame(string key, string? gameDir, string exeName)
        {
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                MessageBox.Show("Game directory is not set or missing.",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string exe = Path.Combine(gameDir, exeName);
            if (!File.Exists(exe))
            {
                MessageBox.Show($"{exeName} not found in the game directory.",
                    "Launch Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gameDir });
        }

        // ══════════════════════════════════════════════════════════════════════════
        // SHARED MOD LIST INTERACTION (multi-column aware)
        // ══════════════════════════════════════════════════════════════════════════

        private void List_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is ListView lv) _activeList = lv;
        }

        private void ModCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox { DataContext: ModItem item })
            {
                SyncModInfoToFolder(item);
                if (_mode == DashMode.IIISeries)
                {
                    UpdateDeployButtonsIII();
                    SaveModsIII();
                }
                else if (_mode == DashMode.IVSeries)
                {
                    string? key = FindEpisodeKeyForMod(item);
                    if (key != null)
                    {
                        int idx = IVEpisodeIndex(key);
                        if (idx >= 0) _pendingIV[idx] = true;
                    }
                    UpdateDeployButtonsIV();
                }
                else if (_mode == DashMode.Custom)
                {
                    _pendingCustom = true;
                    UpdateDeployButtonCustom();
                    SaveModsCustom();
                }
            }
        }

        private string? FindEpisodeKeyForMod(ModItem item)
        {
            foreach (var (profile, mods) in _episodesIV)
                if (mods.Contains(item)) return profile.Key;
            return null;
        }

        // ── Keyboard shortcuts ────────────────────────────────────────────────────

        private void List_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2)          { MenuRename_Click(null, null!);   e.Handled = true; }
            else if (e.Key == Key.Space)  { MenuToggle_Click(null, null!);   e.Handled = true; }
            else if (e.Key == Key.Delete) { MenuDelete_Click(null, null!);   e.Handled = true; }
            else if (e.Key == Key.F5)
            {
                if (_mode == DashMode.IIISeries)      BtnDeployIII_Click(null!, null!);
                else if (_mode == DashMode.IVSeries)  BtnDeployAllIV_Click(null!, null!);
                else if (_mode == DashMode.Custom)    BtnDeployCustom_Click(null!, null!);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.Up)        { MenuMoveUp_Click(null, null!);   e.Handled = true; }
                else if (e.Key == Key.Down) { MenuMoveDown_Click(null, null!); e.Handled = true; }
            }
        }

        private void Cust_List_KeyDown(object sender, KeyEventArgs e)
        {
            var selected = Cust_ModList.SelectedItem as ModItem;
            if (selected == null) return;
            switch (e.Key)
            {
                case Key.F2:    CustStartRename(selected);      break;
                case Key.Space: CustToggleMod(selected);        break;
                case Key.Delete: CustDeleteSelected();          break;
                case Key.F5:    BtnDeployCustom_Click(null!, null!); break;
                case Key.Up   when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    CustMoveUp(selected);   break;
                case Key.Down when e.KeyboardDevice.Modifiers == ModifierKeys.Control:
                    CustMoveDown(selected); break;
            }
        }

        // ── Search ────────────────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb || tb.Tag is not string key) return;
            var mods = _mode == DashMode.IVSeries
                ? _episodesIV.FirstOrDefault(ep => ep.Profile.Key == key).Mods
                : _core.Mods.ContainsKey(key) ? _core.Mods[key] : null;
            if (mods == null) return;
            string text = tb.Text;
            CollectionViewSource.GetDefaultView(mods).Filter = string.IsNullOrWhiteSpace(text)
                ? null
                : i => ((ModItem)i).Name.Contains(text, StringComparison.OrdinalIgnoreCase);
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
            SaveActiveMods();
        }

        private void MenuToggle_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod == null) return;
            mod.IsEnabled = !mod.IsEnabled;
            SyncModInfoToFolder(mod);
            FlagActivePending();
            SaveActiveMods();
        }

        private void MenuDelete_Click(object? sender, RoutedEventArgs e)
        {
            if (_activeList == null) return;
            var selected = _activeList.SelectedItems.Cast<ModItem>().ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show($"Delete {selected.Count} mod(s)?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            var list = GetActiveModList();
            foreach (var m in selected)
            {
                try
                {
                    if (Directory.Exists(m.RawFolderPath))
                        BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                    list?.Remove(m);
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error deleting '{m.Name}': {ex.Message}");
                }
            }
            SaveActiveMods();
        }

        private void MenuMoveUp_Click(object? sender, RoutedEventArgs e)
        {
            var mod  = GetSelectedMod();
            var list = GetActiveModList();
            if (mod == null || list == null) return;
            int idx = list.IndexOf(mod);
            if (idx <= 0) return;
            var other = list[idx - 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            list.Move(idx, idx - 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            FlagActivePending();
            SaveActiveMods();
        }

        private void MenuMoveDown_Click(object? sender, RoutedEventArgs e)
        {
            var mod  = GetSelectedMod();
            var list = GetActiveModList();
            if (mod == null || list == null) return;
            int idx = list.IndexOf(mod);
            if (idx < 0 || idx >= list.Count - 1) return;
            var other = list[idx + 1];
            (mod.LoadOrder, other.LoadOrder) = (other.LoadOrder, mod.LoadOrder);
            list.Move(idx, idx + 1);
            SyncModInfoToFolder(mod);
            SyncModInfoToFolder(other);
            FlagActivePending();
            SaveActiveMods();
        }

        private void MenuSetLoadOrder_Click(object? sender, RoutedEventArgs e)
        {
            var mod  = GetSelectedMod();
            var list = GetActiveModList();
            if (mod == null || list == null) return;

            int max = list.Count - 1;
            var win = new RenameWindow(mod.LoadOrder.ToString())
            {
                Title = $"Set Load Order (0–{max})",
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true || !int.TryParse(win.NewName, out int newOrder)) return;
            newOrder = Math.Clamp(newOrder, 0, max);
            list.Remove(mod);
            list.Insert(newOrder, mod);
            for (int i = 0; i < list.Count; i++) list[i].LoadOrder = i;
            foreach (var m in list) SyncModInfoToFolder(m);
            FlagActivePending();
            SaveActiveMods();
        }

        private void MenuOpenFolder_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null && Directory.Exists(mod.RawFolderPath))
                ShellHelper.OpenFolder(mod.RawFolderPath);
        }

        private void MenuOpenGameFolder_Click(object? sender, RoutedEventArgs e)
        {
            string? path = null;
            if (_mode == DashMode.Custom)
                path = _customConfig.GameDirectory;
            else if (_activeList?.Tag is string key)
            {
                var profile = GameProfile.ByKey(key);
                if (profile != null) path = _core.GetVanillaPath(profile);
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                ShellHelper.OpenFolder(path);
            else
                MessageBox.Show("Game folder is not set or missing.", "Folder Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuOpenBackupFolder_Click(object? sender, RoutedEventArgs e)
        {
            string key = _activeList?.Tag as string ?? _customProfile?.Key ?? "";
            string path = Path.Combine(_core.BackupsPath, key);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuOpenModsFolder_Click(object? sender, RoutedEventArgs e)
        {
            string key = _activeList?.Tag as string ?? _customProfile?.Key ?? "";
            var profile = GameProfile.ByKey(key) ?? _customProfile;
            if (profile == null) return;
            string path = Path.Combine(_core.AppDataPath, profile.RawFolderName);
            Directory.CreateDirectory(path);
            ShellHelper.OpenFolder(path);
        }

        private void MenuProperties_Click(object? sender, RoutedEventArgs e)
        {
            var mod = GetSelectedMod();
            if (mod != null)
                new ModPropertiesWindow(mod) { Owner = Window.GetWindow(this) }.ShowDialog();
        }

        // ── Active list helpers ───────────────────────────────────────────────────

        private ModItem? GetSelectedMod() => _activeList?.SelectedItem as ModItem;

        private ObservableCollection<ModItem>? GetActiveModList()
        {
            if (_mode == DashMode.Custom) return _modsCustom;
            if (_activeList?.Tag is string key && _core.Mods.ContainsKey(key))
                return _core.Mods[key];
            return null;
        }

        private void FlagActivePending()
        {
            if (_mode == DashMode.IIISeries) UpdateDeployButtonsIII();
            else if (_mode == DashMode.IVSeries)
            {
                if (_activeList?.Tag is string key)
                {
                    int idx = IVEpisodeIndex(key);
                    if (idx >= 0) _pendingIV[idx] = true;
                }
                UpdateDeployButtonsIV();
            }
            else if (_mode == DashMode.Custom) { _pendingCustom = true; UpdateDeployButtonCustom(); }
        }

        private void SaveActiveMods()
        {
            if (_mode == DashMode.IIISeries)      SaveModsIII();
            else if (_mode == DashMode.Custom)    SaveModsCustom();
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
        // DRAG & DROP (multi-list: III/IV series, shared logic)
        // ══════════════════════════════════════════════════════════════════════════

        private void List_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView lv)
            {
                _activeList = lv;
                if (_mode == DashMode.IIISeries)
                {
                    _startIII = e.GetPosition(null);
                    if (e.OriginalSource is FrameworkElement el && el.DataContext is ModItem m)
                        _draggedIII = m;
                }
                else if (_mode == DashMode.IVSeries)
                {
                    _startIV = e.GetPosition(null);
                    if (e.OriginalSource is FrameworkElement el && el.DataContext is ModItem m)
                        _draggedIV = m;
                }
            }
        }

        private void List_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not ListView lv) return;

            if (_mode == DashMode.IIISeries && _draggedIII != null)
            {
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _startIII.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pos.Y - _startIII.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop(lv, _draggedIII, DragDropEffects.Move);
            }
            else if (_mode == DashMode.IVSeries && _draggedIV != null)
            {
                var pos = e.GetPosition(null);
                if (Math.Abs(pos.X - _startIV.X) < SystemParameters.MinimumHorizontalDragDistance &&
                    Math.Abs(pos.Y - _startIV.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop(lv, _draggedIV, DragDropEffects.Move);
            }
        }

        private void List_DragOver(object sender, DragEventArgs e)
        {
            if (sender is not ListView lv || GetDraggedItem() == null) { e.Effects = DragDropEffects.None; return; }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            var line = GetDropLine(lv);
            if (line == null) return;
            double y = GetInsertionLineY(lv, e.GetPosition(lv).Y);
            System.Windows.Controls.Canvas.SetTop(line, y - 1);
            line.Width = Math.Max(lv.ActualWidth - 4, 0);
            System.Windows.Controls.Canvas.SetLeft(line, 2);
            line.Visibility = Visibility.Visible;
        }

        private void List_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListView lv) GetDropLine(lv)!.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }

        private void List_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListView lv) { var l = GetDropLine(lv); if (l != null) l.Visibility = Visibility.Collapsed; }
            if (sender is not ListView targetList || targetList.Tag is not string key) return;

            var mods = _core.Mods.ContainsKey(key) ? _core.Mods[key] : null;
            var dragged = GetDraggedItem();
            if (mods == null || dragged == null || !mods.Contains(dragged)) { ClearDraggedItem(); return; }

            int fromIdx   = mods.IndexOf(dragged);
            int insertIdx = GetInsertionIndex(targetList, e.GetPosition(targetList).Y);
            if (insertIdx > fromIdx) insertIdx--;
            int toIdx = Math.Clamp(insertIdx, 0, mods.Count - 1);

            if (fromIdx != toIdx)
            {
                mods.Move(fromIdx, toIdx);
                for (int i = 0; i < mods.Count; i++) { mods[i].LoadOrder = i; SyncModInfoToFolder(mods[i]); }
                if (_mode == DashMode.IVSeries)
                {
                    int ep = IVEpisodeIndex(key);
                    if (ep >= 0) _pendingIV[ep] = true;
                    UpdateDeployButtonsIV();
                }
                else if (_mode == DashMode.IIISeries)
                {
                    UpdateDeployButtonsIII();
                    SaveModsIII();
                }
            }
            ClearDraggedItem();
        }

        private ModItem? GetDraggedItem() => _mode == DashMode.IIISeries ? _draggedIII : _draggedIV;
        private void ClearDraggedItem()
        {
            _draggedIII = null;
            _draggedIV  = null;
        }

        private System.Windows.Shapes.Rectangle? GetDropLine(ListView lv) => lv.Name switch
        {
            "III_listIII"   => III_DropLineIII,
            "III_listVC"    => III_DropLineVC,
            "III_listSA"    => III_DropLineSA,
            "IV_listIV"     => IV_DropLineIV,
            "IV_listTLaD"   => IV_DropLineTLaD,
            "IV_listTBoGT"  => IV_DropLineTBoGT,
            _ => null
        };

        // ── Custom drag-drop ──────────────────────────────────────────────────────

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
