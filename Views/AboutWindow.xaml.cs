using System.Reflection;

namespace TMM
{
    public partial class AboutWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public AboutWindow(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            txtVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build} (Beta)" : "Beta";
        }
    }
}
