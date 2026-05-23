using System.Diagnostics;
using System.Windows;

namespace TMM
{
    public partial class HelpWindow : TmmWindow
    {
        public HelpWindow() => InitializeComponent();

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string url)
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

    }
}
