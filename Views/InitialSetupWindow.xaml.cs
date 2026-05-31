using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TMM.Services;

namespace TMM
{
    public partial class InitialSetupWindow : TmmWindow
    {
        private readonly BackendCore _core;
        private string? _selectedGameDir;

        // (gameKey, displayName) pairs loaded from embedded .tmmgame resources
        private List<(string Key, string Name)> _profiles = new();

        /// <summary>True when Option2 (custom game) was chosen; shell should navigate to AddGamePage.</summary>
        public bool OpenAddGameAfterClose { get; private set; }

        public InitialSetupWindow(BackendCore core)
        {
            _core = core;
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

            var langItems = new List<ComboBoxItem>();
            foreach (var code in languages)
                langItems.Add(new ComboBoxItem { Content = svc.GetDisplayName(code), Tag = code });
            cmbLanguage.ItemsSource = langItems;

            string currentLang = _core.Settings.CurrentLanguage ?? "en-US";
            LocalizationService.Instance.SetLanguage(currentLang);
            foreach (ComboBoxItem item in cmbLanguage.Items)
            {
                if (item.Tag as string == currentLang) { cmbLanguage.SelectedItem = item; break; }
            }

            // Load embedded .tmmgame profile names for the picker
            _profiles = LoadEmbeddedProfileNames();
            var profileItems = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = "— Select a game —", Tag = "" }
            };
            foreach (var (key, name) in _profiles.OrderBy(p => p.Name))
                profileItems.Add(new ComboBoxItem { Content = name, Tag = key });
            cmbProfile.ItemsSource = profileItems;
            cmbProfile.SelectedIndex = 0;
        }

        private static List<(string Key, string Name)> LoadEmbeddedProfileNames()
        {
            var result = new List<(string, string)>();
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var resourceName in assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("TMM.Assets.GameProfiles.") && n.EndsWith(".tmmgame")))
            {
                try
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream is null) continue;
                    using var reader = new System.IO.StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("gameName", out var nameEl)) continue;
                    string name = nameEl.GetString() ?? "";
                    string key = doc.RootElement.TryGetProperty("gameKey", out var keyEl)
                        ? (keyEl.GetString() ?? "")
                        : Path.GetFileNameWithoutExtension(resourceName.Replace("TMM.Assets.GameProfiles.", ""));
                    if (!string.IsNullOrEmpty(name)) result.Add((key, name));
                }
                catch { /* skip */ }
            }
            return result;
        }

        private void CmbProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProfile.SelectedItem is not ComboBoxItem item) return;
            bool hasProfile = item.Tag is string key && !string.IsNullOrEmpty(key);
            dirStep.Visibility = hasProfile ? Visibility.Visible : Visibility.Collapsed;
            _selectedGameDir = null;
            txtDirDisplay.Text = "Click Browse to select folder";
            UpdateStartButton();
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            string gameName = cmbProfile.SelectedItem is ComboBoxItem item ? (item.Content?.ToString() ?? "Game") : "Game";
            var dlg = new Microsoft.Win32.OpenFolderDialog { Title = $"Select {gameName} Folder" };
            if (dlg.ShowDialog() != true) return;
            _selectedGameDir = dlg.FolderName;
            txtDirDisplay.Text = _selectedGameDir;
            UpdateStartButton();
        }

        private void UpdateStartButton()
        {
            bool hasProfile = cmbProfile.SelectedItem is ComboBoxItem item && item.Tag is string key && !string.IsNullOrEmpty(key);
            btnStartWithGame.IsEnabled = hasProfile && !string.IsNullOrEmpty(_selectedGameDir);
        }

        private void BtnStartWithGame_Click(object sender, RoutedEventArgs e)
        {
            if (cmbProfile.SelectedItem is not ComboBoxItem item || item.Tag is not string key || string.IsNullOrEmpty(key)) return;
            _core.Settings.PendingFirstRunGameKey = key;
            _core.Settings.PendingFirstRunGameDir = _selectedGameDir;
            CompleteSetup();
        }

        private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
                    if (item.Tag as string == code) { mainWindow.CmbLanguage.SelectedItem = item; break; }
                }
            }
        }

        private void BtnGoToLibrary_Click(object sender, RoutedEventArgs e) => CompleteSetup();

        private void Card_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => e.Handled = true;

        private void Option2_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
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
