using System.Windows;
using System.Windows.Input;

namespace TMM
{
    /// <summary>
    /// Base class for all TMM windows. Provides shared chrome handlers:
    /// drag-to-move titlebar, minimize, maximize/restore, and close.
    /// </summary>
    public class TmmWindow : Window
    {
        protected void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        protected void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        protected void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
