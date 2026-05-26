using System;
using System.Collections.Generic;
using System.Linq;

namespace TMM.Services
{
    /// <summary>
    /// Resolves the final 0–255 load order for a list of mods.
    ///
    /// Algorithm:
    /// 1. Topological sort respecting LoadAfter / LoadBefore relationships.
    ///    Cycles are broken by preserving the original list order.
    /// 2. Within the sorted sequence, mods biased Lower move to the front,
    ///    mods biased Higher move to the back, and unbiased mods fill the middle.
    /// 3. FinalLoadOrder values are evenly spread across 0–255.
    /// </summary>
    public class LoadOrderResolver
    {
        /// <summary>
        /// Assigns <see cref="ModItem.FinalLoadOrder"/> to every mod in <paramref name="mods"/>.
        /// Mods that are not enabled are still assigned a position (they are skipped at deploy time).
        /// </summary>
        public void ResolveFinalLoadOrders(List<ModItem> mods, CustomGameProfile? gameProfile = null)
        {
            if (mods.Count == 0)
                return;

            var sorted = TopologicalSort(mods);
            sorted = ApplyBiasGrouping(sorted);

            int count = sorted.Count;
            int step = count == 1 ? 0 : 255 / (count - 1);

            for (int i = 0; i < count; i++)
                sorted[i].FinalLoadOrder = Math.Min(255, i * step);
        }

        // ── Topological sort ───────────────────────────────────────────────────────

        /// <summary>
        /// Produces a topologically ordered list that satisfies LoadAfter/LoadBefore
        /// constraints. Mods not referenced by any constraint retain their original order.
        /// </summary>
        private static List<ModItem> TopologicalSort(List<ModItem> mods)
        {
            // Build a name → index map for fast lookup
            var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < mods.Count; i++)
                index[mods[i].Name] = i;

            // adjacency[i] = list of indices that must come AFTER mods[i]
            var adjacency = new List<List<int>>(mods.Count);
            for (int i = 0; i < mods.Count; i++)
                adjacency.Add(new List<int>());

            int[] inDegree = new int[mods.Count];

            for (int i = 0; i < mods.Count; i++)
            {
                var mod = mods[i];

                // LoadAfter: mod[i] must come after mod named LoadAfter
                if (!string.IsNullOrEmpty(mod.LoadAfter) && index.TryGetValue(mod.LoadAfter, out int afterIdx))
                {
                    adjacency[afterIdx].Add(i);
                    inDegree[i]++;
                }

                // LoadBefore: mod[i] must come before mod named LoadBefore
                if (!string.IsNullOrEmpty(mod.LoadBefore) && index.TryGetValue(mod.LoadBefore, out int beforeIdx))
                {
                    adjacency[i].Add(beforeIdx);
                    inDegree[beforeIdx]++;
                }
            }

            // Kahn's algorithm — use a queue ordered by original position to preserve stability
            var queue = new SortedSet<int>(
                Enumerable.Range(0, mods.Count).Where(i => inDegree[i] == 0));

            var result = new List<ModItem>(mods.Count);

            while (queue.Count > 0)
            {
                int current = queue.Min;
                queue.Remove(current);
                result.Add(mods[current]);

                foreach (int neighbor in adjacency[current])
                {
                    if (--inDegree[neighbor] == 0)
                        queue.Add(neighbor);
                }
            }

            // If a cycle was detected, append remaining mods in original order
            if (result.Count < mods.Count)
            {
                var emitted = new HashSet<ModItem>(result);
                foreach (var mod in mods)
                    if (!emitted.Contains(mod))
                        result.Add(mod);
            }

            return result;
        }

        // ── Bias grouping ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reorders the topologically sorted list so that:
        /// - Lower-biased mods appear first (lowest indices)
        /// - Higher-biased mods appear last (highest indices)
        /// - None-biased mods fill the middle
        /// Order within each group is preserved.
        /// </summary>
        private static List<ModItem> ApplyBiasGrouping(List<ModItem> sorted) =>
            sorted.Where(m => m.LoadOrderBias == LoadOrderBias.Lower)
                .Concat(sorted.Where(m => m.LoadOrderBias == LoadOrderBias.None))
                .Concat(sorted.Where(m => m.LoadOrderBias == LoadOrderBias.Higher))
                .ToList();
    }
}
