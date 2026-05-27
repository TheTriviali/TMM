using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    public partial class BackupsPage : UserControl
    {
        private readonly BackendCore _core;

        public BackupsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
        }

        /// <summary>Populate the game selector. Call after library entries are built.</summary>
        public void Initialize(IEnumerable<LibraryEntry> entries)
        {
            var games = entries.Where(e => !e.IsPlaceholder).ToList();
            cmbGame.ItemsSource = games;
            cmbGame.DisplayMemberPath = "DisplayName";

            if (games.Count > 0)
            {
                var def = games.FirstOrDefault(e => e.IsDefault) ?? games[0];
                cmbGame.SelectedItem = def;
            }
        }

        private void CmbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGame.SelectedItem is LibraryEntry entry)
                LoadBackups(entry.Key, entry.DisplayName);
        }

        private void LoadBackups(string gameKey, string displayName)
        {
            backupRowsPanel.Children.Clear();

            var manifests = _core.GetRollbackManifests(gameKey);
            if (manifests.Count == 0)
            {
                backupRowsPanel.Children.Add(BuildEmptyState());
                return;
            }

            foreach (var manifest in manifests)
                backupRowsPanel.Children.Add(BuildBackupRow(manifest, displayName));
        }

        private UIElement BuildEmptyState()
        {
            var loc = LocalizationService.Instance;
            var panel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 80, 0, 0),
                Opacity = 0.4
            };

            var icon = new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

            var title = new TextBlock
            {
                Text = loc["Backups_Empty_Title"],
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 4)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

            var subtitle = new TextBlock
            {
                Text = loc["Backups_Empty_Subtitle"],
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            subtitle.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

            panel.Children.Add(icon);
            panel.Children.Add(title);
            panel.Children.Add(subtitle);
            return panel;
        }

        private UIElement BuildBackupRow(DeployManifest manifest, string displayName)
        {
            string timeStr = FormatTimestamp(manifest.Timestamp);

            var container = new Border
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16),
                CornerRadius = new CornerRadius(8)
            };
            container.SetResourceReference(Border.BackgroundProperty, "PanelBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var ts = new TextBlock
            {
                Text = timeStr,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            ts.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetRow(ts, 0); Grid.SetColumn(ts, 0);

            int count = manifest.ModNames.Count;
            string snippet = string.Join(", ", manifest.ModNames.Take(3));
            if (count > 3) snippet += $" (+{count - 3} more)";
            var modLabel = new TextBlock
            {
                Text = string.Format(LocalizationService.Instance["Backups_ModsList"], snippet),
                FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            modLabel.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
            Grid.SetRow(modLabel, 1); Grid.SetColumn(modLabel, 0);

            var btnRestore = new Button
            {
                Content = LocalizationService.Instance["Button_Restore"],
                Height = 28,
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnRestore.SetResourceReference(Button.BackgroundProperty, "ControlBgBrush");
            btnRestore.SetResourceReference(Button.ForegroundProperty, "TextBrush");
            btnRestore.BorderThickness = new Thickness(0);
            var captured = manifest;
            btnRestore.Click += async (_, _) => await RunRestoreAsync(captured, displayName, timeStr);
            Grid.SetRow(btnRestore, 0); Grid.SetColumn(btnRestore, 1); Grid.SetRowSpan(btnRestore, 2);

            grid.Children.Add(ts);
            grid.Children.Add(modLabel);
            grid.Children.Add(btnRestore);
            container.Child = grid;
            return container;
        }

        private async Task RunRestoreAsync(DeployManifest manifest, string displayName, string timeStr)
        {
            string msg = string.Format(LocalizationService.Instance["Backups_ConfirmRestore"], displayName, timeStr);
            if (MessageBox.Show(msg, "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning)
                != MessageBoxResult.Yes) return;

            restoreOverlay.Visibility = Visibility.Visible;
            try
            {
                await _core.RollbackDeployAsync(manifest, new Progress<DeploymentProgress>(_ => { }));
                NotificationService.ShowSuccess($"{displayName} restored.");
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Restore failed: {ex.Message}");
            }
            finally
            {
                restoreOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private static string FormatTimestamp(string raw)
        {
            if (DateTime.TryParseExact(raw, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                return dt.ToString("yyyy-MM-dd HH:mm");
            return raw;
        }
    }
}
