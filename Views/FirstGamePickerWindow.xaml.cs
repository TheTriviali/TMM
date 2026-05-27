using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class FirstGamePickerWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public FirstGamePickerWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private new void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2) return;
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Option1_Click(object sender, MouseButtonEventArgs e)
        {
            // Open built-in game picker
            var picker = new SelectBuiltinGameWindow(_core) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                DialogResult = true;
                Close();
            }
        }

        private void Option2_Click(object sender, MouseButtonEventArgs e)
        {
            // Open custom game setup wizard
            var wizard = new CustomGameSetupWizard() { Owner = this };
            if (wizard.ShowDialog() == true)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
