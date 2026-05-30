using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using TMM.Services;

namespace TMM
{
    /// <summary>Flat view model for a single error-catalog entry with resolved locale strings.</summary>
    public class TmmErrorViewModel
    {
        public string Code   { get; init; } = "";
        public string Source { get; init; } = "";
        public string Title  { get; init; } = "";
        public string Cause  { get; init; } = "";
        public string Fix    { get; init; } = "";
    }

    /// <summary>
    /// Renders the TMM error catalog as a searchable, grouped reference page.
    /// Entries are grouped by <see cref="TmmErrorViewModel.Source"/> and filtered
    /// by the search box. <see cref="ScrollToCode"/> is the deep-link target wired
    /// in G1's <c>NotificationService.OnErrorGuideRequested</c>.
    /// </summary>
    public partial class TroubleshootingPage : UserControl
    {
        private List<TmmErrorViewModel> _allEntries = [];
        private readonly ObservableCollection<TmmErrorViewModel> _visible = [];
        private readonly ListCollectionView _view;

        public TroubleshootingPage()
        {
            InitializeComponent();

            _view = new ListCollectionView(_visible);
            _view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TmmErrorViewModel.Source)));
            entryList.ItemsSource = _view;

            RebuildEntries();

            // Re-resolve locale strings when language switches
            LocalizationService.Instance.PropertyChanged += (_, _) => RebuildEntries();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears any active search, scrolls to the entry matching <paramref name="code"/>,
        /// and briefly highlights it. No-op when the code is not in the catalog.
        /// </summary>
        public void ScrollToCode(string code)
        {
            txtSearch.Text = "";           // ensure the target item is visible

            // Wait for layout so ItemContainerGenerator has produced containers
            Dispatcher.InvokeAsync(() =>
            {
                var item = _visible.FirstOrDefault(e => e.Code == code);
                if (item is null) return;

                entryList.ScrollIntoView(item);

                // Highlight via selection state (DataTrigger in ItemContainerStyle)
                entryList.SelectedItem = item;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                timer.Tick += (_, _) => { timer.Stop(); entryList.SelectedItem = null; };
                timer.Start();

            }, DispatcherPriority.Loaded);
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        private void RebuildEntries()
        {
            var loc = LocalizationService.Instance;
            _allEntries = TmmError.All.Values
                .Select(e => new TmmErrorViewModel
                {
                    Code   = e.Code,
                    Source = e.Source,
                    Title  = loc[e.TitleKey],
                    Cause  = loc[e.CauseKey],
                    Fix    = loc[e.FixKey]
                })
                .OrderBy(e => e.Source)
                .ThenBy(e => e.Code)
                .ToList();

            ApplyFilter(txtSearch?.Text ?? "");
        }

        private void ApplyFilter(string raw)
        {
            string q = raw.Trim().ToLowerInvariant();
            _visible.Clear();
            foreach (var e in _allEntries)
            {
                if (string.IsNullOrEmpty(q)
                    || e.Code.ToLowerInvariant().Contains(q)
                    || e.Title.ToLowerInvariant().Contains(q))
                {
                    _visible.Add(e);
                }
            }
            _view.Refresh();
            UpdateEmptyState();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
            => ApplyFilter(txtSearch.Text);

        private void UpdateEmptyState()
        {
            bool hasItems = _visible.Count > 0;
            emptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            mainScroll.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        }
    }
}
