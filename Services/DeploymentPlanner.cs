using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TMM.Services
{
    // ── Result types ───────────────────────────────────────────────────────────────

    /// <summary>Deployment plan produced for a single mod before files are copied.</summary>
    public class DeploymentPlan
    {
        public string ModName { get; set; } = string.Empty;

        /// <summary>Files that will be copied to the game directory.</summary>
        public List<FileDeploymentEntry> Files { get; set; } = new();

        /// <summary>Warnings that require user attention before deployment proceeds.</summary>
        public List<DeploymentWarning> Warnings { get; set; } = new();

        /// <summary>True when all files have a resolved destination and no blocking conflicts.</summary>
        public bool IsReady => Warnings.TrueForAll(w => !w.IsBlocking);
    }

    /// <summary>Represents a single file → destination mapping inside a deployment plan.</summary>
    public class FileDeploymentEntry
    {
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>Absolute destination path inside the game's install directory.</summary>
        public string DestinationPath { get; set; } = string.Empty;

        /// <summary>The rule that produced this mapping. Null for default/fallback placement.</summary>
        public RoutingRule? AppliedRule { get; set; }

        /// <summary>Set by the user to skip this file during deployment.</summary>
        public bool Skip { get; set; } = false;
    }

    /// <summary>Warning emitted when a routing conflict or anomaly is detected.</summary>
    public class DeploymentWarning
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>File path that triggered the warning (may be null for mod-level warnings).</summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// When true the file has no destination assigned and deployment cannot proceed for it
        /// without user intervention. When false the warning is informational only.
        /// </summary>
        public bool IsBlocking { get; set; } = false;
    }

    // ── Planner ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates a <see cref="DeploymentPlan"/> for a mod without touching the file system.
    /// Drives <see cref="RuleEngine"/> to match files and resolves special target-path tokens.
    /// </summary>
    public class DeploymentPlanner
    {
        private readonly RuleEngine _ruleEngine;

        public DeploymentPlanner() : this(new RuleEngine()) { }

        public DeploymentPlanner(RuleEngine ruleEngine)
        {
            _ruleEngine = ruleEngine;
        }

        /// <summary>
        /// Enumerates every file under <paramref name="mod"/>'s folder and assigns a destination
        /// path using the routing rules defined in <paramref name="gameProfile"/>.
        /// </summary>
        /// <param name="mod">Mod whose files are being planned.</param>
        /// <param name="gameProfile">Game configuration supplying routing rules and install directory.</param>
        /// <param name="ct">Cancellation token for long-running enumerations.</param>
        public Task<DeploymentPlan> PlanDeploymentAsync(
            ModItem mod,
            CustomGameProfile gameProfile,
            CancellationToken ct = default)
        {
            var plan = new DeploymentPlan { ModName = mod.Name };
            string modFolder = mod.RawFolderPath;

            if (!Directory.Exists(modFolder))
            {
                plan.Warnings.Add(new DeploymentWarning
                {
                    Message = $"Mod folder not found: {modFolder}",
                    IsBlocking = true,
                });
                return Task.FromResult(plan);
            }

            foreach (string file in Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var matches = _ruleEngine.FindMatchingRules(file, modFolder, gameProfile);

                // Separate default/catch-all rules from specific ones
                var specific = matches.FindAll(r => !r.IsDefault);
                var defaults = matches.FindAll(r => r.IsDefault);

                RoutingRule? chosen = null;

                if (specific.Count > 1)
                {
                    bool allAllowConflict = specific.TrueForAll(r => r.AllowConflict);
                    if (allAllowConflict)
                    {
                        // Multiple rules all permit conflicts — pick highest priority, warn user
                        chosen = _ruleEngine.ResolveConflict(specific);
                        plan.Warnings.Add(new DeploymentWarning
                        {
                            Message = $"Multiple rules match '{Path.GetFileName(file)}'; using '{chosen.Name}' (priority {chosen.Priority}).",
                            FilePath = file,
                            IsBlocking = false,
                        });
                    }
                    else
                    {
                        // At least one rule does not allow conflict — block and ask user
                        plan.Warnings.Add(new DeploymentWarning
                        {
                            Message = $"Conflicting rules for '{Path.GetFileName(file)}' with no conflict resolution strategy. Manual override required.",
                            FilePath = file,
                            IsBlocking = true,
                        });
                        continue;
                    }
                }
                else if (specific.Count == 1)
                {
                    chosen = specific[0];
                }
                else if (defaults.Count > 0)
                {
                    chosen = _ruleEngine.ResolveConflict(defaults);
                }

                string destination = chosen is not null
                    ? ResolveTargetPath(chosen.TargetPath, file, modFolder, gameProfile.GameDirectory)
                    : Path.Combine(gameProfile.GameDirectory, Path.GetFileName(file));

                plan.Files.Add(new FileDeploymentEntry
                {
                    SourcePath = file,
                    DestinationPath = destination,
                    AppliedRule = chosen,
                });
            }

            return Task.FromResult(plan);
        }

        // ── Token resolution ───────────────────────────────────────────────────────

        /// <summary>
        /// Resolves special tokens in a TargetPath template and returns an absolute path.
        ///
        /// Supported tokens:
        /// - {gameRoot}     → <paramref name="gameDir"/>
        /// - {scriptname}   → filename without extension
        /// </summary>
        private static string ResolveTargetPath(
            string targetPath,
            string filePath,
            string modFolderPath,
            string gameDir)
        {
            string scriptName = Path.GetFileNameWithoutExtension(filePath);
            string fileName = Path.GetFileName(filePath);

            string resolved = targetPath
                .Replace("{gameRoot}", gameDir, StringComparison.OrdinalIgnoreCase)
                .Replace("{scriptname}", scriptName, StringComparison.OrdinalIgnoreCase);

            // If still a relative path, anchor to game directory
            if (!Path.IsPathRooted(resolved))
                resolved = Path.Combine(gameDir, resolved);

            // Append filename so destination is a full file path (not just directory)
            if (!resolved.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                resolved = Path.Combine(resolved, fileName);

            return Path.GetFullPath(resolved);
        }
    }
}
