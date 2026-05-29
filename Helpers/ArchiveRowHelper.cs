using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Shared factory for archive-list row UI — used by both DownloadsPage and
    /// the ModManagerPage Downloads drawer to avoid duplicating layout code.
    /// </summary>
    internal static class ArchiveRowHelper
    {
        /// <summary>
        /// Build a single archive row.
        /// </summary>
        /// <param name="filePath">Full path to the archive file.</param>
        /// <param name="refreshCallback">Called after the row's Delete action completes.</param>
        /// <param name="installCallback">
        /// When non-null, an "Install" button is added. The callback receives the archive
        /// path and should perform the install + refresh asynchronously.
        /// </param>
        public static UIElement BuildRow(
            string filePath,
            Action refreshCallback,
            Func<string, Task>? installCallback = null)
        {
            string fname   = Path.GetFileName(filePath);
            long   size    = new FileInfo(filePath).Length;
            string sizeStr = BackendCore.FormatBytes(size);

            var row = new Border
            {
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush     = (Brush)Application.Current.Resources["WindowBorderBrush"],
                Padding         = new Thickness(10, 7, 8, 7),
                Cursor          = Cursors.Arrow,
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Archive icon
            var icon = new TextBlock
            {
                FontFamily        = new FontFamily("Segoe MDL2 Assets"),
                FontSize          = 14,
                Text              = "", // ZipFolder
                Foreground        = (Brush)Application.Current.Resources["AccentBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(icon, 0);

            // Name + size stacked
            var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new TextBlock
            {
                Text         = fname,
                FontSize     = 11,
                FontWeight   = FontWeights.SemiBold,
                Foreground   = (Brush)Application.Current.Resources["TextBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            namePanel.Children.Add(new TextBlock
            {
                Text       = sizeStr,
                FontSize   = 10,
                Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
            });
            Grid.SetColumn(namePanel, 1);

            // Action buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (installCallback != null)
            {
                var installBtn = MakeTextButton("Install", async () => await installCallback(filePath));
                btnPanel.Children.Add(installBtn);
            }

            btnPanel.Children.Add(MakeIconButton("", "Open file location",
                () => Process.Start("explorer.exe", $"/select,\"{filePath}\"")));

            btnPanel.Children.Add(MakeIconButton("", "Delete archive", () =>
            {
                try { File.Delete(filePath); refreshCallback(); }
                catch (Exception ex) { NotificationService.ShowWarning($"Could not delete: {ex.Message}"); }
            }));

            Grid.SetColumn(btnPanel, 2);

            grid.Children.Add(icon);
            grid.Children.Add(namePanel);
            grid.Children.Add(btnPanel);
            row.Child = grid;
            return row;
        }

        private static Button MakeIconButton(string glyph, string tooltip, Action onClick)
        {
            var tb = new TextBlock
            {
                Text                = glyph,
                FontFamily          = new FontFamily("Segoe MDL2 Assets"),
                FontSize            = 12,
                Foreground          = (Brush)Application.Current.Resources["SubTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            var btn = new Button
            {
                Width           = 26,
                Height          = 24,
                Background      = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor          = Cursors.Hand,
                ToolTip         = tooltip,
                Margin          = new Thickness(2, 0, 0, 0),
                Content         = tb,
            };
            btn.Click      += (_, _) => onClick();
            btn.MouseEnter += (s, _) => ((Button)s).Background = (Brush)Application.Current.Resources["ControlBgBrush"];
            btn.MouseLeave += (s, _) => ((Button)s).Background = Brushes.Transparent;
            return btn;
        }

        private static Button MakeTextButton(string text, Func<Task> onClick)
        {
            var btn = new Button
            {
                Content         = text,
                FontSize        = 10,
                FontWeight      = FontWeights.SemiBold,
                Padding         = new Thickness(8, 3, 8, 3),
                Margin          = new Thickness(0, 0, 4, 0),
                BorderThickness = new Thickness(0),
                Cursor          = Cursors.Hand,
            };
            btn.SetResourceReference(Button.BackgroundProperty, "AccentBrush");
            btn.SetResourceReference(Button.ForegroundProperty, "AccentTextBrush");
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                try { await onClick(); }
                finally { btn.IsEnabled = true; }
            };
            return btn;
        }
    }
}
