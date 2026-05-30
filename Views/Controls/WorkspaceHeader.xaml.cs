using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// Game-workspace header (M1): cover, title, readiness badge, loadout switcher,
    /// pending-changes pill/banner, and the three primary verbs (Deploy / Play / overflow)
    /// plus a "← Library" affordance. Pure UI — it raises events that the hosting
    /// <see cref="ModManagerPage"/> maps onto its existing action handlers.
    /// </summary>
    public partial class WorkspaceHeader : UserControl
    {
        /// <summary>← Library clicked.</summary>
        public event Action? BackRequested;
        /// <summary>Deploy verb clicked.</summary>
        public event Action? DeployRequested;
        /// <summary>Play verb clicked.</summary>
        public event Action? PlayRequested;
        /// <summary>"Review &amp; Deploy" in the pending banner clicked.</summary>
        public event Action? ReviewDeployRequested;
        /// <summary>A loadout was picked from the switcher (user-initiated).</summary>
        public event Action<string>? LoadoutApplied;
        /// <summary>An overflow-menu item was chosen; argument is its Tag id.</summary>
        public event Action<string>? OverflowAction;

        private bool _suppressLoadoutEvent;

        public WorkspaceHeader()
        {
            InitializeComponent();
        }

        // ── Population ────────────────────────────────────────────────────────────

        /// <summary>
        /// Fills the header for <paramref name="entry"/>. <paramref name="canPlay"/> hides
        /// the Play verb when the game has no launchable executable.
        /// </summary>
        public void Load(LibraryEntry entry, string meta, bool canPlay,
                         IEnumerable<string> loadouts, string? currentLoadout)
        {
            txtTitle.Text = entry.DisplayName;
            txtMeta.Text  = meta;
            coverText.Text = CoverInitials(entry.DisplayName);
            coverTile.Background = BuildGradient(entry.GradientStartHex, entry.GradientEndHex);

            SetReadiness(entry.IsReady);

            actionsPanel.Visibility = Visibility.Visible;
            btnPlay.Visibility = canPlay ? Visibility.Visible : Visibility.Collapsed;

            LoadLoadouts(loadouts, currentLoadout);
        }

        /// <summary>Placeholder ("coming soon") games: identity only, no verbs.</summary>
        public void LoadPlaceholder(LibraryEntry entry)
        {
            txtTitle.Text = entry.DisplayName;
            txtMeta.Text  = "";
            coverText.Text = CoverInitials(entry.DisplayName);
            coverTile.Background = BuildGradient(entry.GradientStartHex, entry.GradientEndHex);
            SetReadiness(false);
            actionsPanel.Visibility = Visibility.Collapsed;
            pendingBanner.Visibility = Visibility.Collapsed;
        }

        public void LoadLoadouts(IEnumerable<string> loadouts, string? currentLoadout)
        {
            _suppressLoadoutEvent = true;
            var loc = LocalizationService.Instance;
            var items = new List<string> { loc["Workspace_LoadoutNone"] };
            items.AddRange(loadouts.OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            cmbLoadout.ItemsSource = items;
            cmbLoadout.SelectedItem = currentLoadout is not null && items.Contains(currentLoadout)
                ? currentLoadout
                : items[0];
            _suppressLoadoutEvent = false;
        }

        /// <summary>Reflects deploy readiness on the accent Deploy button.</summary>
        public void SetDeployEnabled(bool enabled) => btnDeploy.IsEnabled = enabled;

        /// <summary>Shows/hides the amber pending-changes banner with a per-bucket breakdown.</summary>
        public void SetPending(PendingChangesSummary p)
        {
            if (!p.HasChanges)
            {
                pendingBanner.Visibility = Visibility.Collapsed;
                return;
            }

            int total = p.Enabled + p.Disabled + p.Reordered + p.AddedRemoved;
            var loc = LocalizationService.Instance;
            var parts = new List<string>();
            if (p.Enabled > 0)      parts.Add($"{p.Enabled} {loc["Workspace_Pending_Enabled"]}");
            if (p.Disabled > 0)     parts.Add($"{p.Disabled} {loc["Workspace_Pending_Disabled"]}");
            if (p.Reordered > 0)    parts.Add($"{p.Reordered} {loc["Workspace_Pending_Reordered"]}");
            if (p.AddedRemoved > 0) parts.Add($"{p.AddedRemoved} {loc["Workspace_Pending_AddedRemoved"]}");

            string head = string.Format(
                loc[total == 1 ? "Workspace_Pending_One" : "Workspace_Pending_Many"], total);
            pendingText.Text = parts.Count > 0 ? $"{head} — {string.Join(", ", parts)}." : head;
            pendingBanner.Visibility = Visibility.Visible;
        }

        private void SetReadiness(bool ready)
        {
            var loc = LocalizationService.Instance;
            var color = new SolidColorBrush(ready ? UiColors.ReadyGreen : UiColors.NeedsFolderAmber);
            readyDot.Fill = color;
            readyText.Foreground = color;
            readyText.Text = ready ? loc["Card_Ready"] : loc["Card_NeedsFolder"];
        }

        // ── Event handlers ──────────────────────────────────────────────────────────

        private void BtnBack_Click(object sender, RoutedEventArgs e)   => BackRequested?.Invoke();
        private void BtnDeploy_Click(object sender, RoutedEventArgs e) => DeployRequested?.Invoke();
        private void BtnPlay_Click(object sender, RoutedEventArgs e)   => PlayRequested?.Invoke();
        private void BtnReviewDeploy_Click(object sender, RoutedEventArgs e) => ReviewDeployRequested?.Invoke();

        private void BtnOverflow_Click(object sender, RoutedEventArgs e)
        {
            if (overflowMenu is null) return;
            overflowMenu.PlacementTarget = btnOverflow;
            overflowMenu.IsOpen = true;
        }

        private void Overflow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: string id })
                OverflowAction?.Invoke(id);
        }

        private void CmbLoadout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLoadoutEvent) return;
            if (cmbLoadout.SelectedIndex <= 0) return; // index 0 = "(none)"
            if (cmbLoadout.SelectedItem is string name)
                LoadoutApplied?.Invoke(name);
        }

        // ── Helpers (mirror LibraryPage) ────────────────────────────────────────────

        private static string CoverInitials(string name)
        {
            var words = name.Split(new[] { ' ', '-', '·' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "?";
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
                EndPoint   = new Point(1, 1),
            };
            brush.GradientStops.Add(new GradientStop(Parse(startHex, Color.FromRgb(0x1B, 0x3A, 0x1B)), 0));
            brush.GradientStops.Add(new GradientStop(Parse(endHex, Color.FromRgb(0x0C, 0x1E, 0x0C)), 1));
            return brush;
        }
    }
}
