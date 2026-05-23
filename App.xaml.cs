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
            string report = $"Error: {ex.Message}\n\nType: {ex.GetType().FullName}\n\nStack Trace:\n{ex.StackTrace}";
            try { System.Windows.Clipboard.SetText(report); } catch { }
            MessageBox.Show(ex.Message + "\n\n(Details copied to clipboard)",
                "TMM — Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
