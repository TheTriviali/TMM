using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    public partial class CustomGameConfigWindow : TmmWindow
    {
        public CustomGameProfile? Result { get; private set; }
        private readonly bool _isEdit;
        private readonly ObservableCollection<RoutingRule> _rules = new();

        public CustomGameConfigWindow(CustomGameProfile? existing, bool isTemplate = false)
        {
            _isEdit = existing != null && !isTemplate;
            InitializeComponent();
            icRules.ItemsSource = _rules;
            _rules.CollectionChanged += (_, _) => UpdateEmptyState();

            if (existing != null)
            {
                txtWindowTitle.Text = isTemplate ? "Import Game Config" : "Edit Custom Game";
                if (!isTemplate) btnSave.Content = "Update";
                PopulateFormFromConfig(existing);
            }
        }

        private void PopulateFormFromConfig(CustomGameProfile config)
        {
            txtGameName.Text    = config.GameName;
            txtGameDir.Text     = config.GameDirectory;
            txtExePath.Text     = config.ExePath ?? "";
            txtSteamAppId.Text  = config.SteamAppId ?? "";
            txtDescription.Text = config.Description ?? "";
            txtAuthor.Text      = config.Author ?? "";
            txtVersion.Text     = config.Version ?? "";

            _rules.Clear();
            foreach (var rule in config.RoutingRules)
            {
                var r = new RoutingRule
                {
                    RuleName            = rule.RuleName,
                    ExtensionPattern    = rule.ExtensionPattern,
                    NameContains        = rule.NameContains,
                    CheckSubdir         = rule.CheckSubdir,
                    Destination         = rule.Destination,
                    FallbackDestination = rule.FallbackDestination,
                };
                r.PropertyChanged += (_, _) => ValidateRuleConflicts();
                _rules.Add(r);
            }
            ValidateRuleConflicts();
        }

        private CustomGameProfile? BuildCurrentProfile()
        {
            string name = txtGameName.Text.Trim();
            string dir  = txtGameDir.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dir)) return null;

            return new CustomGameProfile
            {
                GameName     = name,
                GameDirectory = dir,
                ExePath      = NullIfBlank(txtExePath.Text),
                SteamAppId   = NullIfBlank(txtSteamAppId.Text),
                RoutingRules = _rules.Select(r => new RoutingRule
                {
                    RuleName            = r.RuleName,
                    ExtensionPattern    = r.ExtensionPattern.Trim(),
                    NameContains        = NullIfBlank(r.NameContains),
                    CheckSubdir         = NullIfBlank(r.CheckSubdir),
                    Destination         = string.IsNullOrWhiteSpace(r.Destination) ? "." : r.Destination.Trim(),
                    FallbackDestination = NullIfBlank(r.FallbackDestination),
                }).ToList(),
                Description = NullIfBlank(txtDescription.Text),
                Author      = NullIfBlank(txtAuthor.Text),
                Version     = NullIfBlank(txtVersion.Text),
            };
        }

        private static string? NullIfBlank(string? s) =>
            string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(((App)Application.Current).Core.Settings);
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            if (pnlRoutingEmpty != null)
                pnlRoutingEmpty.Visibility = _rules.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ValidateRuleConflicts()
        {
            // Flag rules that share the same ExtensionPattern with no NameContains
            // distinguishing them (those might silently shadow each other).
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in _rules) r.HasConflict = false;
            foreach (var r in _rules)
            {
                // Only flag pure-extension conflicts (no name filter makes them ambiguous)
                if (!string.IsNullOrWhiteSpace(r.NameContains)) continue;
                string ext = r.ExtensionPattern.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) continue;
                if (seen.ContainsKey(ext))
                {
                    r.HasConflict = true;
                    _rules[seen[ext]].HasConflict = true;
                }
                else seen[ext] = _rules.IndexOf(r);
            }
        }

        // ── Browse buttons ────────────────────────────────────────────────────────

        private void BtnOpenGameDir_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = txtGameDir.Text.Trim();
            if (!string.IsNullOrEmpty(gameDir) && Directory.Exists(gameDir))
                ShellHelper.OpenFolder(gameDir);
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Game Directory",
                InitialDirectory = string.IsNullOrEmpty(txtGameDir.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    : txtGameDir.Text
            };
            if (dlg.ShowDialog() == true)
            {
                txtGameDir.Text = dlg.FolderName;
                if (string.IsNullOrWhiteSpace(txtGameName.Text))
                    txtGameName.Text = Path.GetFileName(dlg.FolderName);
            }
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = txtGameDir.Text.Trim();
            var dlg = new OpenFileDialog
            {
                Title = "Select Game Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = Directory.Exists(gameDir)
                    ? gameDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            if (dlg.ShowDialog() == true)
            {
                string exePath = dlg.FileName;
                if (!string.IsNullOrEmpty(gameDir) &&
                    exePath.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                    exePath = exePath[gameDir.Length..].TrimStart(Path.DirectorySeparatorChar,
                                                                    Path.AltDirectorySeparatorChar);
                txtExePath.Text = exePath;
            }
        }

        // ── Rule card: WHEN / IF / THEN / ELSE actions ────────────────────────────

        private void BtnAddNameFilter_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
                rule.NameContains = "text";
        }

        private void BtnRemoveNameFilter_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
                rule.NameContains = null;
        }

        private void BtnAddCondition_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
            {
                rule.CheckSubdir       = "folder";
                rule.FallbackDestination = ".";
            }
        }

        private void BtnRemoveCondition_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
            {
                rule.CheckSubdir       = null;
                rule.FallbackDestination = null;
            }
        }

        // ── Rule list management ──────────────────────────────────────────────────

        private void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            var rule = new RoutingRule { ExtensionPattern = ".ext", Destination = "." };
            rule.PropertyChanged += (_, _) => ValidateRuleConflicts();
            _rules.Add(rule);
            ValidateRuleConflicts();
        }

        private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
                _rules.Remove(rule);
        }

        private void BtnMoveRuleUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
            {
                int idx = _rules.IndexOf(rule);
                if (idx > 0) _rules.Move(idx, idx - 1);
            }
        }

        private void BtnMoveRuleDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is RoutingRule rule)
            {
                int idx = _rules.IndexOf(rule);
                if (idx < _rules.Count - 1) _rules.Move(idx, idx + 1);
            }
        }

        // ── Preset templates ──────────────────────────────────────────────────────

        private void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPresets.SelectedItem is not ComboBoxItem item) return;
            string tag = item.Tag as string ?? "";

            RoutingRule[] rules = tag switch
            {
                "asi" => new[]
                {
                    new RoutingRule { ExtensionPattern = ".asi", CheckSubdir = "plugins",
                                      Destination = "plugins", FallbackDestination = "." },
                },
                "source" => new[]
                {
                    new RoutingRule { ExtensionPattern = ".vpk", CheckSubdir = "custom",
                                      Destination = "custom", FallbackDestination = "." },
                    new RoutingRule { ExtensionPattern = ".cfg", CheckSubdir = "cfg",
                                      Destination = "cfg", FallbackDestination = "." },
                },
                "skse" => new[]
                {
                    new RoutingRule { ExtensionPattern = ".dll", CheckSubdir = "Data\\SKSE",
                                      Destination = "Data\\SKSE\\Plugins", FallbackDestination = "." },
                    new RoutingRule { ExtensionPattern = ".pex", CheckSubdir = "Data\\Scripts",
                                      Destination = "Data\\Scripts", FallbackDestination = "." },
                    new RoutingRule { ExtensionPattern = ".esp", Destination = "Data" },
                    new RoutingRule { ExtensionPattern = ".esm", Destination = "Data" },
                    new RoutingRule { ExtensionPattern = ".bsa", Destination = "Data" },
                },
                "cleo" => new[]
                {
                    new RoutingRule { ExtensionPattern = ".cs",   CheckSubdir = "CLEO",
                                      Destination = "CLEO", FallbackDestination = "." },
                    new RoutingRule { ExtensionPattern = ".cleo", CheckSubdir = "CLEO",
                                      Destination = "CLEO", FallbackDestination = "." },
                    new RoutingRule { ExtensionPattern = ".fxt",  CheckSubdir = "CLEO",
                                      Destination = "CLEO", FallbackDestination = "." },
                },
                "clear" => Array.Empty<RoutingRule>(),
                _ => null!
            };

            if (rules == null) return;
            _rules.Clear();
            foreach (var r in rules)
            {
                r.PropertyChanged += (_, _) => ValidateRuleConflicts();
                _rules.Add(r);
            }
            ValidateRuleConflicts();
        }

        // ── Test Routing ──────────────────────────────────────────────────────────

        private void BtnTestRouting_Click(object sender, RoutedEventArgs e)
        {
            if (pnlTestRouting.Visibility == Visibility.Collapsed)
            {
                pnlTestRouting.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(txtTestFile.Text)) RunTestRoute();
            }
            else
            {
                pnlTestRouting.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnTestRoutingBrowse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Title = "Select a file to test routing" };
            if (ofd.ShowDialog() == true)
            {
                txtTestFile.Text = ofd.FileName;
                RunTestRoute();
            }
        }

        private void RunTestRoute()
        {
            string filePath = txtTestFile.Text.Trim();
            if (string.IsNullOrEmpty(filePath)) { txtTestResult.Text = "→ (no file selected)"; return; }

            string ext      = Path.GetExtension(filePath).ToLowerInvariant();
            string fileName = Path.GetFileName(filePath);
            string gameDir  = txtGameDir.Text.Trim();

            foreach (var rule in _rules)
            {
                if (!MatchesExt(rule.ExtensionPattern, ext)) continue;
                if (!string.IsNullOrWhiteSpace(rule.NameContains) &&
                    !fileName.Contains(rule.NameContains, StringComparison.OrdinalIgnoreCase)) continue;

                if (rule.HasCondition)
                {
                    bool exists = !string.IsNullOrEmpty(gameDir) &&
                                  Directory.Exists(Path.Combine(gameDir, rule.CheckSubdir!));
                    string dest   = exists ? rule.Destination : (rule.FallbackDestination ?? ".");
                    string reason = exists
                        ? $"({rule.CheckSubdir}\\ exists)"
                        : $"({rule.CheckSubdir}\\ not found)";
                    txtTestResult.Text = $"→  {(dest == "." ? "(game root)" : dest)}  {reason}";
                }
                else
                {
                    string dest = string.IsNullOrWhiteSpace(rule.Destination) ? "." : rule.Destination;
                    txtTestResult.Text = $"→  {(dest == "." ? "(game root)" : dest)}  (matched rule: {rule.ExtensionPattern})";
                }
                return;
            }

            txtTestResult.Text = "→  (game root)  (no rule matched — default)";
        }

        private static bool MatchesExt(string pattern, string ext)
        {
            if (pattern is "*" or ".*" or "any") return true;
            return pattern.Equals(ext, StringComparison.OrdinalIgnoreCase);
        }

        // ── Import / Export ───────────────────────────────────────────────────────

        private async void BtnImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title  = "Import .tmmgame Config",
                Filter = "TMM Game Config (*.tmmgame)|*.tmmgame|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var config = await GameRegistry.ImportGameConfigAsync(dlg.FileName);
                PopulateFormFromConfig(config);
                txtWindowTitle.Text = "Import Game Config";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import config:\n{ex.Message}", "Import Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var config = BuildCurrentProfile();
            if (config == null)
            {
                MessageBox.Show("Please fill in at least a Game Name and Game Directory before exporting.",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string safeName = string.Join("_", config.GameName.Split(Path.GetInvalidFileNameChars()));
            var dlg = new SaveFileDialog
            {
                Title      = "Export .tmmgame Config",
                Filter     = "TMM Game Config (*.tmmgame)|*.tmmgame",
                FileName   = safeName,
                DefaultExt = ".tmmgame"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                await GameRegistry.ExportConfigAsync(config, dlg.FileName);
                MessageBox.Show($"Exported to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export config:\n{ex.Message}", "Export Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Save / Cancel ─────────────────────────────────────────────────────────

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = txtGameName.Text.Trim();
            string dir  = txtGameDir.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a game name.", "Required Field",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameName.Focus(); return;
            }
            if (string.IsNullOrEmpty(dir))
            {
                MessageBox.Show("Please select the game directory.", "Required Field",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameDir.Focus(); return;
            }

            // Validate Steam AppId
            string? steamAppId = null;
            string appIdText = txtSteamAppId.Text.Trim();
            if (!string.IsNullOrEmpty(appIdText))
            {
                if (!uint.TryParse(appIdText, out _))
                {
                    MessageBox.Show("Steam App ID must be a positive number (e.g., 12210).\n\nLeave empty to disable Steam integration.",
                        "Invalid Steam App ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtSteamAppId.Focus(); return;
                }
                steamAppId = appIdText;
            }

            // Validate routing rules
            var routingRules = new List<RoutingRule>();
            foreach (var r in _rules)
            {
                string ext = r.ExtensionPattern.Trim();
                if (string.IsNullOrEmpty(ext))
                {
                    MessageBox.Show("One or more rules have an empty extension pattern. Use * for catch-all.",
                        "Incomplete Rule", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(r.Destination))
                {
                    MessageBox.Show($"Rule for '{ext}' has no destination. Use . for game root.",
                        "Incomplete Rule", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                routingRules.Add(new RoutingRule
                {
                    RuleName            = r.RuleName,
                    ExtensionPattern    = ext,
                    NameContains        = NullIfBlank(r.NameContains),
                    CheckSubdir         = NullIfBlank(r.CheckSubdir),
                    Destination         = r.Destination.Trim(),
                    FallbackDestination = NullIfBlank(r.FallbackDestination),
                });
            }

            Result = new CustomGameProfile
            {
                GameName      = name,
                GameDirectory = dir,
                ExePath       = NullIfBlank(txtExePath.Text),
                SteamAppId    = steamAppId,
                RoutingRules  = routingRules,
                Description   = NullIfBlank(txtDescription.Text),
                Author        = NullIfBlank(txtAuthor.Text),
                Version       = NullIfBlank(txtVersion.Text),
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private new void BtnClose_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
