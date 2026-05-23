using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class RenameWindow : Window
    {
        public string NewName { get; private set; } = "";

        public RenameWindow(string currentName)
        {
            InitializeComponent();
            txtName.Text = currentName;
            txtName.SelectAll();
            txtName.Focus();
        }

        private void TxtName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)  { NewName = txtName.Text; DialogResult = true;  Close(); }
            if (e.Key == Key.Escape) { DialogResult = false; Close(); }
        }

        private void BtnSave_Click(object s, RoutedEventArgs e)
        {
            NewName = txtName.Text; DialogResult = true; Close();
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e)
        {
            DialogResult = false; Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
