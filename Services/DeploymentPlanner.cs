using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TMM.Services
{
    // ---------------------------------------------------------------------
    // Result types
    // ---------------------------------------------------------------------

    /// <summary>Deployment plan produced for a single mod before files are copied.</summary>
    public class DeploymentPlan
    {
        /// <summary>Serialized plan schema version for future migration.</summary>
        public int PlanVersion { get; set; } = 1;

        public string ModName { get; set; } = string.Empty;

        /// <summary>Files that will be copied to the game directory.</summary>
        public List<FileDeploymentEntry> Files { get; set; } = new();

        /// <summary>Destination directories that should exist before deployment starts.</summary>
        public List<string> Directories { get; set; } = new();

        /// <summary>Warnings that require user attention before deployment proceeds.</summary>
        public List<DeploymentWarning> Warnings { get; set; } = new();

        /// <summary>True when all files have a resolved destination and no blocking conflicts.</summary>
        public bool IsReady => Warnings.TrueForAll(w => !w.IsBlocking);
    }

    /// <summary>Represents a single file -> destination mapping inside a deployment plan.</summary>
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

    // ---------------------------------------------------------------------
    // Planner
    // ---------------------------------------------------------------------

    /// <summary>
    /// Calculates a <see cref="DeploymentPlan"/> for a mod without touching the file system.
    /// Drives <see cref="RuleEngine"/> to match files and resolves special target-path tokens.
    /// </summary>
    public class DeploymentPlanner
    {
        private static readonly string[] CleoScriptExtensions = [".cs", ".cs4", ".cs5"];
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

            if (HasSymlinkedSource(modFolder))
            {
                plan.Warnings.Add(new DeploymentWarning
                {
                    Message = "TMM does not support symlinked mod sources.",
                    IsBlocking = true,
                    FilePath = modFolder,
                });
                return Task.FromResult(plan);
            }

            var sourceFiles = Directory.EnumerateFiles(modFolder, "*", SearchOption.AllDirectories)
                .Select(path => new SourceEntry(modFolder, path))
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sourceDirectories = Directory.EnumerateDirectories(modFolder, "*", SearchOption.AllDirectories)
                .Select(path => CreateSourceDirectoryEntry(modFolder, path))
                .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var fileLookup = sourceFiles
                .ToDictionary(entry => entry.RelativePath, entry => entry, StringComparer.OrdinalIgnoreCase);

            foreach (var file in sourceFiles)
            {
                ct.ThrowIfCancellationRequested();

                var entry = TryResolveFilePlan(mod, gameProfile, modFolder, sourceFiles, file, fileLookup, plan.Warnings);
                if (entry is not null)
                    plan.Files.Add(entry);
            }

            foreach (var directory in sourceDirectories)
            {
                ct.ThrowIfCancellationRequested();
                plan.Directories.Add(ResolveDirectoryDestination(mod, gameProfile, directory.RelativePath, gameProfile.GameDirectory));
            }

            return Task.FromResult(plan);
        }

        private FileDeploymentEntry? TryResolveFilePlan(
            ModItem mod,
            CustomGameProfile gameProfile,
            string modFolder,
            IReadOnlyList<SourceEntry> allFiles,
            SourceEntry file,
            IReadOnlyDictionary<string, SourceEntry> fileLookup,
            List<DeploymentWarning> warnings)
        {
            if (TryResolveOverlayDestination(mod, gameProfile, file, out string overlayDestination))
            {
                return new FileDeploymentEntry
                {
                    SourcePath = file.AbsolutePath,
                    DestinationPath = overlayDestination,
                };
            }

            if (file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) &&
                TryResolveIniCompanionDestination(mod, gameProfile, modFolder, file, allFiles, fileLookup, out string companionDestination, out RoutingRule? companionRule))
            {
                return new FileDeploymentEntry
                {
                    SourcePath = file.AbsolutePath,
                    DestinationPath = companionDestination,
                    AppliedRule = companionRule,
                };
            }

            var matches = _ruleEngine.FindMatchingRules(file.AbsolutePath, modFolder, gameProfile);

            var specific = matches.FindAll(r => !r.IsDefault);
            var defaults = matches.FindAll(r => r.IsDefault);

            RoutingRule? chosen = null;

            if (specific.Count > 1)
            {
                bool allAllowConflict = specific.TrueForAll(r => r.AllowConflict);
                if (allAllowConflict)
                {
                    chosen = _ruleEngine.ResolveConflict(specific);
                    warnings.Add(new DeploymentWarning
                    {
                        Message = $"Multiple rules match '{Path.GetFileName(file.AbsolutePath)}'; using '{chosen.Name}' (priority {chosen.Priority}).",
                        FilePath = file.AbsolutePath,
                        IsBlocking = false,
                    });
                }
                else
                {
                    warnings.Add(new DeploymentWarning
                    {
                        Message = $"Conflicting rules for '{Path.GetFileName(file.AbsolutePath)}' with no conflict resolution strategy. Manual override required.",
                        FilePath = file.AbsolutePath,
                        IsBlocking = true,
                    });
                    return null;
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
                ? ResolveChosenDestination(mod, gameProfile, file, chosen)
                : ResolveDefaultDestination(mod, gameProfile, file);

            // Proxy-DLL hint: known loader/proxy DLLs must sit beside the game exe (game root).
            // If routing placed this file inside a subdirectory, warn — don't block, as the user
            // may have intentionally placed it elsewhere.
            string fileName = Path.GetFileName(file.AbsolutePath);
            if (ProxyDllDetector.IsKnownProxy(fileName))
            {
                string destDir = Path.GetDirectoryName(destination) ?? gameProfile.GameDirectory;
                if (!destDir.Equals(gameProfile.GameDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    string rel = Path.GetRelativePath(gameProfile.GameDirectory, destDir);
                    warnings.Add(new DeploymentWarning
                    {
                        Message    = $"'{fileName}' is a proxy/loader DLL that usually needs to be in the game root, but is routed to '{rel}\\'. Move it to the game root if the loader does not activate.",
                        FilePath   = file.AbsolutePath,
                        IsBlocking = false,
                    });
                }
            }

            return new FileDeploymentEntry
            {
                SourcePath = file.AbsolutePath,
                DestinationPath = destination,
                AppliedRule = chosen,
            };
        }

        private static bool TryResolveOverlayDestination(
            ModItem mod,
            CustomGameProfile gameProfile,
            SourceEntry file,
            out string destination)
        {
            string? topLevel = file.TopLevelFolder;
            if (topLevel is not null &&
                gameProfile.OverlayFolders.Any(folder => folder.Equals(topLevel, StringComparison.OrdinalIgnoreCase)))
            {
                destination = ResolvePreservedRelativeDestination(mod, gameProfile.GameDirectory, file.RelativePath);
                return true;
            }

            destination = string.Empty;
            return false;
        }

        private bool TryResolveIniCompanionDestination(
            ModItem mod,
            CustomGameProfile gameProfile,
            string modFolder,
            SourceEntry iniFile,
            IReadOnlyList<SourceEntry> allFiles,
            IReadOnlyDictionary<string, SourceEntry> fileLookup,
            out string destination,
            out RoutingRule? appliedRule)
        {
            destination = string.Empty;
            appliedRule = null;

            string stem = iniFile.Stem;
            var familyFolders = GetCompanionFamilyFolders(gameProfile, iniFile.TopLevelFolder);

            SourceEntry? companion = allFiles
                .Where(entry => CleoScriptExtensions.Contains(entry.Extension, StringComparer.OrdinalIgnoreCase))
                .Where(entry => entry.Stem.Equals(stem, StringComparison.OrdinalIgnoreCase))
                .Where(entry => familyFolders.Count == 0 ||
                                entry.TopLevelFolder is null ||
                                familyFolders.Contains(entry.TopLevelFolder))
                .OrderByDescending(entry => string.Equals(entry.DirectoryRelativePath, iniFile.DirectoryRelativePath, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(entry => string.Equals(entry.TopLevelFolder, iniFile.TopLevelFolder, StringComparison.OrdinalIgnoreCase))
                .ThenBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (companion is null)
                return false;

            var companionPlan = TryResolveFilePlan(mod, gameProfile, modFolder, allFiles, companion, fileLookup, new List<DeploymentWarning>());
            if (companionPlan is null)
                return false;

            string companionDir = Path.GetDirectoryName(companionPlan.DestinationPath) ?? gameProfile.GameDirectory;
            destination = Path.GetFullPath(Path.Combine(companionDir, iniFile.Stem + ".ini"));
            appliedRule = companionPlan.AppliedRule;
            return true;
        }

        private string ResolveChosenDestination(ModItem mod, CustomGameProfile gameProfile, SourceEntry file, RoutingRule rule)
        {
            if (IsModloaderTarget(rule.TargetPath))
                return ResolveModloaderDestination(mod, gameProfile.GameDirectory, file.RelativePath);

            return ResolveTargetPath(rule.TargetPath, file.AbsolutePath, gameProfile.GameDirectory);
        }

        private string ResolveDefaultDestination(ModItem mod, CustomGameProfile gameProfile, SourceEntry file)
        {
            if (IsModloaderRelativePath(file.RelativePath))
                return ResolveModloaderDestination(mod, gameProfile.GameDirectory, file.RelativePath);

            return Path.GetFullPath(Path.Combine(gameProfile.GameDirectory, Path.GetFileName(file.AbsolutePath)));
        }

        private static string ResolveDirectoryDestination(ModItem mod, CustomGameProfile gameProfile, string relativePath, string gameDir)
        {
            if (IsModloaderRelativePath(relativePath))
                return ResolveModloaderDestination(mod, gameDir, relativePath);

            return Path.GetFullPath(Path.Combine(gameDir, NormalizeRelativePath(relativePath)));
        }

        private static string ResolvePreservedRelativeDestination(ModItem mod, string gameDir, string relativePath)
        {
            if (IsModloaderRelativePath(relativePath))
                return ResolveModloaderDestination(mod, gameDir, relativePath);

            return Path.GetFullPath(Path.Combine(gameDir, NormalizeRelativePath(relativePath)));
        }

        private static string ResolveModloaderDestination(ModItem mod, string gameDir, string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            var parts = SplitRelativePath(normalized);

            string modloaderRelativePath;
            if (parts.Length > 0 && parts[0].Equals("modloader", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(mod.GroupName))
                {
                    modloaderRelativePath = normalized;
                }
                else if (parts.Length > 1 && parts[1].Equals(mod.GroupName, StringComparison.OrdinalIgnoreCase))
                {
                    modloaderRelativePath = normalized;
                }
                else
                {
                    string remainder = parts.Length > 1
                        ? Path.Combine(parts.Skip(1).ToArray())
                        : string.Empty;
                    modloaderRelativePath = string.IsNullOrEmpty(remainder)
                        ? Path.Combine("modloader", mod.GroupName)
                        : Path.Combine("modloader", mod.GroupName, remainder);
                }
            }
            else
            {
                var segments = new List<string> { "modloader" };
                if (!string.IsNullOrWhiteSpace(mod.GroupName))
                    segments.Add(mod.GroupName!);
                segments.Add(mod.Name);
                if (!string.IsNullOrWhiteSpace(normalized))
                    segments.Add(normalized);
                modloaderRelativePath = Path.Combine(segments.ToArray());
            }

            return Path.GetFullPath(Path.Combine(gameDir, modloaderRelativePath));
        }

        // ---------------------------------------------------------------------
        // Token resolution
        // ---------------------------------------------------------------------

        /// <summary>
        /// Resolves special tokens in a TargetPath template and returns an absolute path.
        ///
        /// Supported tokens:
        /// - {gameRoot}   -> <paramref name="gameDir"/>
        /// - {scriptname} -> filename without extension
        /// </summary>
        private static string ResolveTargetPath(
            string targetPath,
            string filePath,
            string gameDir)
        {
            string scriptName = Path.GetFileNameWithoutExtension(filePath);
            string fileName = Path.GetFileName(filePath);

            string resolved = targetPath
                .Replace("{gameRoot}", gameDir, StringComparison.OrdinalIgnoreCase)
                .Replace("{scriptname}", scriptName, StringComparison.OrdinalIgnoreCase);

            if (!Path.IsPathRooted(resolved))
                resolved = Path.Combine(gameDir, resolved);

            if (!resolved.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                resolved = Path.Combine(resolved, fileName);

            return Path.GetFullPath(resolved);
        }

        private static bool IsModloaderTarget(string targetPath) =>
            targetPath.Equals("modloader", StringComparison.OrdinalIgnoreCase);

        private static bool IsModloaderRelativePath(string relativePath)
        {
            var parts = SplitRelativePath(relativePath);
            return parts.Length > 0 && parts[0].Equals("modloader", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSymlinkedSource(string modFolder)
        {
            if (IsReparsePoint(modFolder))
                return true;

            foreach (var path in Directory.EnumerateFileSystemEntries(modFolder, "*", SearchOption.AllDirectories))
            {
                if (IsReparsePoint(path))
                    return true;
            }

            return false;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return false;
            }
        }

        private static HashSet<string> GetCompanionFamilyFolders(CustomGameProfile profile, string? topLevelFolder)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(topLevelFolder))
                return set;

            foreach (var kvp in profile.CompanionSiblings)
            {
                if (kvp.Key.Equals(topLevelFolder, StringComparison.OrdinalIgnoreCase) ||
                    kvp.Value.Any(value => value.Equals(topLevelFolder, StringComparison.OrdinalIgnoreCase)))
                {
                    set.Add(kvp.Key);
                    foreach (var sibling in kvp.Value)
                        set.Add(sibling);
                    break;
                }
            }

            if (set.Count == 0)
                set.Add(topLevelFolder);

            return set;
        }

        private static string NormalizeRelativePath(string path) =>
            path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        private static string[] SplitRelativePath(string relativePath) =>
            NormalizeRelativePath(relativePath).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        private sealed record SourceEntry(
            string AbsolutePath,
            string RelativePath,
            string DirectoryRelativePath,
            string? TopLevelFolder,
            string Stem,
            string Extension)
        {
            public SourceEntry(string modFolder, string absolutePath)
                : this(
                    absolutePath,
                    NormalizeRelativePath(Path.GetRelativePath(modFolder, absolutePath)),
                    NormalizeRelativePath(Path.GetDirectoryName(Path.GetRelativePath(modFolder, absolutePath)) ?? string.Empty),
                    GetTopLevelFolder(Path.GetRelativePath(modFolder, absolutePath)),
                    Path.GetFileNameWithoutExtension(absolutePath),
                    Path.GetExtension(absolutePath))
            {
            }
        }

        private static SourceDirectoryEntry CreateSourceDirectoryEntry(string modFolder, string absolutePath) =>
            new(absolutePath, NormalizeRelativePath(Path.GetRelativePath(modFolder, absolutePath)));

        private sealed record SourceDirectoryEntry(string AbsolutePath, string RelativePath);

        private static string? GetTopLevelFolder(string relativePath)
        {
            var parts = SplitRelativePath(relativePath);
            return parts.Length > 1 ? parts[0] : null;
        }
    }
}
