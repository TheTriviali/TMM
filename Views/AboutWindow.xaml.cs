namespace TMM
{
    public partial class AboutWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public AboutWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            txtVersion.Text = AppInfo.DisplayVersion;
        }

        private void BtnFaq_Click(object sender, System.Windows.RoutedEventArgs e) =>
            ShellHelper.OpenUrl("https://github.com/TheTriviali/TMM/blob/master/docs/FAQ.md");

        private void BtnGitHub_Click(object sender, System.Windows.RoutedEventArgs e) =>
            ShellHelper.OpenUrl("https://github.com/TheTriviali/TMM");
    }
}
