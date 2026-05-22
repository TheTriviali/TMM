using System.Windows;

namespace TMM
{
    public partial class App : Application
    {
        public BackendCore Core { get; private set; } = null!;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            Core = new BackendCore();
            new GameLauncherWindow(Core).Show();
        }
    }
}
