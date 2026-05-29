using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// ModManagerPage — right-hand Downloads drawer.
    /// Shown only after the user has used the built-in download function at least once
    /// (Settings.HasUsedBuiltInDownloads). The toggle button and column are absent entirely
    /// (Visibility.Collapsed) until that flag is set.
    /// </summary>
    public partial class ModManagerPage
    {
        private bool _downloadsDrawerOpen = false;

        // ── Setup (called from InitCustomGame) ────────────────────────────────────

        private void InitializeDownloadsDrawer()
        {
            bool hasFlag = _core.Settings.HasUsedBuiltInDownloads;
            var vis = hasFlag ? Visibility.Visible : Visibility.Collapsed;
            Cust_pnlDownloadsToggle.Visibility  = vis;
            Cust_DownloadsSepToolbar.Visibility = vis;

            // Drawer starts closed
            _downloadsDrawerOpen = false;
            Cust_DownloadsBorder.Visibility = Visibility.Collapsed;
        }

        // ── Toggle handler ────────────────────────────────────────────────────────

        private void BtnToggleDownloadsCustom_Click(object sender, RoutedEventArgs e)
        {
            _downloadsDrawerOpen = !_downloadsDrawerOpen;
            Cust_DownloadsBorder.Visibility = _downloadsDrawerOpen
                ? Visibility.Visible : Visibility.Collapsed;

            if (_downloadsDrawerOpen)
                RefreshDownloadsDrawer();
        }

        // ── Drawer population ─────────────────────────────────────────────────────

        private void RefreshDownloadsDrawer()
        {
            Cust_DownloadsArchiveList.Children.Clear();

            if (_customProfile == null) return;

            string archiveDir = _core.GetModsArchivePath(_customProfile.Key);

            string[] files;
            try
            {
                files = Directory.GetFiles(archiveDir)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext is ".zip" or ".rar" or ".7z";
                    })
                    .OrderByDescending(File.GetLastWriteTime)
                    .ToArray();
            }
            catch
            {
                files = Array.Empty<string>();
            }

            if (files.Length == 0)
            {
                Cust_DownloadsArchiveList.Children.Add(new TextBlock
                {
                    Text              = LocalizationService.Instance["ModManager_Downloads_Empty"],
                    FontSize          = 11,
                    TextAlignment     = System.Windows.TextAlignment.Center,
                    Foreground        = (Brush)Application.Current.Resources["SubTextBrush"],
                    Opacity           = 0.6,
                    TextWrapping      = TextWrapping.Wrap,
                    Margin            = new Thickness(12, 20, 12, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
                return;
            }

            foreach (string file in files)
            {
                Cust_DownloadsArchiveList.Children.Add(
                    ArchiveRowHelper.BuildRow(
                        file,
                        refreshCallback: RefreshDownloadsDrawer,
                        installCallback: InstallFromDrawerAsync));
            }
        }

        private async System.Threading.Tasks.Task InstallFromDrawerAsync(string archivePath)
        {
            await InstallModFileCustomAsync(archivePath);
            await RefreshCustomAsync();
            SaveModsCustom();
        }

        // ── Footer ────────────────────────────────────────────────────────────────

        private void BtnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_customProfile == null) return;
            string dir = _core.GetModsArchivePath(_customProfile.Key);
            Process.Start("explorer.exe", dir);
        }
    }
}
