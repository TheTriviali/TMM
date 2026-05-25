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

        // ── Backward-compat properties (deprecated, for smooth migration) ──────
        // These adapt the new Conditions-based model to the old flat-property interface
        // used by CustomGameConfigWindow. Remove after CustomGameSetupWizard replaces it.

        private bool _hasConflict;
        private string? _fallbackDestination;

        /// <summary>Alias for Name. Use Name instead.</summary>
        [JsonIgnore]
        [Obsolete("Use Name")]
        public string RuleName { get => Name; set { Name = value; OnPropertyChanged(nameof(RuleName)); } }

        /// <summary>Extension pattern from the first FileExtension condition. Use Conditions instead.</summary>
        [JsonIgnore]
        [Obsolete("Use Conditions with ConditionType.FileExtension")]
        public string ExtensionPattern
        {
            get
            {
                var c = Conditions.FirstOrDefault(x => x.Type == ConditionType.FileExtension);
                return c?.Value ?? "*";
            }
            set
            {
                var c = Conditions.FirstOrDefault(x => x.Type == ConditionType.FileExtension);
                if (c is not null) c.Value = value;
                else Conditions.Insert(0, new Condition { Type = ConditionType.FileExtension, Operator = ConditionOperator.Is, Value = value });
                OnPropertyChanged(nameof(ExtensionPattern));
            }
        }

        /// <summary>Filename substring filter from PathContains condition. Use Conditions instead.</summary>
        [JsonIgnore]
        [Obsolete("Use Conditions with ConditionType.PathContains")]
        public string? NameContains
        {
            get => Conditions.FirstOrDefault(x => x.Type == ConditionType.PathContains)?.Value;
            set
            {
                var c = Conditions.FirstOrDefault(x => x.Type == ConditionType.PathContains);
                if (value is null) { if (c is not null) Conditions.Remove(c); }
                else if (c is not null) c.Value = value;
                else Conditions.Add(new Condition { Type = ConditionType.PathContains, Operator = ConditionOperator.Contains, Value = value });
                OnPropertyChanged(nameof(NameContains));
                OnPropertyChanged(nameof(HasCondition));
            }
        }

        /// <summary>HasFolder condition value. Use Conditions instead.</summary>
        [JsonIgnore]
        [Obsolete("Use Conditions with ConditionType.HasFolder")]
        public string? CheckSubdir
        {
            get => Conditions.FirstOrDefault(x => x.Type == ConditionType.HasFolder)?.Value;
            set
            {
                var c = Conditions.FirstOrDefault(x => x.Type == ConditionType.HasFolder);
                if (value is null) { if (c is not null) Conditions.Remove(c); }
                else if (c is not null) c.Value = value;
                else Conditions.Add(new Condition { Type = ConditionType.HasFolder, Operator = ConditionOperator.Is, Value = value });
                OnPropertyChanged(nameof(CheckSubdir));
                OnPropertyChanged(nameof(HasCondition));
            }
        }

        /// <summary>Alias for TargetPath. Use TargetPath instead.</summary>
        [JsonIgnore]
        [Obsolete("Use TargetPath")]
        public string Destination
        {
            get => TargetPath;
            set { TargetPath = value; OnPropertyChanged(nameof(Destination)); }
        }

        /// <summary>Legacy fallback destination when the HasFolder condition is not met. Not modeled in new system.</summary>
        [JsonIgnore]
        [Obsolete("Conditional fallback not directly supported in the new model")]
        public string? FallbackDestination
        {
            get => _fallbackDestination;
            set { _fallbackDestination = value; OnPropertyChanged(nameof(FallbackDestination)); }
        }

        /// <summary>True when a HasFolder condition is present. Use Conditions directly.</summary>
        [JsonIgnore]
        [Obsolete("Check Conditions for ConditionType.HasFolder directly")]
        public bool HasCondition => CheckSubdir is not null;

        /// <summary>UI-only flag: true when this rule conflicts with another rule in the list.</summary>
        [JsonIgnore]
        public bool HasConflict
        {
            get => _hasConflict;
            set { _hasConflict = value; OnPropertyChanged(nameof(HasConflict)); }
        }

        // ── INotifyPropertyChanged ─────────────────────────────────────────────

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
