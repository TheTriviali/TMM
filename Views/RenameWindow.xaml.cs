using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class RenameWindow : TmmWindow
    {
        public string NewName { get; private set; } = "";

        public RenameWindow(string currentName)
            : this(currentName, "Rename Mod", "New name:")
        {
        }

        public RenameWindow(string currentName, string title, string prompt)
        {
            InitializeComponent();
            Title = title;
            lblTitle.Text = title;
            lblPrompt.Text = prompt;
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

    }
}
