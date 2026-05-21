using System.Windows;
using System.Windows.Input;

namespace TGTAMM
{
    public partial class NotificationWindow : Window
    {
        public NotificationWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            Left = screen.WorkingArea.Right - Width - 16;
            Top = screen.WorkingArea.Bottom - Height - 16;
        }

        private void Window_LocationChanged(object sender, System.EventArgs e)
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen;
            Left = screen.WorkingArea.Right - Width - 16;
            Top = screen.WorkingArea.Bottom - Height - 16;
        }

        private void Toast_Close(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement elem && elem.DataContext is NotificationItem notif)
            {
                NotificationService.Queue.Remove(notif);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is NotificationItem notif)
            {
                NotificationService.Queue.Remove(notif);
            }
            e.Handled = true;
        }
    }
}
