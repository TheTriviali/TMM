using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    public partial class DownloadsPage : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private BackendCore? _core;
        private List<LibraryEntry> _entries = new();
        private string _selectedGameKey = "";
        private bool _webViewInitialized = false;

        // ── Public API ────────────────────────────────────────────────────────────

        public event Action<string>? UrlChanged;
        public string CurrentUrl => webView.Source?.ToString() ?? "";

        public void GoBack()    { if (webView.CoreWebView2?.CanGoBack    == true) webView.GoBack(); }
        public void GoForward() { if (webView.CoreWebView2?.CanGoForward == true) webView.GoForward(); }
        public void Reload()    { webView.CoreWebView2?.Reload(); }

        public void Navigate(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            Uri? uri = null;
            if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                Uri.TryCreate(input, UriKind.Absolute, out uri);
            else if (input.Contains('.') && !input.Contains(' '))
                Uri.TryCreate("https://" + input, UriKind.Absolute, out uri);

            webView.Source = uri ?? new Uri($"https://www.google.com/search?q={Uri.EscapeDataString(input)}");
        }

        // ── Constructor ───────────────────────────────────────────────────────────

        public DownloadsPage()
        {
            InitializeComponent();
        }

        public void Initialize(BackendCore core, IEnumerable<LibraryEntry> entries)
        {
            _core = core;
            _entries = entries.Where(e => !e.IsPlaceholder).ToList();

            cmbGame.ItemsSource = _entries;
            cmbGame.DisplayMemberPath = "DisplayName";

            if (_entries.Count > 0)
            {
                var def = _entries.FirstOrDefault(e => e.IsDefault) ?? _entries[0];
                cmbGame.SelectedItem = def;
            }
        }

        // ── Visibility trigger — init WebView2 on first show ──────────────────────

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue && !_webViewInitialized)
            {
                _webViewInitialized = true;
                _ = InitializeWebViewAsync();
            }
        }

        private async System.Threading.Tasks.Task InitializeWebViewAsync()
        {
            try
            {
                webViewLoading.Visibility = Visibility.Visible;
                webViewFallback.Visibility = Visibility.Collapsed;

                await webView.EnsureCoreWebView2Async(null);

                webView.CoreWebView2.DownloadStarting    += OnDownloadStarting;
                webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                webViewLoading.Visibility = Visibility.Collapsed;
                webView.Visibility = Visibility.Visible;
                webView.Source = new Uri("https://www.google.com");
            }
            catch (Exception ex)
            {
                webViewLoading.Visibility = Visibility.Collapsed;
                webViewFallback.Visibility = Visibility.Visible;
                txtWebViewError.Text =
                    $"Could not load the WebView2 runtime.\n\n{ex.Message}\n\n" +
                    "Install the Microsoft Edge WebView2 Runtime and restart TMM.";
            }
        }

        // ── Navigation events ─────────────────────────────────────────────────────

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            string url = webView.Source?.ToString() ?? "";
            Dispatcher.Invoke(() => UrlChanged?.Invoke(url));
        }

        // ── Download intercept ────────────────────────────────────────────────────

        private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string fname = Path.GetFileName(e.ResultFilePath);
            string ext   = Path.GetExtension(fname).ToLowerInvariant();

            if (ext != ".zip" && ext != ".rar" && ext != ".7z") return;
            if (_core == null || string.IsNullOrEmpty(_selectedGameKey)) return;

            string archiveDir = _core.GetModsArchivePath(_selectedGameKey);
            string destPath   = UniqueFilePath(archiveDir, fname);

            e.ResultFilePath = destPath;
            e.Handled = true;

            var op = e.DownloadOperation;
            op.StateChanged += (s, _) =>
            {
                if (op.State == CoreWebView2DownloadState.Completed)
                    Dispatcher.Invoke(() =>
                    {
                        RefreshArchiveList();
                        NotificationService.ShowSuccess($"Saved: {Path.GetFileName(destPath)}");
                    });
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                    Dispatcher.Invoke(() =>
                        NotificationService.ShowWarning($"Download interrupted: {fname}"));
            };
        }

        private static string UniqueFilePath(string dir, string fileName)
        {
            string dest = Path.Combine(dir, fileName);
            if (!File.Exists(dest)) return dest;
            string nameNoExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int n = 1;
            while (File.Exists(dest))
                dest = Path.Combine(dir, $"{nameNoExt} ({n++}){ext}");
            return dest;
        }

        // ── Game selector ─────────────────────────────────────────────────────────

        private void CmbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbGame.SelectedItem is LibraryEntry entry)
            {
                _selectedGameKey = entry.Key;
                RefreshArchiveList();
            }
        }

        // ── Archive list ──────────────────────────────────────────────────────────

        private void RefreshArchiveList()
        {
            archiveList.Children.Clear();

            if (_core == null || string.IsNullOrEmpty(_selectedGameKey)) return;

            string archiveDir = _core.GetModsArchivePath(_selectedGameKey);
            txtArchivePath.Text = archiveDir;

            var files = Directory.GetFiles(archiveDir)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".zip" || ext == ".rar" || ext == ".7z";
                })
                .OrderByDescending(File.GetLastWriteTime)
                .ToList();

            if (files.Count == 0)
            {
                archiveList.Children.Add(new TextBlock
                {
                    Text = "No archives yet.\nDownloaded .zip/.rar/.7z files\nwill appear here.",
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
                    Opacity = 0.6,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(12, 20, 12, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            foreach (string file in files)
                archiveList.Children.Add(BuildArchiveRow(file));
        }

        private UIElement BuildArchiveRow(string filePath)
        {
            string fname  = Path.GetFileName(filePath);
            long   size   = new FileInfo(filePath).Length;
            string sizeStr = BackendCore.FormatBytes(size);

            var row = new Border
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = (Brush)Application.Current.Resources["WindowBorderBrush"],
                Padding = new Thickness(10, 7, 8, 7),
                Cursor = Cursors.Arrow
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var iconTb = new TextBlock
            {
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14, Text = "",
                Foreground = (Brush)Application.Current.Resources["AccentBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(iconTb, 0);

            var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            namePanel.Children.Add(new TextBlock
            {
                Text = fname, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            namePanel.Children.Add(new TextBlock
            {
                Text = sizeStr, FontSize = 10,
                Foreground = (Brush)Application.Current.Resources["SubTextBrush"]
            });
            Grid.SetColumn(namePanel, 1);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            btnPanel.Children.Add(MakeIconButton("", "Open file location",
                () => Process.Start("explorer.exe", $"/select,\"{filePath}\"")));
            btnPanel.Children.Add(MakeIconButton("", "Delete archive", () =>
            {
                try { File.Delete(filePath); RefreshArchiveList(); }
                catch (Exception ex) { NotificationService.ShowWarning($"Could not delete: {ex.Message}"); }
            }));
            Grid.SetColumn(btnPanel, 2);

            grid.Children.Add(iconTb);
            grid.Children.Add(namePanel);
            grid.Children.Add(btnPanel);
            row.Child = grid;
            return row;
        }

        private static Button MakeIconButton(string glyph, string tooltip, Action onClick)
        {
            var tb = new TextBlock
            {
                Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["SubTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var btn = new Button
            {
                Width = 26, Height = 24, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                ToolTip = tooltip, Margin = new Thickness(2, 0, 0, 0), Content = tb
            };
            btn.Click += (_, _) => onClick();
            btn.MouseEnter += (s, _) => ((Button)s).Background = (Brush)Application.Current.Resources["ControlBgBrush"];
            btn.MouseLeave += (s, _) => ((Button)s).Background = Brushes.Transparent;
            return btn;
        }

        // ── Open folder ───────────────────────────────────────────────────────────

        private void BtnOpenArchiveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_core == null || string.IsNullOrEmpty(_selectedGameKey)) return;
            Process.Start("explorer.exe", _core.GetModsArchivePath(_selectedGameKey));
        }
    }
}
