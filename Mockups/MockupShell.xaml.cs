using System.Windows;
using System.Windows.Controls;
using TMM.Mockups.Views;

namespace TMM.Mockups
{
    public partial class MockupShell : Window
    {
        public MockupShell()
        {
            InitializeComponent();
            host.Content = new Mockup1_Workspace();
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (host is null) return; // fires during InitializeComponent before host exists
            var tag = (sender as RadioButton)?.Tag as string;
            host.Content = tag switch
            {
                "list" => new Mockup2_ModList(),
                "home" => new Mockup3_Home(),
                _ => new Mockup1_Workspace(),
            };
        }
    }
}
