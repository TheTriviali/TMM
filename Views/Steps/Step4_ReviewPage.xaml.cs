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

        public void LoadProfile(GameConfig profile)
        {
            lblName.Text  = string.IsNullOrEmpty(profile.GameName) ? "(none)" : profile.GameName;
            lblDir.Text   = string.IsNullOrEmpty(profile.GameDirectory) ? "(none)" : profile.GameDirectory;
            lblExe.Text   = string.IsNullOrEmpty(profile.ExePath) ? "(not set)" : profile.ExePath;
            lblSteam.Text = string.IsNullOrEmpty(profile.SteamAppId) ? "(not set)" : profile.SteamAppId;

            lblIntegrity.Text  = DescribeIntegrity(profile);
            lblNexusSlug.Text  = string.IsNullOrEmpty(profile.NexusSlug) ? "(not set)" : profile.NexusSlug;
            lblOverlayFolders.Text = DescribeOverlayFolders(profile);
            lblCompanionSiblings.Text = DescribeCompanionSiblings(profile);
            lblSearchHints.Text = profile.SearchHints.Count == 0
                ? "(not set)"
                : string.Join("; ", profile.SearchHints);
            lblModCategories.Text = profile.ModCategories.Count == 0
                ? $"(default: {string.Join(", ", ModCategories.DefaultCategories)})"
                : string.Join(", ", profile.ModCategories);

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

        public void SaveProfile(GameConfig profile)
        {
            profile.Robustness = rbExperimental.IsChecked == true ? RobustnessLevel.Experimental
                               : rbMature.IsChecked == true       ? RobustnessLevel.Mature
                               : RobustnessLevel.Stable;

            string tag = txtCustomTag.Text.Trim();
            profile.CustomTag  = string.IsNullOrEmpty(tag) ? null : tag;
            profile.IsNative   = chkNative.IsChecked == true;
            profile.ReleaseTag = ReleaseTag.Release;
        }

        private static string DescribeIntegrity(GameConfig profile)
        {
            bool hasSize = profile.ExpectedExeBytes.HasValue;
            int hashCount = profile.AcceptedExeMd5s.Count;
            if (!hasSize && hashCount == 0) return "(not configured)";

            var parts = new System.Collections.Generic.List<string>(2);
            if (hasSize) parts.Add($"{profile.ExpectedExeBytes:N0} bytes");
            if (hashCount > 0) parts.Add($"{hashCount} MD5 hash{(hashCount == 1 ? "" : "es")}");
            return string.Join(" + ", parts);
        }

        private static string DescribeOverlayFolders(GameConfig profile) =>
            profile.OverlayFolders.Count == 0
                ? "(not set)"
                : string.Join(", ", profile.OverlayFolders);

        private static string DescribeCompanionSiblings(GameConfig profile) =>
            profile.CompanionSiblings.Count == 0
                ? "(not set)"
                : string.Join("; ", profile.CompanionSiblings.Select(kvp =>
                    $"{kvp.Key}: {string.Join(", ", kvp.Value)}"));
    }
}
