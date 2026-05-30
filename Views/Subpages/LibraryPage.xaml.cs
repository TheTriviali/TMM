using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        public event Action<LibraryEntry>? EditGameRequested;

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
            gridScrollViewer.Visibility = Visibility.Collapsed;
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
                default:      RenderGridView(); break;
            }
        }

        // ── Grid / Large view ─────────────────────────────────────────────────────

        private void RenderGridView()
        {
            gridScrollViewer.Visibility = Visibility.Visible;
            cardPanel.Children.Clear();

            double scale = 1.0;

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

        // ── Card factory ──────────────────────────────────────────────────────────

        private GameCard CreateCard(LibraryEntry entry)
        {
            var card = new GameCard { Entry = entry, Core = _core };
            card.CardClicked     += e => CardClicked?.Invoke(e);
            card.PlayRequested   += e => PlayRequested?.Invoke(e);
            card.ManageRequested += e => ManageRequested?.Invoke(e);
            card.ArchiveToggled  += (e, v) => ArchiveToggled?.Invoke(e, v);
            card.DefaultToggled  += (e, v) => DefaultToggled?.Invoke(e, v);
            card.EditRequested   += e => EditGameRequested?.Invoke(e);
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

    }
}
