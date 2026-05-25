using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class PathsPage : UserControl
    {
        private readonly BackendCore _core;

        public PathsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
            BuildPathRows();
        }

        private record PathRowDef(
            string Label,
            string Description,
            Func<string> GetPath,
            Action<string>? SetPath = null   // null = read-only (log dir)
        );

        private void BuildPathRows()
        {
            var rows = new[]
            {
                new PathRowDef(
                    "Library Art",
                    "Custom artwork for game cards (PNG files)",
                    () => _core.LibraryArtPath,
                    null   // Always under AppData — not user-configurable in v1
                ),
                new PathRowDef(
                    "Tmmpack Archive",
                    "Exported .tmmpack bundles",
                    () => Path.Combine(_core.AppDataPath, "Packs"),
                    null
                ),
                new PathRowDef(
                    "Backups",
                    "Automatic mod backups before deploy",
                    () => _core.BackupsPath,
                    null
                ),
                new PathRowDef(
                    "Downloads Cache",
                    "Temporary files during downloads",
                    () => _core.DownloadCachePath,
                    null
                ),
                new PathRowDef(
                    "Log Files",
                    "TMM.log and diagnostic output",
                    () => _core.AppDataPath,
                    null
                ),
            };

            foreach (var row in rows)
                pathRowsPanel.Children.Add(BuildRow(row));
        }

        private UIElement BuildRow(PathRowDef def)
        {
            var container = new Border
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16),
                CornerRadius = new System.Windows.CornerRadius(8),
            };
            container.SetResourceReference(Border.BackgroundProperty, "PanelBrush");

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Label
            var label = new TextBlock
            {
                Text = def.Label, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetRow(label, 0); Grid.SetColumn(label, 0); Grid.SetColumnSpan(label, 3);

            // Description
            var desc = new TextBlock
            {
                Text = def.Description, FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            desc.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
            Grid.SetRow(desc, 1); Grid.SetColumn(desc, 0); Grid.SetColumnSpan(desc, 3);

            // Path text
            var pathText = new TextBlock
            {
                Text = def.GetPath(), FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            pathText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
            Grid.SetRow(pathText, 2); Grid.SetColumn(pathText, 0);

            // Open button
            var btnOpen = new System.Windows.Controls.Button
            {
                Content = "Open", Height = 28, Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnOpen.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "ControlBgBrush");
            btnOpen.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "TextBrush");
            btnOpen.BorderThickness = new Thickness(0);
            btnOpen.Click += (_, _) =>
            {
                var path = def.GetPath();
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            };
            Grid.SetRow(btnOpen, 2); Grid.SetColumn(btnOpen, 1);

            grid.Children.Add(label);
            grid.Children.Add(desc);
            grid.Children.Add(pathText);
            grid.Children.Add(btnOpen);

            container.Child = grid;
            return container;
        }
    }
}
