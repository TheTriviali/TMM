using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace TGTAMM
{
    public partial class ArchiveExtractionWindow : Window
    {
        private readonly BackendCore _core;
        public List<string> SelectedTargets { get; } = new();

        public ArchiveExtractionWindow(BackendCore core, string archivePath, string stagingPath, string hintKey)
        {
            _core = core;
            InitializeComponent();

            chkIII.IsEnabled = _core.IsGameReady(GameProfile.III);
            chkVC.IsEnabled = _core.IsGameReady(GameProfile.VC);
            chkSA.IsEnabled = _core.IsGameReady(GameProfile.SA);

            txtArchiveName.Text = Path.GetFileName(archivePath);

            // Auto-check the suggested target.
            if (hintKey == "ALL")
            {
                chkAll.IsChecked = true;
            }
            else if (hintKey == "III" && chkIII.IsEnabled) chkIII.IsChecked = true;
            else if (hintKey == "VC" && chkVC.IsEnabled) chkVC.IsChecked = true;
            else if (hintKey == "SA" && chkSA.IsEnabled) chkSA.IsChecked = true;

            // If a specific game was hinted, lock the others out.
            if (!string.IsNullOrEmpty(hintKey) && hintKey != "ALL")
            {
                chkIII.IsEnabled = hintKey == "III" && chkIII.IsEnabled;
                chkVC.IsEnabled = hintKey == "VC" && chkVC.IsEnabled;
                chkSA.IsEnabled = hintKey == "SA" && chkSA.IsEnabled;
                chkAll.IsEnabled = false;
            }
        }

        private void ChkAll_Changed(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkAll.IsChecked == true;
            chkIII.IsChecked = chkVC.IsChecked = chkSA.IsChecked = isChecked;
        }

        private void BtnYesToAll_Click(object sender, RoutedEventArgs e)
        {
            if (chkIII.IsEnabled) chkIII.IsChecked = true;
            if (chkVC.IsEnabled)  chkVC.IsChecked  = true;
            if (chkSA.IsEnabled)  chkSA.IsChecked  = true;
            BtnInstall_Click(sender, e);
        }

        private void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (chkIII.IsChecked == true) SelectedTargets.Add(GameProfile.III.Key);
            if (chkVC.IsChecked == true) SelectedTargets.Add(GameProfile.VC.Key);
            if (chkSA.IsChecked == true) SelectedTargets.Add(GameProfile.SA.Key);

            if (SelectedTargets.Count == 0)
            {
                MessageBox.Show("Please select at least one game.");
                return;
            }

            DialogResult = true;
            Close();
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
