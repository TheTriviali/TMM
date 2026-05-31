using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    public partial class UnifiedShellWindow : TmmWindow
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly BackendCore _core;
        private string _currentPage = "Library";
        private LibraryEntry? _modalEntry;
        private LibraryEntry? _activeModManagerEntry;   // last game opened in mod manager
        private LibraryEntry? _workspaceEntry;          // the game whose workspace is open (M1)
        private string _workspaceTab = "Mods";          // last active workspace sub-tab
        // Pages instantiated lazily in Window_Loaded
        private BackupsPage? _pageBackups;
        private PathsPage? _pagePaths;
        private SettingsPage? _pageSettingsInstance;
        private AddGamePage? _pageAddGame;
        private NotificationsPage? _pageNotifications;
        private TroubleshootingPage? _pageTroubleshooting;

        // Public property for child windows to access language selector
        public ComboBox CmbLanguage => cmbLanguage;

        // ── Constructor ───────────────────────────────────────────────────────────

        public UnifiedShellWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            // Wire error-guide deep-link → Troubleshooting page + scroll to the matching entry.
            // _pageTroubleshooting is set in Window_Loaded; the lambda captures it by reference safely
            // because OnErrorGuideRequested is only ever invoked on the UI thread after Loaded.
            NotificationService.OnErrorGuideRequested = code =>
            {
                NavigateTo("Troubleshooting");
                _pageTroubleshooting?.ScrollToCode(code);
            };

            // Wire up pages that need the core
            pageLibrary.Initialize(_core);
            // "← Library" in the workspace header: exit the workspace back to the library list.
            pageModManager.BackRequested += () =>
            {
                _workspaceEntry = null;
                NavigateTo("Library");
            };

            // Wire GameCard events from library
            pageLibrary.CardClicked       += OnCardClicked;
            pageLibrary.PlayRequested     += OnPlayRequested;
            pageLibrary.ManageRequested   += OnManageRequested;
            pageLibrary.ArchiveToggled    += OnArchiveToggled;
            pageLibrary.ActiveToggled     += OnActiveToggled;
            pageLibrary.OrderChanged      += OnOrderChanged;
            pageLibrary.AddGameRequested  += OnAddGameRequested;
            pageLibrary.EditGameRequested += OnEditGameRequested;
            pageLibrary.SetFolderRequested += OnSetFolderRequested;
        }

        // ── Loaded ────────────────────────────────────────────────────────────────

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(_core.Settings);

            // Initialize language selector with display names
            var svc = LocalizationService.Instance;
            var langItems = svc.GetAvailableLanguages()
                .Select(code => new ComboBoxItem { Content = svc.GetDisplayName(code), Tag = code })
                .ToList();
            cmbLanguage.ItemsSource = langItems;
            cmbLanguage.SelectedItem = langItems.FirstOrDefault(i => i.Tag is string c && c == _core.Settings.CurrentLanguage);

            // Restore position/size
            if (_core.Settings.WindowLeft > 0 && _core.Settings.WindowTop > 0)
            {
                Left = _core.Settings.WindowLeft;
                Top  = _core.Settings.WindowTop;
            }
            if (_core.Settings.WindowWidth >= 800 && _core.Settings.WindowHeight >= 500)
            {
                Width  = _core.Settings.WindowWidth;
                Height = _core.Settings.WindowHeight;
            }

            InitialSetupWindow? setup = null;
            if (_core.Settings.FirstLaunch)
            {
                setup = new InitialSetupWindow(_core) { Owner = this };
                setup.ShowDialog();
            }

            // Build and inject pages that require BackendCore constructor
            _pageBackups          = new BackupsPage(_core);
            _pagePaths            = new PathsPage(_core);
            _pageSettingsInstance = new SettingsPage(_core);
            _pageAddGame          = new AddGamePage(_core);
            _pageNotifications    = new NotificationsPage();
            _pageTroubleshooting  = new TroubleshootingPage();

            pageBackupsPlaceholder.Content          = _pageBackups;
            pagePathsPlaceholder.Content            = _pagePaths;
            pageSettings.Content                    = _pageSettingsInstance;
            pageAddGamePlaceholder.Content          = _pageAddGame;
            pageNotificationsPlaceholder.Content    = _pageNotifications;
            pageTroubleshootingPlaceholder.Content  = _pageTroubleshooting;

            _pageAddGame.Cancelled  += () => NavigateTo("Library");
            _pageAddGame.GameSaved  += async () =>
            {
                await _core.InitializeAsync();
                RefreshLibrary();
                NavigateTo("Library");
            };

            // Build library entries from all known games
            await _core.InitializeAsync();
            var entries = BuildLibraryEntries().ToList();
            pageLibrary.LoadEntries(entries);
            _pageBackups.Initialize(entries);
            pageDownloads.Initialize(_core, entries);
            pageDownloads.UrlChanged += url =>
            {
                if (_currentPage == "Downloads") txtBrowserUrl.Text = url;
            };
            // Migrate users who were last on a now-removed view (showcase, grid) to Home.
            if (_core.Settings.LibraryViewMode is "showcase" or "grid")
            {
                _core.Settings.LibraryViewMode = "home";
                _core.SaveSettings();
            }
            pageLibrary.SetViewMode(_core.Settings.LibraryViewMode);
            UpdateViewModeButtonStyles(_core.Settings.LibraryViewMode);

            // Warm the Home mod-size cache off the render path (background, best-effort).
            _ = _core.RecomputeModsInstalledSizeAsync();

            if (setup?.OpenAddGameAfterClose == true)
            {
                _pageAddGame?.LoadForAdd();
                NavigateTo("AddGame");
            }
            else
            {
                // Respect startup preference: open to Mod Manager only when the user asked for it
                if (_core.Settings.StartupPage == "ModManager" &&
                    !string.IsNullOrEmpty(_core.Settings.ActiveGameKey))
                {
                    var defaultEntry = entries.FirstOrDefault(e => e.IsActive && !e.IsPlaceholder);
                    if (defaultEntry != null)
                    {
                        OnManageRequested(defaultEntry);
                        return;
                    }
                }
                SetNavActive("Library");
            }
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

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string code)
            {
                LocalizationService.Instance.SetLanguage(code);
                _core.Settings.CurrentLanguage = code;
                _core.SaveSettings();
            }
        }

        // ── Navigation ────────────────────────────────────────────────────────────

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string page) return;

            // While a game workspace is open, the Library rail item returns to that
            // workspace (same game + last sub-tab). Use "← Library" in the header to
            // actually exit to the library list.
            if (page == "Library" && _workspaceEntry is { } ws)
            {
                NavigateToWorkspace(ws, _workspaceTab);
                return;
            }

            NavigateTo(page);
        }

        /// <summary>
        /// Opens <paramref name="entry"/>'s full game workspace (M1) on sub-tab
        /// <paramref name="tab"/> and remembers it so global-rail round-trips restore it.
        /// </summary>
        private void NavigateToWorkspace(LibraryEntry entry, string tab)
        {
            _activeModManagerEntry = entry;
            _workspaceEntry = entry;
            _workspaceTab = tab;
            pageModManager.LoadEntry(entry, _core, tab);
            NavigateTo("ModManager");
        }

        private void NavigateTo(string page)
        {
            // Preserve the active workspace sub-tab when stepping out to a global page.
            if (_currentPage == "ModManager" && _workspaceEntry is not null)
                _workspaceTab = pageModManager.CurrentTab;

            _currentPage = page;

            pageLibrary.Visibility                       = Visibility.Collapsed;
            pageModManager.Visibility                    = Visibility.Collapsed;
            pageDownloads.Visibility                     = Visibility.Collapsed;
            pageBackupsPlaceholder.Visibility            = Visibility.Collapsed;
            pagePathsPlaceholder.Visibility              = Visibility.Collapsed;
            pageSettings.Visibility                      = Visibility.Collapsed;
            pageAddGamePlaceholder.Visibility            = Visibility.Collapsed;
            pageNotificationsPlaceholder.Visibility      = Visibility.Collapsed;
            pageTroubleshootingPlaceholder.Visibility    = Visibility.Collapsed;

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
                    // The workspace is loaded by NavigateToWorkspace; here we just reveal it.
                    if (_activeModManagerEntry == null)
                        return; // no games yet — no-op on fresh install
                    pageModManager.Visibility = Visibility.Visible;
                    titleSubtext.Text         = $" — {_activeModManagerEntry.DisplayName}";
                    break;

                case "Downloads":
                    // Pre-select the active/default game so Downloads follows what the user is working on
                    var activeKey = _activeModManagerEntry?.Key
                        ?? BuildLibraryEntries().FirstOrDefault(e => e.IsActive && !e.IsPlaceholder)?.Key;
                    pageDownloads.SetActiveGame(activeKey);
                    pageDownloads.Visibility    = Visibility.Visible;
                    downloadsNavBar.Visibility  = Visibility.Visible;
                    titleSubtext.Text           = " — Downloads";
                    txtBrowserUrl.Text          = pageDownloads.CurrentUrl;
                    break;

                case "Backups":
                    pageBackupsPlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text                 = " — Backups";
                    break;

                case "Paths":
                    pagePathsPlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text = " — File Locations";
                    break;

                case "Settings":
                    pageSettings.Visibility = Visibility.Visible;
                    titleSubtext.Text       = " — Settings";
                    break;

                case "AddGame":
                    pageAddGamePlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text = _pageAddGame?.IsEditMode == true
                        ? " — Edit Game"
                        : " — Add a Game";
                    break;

                case "Notifications":
                    pageNotificationsPlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text = " — Notifications";
                    break;

                case "Troubleshooting":
                    pageTroubleshootingPlaceholder.Visibility = Visibility.Visible;
                    titleSubtext.Text = " — Help & Troubleshooting";
                    break;
            }

            SetNavActive(page);
            cardModal.Visibility     = Visibility.Collapsed;
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            // Active game indicator
            string? activeKey = _core.Settings.ActiveGameKey;
            if (!string.IsNullOrEmpty(activeKey))
            {
                string? name = GameRegistry.Instance.GetGameProfile(activeKey)?.DisplayName ?? activeKey;
                statusActiveGame.Text = $"★ {name}";
                statusActiveGame.Visibility = Visibility.Visible;
            }
            else
            {
                statusActiveGame.Visibility = Visibility.Collapsed;
            }

            // Disk space for the workspace game directory (if open), else hide
            if (_workspaceEntry is not null && _currentPage == "ModManager")
            {
                statusDiskSpace.Text = _core.GetDriveSpaceInfo();
                statusDiskSpace.Visibility = Visibility.Visible;
            }
            else
            {
                statusDiskSpace.Text = "";
                statusDiskSpace.Visibility = Visibility.Collapsed;
            }
        }

        private void SetNavActive(string page)
        {
            // Reset all to default style. Post-M1 the rail holds only truly-global
            // destinations; Mod Manager / Downloads / Backups / Add Game moved into the
            // per-game workspace (or the library header).
            navBtnLibrary.Style           = (Style)Resources["NavBtnStyle"];
            navBtnNotifications.Style     = (Style)Resources["NavBtnStyle"];
            navBtnTroubleshooting.Style   = (Style)Resources["NavBtnStyle"];
            navBtnSettings.Style          = (Style)Resources["NavBtnStyle"];

            // Highlight the active button. The workspace (ModManager) keeps Library lit,
            // since it is reached from the library.
            var activeStyle = (Style)Resources["NavBtnActiveStyle"];
            switch (page)
            {
                case "Library":          navBtnLibrary.Style         = activeStyle; break;
                case "ModManager":       navBtnLibrary.Style         = activeStyle; break;
                case "Notifications":    navBtnNotifications.Style    = activeStyle; break;
                case "Troubleshooting":  navBtnTroubleshooting.Style  = activeStyle; break;
                case "Paths":            navBtnSettings.Style         = activeStyle; break; // Paths merged into Settings
                case "Settings":         navBtnSettings.Style         = activeStyle; break;
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

            btnViewHome.Style = activeMode != "list" ? activeStyle : inactiveStyle;
            btnViewList.Style = activeMode == "list" ? activeStyle : inactiveStyle;
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

        private void OnSetFolderRequested(LibraryEntry entry)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = $"Select {entry.DisplayName} Folder"
            };
            if (dlg.ShowDialog() != true) return;

            var config = GameRegistry.Instance.GetCustomGameConfig(entry.Key);
            var builtIn = GameProfile.All.FirstOrDefault(p => p.Key == entry.Key);
            if (builtIn != null)
            {
                _core.SetVanillaPath(builtIn, dlg.FolderName);
            }
            else if (config != null)
            {
                config.GameDirectory = dlg.FolderName;
                GameRegistry.Instance.SaveCustomGameSync(entry.Key, config);
            }
            NotificationService.ShowSuccess($"Game folder set for {entry.DisplayName}.", "Library");
            RefreshLibrary();
        }

        private void OnManageRequested(LibraryEntry entry)
        {
            // Prompt to set default if none is set
            if (string.IsNullOrEmpty(_core.Settings.ActiveGameKey) && !entry.IsPlaceholder)
            {
                var svc = LocalizationService.Instance;
                string title = svc["Prompt_SetActive_Title"];
                string message = string.Format(svc["Prompt_SetActive_Message"], entry.DisplayName);
                if (MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _core.Settings.ActiveGameKey = entry.Key;
                    _core.SaveSettings();
                    RefreshLibrary();
                }
            }

            // Opening a game from the library always starts on the Mods tab.
            NavigateToWorkspace(entry, "Mods");
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

        private void OnActiveToggled(LibraryEntry entry, bool isActive)
        {
            string? prevKey = _core.Settings.ActiveGameKey;

            // Toggle: if already active, clicking again clears it
            if (!isActive || _core.Settings.ActiveGameKey == entry.Key)
                _core.Settings.ActiveGameKey = null;
            else
                _core.Settings.ActiveGameKey = entry.Key;
            _core.SaveSettings();
            RefreshLibrary();

            string? newKey = _core.Settings.ActiveGameKey;
            if (newKey is null)
            {
                NotificationService.ShowInfo($"Default game cleared.", "Default");
            }
            else if (prevKey is not null && prevKey != newKey)
            {
                string? prevName = GameRegistry.Instance.GetGameProfile(prevKey)?.DisplayName ?? prevKey;
                NotificationService.ShowInfo($"Default changed from {prevName} to {entry.DisplayName}.", "Default");
            }
            else
            {
                NotificationService.ShowInfo($"Default game set to {entry.DisplayName}.", "Default");
            }
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
            UpdateStatusBar();
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
                // A user color override (if any) wins over the profile's shipped gradient.
                var colorOverride = _core.GetCardColor(key);
                string startHex = colorOverride?.Start ?? config.GradientStartHex ?? "#1A1A2E";
                string endHex   = colorOverride?.End   ?? config.GradientEndHex   ?? "#0D0D1A";

                result.Add(new LibraryEntry(
                    Key:             key,
                    DisplayName:     config.GameName,
                    Subtitle:        config.Description ?? "",
                    GradientStartHex: startHex,
                    GradientEndHex:   endHex,
                    Status:          config.LibraryStatus,
                    ModCount:        CountMods(key),
                    IsReady:         !string.IsNullOrEmpty(config.GameDirectory) &&
                                     Directory.Exists(config.GameDirectory),
                    Category:        config.IsBuiltIn ? "Built-in" : "Custom",
                    GameKeys:        [key],
                    IsPlaceholder:   false,
                    IsArchived:      settings.ArchivedGameKeys.Contains(key),
                    IsActive:        settings.ActiveGameKey == key,
                    TmmGameFileName: config.SourceFileName
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

            // Default game is pinned to the front (locked first in both views,
            // regardless of saved drag order).
            var defaultIdx = result.FindIndex(e => !e.IsPlaceholder && e.IsActive);
            if (defaultIdx > 0)
            {
                var def = result[defaultIdx];
                result.RemoveAt(defaultIdx);
                result.Insert(0, def);
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

        // ── Toast handlers ────────────────────────────────────────────────────────

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

        private void Toast_ErrorLink(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.DataContext is NotificationItem item
                && item.ErrorCode is not null)
            {
                NotificationService.OnErrorGuideRequested?.Invoke(item.ErrorCode);
            }
        }

        // ── Add / Edit game flow ──────────────────────────────────────────────────

        private void OnAddGameRequested()
        {
            if (_pageAddGame is null) return;
            _pageAddGame.LoadForAdd();
            NavigateTo("AddGame");
        }

        private void OnEditGameRequested(LibraryEntry entry)
        {
            if (_pageAddGame is null) return;
            var config = GameRegistry.Instance.GetCustomGameConfig(entry.Key);
            if (config is null) return;
            _pageAddGame.LoadForEdit(entry.Key, config);
            NavigateTo("AddGame");
        }

    }
}
