// TABLE OF CONTENTS
// -----------------------------------------------------------------
//   ModItem CLASS  (INotifyPropertyChanged)
//     Name - notifying string property ........................... ~12
//     IsEnabled - notifying bool property ........................ ~19
//     LoadOrder - notifying int property ......................... ~26
//     RawFolderPath, PackedFilePath - plain string props ......... ~33
//     DetectedType, LoadAfter, LoadBefore - type/order props ..... ~36
//     LoadOrderBias, FinalLoadOrder - calculated props ........... ~40
//     PropertyChanged event + OnPropertyChanged helper ........... ~45
// -----------------------------------------------------------------

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// A single mod entry. Implements INotifyPropertyChanged so the UI
    /// reacts to renames/toggles/load-order changes automatically.
    /// Includes type detection, load order preferences, and final calculated order.
    /// </summary>
    public class ModItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        /// <summary>User-visible name of the mod.</summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        private bool _isEnabled = false;
        /// <summary>True if this mod is active and should be deployed.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        private int _loadOrder = 0;
        /// <summary>User-specified load order index (for relative ordering hints).</summary>
        public int LoadOrder
        {
            get => _loadOrder;
            set { if (_loadOrder != value) { _loadOrder = value; OnPropertyChanged(); } }
        }

        /// <summary>Path to the mod's folder in ModsRaw{gameKey}/.</summary>
        public string RawFolderPath { get; set; } = string.Empty;

        /// <summary>Path to the packed archive file (if imported from .zip/.rar/.7z).</summary>
        public string PackedFilePath { get; set; } = string.Empty;

        /// <summary>Optional group name used to nest the mod under a deployment group.</summary>
        public string? GroupName { get; set; }

        /// <summary>
        /// Auto-detected mod type based on file contents.
        /// Null if type could not be determined.
        /// Set during mod import/analysis.
        /// </summary>
        [JsonIgnore]
        public ModType? DetectedType { get; set; }

        /// <summary>
        /// User preference: load this mod after another mod by name.
        /// Used as a hint when resolving final load order.
        /// </summary>
        public string? LoadAfter { get; set; }

        /// <summary>
        /// User preference: load this mod before another mod by name.
        /// Used as a hint when resolving final load order.
        /// </summary>
        public string? LoadBefore { get; set; }

        /// <summary>User preference for load order bias (Lower/Higher/None).</summary>
        public LoadOrderBias LoadOrderBias { get; set; } = LoadOrderBias.None;

        /// <summary>
        /// Final calculated load order (0–255) resolved at deploy time.
        /// Combines user preferences, mod type defaults, and routing rules.
        /// </summary>
        [JsonIgnore]
        public int FinalLoadOrder { get; set; } = 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
