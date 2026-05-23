using System;
using System.Windows;

namespace TMM
{
    public partial class App : Application
    {
        public BackendCore Core { get; private set; } = null!;

        private void OnStartup(object sender, StartupEventArgs e)
        {
            DispatcherUnhandledException += (_, ex) =>
            {
                ShowCrashDialog(ex.Exception);
                ex.Handled = false;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            {
                if (ex.ExceptionObject is Exception e) ShowCrashDialog(e);
            };

            TaskScheduler.UnobservedTaskException += (_, ex) =>
            {
                ShowCrashDialog(ex.Exception);
                ex.SetObserved();
            };

            Core = new BackendCore();
            new GameLauncherWindow(Core).Show();
        }

        private static void ShowCrashDialog(Exception ex)
        {
            string msg = $"TMM ran into a problem and needs to close.\n\n" +
                         $"Error: {ex.Message}\n\n" +
                         $"If this keeps happening, please report it on GitHub.";
            MessageBox.Show(msg, "TMM — Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
