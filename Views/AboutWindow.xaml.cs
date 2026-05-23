using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class AboutWindow : Window
    {
        private readonly BackendCore _core;

        public AboutWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            txtVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build} (Beta)" : "Beta";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void BtnGitHub_Click(object sender, RoutedEventArgs e)
            => Open("https://github.com/triviali/tgtamm");

        private void BtnBugReport_Click(object sender, RoutedEventArgs e)
            => Open("https://github.com/triviali/tgtamm/issues");

        private static void Open(string url)
            => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
