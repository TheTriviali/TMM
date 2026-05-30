using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TMM
{
    /// <summary>
    /// Summary of mod-state changes since the last successful deploy for a game.
    /// All counts reflect differences between the current list and the last
    /// <see cref="DeployManifest.ModNames"/> written to disk.
    /// </summary>
    public sealed record PendingChangesSummary(
        /// <summary>True when any of the counts below is non-zero.</summary>
        bool HasChanges,
        /// <summary>Number of mods enabled since the last deploy.</summary>
        int Enabled,
        /// <summary>Number of mods disabled since the last deploy.</summary>
        int Disabled,
        /// <summary>Number of mods whose load-order position changed.</summary>
        int Reordered,
        /// <summary>Number of mods installed or removed since the last deploy.</summary>
        int AddedRemoved
    );

    public partial class BackendCore
    {
        /// <summary>
        /// Returns a pending-changes summary by diffing the current mod list against
        /// the most recent <see cref="DeployManifest"/> for <paramref name="gameKey"/>.
        ///
        /// "Pending" is always relative to what is physically deployed on disk (the last
        /// manifest), regardless of any loadout switches. Read-only; no side effects.
        /// </summary>
        /// <param name="gameKey">The game to check.</param>
        public PendingChangesSummary PendingChanges(string gameKey)
        {
            var manifest = GetMostRecentManifest(gameKey);

            if (!Mods.TryGetValue(gameKey, out var currentMods))
                return new PendingChangesSummary(false, 0, 0, 0, 0);

            // ── Baseline: what was deployed ──────────────────────────────────
            // ModNames = the enabled mods in load order at the time of the last deploy.
            var deployedNames = manifest?.ModNames ?? [];

            // ── Current: enabled mods in load order ──────────────────────────
            var currentEnabled = currentMods
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.LoadOrder)
                .Select(m => m.Name)
                .ToList();

            var currentAllNames = currentMods.Select(m => m.Name).ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var deployedNamesSet = deployedNames.ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            // Added or removed: names present in one set but not the other.
            int addedRemoved =
                currentAllNames.Count(n => !deployedNamesSet.Contains(n)) +
                deployedNamesSet.Count(n => !currentAllNames.Contains(n));

            // Enabled/disabled: names that swapped enabled state.
            var deployedEnabledSet = deployedNames.ToHashSet(System.StringComparer.OrdinalIgnoreCase);
            var currentEnabledSet  = currentEnabled.ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            // Among mods present in both sets, count those whose enabled state changed.
            var commonNames = currentAllNames.Intersect(deployedNamesSet, System.StringComparer.OrdinalIgnoreCase);

            int enabled  = commonNames.Count(n => !deployedEnabledSet.Contains(n) && currentEnabledSet.Contains(n));
            int disabled = commonNames.Count(n =>  deployedEnabledSet.Contains(n) && !currentEnabledSet.Contains(n));

            // Reordered: among mods enabled in both states, count those at a different
            // position in the ordered list (positional diff, not absolute LoadOrder value).
            var sharedEnabled = deployedNames
                .Where(n => currentEnabledSet.Contains(n))
                .ToList();
            var currentEnabledFiltered = currentEnabled
                .Where(n => deployedEnabledSet.Contains(n))
                .ToList();

            int reordered = 0;
            int compareLen = System.Math.Min(sharedEnabled.Count, currentEnabledFiltered.Count);
            for (int i = 0; i < compareLen; i++)
            {
                if (!string.Equals(sharedEnabled[i], currentEnabledFiltered[i],
                        System.StringComparison.OrdinalIgnoreCase))
                    reordered++;
            }
            // Any length mismatch also counts (mods pushed off one end).
            reordered += System.Math.Abs(sharedEnabled.Count - currentEnabledFiltered.Count);

            bool hasChanges = enabled > 0 || disabled > 0 || reordered > 0 || addedRemoved > 0;
            return new PendingChangesSummary(hasChanges, enabled, disabled, reordered, addedRemoved);
        }

        /// <summary>
        /// Recomputes <see cref="AppSettings.CachedModsInstalledBytes"/> by summing the size
        /// of every file under each game's ModsRaw_* folder. Runs the disk walk off-thread
        /// and persists the result. Call after install/remove/deploy and on init — never on
        /// the Home render path (Home reads the cached value directly).
        /// </summary>
        public async Task RecomputeModsInstalledSizeAsync()
        {
            long total = await Task.Run(() =>
            {
                long sum = 0;
                foreach (var profile in GameRegistry.Instance.GetAllGames())
                {
                    string folder = Path.Combine(AppDataPath, profile.RawFolderName);
                    if (!Directory.Exists(folder)) continue;
                    try
                    {
                        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                        {
                            try { sum += new FileInfo(file).Length; } catch { /* locked/gone */ }
                        }
                    }
                    catch { /* skip protected dirs */ }
                }
                return sum;
            }).ConfigureAwait(false);

            Settings.CachedModsInstalledBytes = total;
            SaveSettings();
        }

        private DeployManifest? GetMostRecentManifest(string gameKey)
        {
            string gameBackupDir = Path.Combine(BackupsPath, gameKey);
            if (!Directory.Exists(gameBackupDir)) return null;

            string? latestDir = Directory.GetDirectories(gameBackupDir)
                .OrderByDescending(d => d)
                .FirstOrDefault();
            if (latestDir is null) return null;

            string mPath = Path.Combine(latestDir, "manifest.json");
            if (!File.Exists(mPath)) return null;

            try
            {
                return JsonSerializer.Deserialize<DeployManifest>(File.ReadAllText(mPath));
            }
            catch
            {
                return null;
            }
        }
    }
}
