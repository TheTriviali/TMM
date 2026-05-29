using System.Linq;
using System.Windows;

namespace TMM
{
    public partial class ActivityFeedWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public ActivityFeedWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
            Refresh();
        }

        public sealed class Row
        {
            public string TimeDisplay { get; set; } = "";
            public string KindDisplay { get; set; } = "";
            public string GameName { get; set; } = "";
            public string Detail { get; set; } = "";
        }

        private void Refresh()
        {
            lvFeed.ItemsSource = _core.Activity.Recent.Select(a => new Row
            {
                TimeDisplay = a.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                KindDisplay = a.Kind.ToString(),
                GameName = a.GameName,
                Detail = a.Count > 0 ? $"{a.Detail} ({a.Count})" : a.Detail,
            }).ToList();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Clear all activity history? This cannot be undone.",
                "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            _core.Activity.Clear();
            Refresh();
        }
    }
}
