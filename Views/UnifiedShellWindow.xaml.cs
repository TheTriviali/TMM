using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    public partial class UnifiedShellWindow : TmmWindow
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly BackendCore _core;
        private string _currentPage = "Library";
        private LibraryEntry? _modalEntry;
        private LibraryEntry? _activeModManagerEntry;   // last game opened in mod manager
        // Pages instantiated lazily in Window_Loaded
        private PathsPage? _pagePaths;
        private SettingsPage? _pageSettingsInstance;

        // ── Constructor ───────────────────────────────────────────────────────────

        public UnifiedShellWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            // Wire up pages that need the core
            pageLibrary.Initialize(_core);
            pageModManager.BackRequested += () => NavigateTo("Library");

            // Wire GameCard events from library
            pageLibrary.CardClicked     += OnCardClicked;
            pageLibrary.PlayRequested   += OnPlayRequested;
            pageLibrary.ManageRequested += OnManageRequested;
            pageLibrary.ArchiveToggled  += OnArchiveToggled;
            pageLibrary.DefaultToggled  += OnDefaultToggled;
            pageLibrary.OrderChanged    += OnOrderChanged;
            pageLibrary.AddGameRequested += OnAddGameRequested;
        }

        // ── Loaded ────────────────────────────────────────────────────────────────

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(_core.Settings);

            // Restore position/size
            if (_core.Settings.WindowLeft > 0 && _core.Settings.WindowTop > 0)
            {
                Left = _core.Settings.WindowLeft;
                Top  = _core.Settings.WindowTop;
            }
            if (_core.Settings.WindowWidth >= 900 && _core.Settings.WindowHeight >= 600)
            {
                Width  = _core.Settings.WindowWidth;
                Height = _core.Settings.WindowHeight;
            }

            if (_core.Settings.FirstLaunch)
            {
                var setup = new InitialSetupWindow(_core) { Owner = this };
                setup.ShowDialog();
            }

            // Build and inject pages that require BackendCore constructor
            _pagePaths          = new PathsPage(_core);
            _pageSettingsInstance = new SettingsPage(_core);
            pagePathsPlaceholder.Content = _pagePaths;
            pageSettings.Content         = _pageSettingsInstance;

            // Build library entries from all known games
            await _core.InitializeAsync();
            var entries = BuildLibraryEntries().ToList();
            pageLibrary.LoadEntries(entries);
            pageDownloads.Initialize(_core, entries);
            pageDownloads.UrlChanged += url =>
            {
                if (_currentPage == "Downloads") txtBrowserUrl.Text = url;
            };
            pageLibrary.SetViewMode(_core.Settings.LibraryViewMode);
            UpdateViewModeButtonStyles(_core.Settings.LibraryViewMode);

            SetNavActive("Library");
        }

        // ── Window chrome ─────────────────────────────────────────────────────────

        private new void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { /* button released before DragMove could start */ }
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            bool max = WindowState == WindowState.Maximized;
            MainWindowBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(10);
            MainWindowBorder.Margin       = max ? new Thickness(8)    : new Thickness(0);
        }

        private new void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private new void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveWindowState();
            Close();
        }

        private void BtnQuit_Click(object sender, RoutedEventArgs e)
        {
            SaveWindowState();
            Application.Current.Shutdown();
        }

        private void SaveWindowState()
        {
            if (WindowState == WindowState.Normal)
            {
                _core.Settings.WindowLeft   = Left;
                _core.Settings.WindowTop    = Top;
                _core.Settings.WindowWidth  = Width;
                _core.Settings.WindowHeight = Height;
                _core.SaveSettings();
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string page)
                NavigateTo(page);
        }

        private void NavigateTo(string page)
        {
            _currentPage = page;

            pageLibrary.Visibility       = Visibility.Collapsed;
            pageModManager.Visibility    = Visibility.Collapsed;
            pageDownloads.Visibility     = Visibility.Collapsed;
            pageBackups.Visibility       = Visibility.Collapsed;
            pagePathsPlaceholder.Visibility = Visibility.Collapsed;
            pageSettings.Visibility      = Visibility.Collapsed;

            searchContainer.Visibility  = Visibility.Collapsed;
            viewModePanel.Visibility    = Visibility.Collapsed;
            downloadsNavBar.Visibility  = Visibility.Collapsed;

            switch (page)
            {
                case "Library":
                    pageLibrary.Visibility     = Visibility.Visible;
                    searchContainer.Visibility = Visibility.Visible;
                    viewModePanel.Visibility   = Visibility.Visible;
                    titleSubtext.Text          = " — Library";
                    break;

                case "ModManager":
                    // If no active entry, fall back to default game then first available
                    if (_activeModManagerEntry == null)
                    {
                        var entries = BuildLibraryEntries();
                        _activeModManagerEntry =
                            entries.FirstOrDefault(e => !e.IsPlaceholder && e.IsDefault) ??
                            entries.FirstOrDefault(e => !e.IsPlaceholder && !e.IsArchived);
                    }
                    if (_activeModManagerEntry == null)
                        return; // no games yet — button click is a no-op on fresh install
                    pageModManager.LoadEntry(_activeModManagerEntry, _core);
                    pageModManager.Visibility = Visibility.Visible;
                    titleSubtext.Text         = $" — {_activeModManagerEntry.DisplayName}";
                    UpdateModManagerNavTip();
                    break;

                case "Downloads":
                    pageDownloads.Visibility    = Visibility.Visible;
                    downloadsNavBar.Visibility  = Visibility.Visible;
                    titleSubtext.Text           = " — Downloads";
                    txtBrowserUrl.Text          = pageDownloads.CurrentUrl;
                    break;

                case "Backups":
                    pageBackups.Visibility = Visibility.Visible;
                    titleSubtext.Text      = " — Backups";
                    break;

                case "Paths":
                    pagePathsPlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text = " — File Locations";
                    break;

                case "Settings":
                    pageSettings.Visibility = Visibility.Visible;
                    titleSubtext.Text       = " — Settings";
                    break;
            }

            SetNavActive(page);
            cardModal.Visibility     = Visibility.Collapsed;
        }

        private void SetNavActive(string page)
        {
            // Reset all to default style
            navBtnLibrary.Style   = (Style)Resources["NavBtnStyle"];
            navBtnModMgr.Style    = (Style)Resources["NavBtnStyle"];
            navBtnDownloads.Style = (Style)Resources["NavBtnStyle"];
            navBtnBackups.Style   = (Style)Resources["NavBtnStyle"];
            navBtnPaths.Style     = (Style)Resources["NavBtnStyle"];
            navBtnSettings.Style  = (Style)Resources["NavBtnStyle"];

            // Highlight the active button
            var activeStyle = (Style)Resources["NavBtnActiveStyle"];
            switch (page)
            {
                case "Library":    navBtnLibrary.Style   = activeStyle; break;
                case "ModManager": navBtnModMgr.Style    = activeStyle; break;
                case "Downloads":  navBtnDownloads.Style = activeStyle; break;
                case "Backups":    navBtnBackups.Style   = activeStyle; break;
                case "Paths":      navBtnPaths.Style     = activeStyle; break;
                case "Settings":   navBtnSettings.Style  = activeStyle; break;
            }
        }

        // ── Downloads browser nav ────────────────────────────────────────────────

        private void BtnBrowserBack_Click(object sender, RoutedEventArgs e)
            => pageDownloads.GoBack();

        private void BtnBrowserForward_Click(object sender, RoutedEventArgs e)
            => pageDownloads.GoForward();

        private void BtnBrowserRefresh_Click(object sender, RoutedEventArgs e)
            => pageDownloads.Reload();

        private void TxtBrowserUrl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                pageDownloads.Navigate(txtBrowserUrl.Text);
        }

        // ── Search (library) ──────────────────────────────────────────────────────

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string q = txtSearch.Text;
            btnClearSearch.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;
            if (_currentPage == "Library")
                pageLibrary.ApplySearchFilter(q);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            pageLibrary.ApplySearchFilter("");
        }

        // ── View mode (library) ───────────────────────────────────────────────────

        private void BtnViewMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string mode) return;
            _core.Settings.LibraryViewMode = mode;
            _core.SaveSettings();
            pageLibrary.SetViewMode(mode);
            UpdateViewModeButtonStyles(mode);
        }

        private void UpdateViewModeButtonStyles(string activeMode)
        {
            var activeStyle = (Style)Resources["ViewModeBtnActiveStyle"];
            var inactiveStyle = (Style)Resources["ViewModeBtnStyle"];

            btnViewGrid.Style     = activeMode == "grid"     ? activeStyle : inactiveStyle;
            btnViewList.Style     = activeMode == "list"     ? activeStyle : inactiveStyle;
            btnViewShowcase.Style = activeMode == "showcase" ? activeStyle : inactiveStyle;
        }

        // ── Card-click modal ──────────────────────────────────────────────────────

        private void OnCardClicked(LibraryEntry entry)
        {
            _modalEntry = entry;
            modalGameName.Text = entry.DisplayName;
            modalGameSub.Text  = entry.Subtitle;

            // Show Launch button only if game has an executable we can run
            bool canLaunch = CanLaunchEntry(entry);
            modalBtnLaunch.Visibility = canLaunch ? Visibility.Visible : Visibility.Collapsed;

            cardModal.Visibility = Visibility.Visible;
        }

        private bool CanLaunchEntry(LibraryEntry entry)
        {
            foreach (var key in entry.GameKeys)
            {
                var profile = GameProfile.ByKey(key) ?? GameRegistry.Instance.GetGameProfile(key);
                if (profile == null) continue;
                string? gameDir = _core.GetVanillaPath(profile);
                if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir) &&
                    File.Exists(Path.Combine(gameDir, profile.ExeName)))
                    return true;
            }
            // Custom game — check ExePath
            var cfg = GameRegistry.Instance.GetCustomGameConfig(entry.Key);
            if (cfg != null && !string.IsNullOrEmpty(cfg.ExePath)) return true;
            return false;
        }

        private void ModalBtnLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_modalEntry == null) return;
            cardModal.Visibility = Visibility.Collapsed;
            OnPlayRequested(_modalEntry);
        }

        private void ModalBtnManage_Click(object sender, RoutedEventArgs e)
        {
            if (_modalEntry == null) return;
            cardModal.Visibility = Visibility.Collapsed;
            OnManageRequested(_modalEntry);
        }

        private void ModalBtnCancel_Click(object sender, RoutedEventArgs e)
            => cardModal.Visibility = Visibility.Collapsed;

        // ── Library card event handlers ───────────────────────────────────────────

        private void OnPlayRequested(LibraryEntry entry)
        {
            // Try each key in turn until we find a launchable game
            foreach (var key in entry.GameKeys)
            {
                var profile = GameProfile.ByKey(key) ?? GameRegistry.Instance.GetGameProfile(key);
                if (profile == null) continue;
                string? gameDir = _core.GetVanillaPath(profile);
                if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir)) continue;
                string exe = Path.Combine(gameDir, profile.ExeName);
                if (!File.Exists(exe)) continue;
                Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = gameDir });
                return;
            }

            // Custom game via SteamAppId or ExePath
            var cfg = GameRegistry.Instance.GetCustomGameConfig(entry.Key);
            if (cfg != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.SteamAppId))
                {
                    SteamLauncher.Invoke("rungameid", cfg.SteamAppId);
                    return;
                }
                if (!string.IsNullOrEmpty(cfg.ExePath))
                {
                    string exeFull = Path.IsPathRooted(cfg.ExePath)
                        ? cfg.ExePath
                        : Path.Combine(cfg.GameDirectory, cfg.ExePath);
                    if (File.Exists(exeFull))
                    {
                        Process.Start(new ProcessStartInfo(exeFull) { UseShellExecute = true });
                        return;
                    }
                }
            }

            MessageBox.Show("No launchable executable found for this game.\n\nSet the game directory in Mod Manager.",
                "Cannot Launch", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnManageRequested(LibraryEntry entry)
        {
            _activeModManagerEntry = entry;
            pageModManager.LoadEntry(entry, _core);
            UpdateModManagerNavTip();
            NavigateTo("ModManager");
        }

        private void UpdateModManagerNavTip()
        {
            navBtnModMgr.ToolTip = _activeModManagerEntry != null
                ? $"Mod Manager — {_activeModManagerEntry.DisplayName}"
                : "Mod Manager";
        }

        private void OnArchiveToggled(LibraryEntry entry, bool archive)
        {
            if (archive)
            {
                if (!_core.Settings.ArchivedGameKeys.Contains(entry.Key))
                    _core.Settings.ArchivedGameKeys.Add(entry.Key);
            }
            else
            {
                _core.Settings.ArchivedGameKeys.Remove(entry.Key);
            }
            _core.SaveSettings();
            RefreshLibrary();
        }

        private void OnDefaultToggled(LibraryEntry entry, bool isDefault)
        {
            // Toggle: if already default, clicking again clears it
            if (!isDefault || _core.Settings.DefaultGameKey == entry.Key)
                _core.Settings.DefaultGameKey = null;
            else
                _core.Settings.DefaultGameKey = entry.Key;
            _core.SaveSettings();
            RefreshLibrary();
        }

        private void OnOrderChanged(List<string> newOrder)
        {
            _core.Settings.GameOrder = newOrder;
            _core.SaveSettings();
            // No re-render needed — LibraryPage already shows the new order
        }

        // ── Library building ──────────────────────────────────────────────────────

        private void RefreshLibrary()
        {
            var entries = BuildLibraryEntries();
            pageLibrary.LoadEntries(entries);
        }

        private IEnumerable<LibraryEntry> BuildLibraryEntries()
        {
            var result = new List<LibraryEntry>();
            var settings = _core.Settings;

            // ── All games from GameRegistry (built-in .tmmgame profiles + user-added) ──
            var registry = GameRegistry.Instance;
            var allCustom = registry.GetBuiltInCustomGames()
                .Concat(registry.GetCustomGames())
                .ToList();
            foreach (var (key, config) in allCustom)
            {
                result.Add(new LibraryEntry(
                    Key:             key,
                    DisplayName:     config.GameName,
                    Subtitle:        config.Description ?? "",
                    GradientStartHex: config.GradientStartHex ?? "#1A1A2E",
                    GradientEndHex:   config.GradientEndHex   ?? "#0D0D1A",
                    Status:          config.LibraryStatus,
                    ModCount:        CountMods(key),
                    IsReady:         !string.IsNullOrEmpty(config.GameDirectory) &&
                                     Directory.Exists(config.GameDirectory),
                    Category:        config.IsBuiltIn ? "Built-in" : "Custom",
                    GameKeys:        [key],
                    IsPlaceholder:   false,
                    IsArchived:      settings.ArchivedGameKeys.Contains(key),
                    IsDefault:       settings.DefaultGameKey == key
                ));
            }

            // Apply order from settings
            if (settings.GameOrder.Count > 0)
            {
                var ordered = new List<LibraryEntry>();
                foreach (var key in settings.GameOrder)
                {
                    var match = result.FirstOrDefault(e => e.Key == key);
                    if (match != null) ordered.Add(match);
                }
                // Append any new entries not in the saved order
                foreach (var e in result)
                    if (!ordered.Contains(e)) ordered.Add(e);
                result = ordered;
            }

            return result;
        }

        private int CountMods(params string[] keys)
        {
            int count = 0;
            foreach (var key in keys)
                if (_core.Mods.TryGetValue(key, out var list))
                    count += list.Count;
            return count;
        }

        // ── Add-game flow ─────────────────────────────────────────────────────────

        private async void OnAddGameRequested()
        {
            var wizard = new CustomGameSetupWizard { Owner = this };
            if (wizard.ShowDialog() != true || wizard.Result is null) return;

            try
            {
                await GameRegistry.Instance.AddCustomGameAsync(wizard.Result);

                // Re-init registers the new key in BackendCore.Mods and creates the mod folder.
                await _core.InitializeAsync();

                RefreshLibrary();
                NotificationService.ShowSuccess($"'{wizard.Result.GameName}' added.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Could not save game: {ex.Message}");
            }
        }

    }
}
