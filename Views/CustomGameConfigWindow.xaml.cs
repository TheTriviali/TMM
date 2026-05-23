using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    // ── Observable row models for the DataGrids ───────────────────────────────────

    public class MappingRow : INotifyPropertyChanged
    {
        private string _extension = "";
        private string _outputFolder = ".";

        public string Extension
        {
            get => _extension;
            set { _extension = value; OnPropertyChanged(nameof(Extension)); }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set { _outputFolder = value; OnPropertyChanged(nameof(OutputFolder)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class CondRouteRow : INotifyPropertyChanged
    {
        private string _extension = ".asi";
        private string _checkSubdir = "plugins";
        private string _routeIfExists = "plugins";
        private string _routeIfMissing = ".";

        public string Extension     { get => _extension;     set { _extension     = value; OnPropertyChanged(nameof(Extension)); } }
        public string CheckSubdir   { get => _checkSubdir;   set { _checkSubdir   = value; OnPropertyChanged(nameof(CheckSubdir)); } }
        public string RouteIfExists { get => _routeIfExists; set { _routeIfExists = value; OnPropertyChanged(nameof(RouteIfExists)); } }
        public string RouteIfMissing{ get => _routeIfMissing;set { _routeIfMissing= value; OnPropertyChanged(nameof(RouteIfMissing)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Window ────────────────────────────────────────────────────────────────────

    public partial class CustomGameConfigWindow : TmmWindow
    {
        public CustomGameProfile? Result { get; private set; }
        private readonly bool _isEdit;
        private readonly ObservableCollection<MappingRow>   _mappings   = new();
        private readonly ObservableCollection<CondRouteRow> _condRoutes = new();

        public CustomGameConfigWindow(CustomGameProfile? existing, bool isTemplate = false)
        {
            _isEdit = existing != null && !isTemplate;
            InitializeComponent();
            dgMappings.ItemsSource    = _mappings;
            icCondRoutes.ItemsSource  = _condRoutes;
            _condRoutes.CollectionChanged += (_, _) => UpdateEmptyState();

            if (existing != null)
            {
                txtWindowTitle.Text = isTemplate ? "Import Game Config" : "Edit Custom Game";
                if (!isTemplate) btnSave.Content = "Update";
                PopulateFormFromConfig(existing);
            }
        }

        private void PopulateFormFromConfig(CustomGameProfile config)
        {
            txtGameName.Text   = config.GameName;
            txtGameDir.Text    = config.GameDirectory;
            txtExePath.Text    = config.ExePath ?? "";
            txtFileTypes.Text  = config.ModFileTypes;
            txtSteamAppId.Text = config.SteamAppId ?? "";
            _mappings.Clear();
            _condRoutes.Clear();

            foreach (var kvp in config.OutputDirectories)
                _mappings.Add(new MappingRow { Extension = kvp.Key, OutputFolder = kvp.Value });

            foreach (var cr in config.ConditionalRoutes)
                _condRoutes.Add(new CondRouteRow
                {
                    Extension      = cr.Extension,
                    CheckSubdir    = cr.CheckSubdir,
                    RouteIfExists  = cr.RouteIfExists,
                    RouteIfMissing = cr.RouteIfMissing
                });
        }

        private CustomGameProfile? BuildCurrentProfile()
        {
            string name = txtGameName.Text.Trim();
            string dir  = txtGameDir.Text.Trim();
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(dir)) return null;

            var outputDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _mappings)
            {
                string ext    = row.Extension.Trim().ToLowerInvariant();
                string folder = string.IsNullOrWhiteSpace(row.OutputFolder) ? "." : row.OutputFolder.Trim();
                if (!string.IsNullOrEmpty(ext) && ext != ".ext" && ext.StartsWith("."))
                    outputDirs[ext] = folder;
            }

            var condRoutes = new List<ConditionalRoute>();
            foreach (var r in _condRoutes)
            {
                string ext = r.Extension.Trim();
                string sub = r.CheckSubdir.Trim();
                if (string.IsNullOrWhiteSpace(ext) || string.IsNullOrWhiteSpace(sub)) continue;
                condRoutes.Add(new ConditionalRoute(
                    ext.ToLowerInvariant(), sub,
                    string.IsNullOrWhiteSpace(r.RouteIfExists)  ? "." : r.RouteIfExists.Trim(),
                    string.IsNullOrWhiteSpace(r.RouteIfMissing) ? "." : r.RouteIfMissing.Trim()));
            }

            return new CustomGameProfile
            {
                GameName          = name,
                GameDirectory     = dir,
                ExePath           = string.IsNullOrWhiteSpace(txtExePath.Text) ? null : txtExePath.Text.Trim(),
                SteamAppId        = string.IsNullOrWhiteSpace(txtSteamAppId.Text) ? null : txtSteamAppId.Text.Trim(),
                ModFileTypes      = string.IsNullOrWhiteSpace(txtFileTypes.Text) ? ".zip, .rar, .7z" : txtFileTypes.Text.Trim(),
                OutputDirectories = outputDirs,
                ConditionalRoutes = condRoutes
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(((App)Application.Current).Core.Settings);
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            if (pnlRoutingEmpty != null)
                pnlRoutingEmpty.Visibility = _condRoutes.Count == 0
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
        }

        // ── Browse buttons ────────────────────────────────────────────────────────

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
                    txtGameName.Text = System.IO.Path.GetFileName(dlg.FolderName);
            }
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = txtGameDir.Text.Trim();
            var dlg = new OpenFileDialog
            {
                Title = "Select Game Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Directory.Exists(gameDir)
                    ? gameDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            if (dlg.ShowDialog() == true)
            {
                string exePath = dlg.FileName;
                if (!string.IsNullOrEmpty(gameDir) && exePath.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                    exePath = exePath[gameDir.Length..].TrimStart(System.IO.Path.DirectorySeparatorChar,
                                                                  System.IO.Path.AltDirectorySeparatorChar);
                txtExePath.Text = exePath;
            }
        }

        // ── Static mapping DataGrid ───────────────────────────────────────────────

        private void BtnAddMapping_Click(object sender, RoutedEventArgs e)
        {
            var row = new MappingRow { Extension = ".ext", OutputFolder = "." };
            _mappings.Add(row);
            dgMappings.ScrollIntoView(row);
            dgMappings.SelectedItem = row;
            dgMappings.CurrentCell  = new System.Windows.Controls.DataGridCellInfo(row, dgMappings.Columns[0]);
            dgMappings.BeginEdit();
        }

        private void BtnRemoveMapping_Click(object sender, RoutedEventArgs e)
        {
            if (dgMappings.SelectedItem is MappingRow row)
                _mappings.Remove(row);
        }

        // ── Conditional routes (sentence builder) ────────────────────────────────

        private void BtnAddCondRoute_Click(object sender, RoutedEventArgs e)
        {
            _condRoutes.Add(new CondRouteRow());
        }

        private void BtnRemoveCondRouteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is CondRouteRow row)
                _condRoutes.Remove(row);
        }

        // ── Preset templates ──────────────────────────────────────────────────────

        private void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPresets.SelectedItem is not System.Windows.Controls.ComboBoxItem item) return;
            string tag = item.Tag as string ?? "";

            var rules = tag switch
            {
                "asi" => new[]
                {
                    new CondRouteRow { Extension = ".asi", CheckSubdir = "plugins", RouteIfExists = "plugins", RouteIfMissing = "." }
                },
                "source" => new[]
                {
                    new CondRouteRow { Extension = ".vpk", CheckSubdir = "custom",  RouteIfExists = "custom",  RouteIfMissing = "." },
                    new CondRouteRow { Extension = ".cfg", CheckSubdir = "cfg",     RouteIfExists = "cfg",     RouteIfMissing = "." }
                },
                "skse" => new[]
                {
                    new CondRouteRow { Extension = ".dll", CheckSubdir = "SKSE\\Plugins", RouteIfExists = "SKSE\\Plugins", RouteIfMissing = "." },
                    new CondRouteRow { Extension = ".pex", CheckSubdir = "Data\\Scripts",  RouteIfExists = "Data\\Scripts",  RouteIfMissing = "." }
                },
                "cleo" => new[]
                {
                    new CondRouteRow { Extension = ".cs",   CheckSubdir = "CLEO", RouteIfExists = "CLEO", RouteIfMissing = "." },
                    new CondRouteRow { Extension = ".cleo", CheckSubdir = "CLEO", RouteIfExists = "CLEO", RouteIfMissing = "." },
                    new CondRouteRow { Extension = ".fxt",  CheckSubdir = "CLEO", RouteIfExists = "CLEO", RouteIfMissing = "." }
                },
                "clear" => Array.Empty<CondRouteRow>(),
                _ => null
            };

            if (rules == null) return;
            _condRoutes.Clear();
            foreach (var r in rules) _condRoutes.Add(r);
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
            dgMappings.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
            var config = BuildCurrentProfile();
            if (config == null)
            {
                MessageBox.Show("Please fill in at least a Game Name and Game Directory before exporting.",
                    "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string safeName = string.Join("_", config.GameName.Split(System.IO.Path.GetInvalidFileNameChars()));
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
            dgMappings.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            string name = txtGameName.Text.Trim();
            string dir  = txtGameDir.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a game name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(dir))
            {
                MessageBox.Show("Please select the game directory.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameDir.Focus();
                return;
            }

            // ── Validate Steam AppId (optional, must be numeric if provided) ────
            string? steamAppId = null;
            string appIdText = txtSteamAppId.Text.Trim();
            if (!string.IsNullOrEmpty(appIdText))
            {
                if (!uint.TryParse(appIdText, out _))
                {
                    MessageBox.Show("Steam App ID must be a positive number (e.g., 12210).\n\nLeave empty to disable Steam integration.",
                        "Invalid Steam App ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    txtSteamAppId.Focus();
                    return;
                }
                steamAppId = appIdText;
            }

            string fileTypes = string.IsNullOrWhiteSpace(txtFileTypes.Text)
                ? ".zip, .rar, .7z"
                : txtFileTypes.Text.Trim();

            // ── Validate output directory mappings ──────────────────────────────
            var outputDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _mappings)
            {
                string ext    = row.Extension.Trim().ToLowerInvariant();
                string folder = string.IsNullOrWhiteSpace(row.OutputFolder) ? "." : row.OutputFolder.Trim();

                // Validate extension format (must start with . or be empty/default)
                if (!string.IsNullOrEmpty(ext) && ext != ".ext")
                {
                    if (!ext.StartsWith("."))
                    {
                        MessageBox.Show($"Extension '{ext}' must start with a dot (e.g., .asi, .ini).",
                            "Invalid Extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    outputDirs[ext] = folder;
                }
            }

            // ── Validate conditional routes ─────────────────────────────────────
            var condRoutes = new List<ConditionalRoute>();
            foreach (var r in _condRoutes)
            {
                string ext = r.Extension.Trim();
                string sub = r.CheckSubdir.Trim();

                // Skip empty rows
                if (string.IsNullOrWhiteSpace(ext) && string.IsNullOrWhiteSpace(sub))
                    continue;

                // Both fields required if either is filled
                if (string.IsNullOrWhiteSpace(ext) || string.IsNullOrWhiteSpace(sub))
                {
                    MessageBox.Show("Conditional routing rules require both Extension and Check Subfolder fields.\n\n" +
                        "Leave both empty to skip a row, or fill both to create a rule.",
                        "Incomplete Rule", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Validate extension format
                if (!ext.StartsWith("."))
                {
                    MessageBox.Show($"Extension '{ext}' must start with a dot (e.g., .asi).",
                        "Invalid Extension", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string routeIfExists  = string.IsNullOrWhiteSpace(r.RouteIfExists)  ? "." : r.RouteIfExists.Trim();
                string routeIfMissing = string.IsNullOrWhiteSpace(r.RouteIfMissing) ? "." : r.RouteIfMissing.Trim();

                condRoutes.Add(new ConditionalRoute(ext.ToLowerInvariant(), sub, routeIfExists, routeIfMissing));
            }

            Result = new CustomGameProfile
            {
                GameName          = name,
                GameDirectory     = dir,
                ExePath           = string.IsNullOrWhiteSpace(txtExePath.Text) ? null : txtExePath.Text.Trim(),
                SteamAppId        = steamAppId,
                ModFileTypes      = fileTypes,
                OutputDirectories = outputDirs,
                ConditionalRoutes = condRoutes
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private new void BtnClose_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    }
}
