using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// BackendCore — named loadout persistence and application (Block D).
    /// </summary>
    public partial class BackendCore
    {
        /// <summary>
        /// True when <paramref name="name"/> is safe to use as a loadout filename:
        /// non-empty after trimming and free of path separators / characters that are
        /// illegal in a Windows filename.
        /// </summary>
        public static bool IsValidLoadoutName(string? name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public string GetLoadoutsPath(string gameKey)
        {
            string path = Path.Combine(AppDataPath, $"Loadouts_{gameKey}");
            bool created = !Directory.Exists(path);
            Directory.CreateDirectory(path);
            if (created) NotificationService.ShowVerbose($"Created Loadouts_{gameKey}", "Init");
            return path;
        }

        public bool LoadoutExists(string gameKey, string loadoutName) =>
            File.Exists(Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json"));

        public IEnumerable<string> ListLoadouts(string gameKey)
        {
            string dir = GetLoadoutsPath(gameKey);
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension)!;
        }

        public bool RenameLoadout(string gameKey, string oldName, string newName)
        {
            if (!IsValidLoadoutName(newName))
                throw new ArgumentException("Loadout name contains invalid characters.", nameof(newName));
            string dir = GetLoadoutsPath(gameKey);
            string src = Path.Combine(dir, $"{oldName}.json");
            string dst = Path.Combine(dir, $"{newName}.json");
            if (!File.Exists(src) || File.Exists(dst)) return false;
            File.Move(src, dst);
            return true;
        }

        public bool DeleteLoadout(string gameKey, string loadoutName)
        {
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public async Task<ModLoadout?> ReadLoadoutAsync(string gameKey, string loadoutName)
        {
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ModLoadout>(await File.ReadAllTextAsync(path));
        }

        /// <summary>
        /// Persists a named loadout to disk. Caller is responsible for prompting if
        /// the name already exists (use <see cref="LoadoutExists"/> first).
        /// </summary>
        public async Task SaveLoadoutAsync(string gameKey, string loadoutName, IEnumerable<ModItem> mods)
        {
            if (!IsValidLoadoutName(loadoutName))
                throw new ArgumentException("Loadout name contains invalid characters.", nameof(loadoutName));
            var loadout = new ModLoadout { Name = loadoutName };
            foreach (var m in mods)
                loadout.ModStates[m.Name] = new ModLoadout.LoadoutModState(m.IsEnabled, m.LoadOrder);
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(loadout, JsonHelper.PrettyOptions));
            Logger.Info($"Saved loadout '{loadoutName}' ({loadout.ModStates.Count} mods) for {gameKey}");
        }

        /// <summary>
        /// Persists an already-built <see cref="ModLoadout"/> verbatim (used by .tmmpack import,
        /// which carries its own enabled-state + order map). Uses <see cref="ModLoadout.Name"/>
        /// as the filename.
        /// </summary>
        public async Task SaveLoadoutAsync(string gameKey, ModLoadout loadout)
        {
            if (!IsValidLoadoutName(loadout.Name))
                throw new ArgumentException("Loadout name contains invalid characters.", nameof(loadout));
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadout.Name}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(loadout, JsonHelper.PrettyOptions));
            Logger.Info($"Saved loadout '{loadout.Name}' ({loadout.ModStates.Count} mods) for {gameKey}");
        }

        public async Task ApplyLoadoutAsync(string gameKey, string loadoutName, ObservableCollection<ModItem> currentMods)
        {
            var loadout = await ReadLoadoutAsync(gameKey, loadoutName);
            if (loadout is null) return;

            foreach (var mod in currentMods)
            {
                if (loadout.ModStates.TryGetValue(mod.Name, out var state))
                {
                    mod.IsEnabled = state.IsEnabled;
                    mod.LoadOrder = state.LoadOrder;
                }
                else
                {
                    mod.IsEnabled = false;
                }
            }

            var sorted = currentMods.OrderBy(m => m.LoadOrder).ToList();
            currentMods.Clear();
            foreach (var m in sorted) currentMods.Add(m);
        }
    }
}
