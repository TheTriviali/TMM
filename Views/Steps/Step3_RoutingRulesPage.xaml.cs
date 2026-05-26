using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    public partial class Step3_RoutingRulesPage : UserControl, IWizardStep
    {
        public event EventHandler? ValidationChanged;
        public bool IsValid => true; // routing rules are optional

        // Each group: (mod type name, rules list). null name = game-wide rules.
        private readonly List<(string? TypeName, ObservableCollection<RoutingRule> Rules)> _groups = new();

        public Step3_RoutingRulesPage()
        {
            InitializeComponent();
        }

        public void LoadProfile(CustomGameProfile profile)
        {
            _groups.Clear();

            foreach (var mt in profile.ModTypes)
            {
                var rules = new ObservableCollection<RoutingRule>(mt.RoutingRules);
                _groups.Add((mt.Name, rules));
            }

            // Game-wide rules group
            var gameWide = new ObservableCollection<RoutingRule>(profile.RoutingRules);
            _groups.Add((null, gameWide));

            RebuildGroupPanels();
        }

        public void SaveProfile(CustomGameProfile profile)
        {
            // Sync game-wide rules
            var gameWideGroup = _groups.LastOrDefault(g => g.TypeName is null);
            profile.RoutingRules.Clear();
            if (gameWideGroup.Rules is not null)
                foreach (var r in gameWideGroup.Rules)
                    profile.RoutingRules.Add(r);

            // Sync mod type rules
            for (int i = 0; i < profile.ModTypes.Count; i++)
            {
                var mt = profile.ModTypes[i];
                var grp = _groups.FirstOrDefault(g => g.TypeName == mt.Name);
                mt.RoutingRules.Clear();
                if (grp.Rules is not null)
                    foreach (var r in grp.Rules)
                        mt.RoutingRules.Add(r);
            }

            CheckConflicts();
        }

        private void RebuildGroupPanels()
        {
            spRuleGroups.Children.Clear();
            icConflicts.ItemsSource = null;

            foreach (var (typeName, rules) in _groups)
            {
                string header = typeName ?? "Game-wide Rules";
                spRuleGroups.Children.Add(BuildGroupPanel(header, rules, typeName));
            }

            CheckConflicts();
        }

        private UIElement BuildGroupPanel(string groupName, ObservableCollection<RoutingRule> rules, string? typeName)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            // Group header
            var headerBorder = new Border
            {
                Background      = FindRes("PanelBrush"),
                BorderBrush     = FindRes("ControlBgBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(10, 8, 10, 8),
                Margin          = new Thickness(0, 0, 0, 4),
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerLabel = new TextBlock
            {
                Text       = groupName,
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FindRes("TextBrush"),
            };
            Grid.SetColumn(headerLabel, 0);

            var addBtn = new Button
            {
                Content         = "+ Add Rule",
                Tag             = (typeName, rules),
                Background      = FindRes("AccentSoftBrush"),
                Foreground      = FindRes("AccentBrush"),
                BorderBrush     = FindRes("AccentBrush"),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 3, 8, 3),
                FontSize        = 11,
                Cursor          = System.Windows.Input.Cursors.Hand,
            };
            addBtn.Click += BtnAddRule_Click;
            Grid.SetColumn(addBtn, 1);

            headerGrid.Children.Add(headerLabel);
            headerGrid.Children.Add(addBtn);
            headerBorder.Child = headerGrid;
            panel.Children.Add(headerBorder);

            // Rules
            var rulesHost = new StackPanel { Tag = rules };
            foreach (var rule in rules)
                rulesHost.Children.Add(BuildRuleCard(rule, rules, rulesHost));

            rules.CollectionChanged += (_, _) =>
            {
                rulesHost.Children.Clear();
                foreach (var r in rules)
                    rulesHost.Children.Add(BuildRuleCard(r, rules, rulesHost));
                CheckConflicts();
            };

            panel.Children.Add(rulesHost);

            // Empty state for group
            var emptyHint = new TextBlock
            {
                Text       = "No rules yet. Click + Add Rule to define routing for this type.",
                FontSize   = 11,
                Foreground = FindRes("SubTextBrush"),
                Opacity    = 0.6,
                Margin     = new Thickness(16, 0, 0, 8),
                Visibility = rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed,
            };
            rules.CollectionChanged += (_, _) =>
                emptyHint.Visibility = rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            panel.Children.Add(emptyHint);

            return panel;
        }

        private UIElement BuildRuleCard(RoutingRule rule,
                                        ObservableCollection<RoutingRule> parentCollection,
                                        StackPanel host)
        {
            var border = new Border
            {
                Background      = FindRes("PanelBrush"),
                BorderBrush     = FindRes("ControlBgBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Margin          = new Thickness(16, 0, 0, 4),
                Padding         = new Thickness(10, 8, 10, 8),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            var nameLbl = new TextBlock
            {
                Text       = string.IsNullOrEmpty(rule.Name) ? "(unnamed rule)" : rule.Name,
                FontSize   = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = FindRes("TextBrush"),
            };
            var condSummary = new TextBlock
            {
                Text       = BuildConditionSummary(rule) + $"  →  {(string.IsNullOrEmpty(rule.TargetPath) ? "(no target)" : rule.TargetPath)}",
                FontSize   = 10,
                Foreground = FindRes("SubTextBrush"),
                Opacity    = 0.8,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 2, 0, 0),
            };
            var priorityLbl = new TextBlock
            {
                Text       = $"Priority: {rule.Priority}  |  Bias: {rule.LoadOrderBias}",
                FontSize   = 10,
                Foreground = FindRes("SubTextBrush"),
                Opacity    = 0.6,
                Margin     = new Thickness(0, 2, 0, 0),
            };
            info.Children.Add(nameLbl);
            info.Children.Add(condSummary);
            info.Children.Add(priorityLbl);
            Grid.SetColumn(info, 0);

            var editBtn = new Button
            {
                Content         = "Edit",
                Tag             = rule,
                Background      = Brushes.Transparent,
                Foreground      = FindRes("AccentBrush"),
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(4),
                FontSize        = 11,
                Cursor          = System.Windows.Input.Cursors.Hand,
            };
            editBtn.Click += (s, e) =>
            {
                var dlg = new RuleEditorWindow(rule) { Owner = Window.GetWindow(this) };
                if (dlg.ShowDialog() != true || dlg.Result is null) return;

                // Replace the rule in the collection
                int idx = parentCollection.IndexOf(rule);
                if (idx >= 0) parentCollection[idx] = dlg.Result;
            };
            Grid.SetColumn(editBtn, 1);

            var delBtn = new Button
            {
                Content         = "Delete",
                Tag             = rule,
                Background      = Brushes.Transparent,
                Foreground      = new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x70)),
                BorderThickness = new Thickness(0),
                Padding         = new Thickness(4),
                FontSize        = 11,
                Cursor          = System.Windows.Input.Cursors.Hand,
            };
            delBtn.Click += (s, e) =>
            {
                if (parentCollection.Contains(rule))
                    parentCollection.Remove(rule);
            };
            Grid.SetColumn(delBtn, 3);

            grid.Children.Add(info);
            grid.Children.Add(editBtn);
            grid.Children.Add(delBtn);
            border.Child = grid;
            return border;
        }

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ValueTuple<string?, ObservableCollection<RoutingRule>> tag)
                return;

            var dlg = new RuleEditorWindow { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || dlg.Result is null) return;

            tag.Item2.Add(dlg.Result);
        }

        private void CheckConflicts()
        {
            var warnings = new List<string>();
            var allRules = _groups.SelectMany(g => g.Rules).ToList();

            // Simple conflict: two rules with same extension, same priority, AllowConflict=false
            for (int i = 0; i < allRules.Count; i++)
            {
                for (int j = i + 1; j < allRules.Count; j++)
                {
                    var a = allRules[i];
                    var b = allRules[j];

                    if (a.Priority == b.Priority &&
                        ExtOf(a) == ExtOf(b) &&
                        !string.IsNullOrEmpty(ExtOf(a)) &&
                        !a.AllowConflict && !b.AllowConflict)
                    {
                        warnings.Add($"Rules \"{a.Name}\" and \"{b.Name}\" share priority {a.Priority} " +
                                     $"and both have conflict prompts disabled.");
                    }
                }
            }

            icConflicts.ItemsSource = warnings;
        }

        private static string ExtOf(RoutingRule r)
        {
            var c = r.Conditions.FirstOrDefault(x => x.Type == ConditionType.FileExtension);
            return c?.Value ?? "*";
        }

        private static string BuildConditionSummary(RoutingRule rule)
        {
            if (rule.Conditions.Count == 0) return "(no conditions — catch-all)";
            return string.Join(" ", rule.Conditions.Select(c =>
                $"{c.Type} {c.Operator} \"{c.Value}\" {c.Logic}").ToArray())
                .TrimEnd(" AND".ToCharArray())
                .TrimEnd(" OR".ToCharArray())
                .Trim();
        }

        private Brush FindRes(string key)
        {
            if (Application.Current.Resources[key] is Brush b) return b;
            return Brushes.Gray;
        }
    }
}
