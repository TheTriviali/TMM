using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace TMM
{
    public partial class NotificationsPage : UserControl
    {
        private readonly ListCollectionView _view;
        private NotificationType? _levelFilter;

        public NotificationsPage()
        {
            InitializeComponent();
            _view = new ListCollectionView(NotificationService.History);
            _view.Filter = obj => obj is NotificationItem n && MatchesFilter(n);
            notifList.ItemsSource = _view;

            ((INotifyCollectionChanged)_view).CollectionChanged += (_, _) => UpdateEmptyState();
            UpdateEmptyState();
        }

        private bool MatchesFilter(NotificationItem n)
            => _levelFilter is null || n.Type == _levelFilter;

        private void UpdateEmptyState()
        {
            bool hasItems = _view != null && _view.Count > 0;
            emptyState.Visibility        = hasItems ? Visibility.Collapsed : Visibility.Visible;
            notifScrollViewer.Visibility = hasItems ? Visibility.Visible   : Visibility.Collapsed;
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbFilter.SelectedItem is not ComboBoxItem item) return;
            _levelFilter = (item.Tag as string) switch
            {
                "Info"    => NotificationType.Info,
                "Success" => NotificationType.Success,
                "Warning" => NotificationType.Warning,
                "Error"   => NotificationType.Error,
                _         => (NotificationType?)null
            };
            if (_view == null) return;

            _view.Refresh();
            UpdateEmptyState();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            NotificationService.ClearHistory();
            UpdateEmptyState();
        }
    }
}
