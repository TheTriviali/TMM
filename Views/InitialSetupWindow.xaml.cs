using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using TMM.Services;

namespace TMM
{
    public partial class InitialSetupWindow : TmmWindow
    {
        private readonly BackendCore _core;
        private bool _suppressDropdownEvent;

        /// <summary>True when Option2 (custom game) was chosen; shell should navigate to AddGamePage.</summary>
        public bool OpenAddGameAfterClose { get; private set; }

        public InitialSetupWindow(BackendCore core)
        {
            _core = core;
            // Set language BEFORE InitializeComponent so XAML bindings evaluate with correct translations
            string defaultLang = core.Settings.CurrentLanguage ?? "en-US";
            LocalizationService.Instance.SetLanguage(defaultLang);
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            txtBrandVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "";

            var svc = LocalizationService.Instance;
            var languages = svc.GetAvailableLanguages();

            var items = new List<ComboBoxItem>();
            foreach (var code in languages)
                items.Add(new ComboBoxItem { Content = svc.GetDisplayName(code), Tag = code });
            cmbLanguage.ItemsSource = items;

            string currentLang = _core.Settings.CurrentLanguage ?? "en-US";
            LocalizationService.Instance.SetLanguage(currentLang);

            foreach (ComboBoxItem item in cmbLanguage.Items)
            {
                if (item.Tag as string == currentLang)
                {
                    cmbLanguage.SelectedItem = item;
                    break;
                }
            }
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDropdownEvent) return;
            if (cmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string code)
                ApplyLanguage(code);
        }

        private void ApplyLanguage(string code)
        {
            LocalizationService.Instance.SetLanguage(code);
            _core.Settings.CurrentLanguage = code;
            _core.SaveSettings();

            if (Owner is UnifiedShellWindow mainWindow)
            {
                foreach (ComboBoxItem item in mainWindow.CmbLanguage.Items)
                {
                    if (item.Tag as string == code)
                    {
                        mainWindow.CmbLanguage.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        private void BtnGoToLibrary_Click(object sender, RoutedEventArgs e)
            => CompleteSetup();

        /// <summary>
        /// Marks the press on an option card as handled so it doesn't bubble up to
        /// <c>RootBorder.TitleBar_MouseDown</c>, whose <c>DragMove()</c> would otherwise
        /// capture the mouse and swallow the card's <c>MouseLeftButtonUp</c> click.
        /// </summary>
        private void Card_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => e.Handled = true;

        private void Option1_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            CompleteSetup();
        }

        private void Option2_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Navigate to the AddGamePage in the main shell after closing initial setup
            OpenAddGameAfterClose = true;
            CompleteSetup();
        }

        private void CompleteSetup()
        {
            _core.Settings.FirstLaunch = false;
            _core.SaveSettings();
            DialogResult = true;
            Close();
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
