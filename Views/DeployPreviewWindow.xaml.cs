using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class DeployPreviewWindow : TmmWindow
    {
        public bool ShouldDeploy { get; private set; }

        public DeployPreviewWindow()
        {
            InitializeComponent();
        }

        public void SetDeploymentSummary(int totalFiles, List<DeploymentGroup> groups)
        {
            txtSummary.Text = $"Deploying {totalFiles} file(s) from your mods:";
            icDeployGroups.ItemsSource = new ObservableCollection<DeploymentGroup>(groups);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ShouldDeploy = false;
            DialogResult = true;
            Close();
        }

        private void BtnDeploy_Click(object sender, RoutedEventArgs e)
        {
            ShouldDeploy = true;
            DialogResult = true;
            Close();
        }
    }

    public class DeploymentGroup
    {
        public int FileCount { get; set; }
        public string Destination { get; set; } = "";
        public string Extensions { get; set; } = "";
    }
}
