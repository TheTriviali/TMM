using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// ModManagerPage — M1 game-workspace chrome: header wiring, sub-tab switching
    /// (Mods · Conflicts · Backups · Downloads · Config), and the Conflicts / Config
    /// tab bodies. The header carries identity + the three primary verbs; the old
    /// 14-button toolbar collapsed into the header overflow + this tab bar.
    /// </summary>
    public partial class ModManagerPage
    {
        private string _currentTab = "Mods";
        private BackupsPage? _backupsTab;
        private int _conflictCount;

        // ── Header wiring ─────────────────────────────────────────────────────────

        private void WireHeader()
        {
            Cust_Header.BackRequested         += () => BackRequested?.Invoke();
            Cust_Header.InstallRequested      += () => BtnInstallModCustom_Click(null!, null!);
            Cust_Header.DeployRequested       += () => BtnDeployCustom_Click(null!, null!);
            Cust_Header.PlayRequested         += () => BtnLaunchCustom_Click(null!, null!);
            Cust_Header.ReviewDeployRequested += () => BtnDeployCustom_Click(null!, null!);
            Cust_Header.OverflowAction        += HandleOverflow;
        }

        private void UpdateHeader()
        {
            if (_customConfig is null || _entry is null) return;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(_customConfig.Author)) parts.Add(_customConfig.Author!);
            int modCount = _modsCustom.Count;
            if (modCount > 0) parts.Add($"{modCount} {(modCount == 1 ? "mod" : "mods")}");
            if (!string.IsNullOrWhiteSpace(_customConfig.GameDirectory)) parts.Add(_customConfig.GameDirectory!);
            string meta = string.Join("  ·  ", parts);

            bool canPlay = !string.IsNullOrWhiteSpace(_customConfig.SteamAppId)
                        || !string.IsNullOrEmpty(_customConfig.ExePath);

            Cust_Header.Load(_entry, meta, canPlay);
        }

        private void UpdateHeaderPending()
        {
            if (_customProfile is null) return;
            Cust_Header.SetPending(_core.PendingChanges(_customProfile.Key));
        }

        private async void ApplyLoadoutByName(string name)
        {
            try
            {
                await _core.ApplyLoadoutAsync(_customProfile.Key, name, _modsCustom);
                SaveModsCustom();
                _core.Activity.Record(ActivityKind.LoadoutApplied, _customProfile.Key, _customConfig.GameName, $"Applied '{name}'");
                NotificationService.ShowSuccess($"Applied loadout '{name}'", "Loadouts");
                RefreshCustomView();
                _pendingCustom = true;
                UpdateDeployButtonCustom();
                ScheduleConflictAnalysis();
            }
            catch (Exception ex)
            {
                Logger.Error($"Apply loadout '{name}' failed", ex);
                NotificationService.ShowError($"Could not apply loadout '{name}': {ex.Message}", "Loadouts", "TMM-E011");
            }
        }

        private void HandleOverflow(string id)
        {
            switch (id)
            {
                case "config":   ShowTab("Config");                   break;
                case "refresh":  BtnRefreshCustom_Click(null!, null!); break;
                case "import":   BtnImportFromGame_Click(null!, null!); break;
                case "openmods": MenuOpenModsFolder_Click(null, null!); break;
                case "rollback": BtnRollbackCustom_Click(null!, null!); break;
                case "help":     BtnHelp_Click(null!, null!);          break;
                case "about":    BtnAbout_Click(null!, null!);         break;
            }
        }

        // ── Sub-tab switching ───────────────────────────────────────────────────────

        private void Tab_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { Tag: string tab }) ShowTab(tab);
        }

        private void ShowTab(string tab)
        {
            _currentTab = tab;

            Cust_TabBodyMods.Visibility      = tab == "Mods"      ? Visibility.Visible : Visibility.Collapsed;
            Cust_TabBodyConflicts.Visibility = tab == "Conflicts" ? Visibility.Visible : Visibility.Collapsed;
            Cust_BackupsHost.Visibility      = tab == "Backups"   ? Visibility.Visible : Visibility.Collapsed;
            Cust_TabBodyDownloads.Visibility = tab == "Downloads" ? Visibility.Visible : Visibility.Collapsed;
            Cust_TabBodyConfig.Visibility    = tab == "Config"    ? Visibility.Visible : Visibility.Collapsed;

            StyleTab(Cust_TabMods,      Cust_TabModsText,      tab == "Mods");
            StyleTab(Cust_TabConflicts, Cust_TabConflictsText, tab == "Conflicts");
            StyleTab(Cust_TabBackups,   Cust_TabBackupsText,   tab == "Backups");
            StyleTab(Cust_TabDownloads, Cust_TabDownloadsText, tab == "Downloads");
            StyleTab(Cust_TabConfig,    Cust_TabConfigText,    tab == "Config");

            switch (tab)
            {
                case "Conflicts": RenderConflictsTab(); break;
                case "Backups":   _backupsTab?.ScopeToGame(_entry); break;
                case "Downloads": RefreshDownloadsDrawer(); break;
                case "Config":    RenderConfigTab(); break;
            }
        }

        private void StyleTab(Border border, TextBlock text, bool active)
        {
            border.BorderBrush = active
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : Brushes.Transparent;
            text.Foreground = active
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["SubTextBrush"];
            text.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
        }

        private void UpdateConflictsTabBadge(int count)
        {
            _conflictCount = count;
            Cust_TabConflictsBadgeText.Text = count.ToString();
            Cust_TabConflictsBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Conflicts tab ─────────────────────────────────────────────────────────

        private void RenderConflictsTab()
        {
            Cust_ConflictsList.Children.Clear();

            var conflicted = _modsCustom
                .Where(m => m.ConflictSummary is { } s && (s.OverwritesCount > 0 || s.OverwrittenByCount > 0))
                .OrderByDescending(m => m.ConflictSummary!.OverwrittenByCount + m.ConflictSummary!.OverwritesCount)
                .ToList();

            Cust_ConflictsEmpty.Visibility = conflicted.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var mod in conflicted)
                Cust_ConflictsList.Children.Add(BuildConflictCard(mod));
        }

        private UIElement BuildConflictCard(ModItem mod)
        {
            var summary = mod.ConflictSummary!;
            var sub = (Brush)Application.Current.Resources["SubTextBrush"];
            var text = (Brush)Application.Current.Resources["TextBrush"];

            var card = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 12),
                Margin = new Thickness(0, 0, 0, 8),
                Background = (Brush)Application.Current.Resources["PanelBrush"],
            };

            var stack = new StackPanel();

            // Title row: mod name + overwrites/overwritten counts
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(new TextBlock
            {
                Text = mod.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = text,
                VerticalAlignment = VerticalAlignment.Center,
            });
            string counts = string.Format(LocalizationService.Instance["Workspace_Conflicts_Counts"],
                summary.OverwritesCount, summary.OverwrittenByCount);
            titleRow.Children.Add(new TextBlock
            {
                Text = "   " + counts,
                FontSize = 11.5,
                Foreground = sub,
                VerticalAlignment = VerticalAlignment.Center,
            });
            stack.Children.Add(titleRow);

            // Clash detail lines
            foreach (var clash in summary.Clashes)
            {
                var line = new TextBlock
                {
                    FontSize = 11,
                    Margin = new Thickness(8, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = sub,
                };
                line.Inlines.Add(new System.Windows.Documents.Run("└─ "));
                line.Inlines.Add(new System.Windows.Documents.Run(clash.Destination) { Foreground = text });
                line.Inlines.Add(new System.Windows.Documents.Run("  →  " + LocalizationService.Instance["Workspace_Conflicts_Winner"] + " "));
                line.Inlines.Add(new System.Windows.Documents.Run(clash.WinnerModName)
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0xC8, 0x78)),
                    FontWeight = FontWeights.SemiBold,
                });
                stack.Children.Add(line);
            }

            card.Child = stack;
            return card;
        }

        // ── Config tab ──────────────────────────────────────────────────────────────

        private void RenderConfigTab()
        {
            Cust_ConfigSummary.Children.Clear();
            if (_customConfig is null) return;

            Cust_ConfigPathDisplay.Text = string.IsNullOrEmpty(_customConfig.GameDirectory)
                ? "Not set — click Browse to configure"
                : _customConfig.GameDirectory;

            var loc = LocalizationService.Instance;
            AddConfigRow(loc["Workspace_Config_Folder"],
                string.IsNullOrEmpty(_customConfig.GameDirectory) ? "—" : _customConfig.GameDirectory!);
            AddConfigRow(loc["Workspace_Config_Executable"],
                string.IsNullOrEmpty(_customConfig.ExePath) ? _customProfile.ExeName : _customConfig.ExePath!);
            if (!string.IsNullOrWhiteSpace(_customConfig.Author))
                AddConfigRow(loc["Workspace_Config_Author"], _customConfig.Author!);
            if (_customConfig.Version is { } ver)
                AddConfigRow(loc["Workspace_Config_Version"], ver.ToString());
            AddConfigRow(loc["Workspace_Config_ModTypes"], _customConfig.ModTypes.Count.ToString());
            AddConfigRow(loc["Workspace_Config_Categories"],
                string.Join(", ", ModCategories.ForGame(_customConfig)));
        }

        private void AddConfigRow(string label, string value)
        {
            var sub = (Brush)Application.Current.Resources["SubTextBrush"];
            var text = (Brush)Application.Current.Resources["TextBrush"];

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = sub, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(lbl, 0);
            var val = new TextBlock { Text = value, FontSize = 12, Foreground = text, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(val, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(val);
            Cust_ConfigSummary.Children.Add(grid);
        }

        // ── Backups tab ───────────────────────────────────────────────────────────

        private void InitBackupsTab()
        {
            _backupsTab = new BackupsPage(_core);
            Cust_BackupsHost.Content = _backupsTab;
        }
    }
}
