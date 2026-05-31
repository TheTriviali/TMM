using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TMM.Services;

namespace TMM
{
    public partial class LibraryPage : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private List<LibraryEntry> _allEntries   = new();
        private List<LibraryEntry> _filteredEntries = new();
        private string  _viewMode     = "home";
        private string  _searchQuery  = "";
        private BackendCore? _core;

        // Drag-reorder state (list view)
        private GameCard? _dragSource;
        private int       _dragSourceIdx = -1;

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set { _showArchived = value; RebuildFilter(); }
        }

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action<LibraryEntry>? CardClicked;
        public event Action<LibraryEntry>? PlayRequested;
        public event Action<LibraryEntry>? ManageRequested;
        public event Action<LibraryEntry, bool>? ArchiveToggled;
        public event Action<LibraryEntry, bool>? ActiveToggled;
        public event Action<List<string>>? OrderChanged;
        public event Action? AddGameRequested;
        public event Action<LibraryEntry>? EditGameRequested;
        public event Action<LibraryEntry>? SetFolderRequested;

        // ── Constructor ───────────────────────────────────────────────────────────

        public LibraryPage()
        {
            InitializeComponent();
        }

        // ── Scroll overflow indicators ────────────────────────────────────────────

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            scrollArrowUp.Visibility   = sv.VerticalOffset > 4            ? Visibility.Visible : Visibility.Collapsed;
            scrollArrowDown.Visibility = sv.VerticalOffset < sv.ScrollableHeight - 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void Initialize(BackendCore core) => _core = core;

        public void LoadEntries(IEnumerable<LibraryEntry> entries)
        {
            _allEntries = entries.ToList();
            UpdateHeaderCount();
            RebuildFilter();
        }

        public void ApplySearchFilter(string query)
        {
            _searchQuery = query;
            RebuildFilter();
        }

        public void SetViewMode(string mode)
        {
            _viewMode = mode;
            RenderCurrentView();
        }

        // ── Filtering ─────────────────────────────────────────────────────────────

        private void RebuildFilter()
        {
            var q = _searchQuery?.Trim().ToLowerInvariant() ?? "";

            _filteredEntries = _allEntries.Where(e =>
            {
                // Archived entries: only shown when ShowArchived=true
                if (e.IsArchived && !_showArchived) return false;
                // Search filter
                if (string.IsNullOrEmpty(q)) return true;
                if (e.IsPlaceholder) return false;
                return e.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || e.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || e.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            // Default game is pinned to the front in both views.
            var defIdx = _filteredEntries.FindIndex(e => e.IsActive && !e.IsPlaceholder);
            if (defIdx > 0)
            {
                var def = _filteredEntries[defIdx];
                _filteredEntries.RemoveAt(defIdx);
                _filteredEntries.Insert(0, def);
            }

            UpdateHeaderCount();
            RenderCurrentView();
        }

        private void UpdateHeaderCount()
        {
            int total    = _allEntries.Count(e => !e.IsPlaceholder && !e.IsArchived);
            int archived = _allEntries.Count(e => !e.IsPlaceholder && e.IsArchived);
            txtGameCount.Text = archived > 0
                ? $"{total} games · {archived} archived"
                : $"{total} game{(total != 1 ? "s" : "")}";

            // Archived toggle pill: only offered when there's something archived to reveal.
            var loc = TMM.Services.LocalizationService.Instance;
            btnArchiveToggle.Visibility = archived > 0 ? Visibility.Visible : Visibility.Collapsed;
            txtArchiveToggle.Text = _showArchived
                ? loc["Library_HideArchived"]
                : $"{loc["Library_ShowArchived"]} ({archived})";
        }

        private void BtnArchiveToggle_Click(object sender, RoutedEventArgs e)
        {
            _showArchived = !_showArchived;
            RebuildFilter(); // re-filters + refreshes the pill label via UpdateHeaderCount
        }

        // ── Rendering ─────────────────────────────────────────────────────────────

        private void RenderCurrentView()
        {
            homeScrollViewer.Visibility = Visibility.Collapsed;
            listScrollViewer.Visibility = Visibility.Collapsed;
            emptyState.Visibility       = Visibility.Collapsed;

            var real = _filteredEntries.Where(e => !e.IsPlaceholder).ToList();
            if (real.Count == 0 && _filteredEntries.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                return;
            }

            switch (_viewMode)
            {
                case "list":  RenderListView(); break;
                default:      RenderHomeView(); break;
            }
        }

        // ── List view ─────────────────────────────────────────────────────────────

        private void RenderListView()
        {
            listScrollViewer.Visibility = Visibility.Visible;
            listPanel.Children.Clear();

            foreach (var entry in _filteredEntries)
            {
                var card = CreateCard(entry);
                card.IsListMode          = true;
                card.Width               = double.NaN;
                card.Height              = 72;
                card.HorizontalAlignment = HorizontalAlignment.Stretch;
                card.Margin              = new Thickness(0, 4, 0, 4);

                // Wire drag-drop via the grip handle
                card.AllowDrop = true;
                var grip = card.GetListDragHandle();
                grip.MouseLeftButtonDown += ListGrip_MouseDown;
                card.DragOver  += ListCard_DragOver;
                card.DragLeave += ListCard_DragLeave;
                card.Drop      += ListCard_Drop;

                listPanel.Children.Add(card);
            }

            listPanel.Children.Add(CreateAddGameCard(1.0, listMode: true));
        }

        private void ListGrip_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not Border grip) return;

            var card = FindParentGameCard(grip);
            if (card == null) return;
            if (card.Entry?.IsActive == true) return; // default is locked to first

            _dragSource    = card;
            _dragSourceIdx = listPanel.Children.IndexOf(card);
            if (_dragSourceIdx < 0) return;

            card.Opacity = 0.7;
            try
            {
                DragDrop.DoDragDrop(card, card.Entry?.Key ?? "", DragDropEffects.Move);
            }
            finally
            {
                card.Opacity = card.Entry?.IsArchived == true ? 0.55 : 1.0;
                _dragSource    = null;
                _dragSourceIdx = -1;
            }
        }

        private void ListCard_DragOver(object sender, DragEventArgs e)
        {
            if (_dragSource == null) return;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            if (sender is GameCard target && target != _dragSource)
            {
                target.cardBorder.BorderBrush = (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.Cyan);
                target.cardBorder.BorderThickness = new Thickness(2);
            }
        }

        private void ListCard_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is GameCard card)
            {
                card.cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                card.cardBorder.BorderThickness = new Thickness(1);
            }
        }

        private void ListCard_Drop(object sender, DragEventArgs e)
        {
            if (_dragSource == null || sender is not GameCard target) return;

            // Reset visual
            target.cardBorder.BorderBrush     = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
            target.cardBorder.BorderThickness  = new Thickness(1);

            string? fromKey = _dragSource.Entry?.Key;
            string? toKey   = target.Entry?.Key;
            if (fromKey == null || toKey == null || fromKey == toKey) return;

            ReorderEntries(fromKey, toKey);
            e.Handled = true;
        }

        public void ListPanel_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        public void ListPanel_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        // ── Reorder logic ─────────────────────────────────────────────────────────

        private void ReorderEntries(string fromKey, string toKey)
        {
            var fromIdx = _allEntries.FindIndex(e => e.Key == fromKey);
            var toIdx   = _allEntries.FindIndex(e => e.Key == toKey);
            if (fromIdx < 0 || toIdx < 0) return;

            var item = _allEntries[fromIdx];
            _allEntries.RemoveAt(fromIdx);
            _allEntries.Insert(toIdx, item);

            // Fire order-changed so shell can persist
            OrderChanged?.Invoke(_allEntries.Select(e => e.Key).ToList());

            // Re-render with new order
            RebuildFilter();
        }

        // ── Home view ───────────────────────────────────────────────────────────────

        private void RenderHomeView()
        {
            homeScrollViewer.Visibility = Visibility.Visible;
            RenderHero();
            RenderStats();
            RenderHomeGames();
            RenderActivity();
        }

        private LibraryEntry? HeroEntry =>
            _filteredEntries.FirstOrDefault(e => e.IsActive && !e.IsPlaceholder)
            ?? _filteredEntries.FirstOrDefault(e => !e.IsPlaceholder);

        private void RenderHero()
        {
            var hero = HeroEntry;
            if (hero is null)
            {
                heroBorder.Visibility = Visibility.Collapsed;
                return;
            }

            heroBorder.Visibility = Visibility.Visible;
            heroBorder.Background  = BuildGradient(hero.GradientStartHex, hero.GradientEndHex);
            heroCover.Background    = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
            heroCoverText.Text      = CoverInitials(hero.DisplayName);
            heroTitle.Text          = hero.DisplayName;

            int mods    = hero.ModCount;
            int enabled = EnabledCount(hero);
            var parts   = new List<string>
            {
                $"{mods} {(mods == 1 ? "mod" : "mods")}",
                $"{enabled} enabled",
            };
            var lastDeploy = LastDeployTime(hero);
            if (lastDeploy is { } dt) parts.Add($"last deployed {TimeAgo(dt)}");
            else if (!hero.IsReady) parts.Add("needs folder");
            heroSubtitle.Text = string.Join(" · ", parts);

            var (hasPending, pendingCount) = PendingFor(hero);
            if (hasPending && pendingCount > 0)
            {
                heroPendingBadge.Visibility = Visibility.Visible;
                heroPendingText.Text = $"{pendingCount} {(pendingCount == 1 ? "change" : "changes")} pending deploy";
            }
            else
            {
                heroPendingBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void RenderStats()
        {
            int gamesSetUp = _allEntries.Count(e => !e.IsPlaceholder && !e.IsArchived);
            statGames.Text = gamesSetUp.ToString();

            int totalMods = _allEntries.Where(e => !e.IsPlaceholder).Sum(e => e.ModCount);
            statMods.Text = totalMods.ToString();

            var loc = LocalizationService.Instance;
            long cachedBytes = _core?.Settings.CachedModsInstalledBytes ?? 0;
            statModsSub.Text = cachedBytes > 0
                ? $"{loc["Home_StatMods"]} · {BackendCore.FormatBytes(cachedBytes)}"
                : loc["Home_StatMods"];

            // Backups size is a disk walk — compute off the render thread, then update.
            statBackups.Text = "…";
            statBackupsSub.Text = loc["Home_StatBackups"];
            if (_core is { } core)
            {
                long budget = core.Settings.BackupSizeWarnBytes;
                _ = Task.Run(() => core.GetTotalBackupSize()).ContinueWith(t =>
                {
                    long used = t.Result;
                    statBackups.Text = BackendCore.FormatBytes(used);
                    statBackupsSub.Text = budget > 0
                        ? $"{loc["Home_StatBackups"]} / {BackendCore.FormatBytes(budget)}"
                        : loc["Home_StatBackups"];
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void RenderHomeGames()
        {
            homeGamesPanel.Children.Clear();
            foreach (var entry in _filteredEntries.Where(e => !e.IsPlaceholder))
            {
                var card = CreateCard(entry);
                card.Width  = 224;
                card.Height = 150;
                card.Margin = new Thickness(8, 0, 8, 16);
                homeGamesPanel.Children.Add(card);
            }
        }

        private void RenderActivity()
        {
            homeActivityPanel.Children.Clear();
            var recent = _core?.Activity.Recent.Take(6).ToList() ?? new List<ActivityEntry>();

            if (recent.Count == 0)
            {
                homeActivityEmpty.Visibility = Visibility.Visible;
                return;
            }
            homeActivityEmpty.Visibility = Visibility.Collapsed;

            for (int i = 0; i < recent.Count; i++)
            {
                if (i > 0) homeActivityPanel.Children.Add(BuildActivitySeparator());
                homeActivityPanel.Children.Add(BuildActivityRow(recent[i]));
            }
        }

        // ── Home helpers ──────────────────────────────────────────────────────────

        private void HeroPlay_Click(object sender, RoutedEventArgs e)
        {
            if (HeroEntry is { } hero) PlayRequested?.Invoke(hero);
        }

        private void HeroManage_Click(object sender, RoutedEventArgs e)
        {
            if (HeroEntry is { } hero) ManageRequested?.Invoke(hero);
        }

        private void BtnHomeAddGame_Click(object sender, RoutedEventArgs e) => AddGameRequested?.Invoke();

        private int EnabledCount(LibraryEntry entry)
        {
            if (_core is null) return 0;
            return entry.GameKeys.Sum(k =>
                _core.Mods.TryGetValue(k, out var list) ? list.Count(m => m.IsEnabled) : 0);
        }

        private (bool HasPending, int Count) PendingFor(LibraryEntry entry)
        {
            if (_core is null) return (false, 0);
            bool has = false;
            int total = 0;
            foreach (var k in entry.GameKeys)
            {
                var p = _core.PendingChanges(k);
                if (p.HasChanges)
                {
                    has = true;
                    total += p.Enabled + p.Disabled + p.Reordered + p.AddedRemoved;
                }
            }
            return (has, total);
        }

        private DateTime? LastDeployTime(LibraryEntry entry)
        {
            if (_core is null) return null;
            DateTime? latest = null;
            foreach (var k in entry.GameKeys)
            {
                var manifest = _core.GetRollbackManifests(k).FirstOrDefault();
                if (manifest is null) continue;
                if (DateTime.TryParseExact(manifest.Timestamp, "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    if (latest is null || dt > latest) latest = dt;
                }
            }
            return latest;
        }

        private static string TimeAgo(DateTime when)
        {
            var span = DateTime.Now - when;
            if (span.TotalMinutes < 1)  return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
            if (span.TotalHours   < 24) return $"{(int)span.TotalHours} hr ago";
            if (span.TotalDays    < 30) return $"{(int)span.TotalDays} day{((int)span.TotalDays == 1 ? "" : "s")} ago";
            return when.ToString("yyyy-MM-dd");
        }

        private static string CoverInitials(string name)
        {
            var words = name.Split(new[] { ' ', '-', '·' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "?";
            // Prefer trailing roman-numeral / number token (e.g. "Grand Theft Auto III" -> "III").
            string last = words[^1];
            if (last.Length <= 4 && last.All(c => "IVXLCDM0123456789".Contains(char.ToUpperInvariant(c))))
                return last.ToUpperInvariant();
            return string.Concat(words.Take(3).Select(w => char.ToUpperInvariant(w[0])));
        }

        private static LinearGradientBrush BuildGradient(string startHex, string endHex)
        {
            Color Parse(string hex, Color fallback)
            {
                try { return (Color)ColorConverter.ConvertFromString(hex); }
                catch { return fallback; }
            }
            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint   = new Point(1, 0.4),
            };
            brush.GradientStops.Add(new GradientStop(Parse(startHex, Color.FromRgb(0x1B, 0x3A, 0x1B)), 0));
            brush.GradientStops.Add(new GradientStop(Parse(endHex, Color.FromRgb(0x0C, 0x1E, 0x0C)), 1));
            return brush;
        }

        private (string Glyph, Brush Color) ActivityVisual(ActivityKind kind) => kind switch
        {
            ActivityKind.Deploy         => ("", new SolidColorBrush(UiColors.ReadyGreen)),
            ActivityKind.Rollback       => ("", new SolidColorBrush(UiColors.NeedsFolderAmber)),
            ActivityKind.Import         => ("", AccentBrush()),
            ActivityKind.ModAdded       => ("", AccentBrush()),
            ActivityKind.ModRemoved     => ("", SubTextBrush()),
            ActivityKind.LoadoutSaved   => ("", AccentBrush()),
            ActivityKind.LoadoutApplied => ("", AccentBrush()),
            _                           => ("", SubTextBrush()),
        };

        private static Brush AccentBrush() =>
            (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.DeepSkyBlue);
        private static Brush SubTextBrush() =>
            (Brush)(Application.Current.Resources["SubTextBrush"] ?? Brushes.Gray);
        private static Brush TextBrush() =>
            (Brush)(Application.Current.Resources["TextBrush"] ?? Brushes.White);

        private UIElement BuildActivityRow(ActivityEntry entry)
        {
            var (glyph, color) = ActivityVisual(entry.Kind);
            var grid = new Grid { Height = 40 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                Foreground = color,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetColumn(icon, 0);

            string detail = string.IsNullOrWhiteSpace(entry.Detail)
                ? $"{entry.Kind} · {entry.GameName}"
                : entry.Detail;
            if (entry.Count > 0 && !detail.Contains(entry.Count.ToString())) detail = $"{detail} ({entry.Count})";

            var text = new TextBlock
            {
                Text = detail,
                FontSize = 12,
                Foreground = TextBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            Grid.SetColumn(text, 1);

            var time = new TextBlock
            {
                Text = TimeAgo(entry.Timestamp),
                FontSize = 11,
                Foreground = SubTextBrush(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
            };
            Grid.SetColumn(time, 2);

            grid.Children.Add(icon);
            grid.Children.Add(text);
            grid.Children.Add(time);
            return grid;
        }

        private static UIElement BuildActivitySeparator() => new Border
        {
            Height = 1,
            Background = (Brush)(Application.Current.Resources["ControlBgBrush"] ?? Brushes.DimGray),
            Opacity = 0.5,
            Margin = new Thickness(38, 0, 14, 0),
        };

        // ── Card factory ──────────────────────────────────────────────────────────

        private GameCard CreateCard(LibraryEntry entry)
        {
            var card = new GameCard { Entry = entry, Core = _core };
            card.CardClicked     += e => CardClicked?.Invoke(e);
            card.PlayRequested   += e => PlayRequested?.Invoke(e);
            card.ManageRequested += e => ManageRequested?.Invoke(e);
            card.ArchiveToggled  += (e, v) => ArchiveToggled?.Invoke(e, v);
            card.ActiveToggled  += (e, v) => ActiveToggled?.Invoke(e, v);
            card.EditRequested      += e => EditGameRequested?.Invoke(e);
            card.SetFolderRequested += e => SetFolderRequested?.Invoke(e);
            return card;
        }

        private UIElement CreateAddGameCard(double scale, bool listMode = false)
        {
            var row = new Border
            {
                Height = 48,
                CornerRadius = new CornerRadius(8),
                BorderBrush = (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.Cyan),
                BorderThickness = new Thickness(1.5),
                Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 3, 0, 3),
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var icon = new TextBlock { Text = "➕", FontSize = 16, VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            var lbl = new TextBlock { Text = "Add Game", FontSize = 12, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            lbl.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            sp.Children.Add(icon);
            sp.Children.Add(lbl);
            row.Child = sp;
            row.MouseLeftButtonUp += (_, _) => AddGameRequested?.Invoke();
            return row;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static GameCard? FindParentGameCard(DependencyObject child)
        {
            var current = VisualTreeHelper.GetParent(child);
            while (current != null)
            {
                if (current is GameCard gc) return gc;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

    }
}
