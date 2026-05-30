using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TMM.Services
{
    /// <summary>
    /// One mod that writes to a contested destination path, plus the load order
    /// used to break the tie. Higher <see cref="LoadOrder"/> wins by default.
    /// </summary>
    public sealed class ConflictParticipant
    {
        /// <summary>Display name of the mod writing to the destination.</summary>
        public string ModName { get; set; } = string.Empty;

        /// <summary>Load order of the mod; the highest value is the default winner.</summary>
        public int LoadOrder { get; set; }

        /// <summary>Absolute source file this mod would copy to the contested destination.</summary>
        public string SourcePath { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single destination path that two or more enabled mods both write to.
    /// Surfaced to the user so they can override the default highest-load-order winner.
    /// </summary>
    public sealed class ConflictEntry
    {
        /// <summary>Absolute destination path inside the game directory contested by multiple mods.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>Every mod that writes to <see cref="DestinationPath"/>.</summary>
        public List<ConflictParticipant> Participants { get; set; } = new();
    }

    /// <summary>
    /// A single clash detail for one destination/DLL that a mod contests with another.
    /// </summary>
    public sealed class ModConflictClash
    {
        /// <summary>
        /// The contested destination path (file conflict) or DLL filename (proxy conflict).
        /// </summary>
        public string Destination { get; set; } = string.Empty;

        /// <summary>Name of the mod that wins this clash by load order.</summary>
        public string WinnerModName { get; set; } = string.Empty;

        /// <summary>True if the mod that owns this summary is the winner for this clash.</summary>
        public bool ThisModWins { get; set; }
    }

    /// <summary>
    /// Aggregated conflict data for a single mod: how many destinations it overwrites
    /// (wins by load order) and is overwritten at (loses), plus the detail list.
    /// Covers both file-destination and proxy-DLL conflicts.
    /// </summary>
    public sealed class ModConflictSummary
    {
        /// <summary>Display name of the mod this summary describes.</summary>
        public string ModName { get; set; } = string.Empty;

        /// <summary>Number of contested destinations where this mod is the winner (highest load order).</summary>
        public int OverwritesCount { get; set; }

        /// <summary>Number of contested destinations where this mod is overwritten by another.</summary>
        public int OverwrittenByCount { get; set; }

        /// <summary>Per-destination clash details for inline display.</summary>
        public List<ModConflictClash> Clashes { get; set; } = new();
    }

    /// <summary>
    /// Detects cross-mod file conflicts: distinct destination paths that more than
    /// one enabled mod's deployment plan writes to. Intra-mod conflicts are already
    /// resolved by <see cref="RuleEngine.ResolveConflict"/> at plan time; this analyzer
    /// only reports the cross-mod overlaps the user may want to arbitrate.
    /// </summary>
    public sealed class ConflictAnalyzer
    {
        /// <summary>
        /// Groups every non-skipped plan file by its destination path and returns the
        /// destinations claimed by two or more distinct mods.
        /// </summary>
        /// <param name="plans">The (mod, frozen plan) pairs about to be deployed.</param>
        public List<ConflictEntry> Analyze(List<(ModItem Mod, DeploymentPlan Plan)> plans)
        {
            // destination -> (modName -> participant). Inner dictionary dedups files
            // from the same mod targeting the same destination (counts as one writer).
            var byDestination = new Dictionary<string, Dictionary<string, ConflictParticipant>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var (mod, plan) in plans)
            {
                foreach (var entry in plan.Files)
                {
                    if (entry.Skip || string.IsNullOrEmpty(entry.DestinationPath))
                        continue;

                    if (!byDestination.TryGetValue(entry.DestinationPath, out var writers))
                    {
                        writers = new Dictionary<string, ConflictParticipant>(StringComparer.OrdinalIgnoreCase);
                        byDestination[entry.DestinationPath] = writers;
                    }

                    if (!writers.ContainsKey(mod.Name))
                    {
                        // Rank by the resolved FinalLoadOrder (the value the deploy actually
                        // uses to break ties), so the resolver's preselected default winner
                        // matches what a plain Deploy would do. ResolveFinalLoadOrders is run
                        // before the preview is built; FinalLoadOrder falls back to LoadOrder
                        // when unresolved (both default to 0).
                        writers[mod.Name] = new ConflictParticipant
                        {
                            ModName    = mod.Name,
                            LoadOrder  = mod.FinalLoadOrder != 0 ? mod.FinalLoadOrder : mod.LoadOrder,
                            SourcePath = entry.SourcePath,
                        };
                    }
                }
            }

            return byDestination
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new ConflictEntry
                {
                    DestinationPath = kvp.Key,
                    Participants    = kvp.Value.Values
                        .OrderByDescending(p => p.LoadOrder)
                        .ToList(),
                })
                .OrderBy(c => c.DestinationPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Detects when two or more enabled mods each include the same proxy/loader DLL by filename
        /// (e.g. two mods both ship <c>dinput8.dll</c>). Unlike a normal file conflict, these may
        /// route to different destinations yet still collide at load time — only the one the OS
        /// actually loads will activate. Surfaces the issue so the user can disable one.
        /// </summary>
        /// <summary>
        /// Returns per-mod conflict summaries combining both file-destination conflicts
        /// (from <see cref="Analyze"/>) and proxy-DLL conflicts (from
        /// <see cref="AnalyzeProxyConflicts"/>). Only mods that participate in at least
        /// one conflict are included in the result.
        /// </summary>
        /// <param name="plans">The (mod, frozen plan) pairs about to be deployed.</param>
        public Dictionary<string, ModConflictSummary> AnalyzeByMod(
            List<(ModItem Mod, DeploymentPlan Plan)> plans)
        {
            var fileConflicts  = Analyze(plans);
            var proxyConflicts = AnalyzeProxyConflicts(plans);

            // Merge: all ConflictEntries from both sets. Proxy entries reuse DestinationPath
            // for the DLL filename — they're structurally identical for our purposes here.
            var all = fileConflicts.Concat(proxyConflicts).ToList();

            var result = new Dictionary<string, ModConflictSummary>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in all)
            {
                // The highest-load-order participant is the winner for this destination/DLL.
                var winner = entry.Participants.OrderByDescending(p => p.LoadOrder).First();

                foreach (var participant in entry.Participants)
                {
                    if (!result.TryGetValue(participant.ModName, out var summary))
                    {
                        summary = new ModConflictSummary { ModName = participant.ModName };
                        result[participant.ModName] = summary;
                    }

                    bool isWinner = string.Equals(participant.ModName, winner.ModName,
                        StringComparison.OrdinalIgnoreCase);

                    if (isWinner)
                    {
                        summary.OverwritesCount++;
                    }
                    else
                    {
                        summary.OverwrittenByCount++;
                    }

                    summary.Clashes.Add(new ModConflictClash
                    {
                        Destination    = entry.DestinationPath,
                        WinnerModName  = winner.ModName,
                        ThisModWins    = isWinner,
                    });
                }
            }

            return result;
        }

        /// <returns>
        /// One <see cref="ConflictEntry"/> per shared proxy DLL name. The
        /// <see cref="ConflictEntry.DestinationPath"/> field repurposed to hold the DLL filename.
        /// </returns>
        public List<ConflictEntry> AnalyzeProxyConflicts(List<(ModItem Mod, DeploymentPlan Plan)> plans)
        {
            var byProxy = new Dictionary<string, Dictionary<string, ConflictParticipant>>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var (mod, plan) in plans)
            {
                foreach (var entry in plan.Files)
                {
                    if (entry.Skip) continue;
                    string name = Path.GetFileName(entry.SourcePath);
                    if (!ProxyDllDetector.IsKnownProxy(name)) continue;

                    if (!byProxy.TryGetValue(name, out var writers))
                    {
                        writers = new Dictionary<string, ConflictParticipant>(StringComparer.OrdinalIgnoreCase);
                        byProxy[name] = writers;
                    }

                    if (!writers.ContainsKey(mod.Name))
                    {
                        writers[mod.Name] = new ConflictParticipant
                        {
                            ModName    = mod.Name,
                            LoadOrder  = mod.FinalLoadOrder != 0 ? mod.FinalLoadOrder : mod.LoadOrder,
                            SourcePath = entry.SourcePath,
                        };
                    }
                }
            }

            return byProxy
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => new ConflictEntry
                {
                    DestinationPath = kvp.Key,  // DLL filename, not a path
                    Participants    = kvp.Value.Values
                        .OrderByDescending(p => p.LoadOrder)
                        .ToList(),
                })
                .OrderBy(c => c.DestinationPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
