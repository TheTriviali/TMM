using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace TMM
{
    public partial class LibraryPage : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private List<LibraryEntry> _allEntries   = new();
        private List<LibraryEntry> _filteredEntries = new();
        private string  _viewMode     = "grid";
        private string  _searchQuery  = "";
        private string? _showcaseHeroKey;
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
        public event Action<LibraryEntry, bool>? DefaultToggled;
        public event Action<List<string>>? OrderChanged;
        public event Action? AddGameRequested;

        // ── Constructor ───────────────────────────────────────────────────────────

        public LibraryPage()
        {
            InitializeComponent();
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
        }

        // ── Rendering ─────────────────────────────────────────────────────────────

        private void RenderCurrentView()
        {
            gridScrollViewer.Visibility    = Visibility.Collapsed;
            listScrollViewer.Visibility    = Visibility.Collapsed;
            showcaseScrollViewer.Visibility = Visibility.Collapsed;
            emptyState.Visibility          = Visibility.Collapsed;

            var real = _filteredEntries.Where(e => !e.IsPlaceholder).ToList();
            if (real.Count == 0 && _filteredEntries.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                return;
            }

            switch (_viewMode)
            {
                case "grid":
                case "large": RenderGridView();    break;
                case "list":  RenderListView();    break;
                case "showcase": RenderShowcaseView(); break;
                default:      RenderGridView();    break;
            }
        }

        // ── Grid / Large view ─────────────────────────────────────────────────────

        private void RenderGridView()
        {
            gridScrollViewer.Visibility = Visibility.Visible;
            cardPanel.Children.Clear();

            double scale = _viewMode == "large" ? 1.25 : 1.0;

            foreach (var entry in _filteredEntries)
            {
                var card = CreateCard(entry);
                card.Width  = 240 * scale;
                card.Height = 160 * scale;
                card.Margin = new Thickness(6);
                // Grid view: enable drag-drop reorder
                card.AllowDrop = true;
                card.MouseMove += GridCard_MouseMove;
                card.DragOver  += GridCard_DragOver;
                card.DragLeave += GridCard_DragLeave;
                card.Drop      += GridCard_Drop;
                cardPanel.Children.Add(card);
            }

            cardPanel.Children.Add(CreateAddGameCard(scale));
        }

        private GameCard? _gridDragSource;
        private int       _gridDragSourceIdx = -1;

        private void GridCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            if (sender is not GameCard card) return;
            if (_gridDragSource != null) return; // already dragging

            _gridDragSource    = card;
            _gridDragSourceIdx = cardPanel.Children.IndexOf(card);
            if (_gridDragSourceIdx < 0) return;

            card.Opacity = 0.4;
            try
            {
                DragDrop.DoDragDrop(card, card.Entry?.Key ?? "", DragDropEffects.Move);
            }
            finally
            {
                card.Opacity = card.Entry?.IsArchived == true ? 0.55 : 1.0;
                _gridDragSource    = null;
                _gridDragSourceIdx = -1;
            }
        }

        private void GridCard_DragOver(object sender, DragEventArgs e)
        {
            if (_gridDragSource == null) return;
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            if (sender is GameCard target && target != _gridDragSource)
            {
                target.cardBorder.BorderBrush = (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.Cyan);
                target.cardBorder.BorderThickness = new Thickness(2);
            }
        }

        private void GridCard_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is GameCard card)
            {
                card.cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
                card.cardBorder.BorderThickness = new Thickness(1.5);
            }
        }

        private void GridCard_Drop(object sender, DragEventArgs e)
        {
            if (_gridDragSource == null || sender is not GameCard target) return;
            if (target == _gridDragSource) return;

            // Reset visual
            target.Opacity = target.Entry?.IsArchived == true ? 0.55 : 1.0;
            target.cardBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
            target.cardBorder.BorderThickness = new Thickness(1.5);

            // Reorder _allEntries
            string? fromKey = _gridDragSource.Entry?.Key;
            string? toKey   = target.Entry?.Key;
            if (fromKey == null || toKey == null) return;

            ReorderEntries(fromKey, toKey);
            e.Handled = true;
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

            _dragSource    = card;
            _dragSourceIdx = listPanel.Children.IndexOf(card);
            if (_dragSourceIdx < 0) return;

            card.Opacity = 0.35;
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

        // ── Showcase view ─────────────────────────────────────────────────────────

        private void RenderShowcaseView()
        {
            showcaseScrollViewer.Visibility = Visibility.Visible;
            carouselPanel.Children.Clear();
            ResetCarousel();

            var real = _filteredEntries.Where(e => !e.IsPlaceholder).ToList();
            if (real.Count == 0) { emptyState.Visibility = Visibility.Visible; return; }

            // Hero: the game the user last clicked in carousel, or default, or first
            var hero = real.FirstOrDefault(e => e.Key == _showcaseHeroKey)
                    ?? real.FirstOrDefault(e => e.IsDefault)
                    ?? real[0];

            // Fill hero panel
            ApplyShowcaseHero(hero);

            // Carousel: all other games as portrait cards
            var others = real.Where(e => e.Key != hero.Key).ToList();
            foreach (var entry in others)
            {
                var portrait = CreatePortraitCard(entry);
                carouselPanel.Children.Add(portrait);
            }

            carouselLabel.Visibility = others.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyShowcaseHero(LibraryEntry hero)
        {
            // Gradient
            if (TryParseHex(hero.GradientStartHex, out var c0)) heroGradStart.Color = c0;
            if (TryParseHex(hero.GradientEndHex,   out var c1)) heroGradEnd.Color   = c1;

            heroCoverTitle.Text  = hero.DisplayName;
            heroMetaName.Text    = hero.DisplayName;
            heroMetaSub.Text     = hero.Subtitle;
            heroStatMods.Text    = hero.ModCount.ToString();
            heroStatReady.Text   = hero.IsReady ? "Yes" : "No";
            heroStatReady.Foreground = new SolidColorBrush(
                hero.IsReady ? UiColors.ReadyGreen : Color.FromRgb(200, 80, 80));
            heroReadyDot.Fill = new SolidColorBrush(
                hero.IsReady ? UiColors.ReadyGreen : Color.FromRgb(160, 60, 60));
            heroReadyText.Text = hero.IsReady
                ? "Ready to deploy"
                : "Configure paths before deploying";

            // Tags
            heroTagsPanel.Children.Clear();
            if (hero.IsArchived) AddHeroTag("Archived", "#30FFFFFF", "#80FFFFFF");
            if (!string.IsNullOrEmpty(hero.Category)) AddHeroTag(hero.Category, "#14FFFFFF", "#80FFFFFF");
            var (tagBg, tagFg) = hero.Status switch
            {
                ReleaseStatus.Beta     => ("#2EB48C14", "#F0D273"),
                ReleaseStatus.Alpha    => ("#2EC86414", "#F0B27A"),
                ReleaseStatus.PreAlpha => ("#2EC8371E", "#FF9988"),
                ReleaseStatus.Testing  => ("#2E5050B4", "#A3A3EE"),
                _ => ("", "")
            };
            if (!string.IsNullOrEmpty(tagBg))
                AddHeroTag(hero.Status.ToString().ToUpperInvariant(), tagBg, tagFg);

            // Store hero key for carousel clicks
            _heroEntry = hero;
        }

        private LibraryEntry? _heroEntry;

        // ── Carousel ──────────────────────────────────────────────────────────────

        private double _carouselOffset = 0;
        private const double CardStep = 150; // card width (140) + margin (10)

        private void ResetCarousel() { _carouselOffset = 0; carouselTranslate.X = 0; }

        private void BtnCarouselPrev_Click(object sender, RoutedEventArgs e)
        {
            int count = carouselPanel.Children.Count;
            if (count == 0) return;

            double panelW   = count * CardStep;
            double viewportW = carouselViewport.ActualWidth;
            double minOffset = Math.Min(0, viewportW - panelW); // negative when panel wider than viewport

            if (_carouselOffset >= 0)
            {
                // Already at start — wrap to end
                _carouselOffset = minOffset;
            }
            else
            {
                _carouselOffset = Math.Min(_carouselOffset + CardStep, 0);
            }
            AnimateCarousel(_carouselOffset);
        }

        private void BtnCarouselNext_Click(object sender, RoutedEventArgs e)
        {
            int count = carouselPanel.Children.Count;
            if (count == 0) return;

            double panelW    = count * CardStep;
            double viewportW = carouselViewport.ActualWidth;
            double minOffset = Math.Min(0, viewportW - panelW);

            if (_carouselOffset <= minOffset)
            {
                // Already at end — wrap to start
                _carouselOffset = 0;
            }
            else
            {
                _carouselOffset = Math.Max(_carouselOffset - CardStep, minOffset);
            }
            AnimateCarousel(_carouselOffset);
        }

        private void AnimateCarousel(double targetX)
        {
            var anim = new DoubleAnimation(targetX, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            carouselTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, anim);
        }

        private void AddHeroTag(string text, string bg, string fg)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin  = new Thickness(0, 0, 6, 0),
                BorderThickness = new Thickness(1),
            };
            try { border.Background   = (Brush)new BrushConverter().ConvertFrom(bg)!; } catch { border.Background = Brushes.Transparent; }
            try { border.BorderBrush  = (Brush)new BrushConverter().ConvertFrom(bg.Replace("2E", "80"))!; } catch { border.BorderBrush = Brushes.Transparent; }

            var tb = new TextBlock
            {
                Text       = text,
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
            };
            try { tb.Foreground = (Brush)new BrushConverter().ConvertFrom(fg)!; } catch { tb.Foreground = Brushes.White; }
            border.Child = tb;
            heroTagsPanel.Children.Add(border);
        }

        private Border CreatePortraitCard(LibraryEntry entry)
        {
            var outer = new Border
            {
                Width  = 140, Height = 186,
                CornerRadius    = new CornerRadius(10),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1.5),
                ClipToBounds    = true,
                Margin          = new Thickness(5, 0, 5, 0),
                Cursor          = Cursors.Hand,
            };
            outer.Effect = new DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black };
            if (entry.Key == _showcaseHeroKey)
                outer.BorderBrush = (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.Cyan);

            // Gradient
            var gradBrush = new LinearGradientBrush(new GradientStopCollection
            {
                new GradientStop(ParseHexColor(entry.GradientStartHex, Color.FromRgb(20,20,30)), 0),
                new GradientStop(ParseHexColor(entry.GradientEndHex,   Color.FromRgb(10,10,15)), 1),
            }, new Point(0, 0), new Point(1, 1));

            var grid = new Grid();
            grid.Children.Add(new Border { Background = gradBrush });

            // Title (bottom-center)
            var title = new TextBlock
            {
                Text             = entry.DisplayName,
                FontSize         = 13, FontWeight = FontWeights.Bold,
                Foreground       = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                TextWrapping     = TextWrapping.Wrap,
                TextAlignment    = TextAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(8, 0, 8, 28),
            };
            title.Effect = new DropShadowEffect { BlurRadius = 6, ShadowDepth = 1, Opacity = 0.6, Color = Colors.Black };
            grid.Children.Add(title);

            // Ready dot (bottom-left above info strip)
            var dot = new Ellipse
            {
                Width = 7, Height = 7,
                Fill  = new SolidColorBrush(entry.IsReady ? UiColors.ReadyGreen : Color.FromRgb(160, 60, 60)),
                VerticalAlignment   = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(10, 0, 0, 26),
            };
            grid.Children.Add(dot);

            // Info strip
            var infoStrip = new Border
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 22,
                CornerRadius = new CornerRadius(0, 0, 10, 10),
                Background = new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0)),
                Padding = new Thickness(8, 0, 8, 0),
            };
            var sub = new TextBlock
            {
                Text = entry.Subtitle,
                FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            infoStrip.Child = sub;
            grid.Children.Add(infoStrip);

            outer.Child = grid;
            outer.MouseLeftButtonUp += (_, _) =>
            {
                _showcaseHeroKey = entry.Key;
                RenderShowcaseView();
            };

            // Archived dim
            if (entry.IsArchived) outer.Opacity = 0.55;

            return outer;
        }

        private void BtnHeroLaunch_Click(object sender, RoutedEventArgs e)
        {
            if (_heroEntry != null) PlayRequested?.Invoke(_heroEntry);
        }

        private void BtnHeroManage_Click(object sender, RoutedEventArgs e)
        {
            if (_heroEntry != null) ManageRequested?.Invoke(_heroEntry);
        }

        // ── Card factory ──────────────────────────────────────────────────────────

        private GameCard CreateCard(LibraryEntry entry)
        {
            var card = new GameCard { Entry = entry, Core = _core };
            card.CardClicked     += e => CardClicked?.Invoke(e);
            card.PlayRequested   += e => PlayRequested?.Invoke(e);
            card.ManageRequested += e => ManageRequested?.Invoke(e);
            card.ArchiveToggled  += (e, v) => ArchiveToggled?.Invoke(e, v);
            card.DefaultToggled  += (e, v) => DefaultToggled?.Invoke(e, v);
            return card;
        }

        private UIElement CreateAddGameCard(double scale, bool listMode = false)
        {
            if (listMode)
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
            else
            {
                double w = 240 * scale, h = 160 * scale;
                var border = new Border
                {
                    Width = w, Height = h,
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromArgb(15, 255, 255, 255)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(6),
                };
                border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                var icon = new TextBlock { Text = "➕", FontSize = 22 * scale, HorizontalAlignment = HorizontalAlignment.Center };
                icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                var lbl = new TextBlock { Text = "Add Game", FontSize = 11 * scale, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
                lbl.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                sp.Children.Add(icon);
                sp.Children.Add(lbl);
                border.Child = sp;
                border.MouseLeftButtonUp += (_, _) => AddGameRequested?.Invoke();
                return border;
            }
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

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrEmpty(hex)) return false;
            try { color = (Color)ColorConverter.ConvertFromString(hex); return true; }
            catch { return false; }
        }

        private static Color ParseHexColor(string hex, Color fallback)
            => TryParseHex(hex, out var c) ? c : fallback;
    }
}
