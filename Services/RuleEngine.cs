using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TMM.Services
{
    /// <summary>
    /// Evaluates routing rules against a file path and mod folder context.
    /// Type-specific rules (from ModType) are tested before game-wide rules.
    /// </summary>
    public class RuleEngine
    {
        /// <summary>
        /// Returns all routing rules from <paramref name="gameProfile"/> that match
        /// <paramref name="filePath"/>. ModType rules are returned before game-wide rules.
        /// Within each group, ordering preserves declaration order (caller resolves conflicts).
        /// </summary>
        /// <param name="filePath">Absolute path to the individual file being evaluated.</param>
        /// <param name="modFolderPath">Root folder of the mod (used for HasFolder/FolderCount/FileCount).</param>
        /// <param name="gameProfile">Game configuration supplying ModTypes and RoutingRules.</param>
        public List<RoutingRule> FindMatchingRules(
            string filePath,
            string modFolderPath,
            CustomGameProfile gameProfile)
        {
            var typeRules = gameProfile.ModTypes
                .SelectMany(m => m.RoutingRules)
                .Where(r => RuleMatches(r, filePath, modFolderPath))
                .ToList();

            var gameRules = gameProfile.RoutingRules
                .Where(r => RuleMatches(r, filePath, modFolderPath))
                .ToList();

            typeRules.AddRange(gameRules);
            return typeRules;
        }

        /// <summary>
        /// Resolves a single rule from a set of candidates. The highest-priority rule wins;
        /// ties are broken by declaration order (first in list).
        /// </summary>
        public RoutingRule ResolveConflict(List<RoutingRule> candidates)
            => candidates.OrderByDescending(r => r.Priority).First();

        // ── Private helpers ────────────────────────────────────────────────────────

        private bool RuleMatches(RoutingRule rule, string filePath, string modFolderPath)
        {
            // Empty condition list = catch-all (always matches)
            if (rule.Conditions.Count == 0)
                return true;

            return EvaluateConditionChain(rule.Conditions, filePath, modFolderPath);
        }

        /// <summary>
        /// Evaluates a condition chain using the Logic operator on each condition.
        /// Each condition's Logic value describes how it combines with the *next* condition,
        /// so evaluation proceeds left-to-right with short-circuiting.
        /// </summary>
        private bool EvaluateConditionChain(
            List<Condition> conditions,
            string filePath,
            string modFolderPath)
        {
            if (conditions.Count == 0)
                return true;

            bool result = EvaluateCondition(conditions[0], filePath, modFolderPath);

            for (int i = 0; i < conditions.Count - 1; i++)
            {
                var logic = conditions[i].Logic;
                bool next = EvaluateCondition(conditions[i + 1], filePath, modFolderPath);

                result = logic == LogicOperator.AND
                    ? result && next
                    : result || next;
            }

            return result;
        }

        private bool EvaluateCondition(Condition cond, string filePath, string modFolderPath)
        {
            return cond.Type switch
            {
                ConditionType.FileExtension  => EvalFileExtension(cond, filePath),
                ConditionType.HasFolder      => EvalHasFolder(cond, modFolderPath),
                ConditionType.FolderCount    => EvalFolderCount(cond, modFolderPath),
                ConditionType.FileCount      => EvalFileCount(cond, modFolderPath),
                ConditionType.PathContains   => EvalPathContains(cond, filePath, modFolderPath),
                ConditionType.FilenameMatches => EvalFilenameMatches(cond, filePath),
                _ => false,
            };
        }

        private static bool EvalFileExtension(Condition cond, string filePath)
        {
            string ext = Path.GetExtension(filePath);
            return ApplyStringOperator(cond.Operator, ext, cond.Value, StringComparison.OrdinalIgnoreCase);
        }

        private static bool EvalHasFolder(Condition cond, string modFolderPath)
        {
            if (string.IsNullOrEmpty(modFolderPath))
                return false;

            // Operator.Is = folder exists; IsNot = folder absent
            bool exists = Directory.Exists(Path.Combine(modFolderPath, cond.Value));
            return cond.Operator == ConditionOperator.IsNot ? !exists : exists;
        }

        private static bool EvalFolderCount(Condition cond, string modFolderPath)
        {
            int count = string.IsNullOrEmpty(modFolderPath) || !Directory.Exists(modFolderPath)
                ? 0
                : Directory.GetDirectories(modFolderPath).Length;

            return ApplyNumericOperator(cond.Operator, count, cond.Value);
        }

        private static bool EvalFileCount(Condition cond, string modFolderPath)
        {
            int count = string.IsNullOrEmpty(modFolderPath) || !Directory.Exists(modFolderPath)
                ? 0
                : Directory.GetFiles(modFolderPath, "*", SearchOption.AllDirectories).Length;

            return ApplyNumericOperator(cond.Operator, count, cond.Value);
        }

        private static bool EvalPathContains(Condition cond, string filePath, string modFolderPath)
        {
            string value = NormalizePathFragment(cond.Value);

            if (ApplyStringOperator(cond.Operator, NormalizePathFragment(filePath), value, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(modFolderPath))
            {
                string relativePath = NormalizePathFragment(Path.GetRelativePath(modFolderPath, filePath));
                if (ApplyStringOperator(cond.Operator, relativePath, value, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool EvalFilenameMatches(Condition cond, string filePath)
        {
            string filename = Path.GetFileName(filePath);
            return ApplyStringOperator(cond.Operator, filename, cond.Value, StringComparison.OrdinalIgnoreCase);
        }

        // ── Operator dispatch ──────────────────────────────────────────────────────

        private static bool ApplyStringOperator(
            ConditionOperator op,
            string subject,
            string value,
            StringComparison comparison)
            => op switch
            {
                ConditionOperator.Is              => subject.Equals(value, comparison),
                ConditionOperator.IsNot           => !subject.Equals(value, comparison),
                ConditionOperator.Contains        => subject.Contains(value, comparison),
                ConditionOperator.DoesNotContain  => !subject.Contains(value, comparison),
                ConditionOperator.StartsWith      => subject.StartsWith(value, comparison),
                ConditionOperator.EndsWith        => subject.EndsWith(value, comparison),
                ConditionOperator.MatchesRegex    => Regex.IsMatch(subject, value, RegexOptions.IgnoreCase),
                _ => false,
            };

        private static bool ApplyNumericOperator(ConditionOperator op, int actual, string rawValue)
        {
            if (!int.TryParse(rawValue, out int target))
                return false;

            return op switch
            {
                ConditionOperator.Equals      => actual == target,
                ConditionOperator.GreaterThan => actual > target,
                ConditionOperator.LessThan    => actual < target,
                _ => false,
            };
        }

        private static string NormalizePathFragment(string path) =>
            path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }
}
