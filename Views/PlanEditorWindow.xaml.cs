using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    // ── View-model nodes ──────────────────────────────────────────────────────

    /// <summary>Node in the left-pane game-directory tree.</summary>
    public class GameDirNode : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isDirectory;
        private string _relativePath = "";
        private List<string> _claimedBy = [];
        private bool _isNewFolder;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public bool IsDirectory
        {
            get => _isDirectory;
            set { _isDirectory = value; OnPropertyChanged(); }
        }
        public string RelativePath
        {
            get => _relativePath;
            set { _relativePath = value; OnPropertyChanged(); }
        }
        public List<string> ClaimedBy
        {
            get => _claimedBy;
            set
            {
                _claimedBy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasConflict));
                OnPropertyChanged(nameof(ConflictText));
            }
        }
        public bool IsNewFolder
        {
            get => _isNewFolder;
            set { _isNewFolder = value; OnPropertyChanged(); }
        }

        public bool HasConflict => _claimedBy.Count > 0;
        public string ConflictText => _claimedBy.Count > 0
            ? $"← [{string.Join(", ", _claimedBy)}] ⚠"
            : "";

        public ObservableCollection<GameDirNode> Children { get; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>Node in the right-pane mod-source tree.</summary>
    public class ModSourceNode : INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isDirectory;
        private string _sourcePath = "";
        private FileDeploymentEntry? _entry;
        private string _destinationDisplay = "";
        private bool _hasConflict;
        private bool _isExcluded;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public bool IsDirectory
        {
            get => _isDirectory;
            set { _isDirectory = value; OnPropertyChanged(); }
        }
        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }
        public FileDeploymentEntry? Entry
        {
            get => _entry;
            set { _entry = value; OnPropertyChanged(); }
        }
        public string DestinationDisplay
        {
            get => _destinationDisplay;
            set { _destinationDisplay = value; OnPropertyChanged(); }
        }
        public bool HasConflict
        {
            get => _hasConflict;
            set { _hasConflict = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusGlyph)); }
        }
        public bool IsExcluded
        {
            get => _isExcluded;
            set { _isExcluded = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusGlyph)); }
        }

        public string StatusGlyph => _isExcluded ? "—" : _hasConflict ? "⚠" : "✓";

        public ObservableCollection<ModSourceNode> Children { get; } = [];

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // ── Window ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two-pane deployment plan editor. Opens after archive extraction so the user
    /// can review and adjust file destinations before the mod is added to the list.
    /// Confirm saves the modified plan; Cancel aborts the install.
    /// </summary>
    public partial class PlanEditorWindow : TmmWindow
    {
        private readonly ModItem _mod;
        private readonly DeploymentPlan _plan;
        private readonly GameConfig _gameConfig;
        private readonly string _gameDir;
        private readonly string _planPath;
        private readonly ObservableCollection<GameDirNode> _gameDirRoots = [];
        private readonly ObservableCollection<ModSourceNode> _modSourceRoots = [];
        private readonly List<string> _newFolderRelPaths = [];
        private readonly Dictionary<string, List<string>> _conflictMap;

        // Drag state
        private Point _dragStart;
        private bool _isDragging;

        public PlanEditorWindow(
            ModItem mod,
            DeploymentPlan plan,
            GameConfig gameConfig,
            IEnumerable<ModItem> installedMods,
            string planPath)
        {
            InitializeComponent();

            _mod = mod;
            _plan = plan;
            _gameConfig = gameConfig;
            _gameDir = gameConfig.GameDirectory ?? "";
            _planPath = planPath;

            txtTitle.Text = $"PLAN EDITOR — {mod.Name}  [{gameConfig.GameName}]";

            _conflictMap = BuildConflictMap(mod, installedMods);
            RebuildGameDirTree();
            RebuildModSourceTree();

            tvGameDir.ItemsSource = _gameDirRoots;
            tvModFiles.ItemsSource = _modSourceRoots;

            UpdateFooter();
        }

        // ── Tree builders ──────────────────────────────────────────────────────

        private void RebuildGameDirTree()
        {
            _gameDirRoots.Clear();
            var nodeMap = new Dictionary<string, GameDirNode>(StringComparer.OrdinalIgnoreCase);

            // Always surface a "(game root)" node so a file can be dragged back to the root
            // even when no file currently routes there.
            GetOrCreateDirNode(nodeMap, "", _gameDirRoots);

            foreach (var entry in _plan.Files.Where(f => !f.Skip))
            {
                string destRel = string.IsNullOrEmpty(_gameDir)
                    ? entry.DestinationPath
                    : GetRelativeSafe(_gameDir, entry.DestinationPath);

                string? dirRel = Path.GetDirectoryName(destRel)?.Replace('/', '\\');
                string fileName = Path.GetFileName(destRel);

                var parentNode = GetOrCreateDirNode(nodeMap, dirRel ?? "", _gameDirRoots);

                var claimedBy = _conflictMap.TryGetValue(entry.DestinationPath, out var list)
                    ? list
                    : [];

                parentNode.Children.Add(new GameDirNode
                {
                    Name = fileName,
                    IsDirectory = false,
                    RelativePath = destRel,
                    ClaimedBy = claimedBy,
                });
            }

            // Add any user-created new folders
            foreach (var folderRel in _newFolderRelPaths)
            {
                GetOrCreateDirNode(nodeMap, folderRel, _gameDirRoots, isNewFolder: true);
            }
        }

        private static GameDirNode GetOrCreateDirNode(
            Dictionary<string, GameDirNode> map,
            string dirRelPath,
            ObservableCollection<GameDirNode> roots,
            bool isNewFolder = false)
        {
            string key = string.IsNullOrEmpty(dirRelPath) || dirRelPath == "." ? "" : dirRelPath;

            if (map.TryGetValue(key, out var existing))
                return existing;

            if (string.IsNullOrEmpty(key))
            {
                var root = new GameDirNode { Name = "(game root)", IsDirectory = true, RelativePath = "" };
                map[""] = root;
                roots.Add(root);
                return root;
            }

            var parts = key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            GameDirNode? parent = null;
            string cum = "";

            foreach (var part in parts)
            {
                cum = cum.Length == 0 ? part : cum + "\\" + part;
                if (!map.TryGetValue(cum, out var node))
                {
                    node = new GameDirNode
                    {
                        Name = part,
                        IsDirectory = true,
                        RelativePath = cum,
                        IsNewFolder = isNewFolder && cum == key,
                    };
                    map[cum] = node;
                    if (parent is null)
                        roots.Add(node);
                    else
                        parent.Children.Add(node);
                }
                parent = node;
            }

            return map[key];
        }

        private void RebuildModSourceTree()
        {
            _modSourceRoots.Clear();
            var dirMap = new Dictionary<string, ModSourceNode>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in _plan.Files)
            {
                string srcRel = string.IsNullOrEmpty(_mod.RawFolderPath)
                    ? Path.GetFileName(entry.SourcePath)
                    : GetRelativeSafe(_mod.RawFolderPath, entry.SourcePath);

                string? dirRel = Path.GetDirectoryName(srcRel)?.Replace('/', '\\');
                string fileName = Path.GetFileName(srcRel);

                var parentNode = GetOrCreateModDirNode(dirMap, dirRel ?? "", _modSourceRoots);

                bool hasConflict = !entry.Skip && _conflictMap.ContainsKey(entry.DestinationPath);

                parentNode.Children.Add(new ModSourceNode
                {
                    Name = fileName,
                    IsDirectory = false,
                    SourcePath = entry.SourcePath,
                    Entry = entry,
                    HasConflict = hasConflict,
                    IsExcluded = entry.Skip,
                    DestinationDisplay = GetDestinationDisplay(entry.DestinationPath),
                });
            }
        }

        private static ModSourceNode GetOrCreateModDirNode(
            Dictionary<string, ModSourceNode> map,
            string dirRelPath,
            ObservableCollection<ModSourceNode> roots)
        {
            string key = string.IsNullOrEmpty(dirRelPath) || dirRelPath == "." ? "" : dirRelPath;

            if (map.TryGetValue(key, out var existing))
                return existing;

            if (string.IsNullOrEmpty(key))
            {
                var root = new ModSourceNode { Name = "(root)", IsDirectory = true };
                map[""] = root;
                roots.Add(root);
                return root;
            }

            var parts = key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            ModSourceNode? parent = null;
            string cum = "";

            foreach (var part in parts)
            {
                cum = cum.Length == 0 ? part : cum + "\\" + part;
                if (!map.TryGetValue(cum, out var node))
                {
                    node = new ModSourceNode { Name = part, IsDirectory = true };
                    map[cum] = node;
                    if (parent is null)
                        roots.Add(node);
                    else
                        parent.Children.Add(node);
                }
                parent = node;
            }

            return map[key];
        }

        // ── Conflict detection ─────────────────────────────────────────────────

        private static Dictionary<string, List<string>> BuildConflictMap(
            ModItem currentMod,
            IEnumerable<ModItem> installedMods)
        {
            // Map EVERY destination claimed by another installed mod -> the mods that claim
            // it. Keyed by destination (not pre-filtered to the current plan) so that a file
            // dragged onto a new location is still conflict-checked after construction.
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var mod in installedMods)
            {
                if (string.Equals(mod.Name, currentMod.Name, StringComparison.OrdinalIgnoreCase))
                    continue;

                string planPath = Path.Combine(mod.RawFolderPath, "_tmm", "deployplan.json");
                if (!File.Exists(planPath)) continue;

                try
                {
                    var plan = JsonSerializer.Deserialize<DeploymentPlan>(
                        File.ReadAllText(planPath), JsonHelper.PrettyOptions);
                    if (plan is null) continue;

                    foreach (var file in plan.Files)
                    {
                        if (file.Skip) continue; // skipped files aren't deployed → no conflict
                        if (!result.TryGetValue(file.DestinationPath, out var names))
                        {
                            names = [];
                            result[file.DestinationPath] = names;
                        }
                        if (!names.Contains(mod.Name))
                            names.Add(mod.Name);
                    }
                }
                catch { /* skip malformed plans */ }
            }

            return result;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private string GetDestinationDisplay(string destPath)
        {
            if (string.IsNullOrEmpty(destPath)) return "(unknown)";
            if (string.IsNullOrEmpty(_gameDir)) return destPath;

            try
            {
                string rel = GetRelativeSafe(_gameDir, destPath);
                string? dir = Path.GetDirectoryName(rel);
                return string.IsNullOrEmpty(dir) ? "(root)" : dir.Replace('/', '\\') + "\\";
            }
            catch { return destPath; }
        }

        private static string GetRelativeSafe(string basePath, string targetPath)
        {
            try { return Path.GetRelativePath(basePath, targetPath); }
            catch { return targetPath; }
        }

        private void UpdateFooter()
        {
            int conflictCount = _plan.Files.Count(f =>
                !f.Skip && _conflictMap.ContainsKey(f.DestinationPath));

            if (conflictCount > 0)
            {
                txtFooter.Text = $"⚠  {conflictCount} conflict{(conflictCount == 1 ? "" : "s")}  —  drag file → game folder to reassign";
                txtFooter.Foreground = (Brush)FindResource("ConflictRedBrush");
            }
            else
            {
                txtFooter.Text = "No conflicts  —  drag file → game folder to reassign";
                txtFooter.Foreground = (Brush)FindResource("SubTextBrush");
            }
        }

        private static T? FindVisualAncestor<T>(DependencyObject? element) where T : DependencyObject
        {
            while (element is not null)
            {
                if (element is T t) return t;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private static TreeViewItem? HitTestTreeItem(TreeView tv, Point point)
        {
            var element = tv.InputHitTest(point) as DependencyObject;
            return FindVisualAncestor<TreeViewItem>(element);
        }

        // ── Drag-drop (right pane initiates, left pane receives) ───────────────

        private void TvModFiles_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _isDragging = false;
        }

        private void TvModFiles_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            var pos = e.GetPosition(null);
            var delta = _dragStart - pos;

            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var tvi = HitTestTreeItem(tvModFiles, e.GetPosition(tvModFiles));
            if (tvi?.DataContext is ModSourceNode { IsDirectory: false } node)
            {
                _isDragging = true;
                DragDrop.DoDragDrop(tvi, new DataObject(typeof(ModSourceNode), node), DragDropEffects.Move);
                _isDragging = false;
            }
        }

        private void TvGameDir_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (!e.Data.GetDataPresent(typeof(ModSourceNode)))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            var tvi = HitTestTreeItem(tvGameDir, e.GetPosition(tvGameDir));
            e.Effects = tvi?.DataContext is GameDirNode { IsDirectory: true }
                ? DragDropEffects.Move
                : DragDropEffects.None;
        }

        private void TvGameDir_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(ModSourceNode))) return;

            var tvi = HitTestTreeItem(tvGameDir, e.GetPosition(tvGameDir));
            if (tvi?.DataContext is not GameDirNode { IsDirectory: true } target) return;

            var node = (ModSourceNode)e.Data.GetData(typeof(ModSourceNode))!;
            if (node.Entry is null) return;

            string newDest = string.IsNullOrEmpty(target.RelativePath)
                ? Path.Combine(_gameDir, node.Name)
                : Path.Combine(_gameDir, target.RelativePath, node.Name);

            node.Entry.DestinationPath = Path.GetFullPath(newDest);
            node.DestinationDisplay = GetDestinationDisplay(node.Entry.DestinationPath);
            node.HasConflict = _conflictMap.ContainsKey(node.Entry.DestinationPath);

            RebuildGameDirTree();
            UpdateFooter();
        }

        // ── Context menu (right pane) ─────────────────────────────────────────

        private void TvModFiles_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var node = tvModFiles.SelectedItem as ModSourceNode;
            if (node is null || node.IsDirectory)
            {
                e.Handled = true;
                return;
            }
            menuExclude.Header = node.IsExcluded ? "Include in plan" : "Exclude from plan";
        }

        private void MenuExclude_Click(object sender, RoutedEventArgs e)
        {
            if (tvModFiles.SelectedItem is not ModSourceNode node || node.Entry is null) return;

            node.Entry.Skip = !node.Entry.Skip;
            node.IsExcluded = node.Entry.Skip;

            // Re-evaluate conflict state: an excluded file isn't deployed so it can't
            // conflict; re-including it re-checks its destination against other mods.
            node.HasConflict = !node.Entry.Skip
                && _conflictMap.ContainsKey(node.Entry.DestinationPath);

            RebuildGameDirTree();
            UpdateFooter();
        }

        // ── New folder ────────────────────────────────────────────────────────

        private void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            pnlNewFolder.Visibility = Visibility.Visible;
            btnNewFolder.Visibility = Visibility.Collapsed;
            txtNewFolderName.Clear();
            txtNewFolderName.Focus();
        }

        private void BtnAddFolderConfirm_Click(object sender, RoutedEventArgs e)
            => CommitNewFolder();

        private void BtnCancelNewFolder_Click(object sender, RoutedEventArgs e)
            => HideNewFolderPanel();

        private void TxtNewFolderName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) CommitNewFolder();
            else if (e.Key == Key.Escape) HideNewFolderPanel();
        }

        private void CommitNewFolder()
        {
            string raw = txtNewFolderName.Text.Trim().Trim('\\', '/').Replace('/', '\\');
            if (string.IsNullOrWhiteSpace(raw))
            {
                HideNewFolderPanel();
                return;
            }

            if (!_newFolderRelPaths.Contains(raw, StringComparer.OrdinalIgnoreCase))
            {
                _newFolderRelPaths.Add(raw);
                RebuildGameDirTree();
            }

            HideNewFolderPanel();
        }

        private void HideNewFolderPanel()
        {
            pnlNewFolder.Visibility = Visibility.Collapsed;
            btnNewFolder.Visibility = Visibility.Visible;
        }

        // ── Explorer quick-links ──────────────────────────────────────────────

        private void BtnOpenGameDir_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_gameDir) && Directory.Exists(_gameDir))
                ShellHelper.OpenFolder(_gameDir);
            else
                NotificationService.ShowInfo("Game directory is not set or does not exist.", "Plan Editor");
        }

        private void BtnOpenModDir_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_mod.RawFolderPath))
                ShellHelper.OpenFolder(_mod.RawFolderPath);
        }

        // ── Confirm / Cancel ─────────────────────────────────────────────────

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Register any new folders the user created
            foreach (var rel in _newFolderRelPaths)
            {
                string full = Path.Combine(_gameDir, rel);
                if (!_plan.Directories.Contains(full, StringComparer.OrdinalIgnoreCase))
                    _plan.Directories.Add(full);
            }

            try
            {
                string json = JsonSerializer.Serialize(_plan, JsonHelper.PrettyOptions);
                File.WriteAllText(_planPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the deployment plan:\n{ex.Message}",
                    "Plan Editor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}
