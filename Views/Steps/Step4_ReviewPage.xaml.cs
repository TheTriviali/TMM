using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class Step4_ReviewPage : UserControl, IWizardStep
    {
        public event EventHandler? ValidationChanged;
        public bool IsValid => true;

        public Step4_ReviewPage()
        {
            InitializeComponent();
        }

        public void LoadProfile(CustomGameProfile profile)
        {
            lblName.Text  = string.IsNullOrEmpty(profile.GameName) ? "(none)" : profile.GameName;
            lblDir.Text   = string.IsNullOrEmpty(profile.GameDirectory) ? "(none)" : profile.GameDirectory;
            lblExe.Text   = string.IsNullOrEmpty(profile.ExePath) ? "(not set)" : profile.ExePath;
            lblSteam.Text = string.IsNullOrEmpty(profile.SteamAppId) ? "(not set)" : profile.SteamAppId;

            var modTypeSummaries = profile.ModTypes
                .Select(mt =>
                {
                    string exts = mt.FileExtensions.Count > 0
                        ? string.Join(", ", mt.FileExtensions)
                        : "no extensions";
                    int ruleCount = mt.RoutingRules.Count;
                    return $"• {mt.Name} ({exts}) — {ruleCount} rule{(ruleCount == 1 ? "" : "s")}";
                })
                .ToList();

            int gameWideCount = profile.RoutingRules.Count;
            if (gameWideCount > 0)
                modTypeSummaries.Add($"• Game-wide — {gameWideCount} rule{(gameWideCount == 1 ? "" : "s")}");

            icModTypeSummary.ItemsSource  = modTypeSummaries;
            lblNoModTypes.Visibility = profile.ModTypes.Count == 0 && gameWideCount == 0
                ? Visibility.Visible : Visibility.Collapsed;

            // Restore robustness
            rbExperimental.IsChecked = profile.Robustness == RobustnessLevel.Experimental;
            rbStable.IsChecked       = profile.Robustness == RobustnessLevel.Stable;
            rbMature.IsChecked       = profile.Robustness == RobustnessLevel.Mature;

            txtCustomTag.Text = profile.CustomTag ?? "";
            chkNative.IsChecked = profile.IsNative;
        }

        public void SaveProfile(CustomGameProfile profile)
        {
            profile.Robustness = rbExperimental.IsChecked == true ? RobustnessLevel.Experimental
                               : rbMature.IsChecked == true       ? RobustnessLevel.Mature
                               : RobustnessLevel.Stable;

            string tag = txtCustomTag.Text.Trim();
            profile.CustomTag  = string.IsNullOrEmpty(tag) ? null : tag;
            profile.IsNative   = chkNative.IsChecked == true;
            profile.ReleaseTag = ReleaseTag.Release;
        }
    }
}
