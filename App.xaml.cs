using System.Windows;

namespace TGTAMM
{
    /// <summary>Application bootstrapper. Theme application happens in MainDashboardWindow's ctor.</summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize notification window (toast system)
            var notificationWindow = new NotificationWindow();
            notificationWindow.Show();
        }
    }
}
