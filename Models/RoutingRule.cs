using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>Load order bias when resolving final deployment order.</summary>
    public enum LoadOrderBias
    {
        /// <summary>Load earlier in the sequence when no specific rule applies.</summary>
        Lower,

        /// <summary>Load later in the sequence when no specific rule applies.</summary>
        Higher,

        /// <summary>Use defaults; no explicit preference.</summary>
        None,
    }

    /// <summary>
    /// Represents a routing rule that determines where files from a mod are deployed
    /// and their relative load order. Rules are evaluated in priority order; first match wins.
    /// Serializable to/from JSON for .tmmgame profile storage.
    ///
    /// Special target path tokens:
    /// - {gameRoot} → Game install directory
    /// - {scriptname} → Filename without extension (for nested structures)
    /// - Literal paths: "scripts/", "plugins/", "modloader/cleo/", etc.
    /// </summary>
    public class RoutingRule : INotifyPropertyChanged
    {
        // ── Core properties ────────────────────────────────────────────────────

        /// <summary>User-readable name for this rule (e.g., "ASI to scripts").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Conditions that must be satisfied for this rule to fire.
        /// Empty list means the rule always applies (catch-all).
        /// Conditions are combined using the Logic operator specified in each Condition.
        /// </summary>
        public List<Condition> Conditions { get; set; } = new();

        /// <summary>
        /// Destination path where files matching this rule are deployed.
        /// Examples: "scripts/", "{gameRoot}/plugins/", "modloader/cleo/{scriptname}/".
        /// Relative to the game install directory.
        /// </summary>
        public string TargetPath { get; set; } = string.Empty;

        /// <summary>
        /// Priority for conflict resolution (0–100).
        /// Higher priority wins when multiple rules could apply to the same file.
        /// </summary>
        public int Priority { get; set; } = 50;

        /// <summary>
        /// When true, user is prompted if a file would overwrite an existing file.
        /// When false, silently overwrite (use with caution).
        /// </summary>
        public bool AllowConflict { get; set; } = true;

        /// <summary>Load order bias for mods matching this rule.</summary>
        public LoadOrderBias LoadOrderBias { get; set; } = LoadOrderBias.None;

        /// <summary>
        /// When true, this is a fallback/catch-all rule applied to unmatched files.
        /// Only one default rule per game is recommended.
        /// </summary>
        public bool IsDefault { get; set; } = false;

        /// <summary>UI-only flag: true when this rule conflicts with another rule in the list.</summary>
        [JsonIgnore]
        public bool HasConflict { get; set; }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
