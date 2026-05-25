using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// Represents a category of mods with shared routing rules and load order preferences.
    /// Examples: "ASI Plugin", "CLEO Script", "Texture Pack".
    /// Serializable to/from JSON for .tmmgame profile storage.
    /// </summary>
    public class ModType
    {
        /// <summary>User-readable name for this mod type (e.g., "ASI Plugin", "CLEO Script").</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>File extensions associated with this mod type (e.g., [".asi", ".dll"] for plugins).</summary>
        public List<string> FileExtensions { get; set; } = new();

        /// <summary>Routing rules specific to this mod type. Evaluated in order; first match wins.</summary>
        public List<RoutingRule> RoutingRules { get; set; } = new();

        /// <summary>Default load order preference when this mod type is detected.</summary>
        public LoadOrderBias DefaultBias { get; set; } = LoadOrderBias.None;

        /// <summary>
        /// When true, this mod type is used for auto-detection when multiple types match a file.
        /// Useful for resolving ambiguous extensions like ".dll" (plugin vs. dependency).
        /// </summary>
        public bool IsPrimary { get; set; } = false;
    }
}
