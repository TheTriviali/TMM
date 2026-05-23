using System.Windows;

namespace TMM
{
    public partial class ExitConfirmationDialog : TmmWindow
    {
        public bool DontAskAgain { get; private set; }

        public ExitConfirmationDialog()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            DontAskAgain = chkDontAskAgain.IsChecked ?? false;
            DialogResult = true;
            Close();
        }
    }
}
