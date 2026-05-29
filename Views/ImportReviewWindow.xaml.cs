using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TMM
{
    // ── Private row VM for the right-pane file list ──────────────────────────

    internal sealed class ImportFileRow : INotifyPropertyChanged
    {
        public string AbsolutePath { get; }
        public string RelativePath { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set { _isChecked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked))); }
        }

        public ImportFileRow(string absPath, string relPath)
        {
            AbsolutePath = absPath;
            RelativePath = relPath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    // ── Window ────────────────────────────────────────────────────────────────

    public partial class ImportReviewWindow : Window
    {
        private readonly string _gameDir;
        private readonly ObservableCollection<ModImportCandidate> _candidates;
        private readonly ObservableCollection<ImportFileRow> _fileRows = new();

        private ModImportCandidate? _focused;
        private bool _suppressNameUpdate;
        private bool _suppressGroupUpdate;

        public ImportReviewWindow(IEnumerable<ModImportCandidate> candidates, string gameDir)
        {
            _gameDir    = gameDir;
            _candidates = new ObservableCollection<ModImportCandidate>(candidates);

            InitializeComponent();

            listCandidates.ItemsSource = _candidates;
            fileList.ItemsSource       = _fileRows;

            _candidates.CollectionChanged += (_, _) => UpdateImportButton();
            foreach (var c in _candidates) WireCandidate(c);

            UpdateImportButton();
        }

        public List<ModImportCandidate> GetSelectedCandidates() =>
            _candidates.Where(c => c.IsSelected).ToList();

        // ── Candidate wiring ─────────────────────────────────────────────────

        private void WireCandidate(ModImportCandidate c)
        {
            c.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ModImportCandidate.IsSelected))
                    UpdateImportButton();
                if (c == _focused && e.PropertyName == nameof(ModImportCandidate.Name))
                    txtFilesHeader.Text = $"FILES IN \"{c.Name}\"";
            };
        }

        // ── Left pane events ──────────────────────────────────────────────────

        private void ListCandidates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var focused = listCandidates.SelectedItem as ModImportCandidate;
            if (focused != _focused)
                RebuildFileRows(focused);

            // Enable "Merge" only when 2+ candidates are selected
            btnMergeSelected.IsEnabled = listCandidates.SelectedItems.Count >= 2;
        }

        private void BtnSelectAllCandidates_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in _candidates) c.IsSelected = true;
            UpdateImportButton();
        }

        private void BtnDeselectAllCandidates_Click(object sender, RoutedEventArgs e)
        {
            foreach (var c in _candidates) c.IsSelected = false;
            UpdateImportButton();
        }

        private void BtnMergeSelected_Click(object sender, RoutedEventArgs e)
        {
            MergeSelected();
        }

        // ── Right pane events ─────────────────────────────────────────────────

        private void FileRow_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateActionButtons();
        }

        private void BtnSelectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _fileRows) r.IsChecked = true;
            UpdateActionButtons();
        }

        private void BtnDeselectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var r in _fileRows) r.IsChecked = false;
            UpdateActionButtons();
        }

        private void BtnSplitChecked_Click(object sender, RoutedEventArgs e)
        {
            SplitCheckedFiles();
        }

        private void BtnMoveChecked_Click(object sender, RoutedEventArgs e)
        {
            var others = _candidates.Where(c => c != _focused).ToList();
            var menu   = new ContextMenu { PlacementTarget = btnMoveChecked, Placement = PlacementMode.Bottom };

            if (others.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "(no other candidates)", IsEnabled = false });
            }
            else
            {
                foreach (var target in others)
                {
                    var item      = new MenuItem { Header = target.Name };
                    var captured  = target;
                    item.Click   += (_, _) => MoveCheckedFilesTo(captured);
                    menu.Items.Add(item);
                }
            }

            menu.IsOpen = true;
        }

        private void TxtCandidateName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressNameUpdate || _focused is null) return;
            _focused.Name = txtCandidateName.Text;
        }

        private void TxtCandidateGroup_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressGroupUpdate || _focused is null) return;
            _focused.GroupName = string.IsNullOrWhiteSpace(txtCandidateGroup.Text)
                ? null
                : txtCandidateGroup.Text;
        }

        // ── Core operations ───────────────────────────────────────────────────

        private void SplitCheckedFiles()
        {
            if (_focused is null) return;
            var checkedRows = _fileRows.Where(r => r.IsChecked).ToList();
            if (checkedRows.Count == 0) return;

            // Don't split if all files would move (would leave source with 0 files)
            if (checkedRows.Count == _focused.FilePaths.Count) return;

            var newCandidate = new ModImportCandidate
            {
                Name          = Path.GetFileNameWithoutExtension(checkedRows[0].AbsolutePath),
                IsSelected    = true,
                SourceSummary = _focused.SourceSummary,
            };

            foreach (var row in checkedRows)
            {
                _focused.FilePaths.Remove(row.AbsolutePath);
                newCandidate.FilePaths.Add(row.AbsolutePath);
            }

            _candidates.Add(newCandidate);
            WireCandidate(newCandidate);

            listCandidates.SelectedItem = newCandidate;
            RebuildFileRows(newCandidate);
            UpdateImportButton();
        }

        private void MoveCheckedFilesTo(ModImportCandidate target)
        {
            if (_focused is null || target == _focused) return;
            var checkedRows = _fileRows.Where(r => r.IsChecked).ToList();
            if (checkedRows.Count == 0) return;

            foreach (var row in checkedRows)
            {
                _focused.FilePaths.Remove(row.AbsolutePath);
                if (!target.FilePaths.Contains(row.AbsolutePath))
                    target.FilePaths.Add(row.AbsolutePath);
            }

            if (_focused.FilePaths.Count == 0)
                _candidates.Remove(_focused);

            var nextFocus = _focused.FilePaths.Count > 0 ? _focused : target;
            listCandidates.SelectedItem = nextFocus;
            RebuildFileRows(nextFocus);
            UpdateImportButton();
        }

        private void MergeSelected()
        {
            var selected = listCandidates.SelectedItems.Cast<ModImportCandidate>().ToList();
            if (selected.Count < 2) return;

            var primary = selected[0];
            foreach (var other in selected.Skip(1))
            {
                foreach (var path in other.FilePaths)
                {
                    if (!primary.FilePaths.Contains(path))
                        primary.FilePaths.Add(path);
                }
                _candidates.Remove(other);
            }

            listCandidates.SelectedItem = primary;
            RebuildFileRows(primary);
            UpdateImportButton();
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private void RebuildFileRows(ModImportCandidate? candidate)
        {
            _focused = candidate;
            _fileRows.Clear();

            bool hasFocus = candidate is not null;
            emptyRightPane.Visibility    = hasFocus ? Visibility.Collapsed : Visibility.Visible;
            fileScrollViewer.Visibility  = hasFocus ? Visibility.Visible   : Visibility.Collapsed;
            txtCandidateName.IsEnabled   = hasFocus;
            txtCandidateGroup.IsEnabled  = hasFocus;

            if (candidate is null)
            {
                txtFilesHeader.Text     = "FILES IN …";
                _suppressNameUpdate     = true;
                txtCandidateName.Text   = "";
                _suppressNameUpdate     = false;
                _suppressGroupUpdate    = true;
                txtCandidateGroup.Text  = "";
                _suppressGroupUpdate    = false;
                txtWarningBadge.Visibility = Visibility.Collapsed;
                UpdateActionButtons();
                return;
            }

            foreach (var absPath in candidate.FilePaths)
            {
                string rel;
                try   { rel = Path.GetRelativePath(_gameDir, absPath); }
                catch { rel = Path.GetFileName(absPath); }
                _fileRows.Add(new ImportFileRow(absPath, rel));
            }

            txtFilesHeader.Text = $"FILES IN \"{candidate.Name}\"";

            _suppressNameUpdate   = true;
            txtCandidateName.Text = candidate.Name;
            _suppressNameUpdate   = false;

            _suppressGroupUpdate   = true;
            txtCandidateGroup.Text = candidate.GroupName ?? "";
            _suppressGroupUpdate   = false;

            txtWarningBadge.Visibility = candidate.HasWarning ? Visibility.Visible : Visibility.Collapsed;
            if (candidate.HasWarning) txtWarningBadge.ToolTip = candidate.Warning;

            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool anyChecked = _fileRows.Any(r => r.IsChecked);
            bool notAll     = anyChecked && _fileRows.Any(r => !r.IsChecked);

            btnSplitChecked.IsEnabled = notAll;   // split only if ≥1 file would remain
            btnMoveChecked.IsEnabled  = anyChecked && _candidates.Count > 1;
        }

        private void UpdateImportButton()
        {
            int count            = _candidates.Count(c => c.IsSelected);
            btnImport.Content    = count == 0 ? "Import" : $"Import {count} mod{(count == 1 ? "" : "s")}";
            btnImport.IsEnabled  = count > 0;
        }

        // ── Footer ────────────────────────────────────────────────────────────

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
