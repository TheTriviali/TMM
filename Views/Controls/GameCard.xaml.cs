using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace TMM
{
    public partial class GameCard : UserControl
    {
        // ── Dependency properties ─────────────────────────────────────────────────

        public static readonly DependencyProperty EntryProperty =
            DependencyProperty.Register(nameof(Entry), typeof(LibraryEntry), typeof(GameCard),
                new PropertyMetadata(null, OnEntryChanged));

        public LibraryEntry? Entry
        {
            get => (LibraryEntry?)GetValue(EntryProperty);
            set => SetValue(EntryProperty, value);
        }

        // ── State ─────────────────────────────────────────────────────────────────

        public BackendCore? Core { get; set; }

        private bool _isListMode;
        public bool IsListMode
        {
            get => _isListMode;
            set
            {
                _isListMode = value;
                cardModeRoot.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                listModeRoot.Visibility = value ? Visibility.Visible   : Visibility.Collapsed;
                // Hide card-mode overlays (default checkbox + status chip) in list mode
                // — list mode has its own inline versions of both
                defaultCheckbox.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                // statusChip visibility is managed by ApplyStatusChip; re-apply after mode switch
                if (Entry != null) ApplyStatusChip(Entry.Status);
                // List mode: flat border, no hover scale
                cardBorder.CornerRadius = value ? new CornerRadius(8) : new CornerRadius(10);
                cardBorder.BorderThickness = value ? new Thickness(1) : new Thickness(1.5);
                // Disable drop shadow in list mode
                if (cardBorder.Effect is DropShadowEffect dse)
                    dse.Opacity = value ? 0.15 : 0.35;
            }
        }

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action<LibraryEntry>? CardClicked;
        public event Action<LibraryEntry>? PlayRequested;
        public event Action<LibraryEntry>? ManageRequested;
        public event Action<LibraryEntry, bool>? ArchiveToggled;
        public event Action<LibraryEntry>? DeleteRequested;
        public event Action<LibraryEntry>? EditRequested;
        public event Action<LibraryEntry, bool>? DefaultToggled;

        // ── Constructor ───────────────────────────────────────────────────────────

        public GameCard()
        {
            InitializeComponent();
            MouseLeftButtonUp += OnCardBodyClick;
            MouseEnter += (_, _) => AnimateHover(true);
            MouseLeave += (_, _) => AnimateHover(false);
        }

        private void OnCardBodyClick(object sender, MouseButtonEventArgs e)
        {
            // Don't fire if a button or the default checkbox was clicked
            var src = e.OriginalSource as DependencyObject;
            while (src != null && src != this)
            {
                if (src is Button) return;
                if (src == defaultCheckbox || src == listDefaultCheckbox) return;
                src = VisualTreeHelper.GetParent(src);
            }
            if (Entry != null) CardClicked?.Invoke(Entry);
        }

        // ── Data binding ──────────────────────────────────────────────────────────

        private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card && e.NewValue is LibraryEntry entry)
                card.ApplyEntry(entry);
        }

        public void ApplyEntry(LibraryEntry entry)
        {
            // ── Card mode ──
            txtArtTitle.Text  = entry.DisplayName.ToUpperInvariant();
            txtSubtitle.Text  = entry.Subtitle;
            txtModCount.Text  = entry.ModCount > 0 ? $"{entry.ModCount} mods" : "";

            // ── List mode ──
            listTxtTitle.Text    = entry.DisplayName;
            listTxtSubtitle.Text = entry.Subtitle;
            listTxtModCount.Text = entry.ModCount > 0 ? $"{entry.ModCount} mods" : "";

            // ── Gradient ──
            if (TryParseHex(entry.GradientStartHex, out var c0)) gradStart.Color = c0;
            if (TryParseHex(entry.GradientEndHex,   out var c1)) gradEnd.Color   = c1;

            // ── Status chip ──
            ApplyStatusChip(entry.Status);

            // ── Ready dot (both modes) ──
            var dotColor = new SolidColorBrush(entry.IsReady
                ? UiColors.ReadyGreen
                : Color.FromRgb(160, 60, 60));
            readyDot.Fill     = dotColor;
            listReadyDot.Fill = dotColor;

            // ── Opacity / archived ──
            double baseOpacity = entry.IsPlaceholder ? 0.72 : entry.IsArchived ? 0.55 : 1.0;
            Opacity = baseOpacity;
            archivedOverlay.Visibility = entry.IsArchived ? Visibility.Visible : Visibility.Collapsed;
            // List-mode archived badge (inline with subtitle)
            listArchivedTag.Visibility = entry.IsArchived ? Visibility.Visible : Visibility.Collapsed;

            // ── Default checkbox (card + list modes) ──
            ApplyDefaultState(entry.IsDefault);

            // ── Archive / delete button (card + list) ──
            ApplyArchiveButton(entry);

            // ── Edit / Export buttons (custom games only) ──
            bool isCustom = entry.GameKeys.Length == 1
                && GameProfile.ByKey(entry.GameKeys[0]) == null
                && !entry.IsPlaceholder;
            btnEdit.Visibility   = isCustom ? Visibility.Visible : Visibility.Collapsed;
            btnExport.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            // ── Custom artwork ──
            string? artPath = Core?.GetLibraryArtPath(entry.Key);
            if (artPath != null)
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource   = new Uri(artPath);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    gradientBg.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                    noiseOverlay.Visibility = Visibility.Collapsed;
                }
                catch { /* fall through to gradient */ }
            }
        }

        private void ApplyDefaultState(bool isDefault)
        {
            if (isDefault && Application.Current.Resources["AccentBrush"] is Brush accent)
            {
                defaultCheckbox.Background     = accent;
                defaultCheckbox.BorderBrush    = accent;
                listDefaultCheckbox.Background  = accent;
                listDefaultCheckbox.BorderBrush = accent;
            }
            else
            {
                var transp = Brushes.Transparent;
                var dim    = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255));
                defaultCheckbox.Background     = transp;
                defaultCheckbox.BorderBrush    = dim;
                listDefaultCheckbox.Background  = transp;
                listDefaultCheckbox.BorderBrush = dim;
            }
            defaultCheckmark.Visibility     = isDefault ? Visibility.Visible : Visibility.Collapsed;
            listDefaultCheckmark.Visibility = isDefault ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyArchiveButton(LibraryEntry entry)
        {
            string icon;
            string tip;
            if (entry.IsArchived)
            {
                icon = "↩"; tip = "Unarchive game";
            }
            else if (entry.IsPlaceholder)
            {
                icon = "✕"; tip = "Remove placeholder";
            }
            else
            {
                icon = "⊟"; tip = "Archive game";
            }

            // Card mode button
            var cardArchiveIcon = btnArchiveOrDelete.Template?.FindName("archiveIcon", btnArchiveOrDelete) as TextBlock;
            if (cardArchiveIcon != null) cardArchiveIcon.Text = icon;
            btnArchiveOrDelete.ToolTip = tip;

            // List mode button
            var listAIcon = listBtnArchiveOrDelete.Template?.FindName("listArchiveIcon", listBtnArchiveOrDelete) as TextBlock;
            if (listAIcon != null) listAIcon.Text = icon;
            listBtnArchiveOrDelete.ToolTip = tip;
        }

        private void ApplyStatusChip(ReleaseStatus status)
        {
            if (status == ReleaseStatus.Release)
            {
                statusChip.Visibility     = Visibility.Collapsed;
                listStatusPill.Visibility = Visibility.Collapsed;
                return;
            }

            (Color chipColor, string label) = status switch
            {
                ReleaseStatus.Beta     => (Color.FromRgb(180, 140,  20), "BETA"),
                ReleaseStatus.Alpha    => (Color.FromRgb(200, 100,  20), "ALPHA"),
                ReleaseStatus.PreAlpha => (Color.FromRgb(200,  55,  30), "PRE-ALPHA"),
                ReleaseStatus.Testing  => (Color.FromRgb( 80,  80, 180), "TESTING"),
                _                      => (Colors.Gray, status.ToString().ToUpperInvariant()),
            };
            var bgBrush = new SolidColorBrush(Color.FromArgb(217, chipColor.R, chipColor.G, chipColor.B));

            // Card-mode overlay (top-right) — hidden when in list mode
            statusChip.Visibility = _isListMode ? Visibility.Collapsed : Visibility.Visible;
            statusChip.Background = bgBrush;
            txtStatus.Text        = label;

            // List-mode inline pill
            listStatusPill.Visibility = _isListMode ? Visibility.Visible : Visibility.Collapsed;
            listStatusPill.Background = bgBrush;
            listTxtStatus.Text        = label;
        }

        private void AnimateHover(bool entering)
        {
            if (_isListMode) return; // no scale in list mode

            var overlayAnim = new DoubleAnimation(entering ? 0.08 : 0, TimeSpan.FromMilliseconds(120));
            hoverOverlay.Background = new SolidColorBrush(Colors.White);
            hoverOverlay.BeginAnimation(OpacityProperty, overlayAnim);

            var scaleAnim = new DoubleAnimation(entering ? 1.03 : 1.0,
                new Duration(TimeSpan.FromMilliseconds(120)));
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = new ScaleTransform(1, 1);
            }
            var st = (ScaleTransform)RenderTransform;
            st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        // ── Button handlers ───────────────────────────────────────────────────────

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) PlayRequested?.Invoke(Entry);
        }

        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) ManageRequested?.Invoke(Entry);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) EditRequested?.Invoke(Entry);
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            // TODO: export dialog
        }

        private void BtnArchiveOrDelete_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry == null) return;
            if (Entry.IsArchived)      ArchiveToggled?.Invoke(Entry, false);
            else if (Entry.IsPlaceholder) DeleteRequested?.Invoke(Entry);
            else                       ArchiveToggled?.Invoke(Entry, true);
        }

        private void DefaultCheckbox_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) DefaultToggled?.Invoke(Entry, !Entry.IsDefault);
        }

        // ── Public helpers ────────────────────────────────────────────────────────

        /// <summary>Returns the drag handle border for use in list-mode drag-drop.</summary>
        public Border GetListDragHandle() => listDragHandle;

        // ── Context menu ──────────────────────────────────────────────────────────

        private void MenuSetArt_Click(object sender, RoutedEventArgs e)
        {
            if (Entry == null || Core == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = $"Select artwork for {Entry.DisplayName}",
                Filter = "PNG Image (*.png)|*.png",
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Core.SaveLibraryArt(Entry.Key, dlg.FileName);
                ApplyEntry(Entry);
                NotificationService.ShowSuccess("Artwork updated");
            }
            catch (ArgumentException ex) { NotificationService.ShowWarning(ex.Message); }
        }

        private void MenuRemoveArt_Click(object sender, RoutedEventArgs e)
        {
            if (Entry == null || Core == null) return;
            Core.DeleteLibraryArt(Entry.Key);
            ApplyEntry(Entry);
            NotificationService.ShowSuccess("Artwork removed — using gradient");
        }

        // ── Static helper ─────────────────────────────────────────────────────────

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrEmpty(hex)) return false;
            try { color = (Color)ColorConverter.ConvertFromString(hex); return true; }
            catch { return false; }
        }
    }
}
