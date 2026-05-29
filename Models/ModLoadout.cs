using System;
using System.Collections.Generic;

namespace TMM
{
    /// <summary>
    /// A named snapshot of a game's mod enable-state + ordering.
    /// Persisted per-game under <c>Loadouts_{gameKey}/{Name}.json</c>.
    /// </summary>
    public class ModLoadout
    {
        public string Name { get; set; } = string.Empty;
        public DateTime SavedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Key: Mod Name (as found in ModsRaw_{key})
        /// Value: LoadoutModState containing enabled status and order
        /// </summary>
        public Dictionary<string, LoadoutModState> ModStates { get; set; } = new();

        public record LoadoutModState(bool IsEnabled, int LoadOrder);
    }
}
