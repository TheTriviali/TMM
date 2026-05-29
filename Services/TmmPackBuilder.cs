using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TMM.Services
{
    /// <summary>
    /// .tmmpack file = a portable archive bundling a loadout snapshot + the mod source
    /// folders it references + a manifest. Shareable so a friend can drop it into TMM
    /// and end up with the same set of mods enabled in the same order.
    ///
    /// Format (zip):
    ///   manifest.json       — schema below
    ///   loadout.json        — the ModLoadout record
    ///   mods/{ModName}/...  — each mod's RawFolderPath contents (minus _tmm metadata)
    /// </summary>
    public static class TmmPackBuilder
    {
        public const int CurrentVersion = 1;

        public sealed class Manifest
        {
            public int Version { get; set; } = CurrentVersion;
            public string GameKey { get; set; } = string.Empty;
            public string GameName { get; set; } = string.Empty;
            public string LoadoutName { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public List<string> ModNames { get; set; } = new();
            public string TmmVersion { get; set; } = "v0.1-alpha-8";
        }

        /// <summary>
        /// Bundles the named loadout and all its enabled mods into a .tmmpack at <paramref name="outputPath"/>.
        /// Returns the number of mods bundled.
        /// </summary>
        public static async Task<int> ExportAsync(BackendCore core, string gameKey, string gameName, string loadoutName, string outputPath)
        {
            var loadout = await core.ReadLoadoutAsync(gameKey, loadoutName)
                ?? throw new FileNotFoundException($"Loadout '{loadoutName}' not found.");

            var enabledModNames = loadout.ModStates
                .Where(kvp => kvp.Value.IsEnabled)
                .Select(kvp => kvp.Key)
                .ToList();

            var profile = GameRegistry.Instance.GetGameProfile(gameKey)
                ?? throw new InvalidOperationException($"Unknown game key '{gameKey}'.");
            string modsRoot = Path.Combine(core.AppDataPath, profile.RawFolderName);

            var manifest = new Manifest
            {
                GameKey = gameKey,
                GameName = gameName,
                LoadoutName = loadoutName,
                ModNames = enabledModNames,
            };

            if (File.Exists(outputPath)) File.Delete(outputPath);

            using var fs = File.Create(outputPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            await WriteJsonEntryAsync(zip, "manifest.json", manifest);
            await WriteJsonEntryAsync(zip, "loadout.json", loadout);

            int bundled = 0;
            foreach (var modName in enabledModNames)
            {
                string modFolder = Path.Combine(modsRoot, modName);
                if (!Directory.Exists(modFolder)) continue;

                foreach (var file in Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(modFolder, file);
                    if (rel.StartsWith("_tmm" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string entryName = $"mods/{modName}/{rel.Replace(Path.DirectorySeparatorChar, '/')}";
                    zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
                bundled++;
            }

            return bundled;
        }

        private static async Task WriteJsonEntryAsync<T>(ZipArchive zip, string entryName, T value)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await JsonSerializer.SerializeAsync(stream, value, JsonHelper.PrettyOptions);
        }
    }
}
