using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// BackendCore — deployment plan freeze/load and the deploy/rollback pipeline.
    /// </summary>
    public partial class BackendCore
    {
        private string GetDeploymentPlanPath(string gameKey, string modName)
        {
            var profile = GameRegistry.Instance.GetGameProfile(gameKey) ?? GameProfile.ByKey(gameKey);
            string rawFolderName = profile?.RawFolderName ?? $"ModsRaw{gameKey}";
            return Path.Combine(AppDataPath, rawFolderName, modName, "_tmm", "deployplan.json");
        }

        private static DeploymentPlan? LoadDeploymentPlan(string planPath)
        {
            if (!File.Exists(planPath)) return null;

            try
            {
                var plan = JsonSerializer.Deserialize<DeploymentPlan>(
                    File.ReadAllText(planPath),
                    JsonHelper.PrettyOptions);

                return plan is not null && plan.PlanVersion == 1 ? plan : null;
            }
            catch (JsonException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static async Task SaveDeploymentPlanAsync(
            string planPath,
            DeploymentPlan plan,
            CancellationToken ct = default)
        {
            string? directory = Path.GetDirectoryName(planPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(
                planPath,
                JsonSerializer.Serialize(plan, JsonHelper.PrettyOptions),
                ct);
        }

        /// <summary>
        /// Captures and persists a frozen deployment plan for a newly added mod.
        /// </summary>
        public async Task<DeploymentPlan> OnModAddedAsync(
            string gameKey,
            string modName,
            CancellationToken ct = default)
        {
            var config = GameRegistry.Instance.GetCustomGameConfig(gameKey);
            string planPath = GetDeploymentPlanPath(gameKey, modName);

            if (config is null)
            {
                Log($"[Plan:{gameKey}] WARN: cannot capture deployment plan for '{modName}' because the game configuration is unavailable.");
                return new DeploymentPlan { ModName = modName };
            }

            string? modFolder = Path.GetDirectoryName(Path.GetDirectoryName(planPath));
            if (string.IsNullOrEmpty(modFolder) || !Directory.Exists(modFolder))
            {
                Log($"[Plan:{gameKey}] WARN: cannot capture deployment plan for '{modName}' because the mod folder is missing.");
                return new DeploymentPlan { ModName = modName };
            }

            var mod = new ModItem
            {
                Name = modName,
                RawFolderPath = modFolder,
                IsEnabled = true,
            };

            var plan = await new DeploymentPlanner().PlanDeploymentAsync(mod, config, ct);
            try
            {
                await SaveDeploymentPlanAsync(planPath, plan, ct);
                Log($"[Plan:{gameKey}] Saved deployment plan for '{modName}' to {planPath}");
                NotificationService.ShowVerbose($"Plan frozen: {modName} ({plan.Files.Count} files)", "Plan");
            }
            catch (IOException ex)
            {
                Log($"[Plan:{gameKey}] WARN: failed to save deployment plan for '{modName}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Log($"[Plan:{gameKey}] WARN: failed to save deployment plan for '{modName}': {ex.Message}");
            }
            return plan;
        }

        /// <summary>
        /// Installs an archive file as a mod for the given game.
        /// Extracts the archive, creates mod metadata, and triggers deployment plan generation.
        /// Returns the created ModItem on success, or null on failure.
        /// </summary>
        public async Task<ModItem?> InstallArchiveForGameAsync(
            string gameKey,
            string archivePath,
            CancellationToken ct = default)
        {
            string modName = Path.GetFileNameWithoutExtension(archivePath);
            var profile = GameRegistry.Instance.GetGameProfile(gameKey) ?? GameProfile.ByKey(gameKey);

            if (profile is null)
            {
                Log($"[Install:{gameKey}] Failed to resolve game profile for key '{gameKey}'");
                return null;
            }

            string rawFolderName = profile.RawFolderName;
            string destFolder = Path.Combine(AppDataPath, rawFolderName, modName);

            try
            {
                // Create destination folder
                if (Directory.Exists(destFolder))
                    ForceDeleteDirectory(destFolder);
                Directory.CreateDirectory(destFolder);

                // Extract archive
                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext is ".zip" or ".rar" or ".7z")
                    await ExtractArchiveSafeAsync(archivePath, destFolder, ct);
                else
                    File.Copy(archivePath, Path.Combine(destFolder, Path.GetFileName(archivePath)), overwrite: true);

                // Create and persist mod metadata
                var item = new ModItem
                {
                    Name = modName,
                    IsEnabled = true,
                    LoadOrder = 0,
                    RawFolderPath = destFolder
                };

                // Write modinfo files
                string legacyPath = Path.Combine(destFolder, "modinfo.txt");
                File.WriteAllText(legacyPath, JsonSerializer.Serialize(item, JsonHelper.PrettyOptions));

                string sidecarDir = Path.Combine(destFolder, "_tmm");
                Directory.CreateDirectory(sidecarDir);
                File.WriteAllText(
                    Path.Combine(sidecarDir, "modinfo.json"),
                    JsonSerializer.Serialize(item, JsonHelper.PrettyOptions));

                // Generate deployment plan
                await OnModAddedAsync(gameKey, modName, ct);

                Log($"[Install:{gameKey}] Successfully installed '{modName}' from {Path.GetFileName(archivePath)}");
                return item;
            }
            catch (Exception ex)
            {
                Log($"[Install:{gameKey}] Failed to install '{modName}': {ex.Message}");
                // Cleanup on failure
                try { if (Directory.Exists(destFolder)) ForceDeleteDirectory(destFolder); } catch { }
                return null;
            }
        }

        /// <summary>
        /// Returns a persisted plan for deployment, falling back to live planning for legacy mods.
        /// </summary>
        public async Task<DeploymentPlan> GetDeploymentPlanAsync(
            string gameKey,
            ModItem mod,
            CustomGameProfile config,
            CancellationToken ct = default)
        {
            string planPath = GetDeploymentPlanPath(gameKey, mod.Name);
            var persisted = LoadDeploymentPlan(planPath);
            if (persisted is not null)
                return persisted;

            Log($"[Deploy:{gameKey}] WARN: missing or stale deployment plan for '{mod.Name}'; using live planning.");
            return await new DeploymentPlanner().PlanDeploymentAsync(mod, config, ct);
        }

        public async Task RollbackDeployAsync(
            DeployManifest manifest,
            IProgress<DeploymentProgress>? progress = null,
            CancellationToken ct = default)
        {
            int total = manifest.Entries.Count, done = 0;
            Log($"[Rollback:{manifest.GameKey}] Restoring {total} entries from {manifest.Timestamp}");
            NotificationService.ShowVerbose($"Rollback started: {manifest.GameKey} — {total} entries from {manifest.Timestamp}", "Rollback");

            foreach (var entry in manifest.Entries)
            {
                ct.ThrowIfCancellationRequested();
                string destFile = Path.Combine(manifest.GameDirectory, entry.RelativePath);
                bool restoredFromBaseline = false;

                if (_baselineSnapshots.TryGetEntry(manifest.GameKey, entry.RelativePath, out var baseline))
                {
                    if (baseline?.SnapshotFile is not null)
                    {
                        string baselinePath = _baselineSnapshots.GetSnapshotPath(manifest.GameKey, baseline.SnapshotFile);
                        if (File.Exists(baselinePath))
                        {
                            string? destDir = Path.GetDirectoryName(destFile);
                            if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                            try { File.SetAttributes(destFile, FileAttributes.Normal); } catch { }
                            await Task.Run(() => File.Copy(baselinePath, destFile, overwrite: true), ct);
                            restoredFromBaseline = true;
                        }
                        else
                        {
                            Log($"[Rollback:{manifest.GameKey}] WARN: baseline snapshot missing for {entry.RelativePath}; falling back to manifest backup.");
                        }
                    }
                    else
                    {
                        if (File.Exists(destFile))
                        {
                            try
                            {
                                File.SetAttributes(destFile, FileAttributes.Normal);
                                File.Delete(destFile);
                            }
                            catch { /* best effort */ }
                        }
                        restoredFromBaseline = true;
                    }
                }

                if (restoredFromBaseline)
                {
                    done++;
                    if (done % 10 == 0 || done == total)
                        progress?.Report(new($"Restoring {done}/{total}", done, total));
                    continue;
                }

                if (entry.BackupFilePath is not null && File.Exists(entry.BackupFilePath))
                {
                    // Restore the backed-up original.
                    string? destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    try { File.SetAttributes(destFile, FileAttributes.Normal); } catch { }
                    await Task.Run(() => File.Copy(entry.BackupFilePath, destFile, overwrite: true), ct);
                }
                else if (entry.BackupFilePath is null && File.Exists(destFile))
                {
                    // File was newly added by deploy — remove it on rollback.
                    try
                    {
                        File.SetAttributes(destFile, FileAttributes.Normal);
                        File.Delete(destFile);
                    }
                    catch { /* best effort */ }
                }

                done++;
                if (done % 10 == 0 || done == total)
                    progress?.Report(new($"Restoring {done}/{total}", done, total));
            }

            if (manifest.Directories is { Count: > 0 })
            {
                foreach (var dir in manifest.Directories.OrderByDescending(d => d.Length))
                {
                    ct.ThrowIfCancellationRequested();
                    string targetDir = Path.Combine(manifest.GameDirectory, dir);
                    if (!Directory.Exists(targetDir))
                        continue;

                    try
                    {
                        if (!Directory.EnumerateFileSystemEntries(targetDir).Any())
                        {
                            Directory.Delete(targetDir, recursive: false);
                        }
                    }
                    catch
                    {
                        // Best effort. A directory may be shared with another mod or still in use.
                    }
                }
            }

            Log($"[Rollback:{manifest.GameKey}] Done.");
            NotificationService.ShowVerbose($"Rollback complete: {manifest.GameKey} — {total} files restored", "Rollback");
        }

        // ==========================================================
        // DEPLOYMENT  (direct to game directory, with backup)
        // ==========================================================

        /// <summary>
        /// Deploy enabled mods for a custom game directly to the game directory.
        /// RoutingRules in the config determine which subfolder each file lands in (first-match-wins).
        /// </summary>
        public async Task DeployCustomGameModsAsync(
            GameProfile profile,
            CustomGameProfile config,
            IEnumerable<ModItem> mods,
            IProgress<DeploymentProgress>? progress = null,
            CancellationToken ct = default)
        {
            // Prefer the profile's vanilla path (for built-in IV family games), fall
            // back to config.GameDirectory (for pure custom games).
            string? vanilla = GetVanillaPath(profile);
            string gameDir = !string.IsNullOrEmpty(vanilla) ? vanilla : config.GameDirectory;
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
                throw new InvalidOperationException($"Game directory for '{config.GameName}' is missing or not set.");

            // Keep config.GameDirectory in sync so DeploymentPlanner resolves tokens correctly.
            if (!string.IsNullOrEmpty(vanilla))
                config.GameDirectory = gameDir;

            var allMods = mods.ToList();
            new LoadOrderResolver().ResolveFinalLoadOrders(allMods, config);
            var enabled = allMods.Where(m => m.IsEnabled).OrderBy(m => m.FinalLoadOrder).ToList();
            var disabled = allMods.Where(m => !m.IsEnabled).ToList();

            Log($"[CustomDeploy:{profile.Key}] Starting - {enabled.Count} enabled, {disabled.Count} disabled");

            // Build file map from frozen install-time plans when available; higher
            // FinalLoadOrder wins on destination conflict.
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in enabled)
            {
                if (!Directory.Exists(mod.RawFolderPath)) continue;

                var plan = await GetDeploymentPlanAsync(profile.Key, mod, config, ct);

                foreach (var warning in plan.Warnings)
                    Log($"[CustomDeploy:{profile.Key}] WARN [{mod.Name}]: {warning.Message}");

                foreach (var entry in plan.Files.Where(f => !f.Skip))
                {
                    string rel = Path.GetRelativePath(gameDir, entry.DestinationPath);
                    fileMap[rel] = entry.SourcePath;
                }

                foreach (var dir in plan.Directories)
                    directories.Add(dir);
            }

            await DeployFilesToGameDirAsync(profile.Key, gameDir, fileMap,
                enabled.Select(m => m.Name).ToList(), progress, ct, directories);
        }

        /// <summary>
        /// Shared deploy core: backs up files that will be overwritten, writes
        /// new files, saves a rollback manifest, and prunes old backups.
        /// fileMap key = path relative to gameDir, value = absolute source path.
        /// </summary>
        public async Task DeployFilesToGameDirAsync(
            string gameKey,
            string gameDir,
            Dictionary<string, string> fileMap,
            List<string> modNames,
            IProgress<DeploymentProgress>? progress = null,
            CancellationToken ct = default,
            IEnumerable<string>? directories = null)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(BackupsPath, gameKey, timestamp);
            Directory.CreateDirectory(backupDir);

            var entries = new List<BackupEntry>();
            int total = fileMap.Count, done = 0;

            NotificationService.ShowVerbose($"Deploy started: {gameKey} — {total} files", "Deploy");

            foreach (var dir in directories ?? [])
            {
                ct.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);
            }

            progress?.Report(new($"Deploying {total} files...", 0, total));

            foreach (var (rel, srcFile) in fileMap)
            {
                ct.ThrowIfCancellationRequested();

                string destFile = Path.Combine(gameDir, rel);
                try
                {
                    await _baselineSnapshots.EnsureCapturedAsync(gameKey, gameDir, rel, ct);
                }
                catch (Exception ex)
                {
                    Log($"[Baseline:{gameKey}] WARN: failed to capture '{rel}': {ex.Message}");
                }
                string? destSubDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destSubDir)) Directory.CreateDirectory(destSubDir);

                string? backupFilePath = null;
                if (File.Exists(destFile))
                {
                    string backupFile = Path.Combine(backupDir, rel);
                    string? backupSubDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(backupSubDir)) Directory.CreateDirectory(backupSubDir);

                    try
                    {
                        File.Copy(destFile, backupFile, overwrite: true);
                        backupFilePath = backupFile;
                    }
                    catch (Exception ex)
                    {
                        Log($"[Deploy:{gameKey}] WARN: backup failed for {rel}: {ex.Message}");
                    }

                    try { File.SetAttributes(destFile, FileAttributes.Normal); } catch { }
                }

                await using var src = new FileStream(srcFile, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 81920, useAsync: true);
                await using var dst = new FileStream(destFile, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);
                await src.CopyToAsync(dst, ct);

                long originalSize = backupFilePath is not null ? new FileInfo(backupFilePath).Length : 0;
                entries.Add(new BackupEntry(rel, backupFilePath, originalSize));

                done++;
                if (done % 10 == 0 || done == total)
                    progress?.Report(new($"Writing file {done}/{total}", done, total));
            }

            var manifestDirectories = (directories ?? [])
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Select(dir => Path.GetRelativePath(gameDir, dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var manifest = new DeployManifest(timestamp, gameKey, gameDir, modNames, entries, manifestDirectories);
            File.WriteAllText(
                Path.Combine(backupDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonHelper.PrettyOptions));

            PruneOldBackups(gameKey);
            int backedUp = entries.Count(e => e.BackupFilePath is not null);
            Log($"[Deploy:{gameKey}] Done - {done} files written, {backedUp} backed up, manifest saved.");
            NotificationService.ShowVerbose($"Deploy complete: {gameKey} — {done} files, {backedUp} backed up to {timestamp}", "Deploy");
        }
    }
}
