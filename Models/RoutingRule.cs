using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// A single mod-routing rule evaluated at deploy-time (first match wins).
    ///
    /// WHEN  ExtensionPattern matches the file  (AND optionally NameContains)
    /// IF    CheckSubdir exists in the game directory           (optional)
    /// THEN  route to Destination
    /// ELSE  route to FallbackDestination  (defaults to game root ".")
    ///
    /// Use ExtensionPattern = "*" as a catch-all for any unmatched file.
    /// </summary>
    public class RoutingRule : INotifyPropertyChanged
    {
        private string _ruleName = "";
        private string _extensionPattern = "*";
        private string? _nameContains;
        private string? _checkSubdir;
        private string _destination = ".";
        private string? _fallbackDestination;
        private bool _hasConflict;

        /// <summary>Optional user-readable label shown on the rule card.</summary>
        public string RuleName
        {
            get => _ruleName;
            set { _ruleName = value; Notify(); }
        }

        /// <summary>
        /// Extension to match: ".dll", ".esp", "*" or "any" for catch-all.
        /// Comparison is case-insensitive.
        /// </summary>
        public string ExtensionPattern
        {
            get => _extensionPattern;
            set { _extensionPattern = value; Notify(); }
        }

        /// <summary>
        /// Optional: filename must contain this substring (case-insensitive) for the rule to fire.
        /// Null or empty means match any filename with the right extension.
        /// </summary>
        public string? NameContains
        {
            get => _nameContains;
            set { _nameContains = value; Notify(); Notify(nameof(HasNameFilter)); }
        }

        /// <summary>
        /// Optional: sub-folder to probe for existence inside the game directory.
        /// When set, THEN/ELSE routing applies. When null/empty, THEN always applies.
        /// </summary>
        public string? CheckSubdir
        {
            get => _checkSubdir;
            set { _checkSubdir = value; Notify(); Notify(nameof(HasCondition)); }
        }

        /// <summary>Output sub-folder when no IF condition, or when IF condition is true.</summary>
        public string Destination
        {
            get => _destination;
            set { _destination = value; Notify(); }
        }

        /// <summary>Output sub-folder when IF condition is false. Null means game root ".".</summary>
        public string? FallbackDestination
        {
            get => _fallbackDestination;
            set { _fallbackDestination = value; Notify(); }
        }

        [JsonIgnore] public bool HasCondition  => !string.IsNullOrWhiteSpace(_checkSubdir);
        [JsonIgnore] public bool HasNameFilter => !string.IsNullOrWhiteSpace(_nameContains);

        [JsonIgnore]
        public bool HasConflict
        {
            get => _hasConflict;
            set { _hasConflict = value; Notify(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
