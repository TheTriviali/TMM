using System.Diagnostics;
using System.Windows;

namespace TGTAMM
{
    public partial class HelpWindow : Window
    {
        public HelpWindow() => InitializeComponent();

        private void BtnLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is string url)
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
