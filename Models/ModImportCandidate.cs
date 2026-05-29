using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TMM
{
    /// <summary>
    /// One detected candidate during import-from-install analysis.
    /// Files are moved into a dedicated mod folder, then restored via the
    /// generated deploy plan.
    /// </summary>
    public sealed class ModImportCandidate : INotifyPropertyChanged
    {
        /// <summary>Stable identity across split/merge operations in the review window.</summary>
        public Guid Id { get; } = Guid.NewGuid();

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileCountDisplay)); }
        }

        private string? _groupName;
        public string? GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        public string SourceSummary { get; set; } = string.Empty;

        private string? _warning;
        public string? Warning
        {
            get => _warning;
            set { _warning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        private readonly ObservableCollection<string> _filePaths = new();

        /// <summary>Absolute paths of files belonging to this candidate.</summary>
        public ObservableCollection<string> FilePaths => _filePaths;

        public int FileCount => _filePaths.Count;

        /// <summary>Display string for the left-pane subline, e.g. "3 files · scripts\".</summary>
        public string FileCountDisplay =>
            $"{FileCount} {(FileCount == 1 ? "file" : "files")} · {SourceSummary}";

        public ModImportCandidate()
        {
            _filePaths.CollectionChanged += OnFilePathsChanged;
        }

        private void OnFilePathsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FileCount));
            OnPropertyChanged(nameof(FileCountDisplay));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
