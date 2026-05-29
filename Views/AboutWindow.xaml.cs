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
    }
}
