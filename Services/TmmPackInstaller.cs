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
    /// Consumes a <c>.tmmpack</c> produced by <see cref="TmmPackBuilder"/> — extracts the
    /// bundled mods into the target game's <c>ModsRaw_{key}</c>, freezes a fresh deployment
    /// plan per mod, and reconstructs the loadout. The pack's own GameKey is ignored: the
    /// caller chooses which local game to import into (custom keys are per-machine).
    /// </summary>
    public static class TmmPackInstaller
    {
        public sealed class ImportResult
        {
            public string SourceGameName { get; init; } = "";
            public string LoadoutName { get; set; } = "";
            public int ModsImported { get; set; }
            public List<string> RenamedMods { get; } = new();
        }

        /// <summary>Reads just the manifest from a pack (for a pre-import confirmation prompt).</summary>
        public static TmmPackBuilder.Manifest ReadManifest(string packPath)
        {
            using var zip = ZipFile.OpenRead(packPath);
            var entry = zip.GetEntry("manifest.json")
                ?? throw new InvalidDataException("Not a valid .tmmpack: manifest.json missing.");
            using var stream = entry.Open();
            return JsonSerializer.Deserialize<TmmPackBuilder.Manifest>(stream, JsonHelper.PrettyOptions)
                ?? throw new InvalidDataException("Not a valid .tmmpack: manifest.json is empty.");
        }

        /// <summary>
        /// Imports <paramref name="packPath"/> into the game identified by <paramref name="targetGameKey"/>.
        /// Returns a summary; throws on an unreadable or forward-incompatible pack.
        /// </summary>
        public static async Task<ImportResult> ImportAsync(
            BackendCore core,
            string packPath,
            string targetGameKey)
        {
            using var zip = ZipFile.OpenRead(packPath);

            var manifest = ReadEntry<TmmPackBuilder.Manifest>(zip, "manifest.json")
                ?? throw new InvalidDataException("Not a valid .tmmpack: manifest.json missing.");
            if (manifest.Version > TmmPackBuilder.CurrentVersion)
                throw new InvalidDataException(
                    $"This pack was made by a newer TMM (format v{manifest.Version}). Update TMM to import it.");

            var loadout = ReadEntry<ModLoadout>(zip, "loadout.json") ?? new ModLoadout();

            var profile = GameRegistry.Instance.GetGameProfile(targetGameKey)
                ?? throw new InvalidOperationException($"Unknown game key '{targetGameKey}'.");
            string modsRoot = Path.Combine(core.AppDataPath, profile.RawFolderName);
            Directory.CreateDirectory(modsRoot);

            // Group "mods/{ModName}/..." entries by their owning mod folder.
            var modGroups = zip.Entries
                .Where(e => e.FullName.StartsWith("mods/", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(e.Name)) // skip directory entries
                .GroupBy(e => e.FullName.Split('/')[1], StringComparer.OrdinalIgnoreCase);

            var result = new ImportResult
            {
                SourceGameName = manifest.GameName,
                LoadoutName    = manifest.LoadoutName,
            };
            var renameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in modGroups)
            {
                string originalName = group.Key;
                string finalName = MakeUniqueModName(modsRoot, originalName);
                if (!finalName.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                {
                    renameMap[originalName] = finalName;
                    result.RenamedMods.Add($"{originalName} → {finalName}");
                }

                string modFolder = Path.Combine(modsRoot, finalName);
                string modFolderFull = Path.GetFullPath(modFolder) + Path.DirectorySeparatorChar;
                Directory.CreateDirectory(modFolder);

                foreach (var entry in group)
                {
                    // Strip the "mods/{ModName}/" prefix to get the in-mod relative path.
                    string rel = string.Join('/', entry.FullName.Split('/').Skip(2));
                    if (string.IsNullOrEmpty(rel)) continue;

                    string destPath = Path.GetFullPath(Path.Combine(modFolder, rel));
                    // Zip-slip guard: names come from another machine — never escape the mod folder.
                    if (!destPath.StartsWith(modFolderFull, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Unsafe path in pack rejected: {entry.FullName}");

                    string? destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

                    await using var src = entry.Open();
                    await using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await src.CopyToAsync(dst);
                }

                // Freeze a fresh plan for the imported mod (never trust a plan from the pack).
                await core.OnModAddedAsync(targetGameKey, finalName);
                result.ModsImported++;
            }

            // Reconstruct the loadout, remapping any mods that were renamed on collision.
            if (loadout.ModStates.Count > 0)
            {
                var remapped = new ModLoadout
                {
                    Name = MakeUniqueLoadoutName(core, targetGameKey,
                        string.IsNullOrWhiteSpace(loadout.Name) ? manifest.LoadoutName : loadout.Name),
                    SavedAt = DateTime.Now,
                };
                foreach (var (modName, state) in loadout.ModStates)
                {
                    string key = renameMap.TryGetValue(modName, out var renamed) ? renamed : modName;
                    remapped.ModStates[key] = state;
                }
                await core.SaveLoadoutAsync(targetGameKey, remapped);
                result.LoadoutName = remapped.Name;
            }

            return result;
        }

        private static T? ReadEntry<T>(ZipArchive zip, string entryName)
        {
            var entry = zip.GetEntry(entryName);
            if (entry is null) return default;
            using var stream = entry.Open();
            return JsonSerializer.Deserialize<T>(stream, JsonHelper.PrettyOptions);
        }

        private static string MakeUniqueModName(string modsRoot, string baseName)
        {
            string candidate = baseName;
            int i = 2;
            while (Directory.Exists(Path.Combine(modsRoot, candidate)))
                candidate = $"{baseName} ({i++})";
            return candidate;
        }

        private static string MakeUniqueLoadoutName(BackendCore core, string gameKey, string baseName)
        {
            string candidate = string.IsNullOrWhiteSpace(baseName) ? "Imported" : baseName;
            int i = 2;
            while (core.LoadoutExists(gameKey, candidate))
                candidate = $"{baseName} ({i++})";
            return candidate;
        }
    }
}
