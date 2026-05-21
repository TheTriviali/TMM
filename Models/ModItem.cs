// TABLE OF CONTENTS
// ─────────────────────────────────────────────────────────────────
//   ModItem CLASS  (INotifyPropertyChanged)
//     Name — notifying string property ........................... ~12
//     IsEnabled — notifying bool property ........................ ~19
//     LoadOrder — notifying int property ......................... ~26
//     RawFolderPath, PackedFilePath — plain string props ......... ~33
//     PropertyChanged event + OnPropertyChanged helper ........... ~36
// ─────────────────────────────────────────────────────────────────

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TGTAMM
{
    /// <summary>
    /// A single mod entry. Implements INotifyPropertyChanged so the UI
    /// reacts to renames/toggles/load-order changes automatically.
    /// </summary>
    public class ModItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        private bool _isEnabled = false;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        private int _loadOrder = 0;
        public int LoadOrder
        {
            get => _loadOrder;
            set { if (_loadOrder != value) { _loadOrder = value; OnPropertyChanged(); } }
        }

        public string RawFolderPath  { get; set; } = string.Empty;
        public string PackedFilePath { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
