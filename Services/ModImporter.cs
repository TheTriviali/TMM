using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TMM.Services
{
    /// <summary>
    /// Heuristically detects obvious mod files/folders inside an already-modded
    /// game directory and moves them into TMM-managed mod folders.
    /// </summary>
    public sealed class ModImporter
    {
        private static readonly HashSet<string> ImportableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asi", ".dll", ".cs", ".cs4", ".cs5", ".fxt", ".ini"
        };

        private sealed record ImportFile(string AbsolutePath, string RelativePath, string? FamilyKey, string CandidateKey, string? GroupName, bool LowConfidence);

        /// <summary>
        /// Scans a game directory and returns likely mod candidates.
        /// </summary>
        public Task<List<ModImportCandidate>> ScanAsync(
            string gameDir,
            CustomGameProfile profile,
            CancellationToken ct = default)
        {
            if (!Directory.Exists(gameDir))
                return Task.FromResult(new List<ModImportCandidate>());

            var importFiles = new List<ImportFile>();
            foreach (var file in Directory.EnumerateFiles(gameDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                string rel = Path.GetRelativePath(gameDir, file);
                if (rel.StartsWith("_tmm" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!IsInteresting(file))
                    continue;

                importFiles.Add(ClassifyFile(rel, file, profile));
            }

            var candidates = new Dictionary<string, ModImportCandidate>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in importFiles)
            {
                if (!candidates.TryGetValue(file.CandidateKey, out var candidate))
                {
                    candidate = new ModImportCandidate
                    {
                        Name = GetCandidateName(file.CandidateKey),
                        GroupName = file.GroupName,
                        SourceSummary = GetSourceSummary(file),
                        Warning = file.LowConfidence ? GetWarning(file) : null,
                    };
                    candidates[file.CandidateKey] = candidate;
                }

                candidate.FilePaths.Add(file.AbsolutePath);
                if (string.IsNullOrWhiteSpace(candidate.Warning) && file.LowConfidence)
                    candidate.Warning = GetWarning(file);
            }

            var result = candidates.Values
                .Where(c => c.FilePaths.Count > 0)
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Task.FromResult(result);
        }

        /// <summary>
        /// Moves selected candidates into ModsRaw and returns the newly created mod items.
        /// The caller is expected to sync mod metadata and then deploy to restore
        /// the install to its original state.
        /// </summary>
        public async Task<List<ModItem>> ImportAsync(
            BackendCore core,
            string gameKey,
            string gameDir,
            CustomGameProfile config,
            IEnumerable<ModImportCandidate> candidates,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(gameDir) || !Directory.Exists(gameDir))
                throw new InvalidOperationException("Game directory is missing.");

            var selected = candidates.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
                return [];

            await core.SeedBaselineAsync(gameKey, gameDir, ct);
            NotificationService.ShowVerbose($"Seeded baseline for {gameKey}", "Baseline");

            string modsRoot = Path.Combine(
                core.AppDataPath,
                GameRegistry.Instance.GetGameProfile(gameKey)?.RawFolderName ?? $"ModsRaw_{gameKey}");
            Directory.CreateDirectory(modsRoot);

            var operations = new List<(string Source, string Destination)>();
            var createdFolders = new List<string>();
            var importedMods = new List<ModItem>();

            foreach (var candidate in selected)
            {
                foreach (var sourceFile in candidate.FilePaths)
                {
                    if (!File.Exists(sourceFile))
                        throw new FileNotFoundException($"Import source missing: {sourceFile}", sourceFile);
                }
            }

            try
            {
                foreach (var candidate in selected)
                {
                    ct.ThrowIfCancellationRequested();

                    string modName = MakeUniqueModName(modsRoot, candidate.Name);
                    string destFolder = Path.Combine(modsRoot, modName);
                    Directory.CreateDirectory(destFolder);
                    createdFolders.Add(destFolder);

                    foreach (var sourceFile in candidate.FilePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                    {
                        ct.ThrowIfCancellationRequested();

                        string rel = Path.GetRelativePath(gameDir, sourceFile);
                        string destFile = Path.Combine(destFolder, rel);
                        string? destDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destDir))
                            Directory.CreateDirectory(destDir);
                        operations.Add((sourceFile, destFile));
                    }

                    importedMods.Add(new ModItem
                    {
                        Name = modName,
                        RawFolderPath = destFolder,
                        IsEnabled = true,
                        LoadOrder = importedMods.Count,
                        GroupName = candidate.GroupName,
                    });
                    NotificationService.ShowVerbose($"Importing: {modName} ({candidate.FilePaths.Count} files)", "Import");
                }

                foreach (var (source, dest) in operations)
                {
                    if (File.Exists(dest))
                        File.Delete(dest);
                    File.Move(source, dest);
                }

                CleanupEmptyAncestors(gameDir, operations.Select(op => op.Source));
                NotificationService.ShowVerbose($"Import complete: {importedMods.Count} mod(s) moved to {gameKey}", "Import");

                return importedMods;
            }
            catch
            {
                for (int i = operations.Count - 1; i >= 0; i--)
                {
                    var (source, dest) = operations[i];
                    try
                    {
                        if (File.Exists(dest))
                        {
                            string? sourceDir = Path.GetDirectoryName(source);
                            if (!string.IsNullOrEmpty(sourceDir))
                                Directory.CreateDirectory(sourceDir);
                            File.Move(dest, source, overwrite: true);
                        }
                    }
                    catch
                    {
                        // Best effort rollback.
                    }
                }

                foreach (var folder in createdFolders.OrderByDescending(p => p.Length))
                {
                    try
                    {
                        if (Directory.Exists(folder))
                            BackendCore.ForceDeleteDirectory(folder);
                    }
                    catch
                    {
                        // Best effort rollback.
                    }
                }

                throw;
            }
        }

        private static void CleanupEmptyAncestors(string gameDir, IEnumerable<string> movedFiles)
        {
            var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in movedFiles)
            {
                string? dir = Path.GetDirectoryName(file);
                while (!string.IsNullOrEmpty(dir) &&
                       !Path.GetFullPath(dir).Equals(Path.GetFullPath(gameDir), StringComparison.OrdinalIgnoreCase) &&
                       touched.Add(dir))
                {
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            Directory.Delete(dir, recursive: false);
                    }
                    catch
                    {
                        break;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }
        }

        private static bool IsInteresting(string path)
        {
            string ext = Path.GetExtension(path);
            return ImportableExtensions.Contains(ext);
        }

        private static ImportFile ClassifyFile(string relativePath, string absolutePath, CustomGameProfile profile)
        {
            var parts = SplitRelativePath(relativePath);
            string stem = Path.GetFileNameWithoutExtension(relativePath);

            if (parts.Length > 0 && parts[0].Equals("modloader", StringComparison.OrdinalIgnoreCase))
            {
                string candidateKey = GetModloaderCandidateKey(parts);
                string? group = GetModloaderGroupName(parts);
                return new ImportFile(absolutePath, relativePath, "modloader", candidateKey, group, LowConfidence: false);
            }

            string? familyKey = GetFamilyKey(parts, profile);
            if (familyKey is not null)
            {
                string candidateKey = $"{familyKey}\\{stem}";
                bool lowConfidence = parts.Length == 1 || IsLooseLocation(parts[0]);
                return new ImportFile(absolutePath, relativePath, familyKey, candidateKey, null, lowConfidence);
            }

            string looseKey = $"loose\\{stem}";
            return new ImportFile(absolutePath, relativePath, null, looseKey, null, LowConfidence: true);
        }

        private static string GetCandidateName(string key)
        {
            if (key.Contains('\\'))
                return key.Split('\\').Last();
            return key;
        }

        private static string GetSourceSummary(ImportFile file)
        {
            if (file.CandidateKey.StartsWith("modloader\\", StringComparison.OrdinalIgnoreCase))
            {
                var parts = SplitRelativePath(file.RelativePath);
                if (parts.Length >= 3)
                    return string.Join("\\", parts.Take(Math.Min(parts.Length, 3)));
                return "modloader";
            }

            if (file.FamilyKey is not null)
            {
                var dir = Path.GetDirectoryName(file.RelativePath);
                return string.IsNullOrEmpty(dir) ? "(root)" : dir.Replace('/', '\\');
            }

            return "(root)";
        }

        private static string? GetWarning(ImportFile file)
        {
            if (file.CandidateKey.StartsWith("loose\\", StringComparison.OrdinalIgnoreCase))
                return "Loose file in game root; review carefully.";

            if (file.LowConfidence)
                return "Unexpected location; confirm this should be imported.";

            return null;
        }

        private static string MakeUniqueModName(string modsRoot, string baseName)
        {
            string candidate = baseName;
            int index = 2;
            while (Directory.Exists(Path.Combine(modsRoot, candidate)))
                candidate = $"{baseName} ({index++})";
            return candidate;
        }

        private static string[] SplitRelativePath(string relativePath) =>
            relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        private static bool IsLooseLocation(string topLevelFolder) =>
            !(topLevelFolder.Equals("scripts", StringComparison.OrdinalIgnoreCase) ||
              topLevelFolder.Equals("cleo", StringComparison.OrdinalIgnoreCase) ||
              topLevelFolder.Equals("plugins", StringComparison.OrdinalIgnoreCase) ||
              topLevelFolder.Equals("CLEO_TEXT", StringComparison.OrdinalIgnoreCase) ||
              topLevelFolder.Equals("CLEO_FONTS", StringComparison.OrdinalIgnoreCase));

        private static string GetModloaderCandidateKey(string[] parts)
        {
            if (parts.Length >= 4)
                return $"modloader\\{parts[1]}\\{parts[2]}";
            if (parts.Length >= 2)
                return $"modloader\\{parts[1]}";
            return "modloader";
        }

        private static string? GetModloaderGroupName(string[] parts) =>
            parts.Length >= 4 ? parts[1] : null;

        private static string? GetFamilyKey(string[] parts, CustomGameProfile profile)
        {
            if (parts.Length == 0)
                return null;

            string top = parts[0];
            if (top.Equals("scripts", StringComparison.OrdinalIgnoreCase))
                return "cleo";
            if (top.Equals("cleo", StringComparison.OrdinalIgnoreCase))
                return "cleo";
            if (top.Equals("plugins", StringComparison.OrdinalIgnoreCase))
                return "plugins";

            if (profile.CompanionSiblings.TryGetValue("cleo", out var siblings) &&
                siblings.Any(s => s.Equals(top, StringComparison.OrdinalIgnoreCase)))
                return "cleo";

            return null;
        }
    }
}
