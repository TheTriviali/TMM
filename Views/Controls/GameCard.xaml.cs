using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TMM.Services;

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
                // Status chip is card-mode-only; list mode has an inline pill
                if (Entry != null) ApplyStatusChip(Entry.Status);
                // List mode: flat border, no hover scale
                cardBorder.CornerRadius = value ? new CornerRadius(8) : new CornerRadius(10);
                cardBorder.BorderThickness = value ? new Thickness(1) : new Thickness(1.5);
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
            // Suppress if a Button (covers Default, Overflow, Play, Manage, etc.)
            var src = e.OriginalSource as DependencyObject;
            while (src != null && src != this)
            {
                if (src is Button) return;
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
            txtArtTitle.Text = entry.DisplayName;
            txtSubtitle.Text = entry.Subtitle;
            txtModCount.Text = entry.ModCount > 0 ? $"{entry.ModCount} mods" : "";

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
            listArchivedTag.Visibility = entry.IsArchived ? Visibility.Visible : Visibility.Collapsed;

            // ── Default pill (card + list modes) ──
            ApplyDefaultState(entry.IsDefault);

            // ── Overflow menu items ──
            bool isCustom = entry.GameKeys.Length == 1
                && GameProfile.ByKey(entry.GameKeys[0]) == null
                && !entry.IsPlaceholder;
            ApplyOverflowMenu(entry, isCustom);

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
            var accentBrush = Application.Current.Resources["AccentBrush"] as Brush;
            var dimBg       = new SolidColorBrush(Color.FromArgb(25, 255, 255, 255));
            var dimBorder   = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            var noBorder    = Brushes.Transparent;

            UpdateDefaultPill(
                btnDefaultCard,
                "defaultCardPillBg", "defaultCardStar",
                isDefault, accentBrush, dimBg, dimBorder, noBorder);

            UpdateDefaultPill(
                listBtnDefault,
                "defaultListPillBg", "defaultListStar",
                isDefault, accentBrush, dimBg, dimBorder, noBorder);
        }

        private static void UpdateDefaultPill(
            Button? btn,
            string bgName, string starName,
            bool isDefault,
            Brush? accentBrush, Brush dimBg, Brush dimBorder, Brush noBorder)
        {
            if (btn?.Template == null) return;

            var bg = btn.Template.FindName(bgName, btn) as Border;
            if (bg != null)
            {
                bg.Background  = isDefault ? accentBrush ?? Brushes.Cyan : dimBg;
                bg.BorderBrush = isDefault ? noBorder : dimBorder;
            }

            var star = btn.Template.FindName(starName, btn) as TextBlock;
            if (star != null) star.Text = isDefault ? "★" : "☆";
        }

        private void ApplyOverflowMenu(LibraryEntry entry, bool isCustom)
        {
            if (menuEdit  != null) menuEdit.Visibility  = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (menuExport != null) menuExport.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (menuSepCustom != null) menuSepCustom.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            if (menuArchive != null)
            {
                menuArchive.Header = entry.IsArchived    ? LocalizationService.Instance["Card_Unarchive"]
                                   : entry.IsPlaceholder ? LocalizationService.Instance["Card_Remove"]
                                   :                       LocalizationService.Instance["Card_Archive"];
            }
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

            statusChip.Visibility = _isListMode ? Visibility.Collapsed : Visibility.Visible;
            statusChip.Background = bgBrush;
            txtStatus.Text        = label;

            listStatusPill.Visibility = _isListMode ? Visibility.Visible : Visibility.Collapsed;
            listStatusPill.Background = bgBrush;
            listTxtStatus.Text        = label;
        }

        private void AnimateHover(bool entering)
        {
            if (_isListMode) return;

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

        private void BtnDefault_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) DefaultToggled?.Invoke(Entry, !Entry.IsDefault);
        }

        private void BtnOverflow_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (ContextMenu == null) return;
            ContextMenu.PlacementTarget = sender as UIElement ?? this;
            ContextMenu.Placement = PlacementMode.Bottom;
            ContextMenu.IsOpen = true;
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) EditRequested?.Invoke(Entry);
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry == null) return;

            var key = Entry.GameKeys.Length == 1 ? Entry.GameKeys[0] : null;
            if (key is null) return;

            var profile = GameRegistry.Instance.GetCustomGameConfig(key);
            if (profile is null) return;

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title    = $"Export {Entry.DisplayName}",
                Filter   = "TMM Game Profile (*.tmmgame)|*.tmmgame",
                FileName = SanitizeFileName(Entry.DisplayName),
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await GameRegistry.ExportConfigAsync(profile, dlg.FileName);
                NotificationService.ShowSuccess("Profile exported");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Export failed: {ex.Message}");
            }
        }

        private void BtnArchiveOrDelete_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry == null) return;
            if (Entry.IsArchived)         ArchiveToggled?.Invoke(Entry, false);
            else if (Entry.IsPlaceholder) DeleteRequested?.Invoke(Entry);
            else                          ArchiveToggled?.Invoke(Entry, true);
        }

        // ── Public helpers ────────────────────────────────────────────────────────

        /// <summary>Returns the drag handle border for list-mode drag-drop wiring.</summary>
        public Border GetListDragHandle() => listDragHandle;

        // ── Context menu (artwork) ────────────────────────────────────────────────

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

        // ── Static helpers ────────────────────────────────────────────────────────

        private static string SanitizeFileName(string name)
        {
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrEmpty(hex)) return false;
            try { color = (Color)ColorConverter.ConvertFromString(hex); return true; }
            catch { return false; }
        }
    }
}
