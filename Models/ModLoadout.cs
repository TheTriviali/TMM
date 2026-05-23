using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>
    /// Represents a saved mod configuration (loadout):
    /// Which mods are enabled and their load order.
    /// </summary>
    public class ModLoadout
    {
        public string Name { get; set; } = "Loadout";
        public List<LoadoutEntry> Mods { get; set; } = new();

        [JsonIgnore]
        public bool IsDefault => Name == "Default";
    }

    public class LoadoutEntry
    {
        public string ModName { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public int LoadOrder { get; set; } = 0;
    }
}
