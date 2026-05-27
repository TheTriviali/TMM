using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    public partial class InitialSetupWindow : TmmWindow
    {
        private readonly BackendCore _core;
        private bool _suppressDropdownEvent;

        public InitialSetupWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            txtBrandVersion.Text = ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "";

            var svc = LocalizationService.Instance;
            var languages = svc.GetAvailableLanguages();

            // Populate dropdown with display names
            var items = new List<ComboBoxItem>();
            foreach (var code in languages)
            {
                items.Add(new ComboBoxItem
                {
                    Content = svc.GetDisplayName(code),
                    Tag = code
                });
            }
            cmbLanguage.ItemsSource = items;

            // Select current language
            ApplyLanguage(_core.Settings.CurrentLanguage, updateDropdown: true);
        }

        private void BtnQuickLang_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string code)
                ApplyLanguage(code, updateDropdown: true);
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressDropdownEvent) return;
            if (cmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string code)
                ApplyLanguage(code, updateDropdown: false);
        }

        private void ApplyLanguage(string code, bool updateDropdown)
        {
            LocalizationService.Instance.SetLanguage(code);
            _core.Settings.CurrentLanguage = code;
            _core.SaveSettings();

            // Highlight active quick-pick button
            UpdateQuickPickState(code);

            // Sync dropdown without re-firing the event
            if (updateDropdown)
            {
                _suppressDropdownEvent = true;
                foreach (ComboBoxItem item in cmbLanguage.Items)
                {
                    if (item.Tag as string == code)
                    {
                        cmbLanguage.SelectedItem = item;
                        break;
                    }
                }
                _suppressDropdownEvent = false;
            }
        }

        private void UpdateQuickPickState(string activeCode)
        {
            HighlightQuickBtn(btnLangEn, activeCode == "en-US");
            HighlightQuickBtn(btnLangEs, activeCode == "es-MX");
        }

        private static void HighlightQuickBtn(Button btn, bool active)
        {
            btn.ApplyTemplate();
            if (btn.Template.FindName("bd", btn) is Border bd)
            {
                bd.BorderBrush = active
                    ? (Brush)Application.Current.FindResource("AccentBrush")
                    : (Brush)Application.Current.FindResource("SubTextBrush");
                bd.Background = active
                    ? (Brush)Application.Current.FindResource("AccentSoftBrush")
                    : (Brush)Application.Current.FindResource("PanelBrush");
            }
        }

        private void BtnSetupGame_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FirstGamePickerWindow(_core) { Owner = this };
            if (picker.ShowDialog() == true)
            {
                _core.Settings.FirstLaunch = false;
                _core.SaveSettings();
                DialogResult = true;
                Close();
            }
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
