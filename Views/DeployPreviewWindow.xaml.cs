using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// Modal preview shown before deployment. Displays the per-file routing plan so the
    /// user can review destinations, skip individual files, and confirm or cancel.
    /// </summary>
    public partial class DeployPreviewWindow : Window
    {
        private readonly string _gameDir;
        private readonly List<FileDeployRow> _rows;

        public DeployPreviewWindow(
            List<(ModItem Mod, DeploymentPlan Plan)> plans,
            string gameDir)
        {
            InitializeComponent();
            _gameDir = gameDir;
            _rows = BuildRows(plans);
            Populate();
        }

        // ── Public API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the fileMap (game-relative path → source file) from entries the user
        /// did not skip. Call only after ShowDialog() returns true.
        /// </summary>
        public Dictionary<string, string> BuildFileMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows.Where(r => !r.Skip && !r.IsBlocking))
            {
                string rel = Path.GetRelativePath(_gameDir, row.DestinationPath);
                map[rel] = row.SourcePath;
            }
            return map;
        }

        // ── Build display rows ─────────────────────────────────────────────────────

        private static List<FileDeployRow> BuildRows(List<(ModItem Mod, DeploymentPlan Plan)> plans)
        {
            var rows = new List<FileDeployRow>();
            foreach (var (mod, plan) in plans)
            {
                foreach (var entry in plan.Files)
                {
                    rows.Add(new FileDeployRow
                    {
                        ModName         = mod.Name,
                        SourcePath      = entry.SourcePath,
                        DestinationPath = entry.DestinationPath,
                        RuleName        = entry.AppliedRule?.Name ?? "(default)",
                        Skip            = entry.Skip,
                        IsBlocking      = false,
                    });
                }

                // Blocking conflicts become red rows (auto-skipped, user cannot override)
                foreach (var warn in plan.Warnings.Where(w => w.IsBlocking))
                {
                    rows.Add(new FileDeployRow
                    {
                        ModName         = mod.Name,
                        SourcePath      = warn.FilePath ?? "",
                        DestinationPath = "",
                        RuleName        = "Conflict",
                        Skip            = true,
                        IsBlocking      = true,
                        ConflictMessage = warn.Message,
                    });
                }
            }
            return rows;
        }

        // ── Populate UI ────────────────────────────────────────────────────────────

        private void Populate()
        {
            lvFiles.ItemsSource = _rows;

            int deployable = _rows.Count(r => !r.IsBlocking);
            int blocking   = _rows.Count(r => r.IsBlocking);

            txtSummary.Text = blocking > 0
                ? $"{deployable} file(s) ready · {blocking} conflict(s) will be skipped"
                : $"{deployable} file(s) ready to deploy";

            // Collect all warning text: blocking rows + non-blocking plan warnings
            var warningLines = _rows
                .Where(r => r.IsBlocking && !string.IsNullOrEmpty(r.ConflictMessage))
                .Select(r => $"[{r.ModName}] {r.ConflictMessage}")
                .ToList();

            if (warningLines.Count > 0)
            {
                pnlWarnings.Visibility  = Visibility.Visible;
                txtBlockingNote.Visibility = Visibility.Visible;
                icWarnings.ItemsSource  = warningLines;
            }

            // Disable Deploy if nothing to deploy
            btnDeploy.IsEnabled = deployable > 0;
        }

        // ── Handlers ───────────────────────────────────────────────────────────────

        private void BtnDeploy_Click(object sender, RoutedEventArgs e)
            => DialogResult = true;

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }

    /// <summary>Row model for the deploy preview list. Mutable so the user can toggle Skip.</summary>
    public class FileDeployRow
    {
        public string ModName         { get; set; } = "";
        public string SourcePath      { get; set; } = "";
        public string DestinationPath { get; set; } = "";
        public string RuleName        { get; set; } = "";
        public bool   Skip            { get; set; }
        public bool   IsBlocking      { get; set; }
        public string ConflictMessage { get; set; } = "";

        public string FileName => string.IsNullOrEmpty(SourcePath)
            ? "(unknown)" : Path.GetFileName(SourcePath);

        /// <summary>Last two path segments for compact display; full path in tooltip.</summary>
        public string DisplayDestination
        {
            get
            {
                if (string.IsNullOrEmpty(DestinationPath)) return "(skipped)";
                var parts = DestinationPath.Split(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar,
                    StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 2
                    ? $"…\\{string.Join('\\', parts.TakeLast(2))}"
                    : DestinationPath;
            }
        }
    }
}
