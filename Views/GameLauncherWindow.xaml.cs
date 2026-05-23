using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TMM
{
    public partial class GameLauncherWindow : Window
    {
        private readonly BackendCore _core;

        public GameLauncherWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(_core.Settings);
            ThemeEngine.ApplyFont(this, _core.Settings);
            ThemeEngine.TryApplyMica(this, _core.Settings.MicaEnabled);

            txtVersion.Text = $"v{_core.Version}";

            await _core.InitializeAsync();
            RebuildCards();
            txtStatus.Text = $"Ready — {GameRegistry.Instance.GetAllGames().Count} game(s) registered";
        }

        // ── Card construction ─────────────────────────────────────────────────────

        private void RebuildCards()
        {
            GameCardsPanel.Children.Clear();

            GameCardsPanel.Children.Add(BuildBuiltInCard());
            GameCardsPanel.Children.Add(BuildIvCard());

            foreach (var (key, config) in GameRegistry.Instance.GetCustomGames())
                GameCardsPanel.Children.Add(BuildCustomCard(key, config));

            GameCardsPanel.Children.Add(BuildAddCard());
        }

        private Border BuildBuiltInCard()
        {
            bool anyReady = _core.IsGameReady(GameProfile.III) ||
                            _core.IsGameReady(GameProfile.VC) ||
                            _core.IsGameReady(GameProfile.SA);

            var card = MakeCardShell();
            var sp = new StackPanel();

            sp.Children.Add(MakeCardTitle("GTA III Series"));
            sp.Children.Add(MakeCardSubtitle("GTA III · Vice City · San Andreas"));
            sp.Children.Add(MakeStatusDot(anyReady));

            var manageBtn = MakeManageButton("Manage");
            manageBtn.Click += (_, _) => OpenGTASeries();
            sp.Children.Add(manageBtn);

            card.Child = sp;
            return card;
        }

        private Border BuildIvCard()
        {
            bool anyReady = _core.IsGameReady(GameProfile.IV) ||
                            _core.IsGameReady(GameProfile.TLaD) ||
                            _core.IsGameReady(GameProfile.TBoGT);

            var card = MakeCardShell();
            var sp = new StackPanel();

            sp.Children.Add(MakeCardTitle("GTA IV Series"));
            sp.Children.Add(MakeCardSubtitle("IV · The Lost and Damned · The Ballad of Gay Tony"));
            sp.Children.Add(MakeStatusDot(anyReady));

            var manageBtn = MakeManageButton("Manage");
            manageBtn.Click += (_, _) => OpenGtaIv();
            sp.Children.Add(manageBtn);

            card.Child = sp;
            return card;
        }

        private Border BuildCustomCard(string key, CustomGameProfile config)
        {
            var profile = GameRegistry.Instance.GetGameProfile(key);
            bool isReady = profile != null && _core.IsGameReady(profile);

            var card = MakeCardShell();
            var sp = new StackPanel();

            sp.Children.Add(MakeCardTitle(config.GameName));

            string subtitle = string.IsNullOrEmpty(config.GameDirectory)
                ? "Directory not set"
                : System.IO.Path.GetFileName(config.GameDirectory.TrimEnd('\\', '/'))
                  ?? config.GameDirectory;
            sp.Children.Add(MakeCardSubtitle(subtitle));
            sp.Children.Add(MakeStatusDot(isReady));

            // Manage + Edit + Delete row
            var row = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            string capturedKey = key;

            var manageBtn = MakeManageButton("Manage");
            manageBtn.Click += (_, _) => OpenCustomGame(capturedKey);
            Grid.SetColumn(manageBtn, 0);
            row.Children.Add(manageBtn);

            var editBtn = MakeIconButton("", "Edit game settings");
            editBtn.Click += (_, _) => EditCustomGame(capturedKey);
            Grid.SetColumn(editBtn, 1);
            row.Children.Add(editBtn);

            var delBtn = MakeIconButton("", "Remove this game",
                new SolidColorBrush(Color.FromRgb(200, 60, 60)));
            delBtn.Click += (_, _) => DeleteCustomGame(capturedKey);
            Grid.SetColumn(delBtn, 2);
            row.Children.Add(delBtn);

            sp.Children.Add(row);
            card.Child = sp;
            return card;
        }

        private Border BuildAddCard()
        {
            var card = new Border
            {
                Width = 190,
                MinHeight = 160,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(6),
                Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Top,
                Cursor = Cursors.Hand
            };
            card.SetResourceReference(Border.BorderBrushProperty, "SubTextBrush");

            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            sp.Children.Add(new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 28,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 16, 0, 8)
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Add Custom Game",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            });
            foreach (TextBlock tb in sp.Children)
                tb.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

            card.Child = sp;
            card.MouseDown += (_, e) => { if (e.ChangedButton == MouseButton.Left) AddCustomGame(); };

            // Hover effect — neutral grey tint works on both light and dark themes
            card.MouseEnter += (_, _) =>
                card.Background = new SolidColorBrush(Color.FromArgb(30, 128, 128, 128));
            card.MouseLeave += (_, _) =>
                card.Background = Brushes.Transparent;

            return card;
        }

        // ── Card helpers ──────────────────────────────────────────────────────────

        private Border MakeCardShell()
        {
            var b = new Border
            {
                Width = 190,
                MinHeight = 160,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(6),
                VerticalAlignment = VerticalAlignment.Top
            };
            b.SetResourceReference(Border.BackgroundProperty, "PanelBrush");
            return b;
        }

        private TextBlock MakeCardTitle(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            return tb;
        }

        private TextBlock MakeCardSubtitle(string text)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.6,
                Margin = new Thickness(0, 0, 0, 10)
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            return tb;
        }

        private static StackPanel MakeStatusDot(bool ready)
        {
            var sp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 2)
            };
            sp.Children.Add(new Ellipse
            {
                Width = 7, Height = 7,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = ready
                    ? new SolidColorBrush(Color.FromRgb(80, 200, 100))
                    : new SolidColorBrush(Color.FromRgb(160, 60, 60))
            });
            var lbl = new TextBlock
            {
                Text = ready ? "Configured" : "Not configured",
                FontSize = 10,
                Opacity = 0.7,
                VerticalAlignment = VerticalAlignment.Center
            };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            sp.Children.Add(lbl);
            return sp;
        }

        private Button MakeManageButton(string label)
        {
            var btn = new Button
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 6, 10, 6),
                Cursor = Cursors.Hand,
                FontSize = 11
            };
            btn.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "AccentTextBrush");
            // Simple template via style
            btn.Style = (Style)FindResource("CardButtonStyle");
            return btn;
        }

        private Button MakeIconButton(string icon, string tooltip, Brush? foreground = null)
        {
            var btn = new Button
            {
                Content = icon,
                ToolTip = tooltip,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(5),
                Cursor = Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 12
            };
            if (foreground != null)
                btn.Foreground = foreground;
            else
                btn.SetResourceReference(Button.ForegroundProperty, "SubTextBrush");
            return btn;
        }

        // ── Actions ───────────────────────────────────────────────────────────────

        private void OpenGTASeries()
        {
            _core.Settings.LastSelectedGameKey = "GTA_SERIES";
            _core.SaveSettings();
            var w = new MainDashboardWindow(_core) { Owner = this };
            w.Closed += (_, _) => { Show(); RebuildCards(); };
            Hide();
            w.Show();
        }

        private void OpenGtaIv()
        {
            _core.Settings.LastSelectedGameKey = "IV";
            _core.SaveSettings();
            var w = new Gta4DashboardWindow(_core) { Owner = this };
            w.Closed += (_, _) => { Show(); RebuildCards(); };
            Hide();
            w.Show();
        }

        private void OpenCustomGame(string key)
        {
            var profile = GameRegistry.Instance.GetGameProfile(key);
            var config  = GameRegistry.Instance.GetCustomGameConfig(key);
            if (profile == null || config == null) return;

            _core.Settings.LastSelectedGameKey = key;
            _core.SaveSettings();

            var w = new CustomGameDashboardWindow(_core, profile, config) { Owner = this };
            w.Closed += (_, _) => { Show(); RebuildCards(); };
            Hide();
            w.Show();
        }

        private async void AddCustomGame()
        {
            var dlg = new CustomGameConfigWindow(null) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            string key = await GameRegistry.Instance.AddCustomGameAsync(dlg.Result);
            await _core.InitializeAsync();

            _core.Settings.GamePaths.TryAdd(key, dlg.Result.GameDirectory);
            _core.SaveSettings();

            RebuildCards();
            txtStatus.Text = $"Added '{dlg.Result.GameName}'";
        }

        private void EditCustomGame(string key)
        {
            var existing = GameRegistry.Instance.GetCustomGameConfig(key);
            if (existing == null) return;

            var dlg = new CustomGameConfigWindow(existing) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;

            _ = GameRegistry.Instance.UpdateCustomGameAsync(key, dlg.Result);
            _core.Settings.GamePaths[key] = dlg.Result.GameDirectory;
            _core.SaveSettings();

            RebuildCards();
        }

        private async void DeleteCustomGame(string key)
        {
            var config = GameRegistry.Instance.GetCustomGameConfig(key);
            if (config == null) return;

            var r = MessageBox.Show(
                $"Remove '{config.GameName}' from TMM?\n\nThis removes the game entry and its TMM data. Your actual game files are not touched.",
                "Remove Custom Game", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            await GameRegistry.Instance.DeleteCustomGameAsync(key);
            _core.Settings.GamePaths.Remove(key);
            _core.Settings.DeployOverrides.Remove(key);
            _core.Settings.CustomGameKeys.Remove(key);
            _core.SaveSettings();

            RebuildCards();
            txtStatus.Text = $"Removed '{config.GameName}'";
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear mod cache for all games?\n\nThis will delete downloaded mods and cached data. Your settings and configurations will be preserved.",
                "Reset / Clear Cache",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                try
                {
                    var modsDir = System.IO.Path.Combine(_core.AppDataPath, "ModsRaw");
                    if (System.IO.Directory.Exists(modsDir))
                        System.IO.Directory.Delete(modsDir, true);

                    txtStatus.Text = "Cache cleared. Restarting...";
                    System.Diagnostics.Process.Start(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing cache: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e) =>
            new SettingsWindow(_core) { Owner = this }.ShowDialog();

        private void BtnAbout_Click(object sender, RoutedEventArgs e) =>
            new AboutWindow(_core) { Owner = this }.ShowDialog();

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            Application.Current.Shutdown();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
