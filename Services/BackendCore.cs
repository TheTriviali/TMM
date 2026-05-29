using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// Core backend. Owns persisted settings, mod lists per game, and the
    /// staging/deploy pipeline. UI windows talk to this; this talks to disk.
    /// </summary>
    /// <summary>Progress payload reported by long-running deploy/rollback operations.</summary>
    public readonly record struct DeploymentProgress(string Stage, int Current, int Total);

    public class BackendCore
    {
        // ==========================================================
        // STATE
        // ==========================================================

        public string AppDataPath { get; }
        public AppSettings Settings { get; private set; } = new();

        // Per-game mod lists, looked up by GameProfile.Key.
        // Exposed as read-only; mutated internally via _modsDict.
        private readonly Dictionary<string, ObservableCollection<ModItem>> _modsDict = new();
        public IReadOnlyDictionary<string, ObservableCollection<ModItem>> Mods => _modsDict;

        private readonly BaselineSnapshotStore _baselineSnapshots;

        private static readonly HttpClient HttpClient = new();

        /// <summary>Rolling 20-entry feed of user actions (deploys, rollbacks, imports, loadout ops).</summary>
        public ActivityLogger Activity { get; }

        // ==========================================================
        // INIT
        // ==========================================================

        public BackendCore()
        {
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TMM");

            Directory.CreateDirectory(AppDataPath);
            Logger.Initialize(AppDataPath);
            _baselineSnapshots = new BaselineSnapshotStore(AppDataPath);
            LoadSettings();

            // Initialize localization with saved language preference
            LocalizationService.Instance.SetLanguage(Settings.CurrentLanguage);

            foreach (var profile in GameProfile.All)
            {
                _modsDict[profile.Key] = new ObservableCollection<ModItem>();
                Directory.CreateDirectory(Path.Combine(AppDataPath, profile.RawFolderName));
            }
            Directory.CreateDirectory(DownloadCachePath);
            Directory.CreateDirectory(BackupsPath);

            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "TMM-Mod-Manager");

            Activity = new ActivityLogger(this);
            Logger.Info($"TMM started; data dir = {AppDataPath}");
        }

        /// <summary>
        /// Async initialization: loads GameRegistry (custom games) and wires up their mod lists.
        /// Safe to call multiple times — registry re-init is idempotent.
        /// </summary>
        public async Task InitializeAsync()
        {
            var registry = GameRegistry.Instance;
            await registry.InitializeAsync(AppDataPath);

            foreach (var profile in registry.GetAllGames())
            {
                if (!_modsDict.ContainsKey(profile.Key))
                    _modsDict[profile.Key] = new ObservableCollection<ModItem>();

                Directory.CreateDirectory(Path.Combine(AppDataPath, profile.RawFolderName));
            }

            // Sync settings dictionaries with custom game keys from registry
            foreach (var (key, _) in registry.GetCustomGames())
            {
                Settings.GamePaths.TryAdd(key, null);
                if (!Settings.CustomGameKeys.Contains(key))
                    Settings.CustomGameKeys.Add(key);
            }
        }

        // ==========================================================
        // LOGGING
        // ==========================================================

        public void Log(string message)
        {
            try
            {
                if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
                File.AppendAllText(
                    Path.Combine(AppDataPath, "TMM.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { /* never crash on log failure */ }
        }

        // ==========================================================
        // SETTINGS
        // ==========================================================

        public void LoadSettings()
        {
            string path = Path.Combine(AppDataPath, "settings.json");
            if (!File.Exists(path)) return;

            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
                if (loaded is not null) Settings = loaded;

                // Ensure all GamePaths keys exist (forward-compat for added games).
                foreach (var profile in GameProfile.All)
                    Settings.GamePaths.TryAdd(profile.Key, null);
            }
            catch (Exception ex) { Log($"LoadSettings failed: {ex.Message}"); }
        }

        public void SaveSettings()
        {
            try
            {
                File.WriteAllText(
                    Path.Combine(AppDataPath, "settings.json"),
                    JsonSerializer.Serialize(Settings, JsonHelper.PrettyOptions));
            }
            catch (Exception ex) { Log($"SaveSettings failed: {ex.Message}"); }
        }

        public void FactoryReset()
        {
            // Log before deleting - after this the log file is gone.
            Log("FactoryReset: wiping AppData directory.");
            try
            {
                if (Directory.Exists(AppDataPath))
                {
                    // Delete everything except the log file so we can audit the reset.
                    foreach (var dir in Directory.GetDirectories(AppDataPath))
                        ForceDeleteDirectory(dir);
                    foreach (var file in Directory.GetFiles(AppDataPath))
                    {
                        if (Path.GetFileName(file).Equals("TMM.log", StringComparison.OrdinalIgnoreCase))
                            continue;
                        try { File.Delete(file); } catch { /* skip locked */ }
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal - settings are gone, remaining files are harmless on next launch.
                try { Log($"FactoryReset partial failure: {ex.Message}"); } catch { }
            }
        }

        public void OpenAppData() => Process.Start("explorer.exe", AppDataPath);

        // ==========================================================
        // GAME PATH ACCESS
        // ==========================================================

        public string? GetVanillaPath(GameProfile profile) =>
            Settings.GamePaths.TryGetValue(profile.Key, out var path) ? path : null;

        public void SetVanillaPath(GameProfile profile, string? path)
        {
            Settings.GamePaths[profile.Key] = path;

            // When the IV base path is set, auto-derive TLaD and TBoGT from it.
            // Standard layout: [IV dir]\TLAD\  and  [IV dir]\EFLC\
            // Also check alternate names: TLAD/TLaD and EFLC/TBoGT
            if (profile.Key == "IV" && !string.IsNullOrEmpty(path))
            {
                var tladPath = Path.Combine(path, "TLAD");
                if (!Directory.Exists(tladPath)) tladPath = Path.Combine(path, "TLaD");
                if (Directory.Exists(tladPath))
                    Settings.GamePaths[GameProfile.TLaD.Key] = tladPath;

                var tbogtPath = Path.Combine(path, "EFLC");
                if (!Directory.Exists(tbogtPath)) tbogtPath = Path.Combine(path, "TBoGT");
                if (Directory.Exists(tbogtPath))
                    Settings.GamePaths[GameProfile.TBoGT.Key] = tbogtPath;
            }

            SaveSettings();
        }

        public bool IsGameReady(GameProfile profile) =>
            !string.IsNullOrEmpty(GetVanillaPath(profile));

        // ==========================================================
        // GAME DETECTION (Quick + Deep scan)
        // ==========================================================

        public void QuickScan()
        {
            Log("--- Starting Quick Scan ---");

            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                foreach (var profile in GameProfile.All)
                {
                    if (!string.IsNullOrEmpty(GetVanillaPath(profile))) continue;

                    // IV episodes are nested inside GTAIV\ so include that sub-folder too
                    bool isIvFamily = GameProfile.IvFamilyKeys.Contains(profile.Key);
                    string[] commonRoots = isIvFamily
                        ? new[]
                        {
                            Path.Combine(drive.Name, "SteamLibrary", "Steam", "steamapps", "common", "Grand Theft Auto IV", "GTAIV"),
                            Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common", "Grand Theft Auto IV", "GTAIV"),
                            Path.Combine(drive.Name, "Program Files (x86)", "Steam", "steamapps", "common", "Grand Theft Auto IV", "GTAIV"),
                            Path.Combine(drive.Name, "Games", "Grand Theft Auto IV", "GTAIV"),
                            Path.Combine(drive.Name, "Rockstar Games", "Grand Theft Auto IV", "GTAIV"),
                        }
                        : new[]
                        {
                            Path.Combine(drive.Name, "SteamLibrary", "Steam", "steamapps", "common"),
                            Path.Combine(drive.Name, "SteamLibrary", "steamapps", "common"),
                            Path.Combine(drive.Name, "Program Files (x86)", "Steam", "steamapps", "common"),
                            Path.Combine(drive.Name, "Games"),
                            Path.Combine(drive.Name, "Rockstar Games")
                        };

                    foreach (var root in commonRoots.Where(Directory.Exists))
                    {
                        Log($"[QUICK] Checking: {root}");
                        string found = "";
                        if (isIvFamily)
                        {
                            // For IV/TLaD/TBoGT, check in root and in episode subdirectories
                            if (File.Exists(Path.Combine(root, profile.ExeName)))
                                found = root;
                            else if (profile.Key == "TLaD" && File.Exists(Path.Combine(root, "TLaD", profile.ExeName)))
                                found = Path.Combine(root, "TLaD");
                            else if (profile.Key == "TLaD" && File.Exists(Path.Combine(root, "TLAD", profile.ExeName)))
                                found = Path.Combine(root, "TLAD");
                            else if (profile.Key == "TBoGT" && File.Exists(Path.Combine(root, "TBoGT", profile.ExeName)))
                                found = Path.Combine(root, "TBoGT");
                            else if (profile.Key == "TBoGT" && File.Exists(Path.Combine(root, "EFLC", profile.ExeName)))
                                found = Path.Combine(root, "EFLC");
                        }
                        else
                            found = ScanForExe(root, profile.ExeName);

                        if (!string.IsNullOrEmpty(found))
                        {
                            Log($"[SUCCESS] Found {profile.Key} in {found}");
                            // Use SetVanillaPath so IV auto-derives TLaD/TBoGT paths.
                            SetVanillaPath(profile, found);
                            break;
                        }
                    }
                }
            }
            SaveSettings();
            Log("--- Quick Scan Finished ---");
        }

        private string ScanForExe(string root, string exeName)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    try
                    {
                        if (File.Exists(Path.Combine(dir, exeName))) return dir;
                        foreach (var sub in Directory.GetDirectories(dir))
                            if (File.Exists(Path.Combine(sub, exeName))) return sub;
                    }
                    catch { /* skip protected dirs */ }
                }
            }
            catch (Exception ex) { Log($"Skip folder {root}: {ex.Message}"); }
            return "";
        }

        // ==========================================================
        // MOD LIST LOAD/SAVE
        // ==========================================================

        public async Task RefreshAllModListsAsync()
        {
            var allGames = GameRegistry.Instance.GetAllGames();
            await Task.WhenAll(allGames.Select(p => Task.Run(() => RefreshModListForGame(p))));
        }

        private void RefreshModListForGame(GameProfile profile)
        {
            string folder = Path.Combine(AppDataPath, profile.RawFolderName);
            var found = new List<ModItem>();

            if (Directory.Exists(folder))
            {
                int order = 0;
                foreach (string subFolder in Directory.GetDirectories(folder))
                {
                    string infoPath = Path.Combine(subFolder, "_tmm", "modinfo.json");
                    if (!File.Exists(infoPath))
                        infoPath = Path.Combine(subFolder, "modinfo.txt");
                    if (File.Exists(infoPath))
                    {
                        try
                        {
                            var loaded = JsonSerializer.Deserialize<ModItem>(File.ReadAllText(infoPath), JsonHelper.PrettyOptions);
                            if (loaded is not null)
                            {
                                loaded.RawFolderPath = subFolder;
                                found.Add(loaded);
                                continue;
                            }
                        }
                        catch { /* corrupt modinfo - fall through to default */ }
                    }
                    found.Add(new ModItem
                    {
                        Name = Path.GetFileName(subFolder),
                        RawFolderPath = subFolder,
                        IsEnabled = true,
                        LoadOrder = order++
                    });
                }
            }

            // Sort by LoadOrder before pushing to UI.
            found = found.OrderBy(m => m.LoadOrder).ToList();

            // Guard: Application.Current can be null if the app is shutting down
            // (e.g. immediately after factory reset before the process exits).
            var app = System.Windows.Application.Current;
            if (app is null) return;
            app.Dispatcher.Invoke(() =>
            {
                var target = Mods[profile.Key];
                target.Clear();
                foreach (var mod in found) target.Add(mod);
            });
        }

        // ==========================================================
        // DOWNLOAD CACHE
        // ==========================================================

        public string DownloadCachePath => Path.Combine(AppDataPath, "DownloadCache");

        public void WipeDownloadCache()
        {
            string cache = DownloadCachePath;
            if (!Directory.Exists(cache)) return;

            try { Directory.Delete(cache, true); }
            catch { /* files might be locked - retry on next launch */ }
        }

        public string GetDriveSpaceInfo()
        {
            try
            {
                string driveLetter = Path.GetPathRoot(AppDataPath) ?? "C:\\";
                var drive = new DriveInfo(driveLetter);
                double freeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);

                long appDataSize = new DirectoryInfo(AppDataPath).Exists
                    ? new DirectoryInfo(AppDataPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length)
                    : 0;

                return $"App Data: {FormatBytes(appDataSize)}\nFree Space: {freeGB:F1} GB";
            }
            catch
            {
                return "Space Info Unavailable";
            }
        }

        /// <summary>
        /// Seeds the first-touch baseline for a game directory by snapshotting
        /// every file currently present. Used by import-from-install so rollback
        /// can restore the original on-disk state.
        /// </summary>
        public Task SeedBaselineAsync(string gameKey, string gameDir, CancellationToken ct = default) =>
            _baselineSnapshots.SeedExistingFilesAsync(gameKey, gameDir, ct);

        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            decimal d = bytes;
            while (Math.Round(d / 1024) >= 1) { d /= 1024; i++; }
            return $"{d:n1} {suffixes[i]}";
        }

        // ==========================================================
        // BACKUP / ROLLBACK
        // ==========================================================

        public string BackupsPath => Path.Combine(AppDataPath, "Backups");

        /// <summary>Folder where raw downloaded archives (.zip/.rar/.7z) are stored per game key.</summary>
        public string GetModsArchivePath(string gameKey)
        {
            string path = Path.Combine(AppDataPath, $"ModsArchive{gameKey}");
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Folder where custom library card artwork is stored.</summary>
        public string LibraryArtPath => Path.Combine(AppDataPath, "LibraryArt");

        /// <summary>
        /// Returns the full path to a game's custom artwork PNG if it exists, else null.
        /// Checks %APPDATA%\TMM\LibraryArt\{gameKey}.png
        /// </summary>
        public string? GetLibraryArtPath(string gameKey)
        {
            var path = Path.Combine(LibraryArtPath, $"{gameKey}.png");
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// Saves custom artwork for a game. Validates: PNG only, max 2 MB, min 200×100px.
        /// Resizes/crops to 460×215 if dimensions differ (preserves aspect, center-crops).
        /// Throws ArgumentException on validation failure.
        /// </summary>
        public void SaveLibraryArt(string gameKey, string sourcePath)
        {
            const long MaxBytes = 2 * 1024 * 1024; // 2 MB
            var info = new FileInfo(sourcePath);
            if (!info.Exists) throw new ArgumentException("Source file not found.");
            if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Only PNG files are accepted for library artwork.");
            if (info.Length > MaxBytes)
                throw new ArgumentException("Image must be under 2 MB.");

            Directory.CreateDirectory(LibraryArtPath);
            var destPath = Path.Combine(LibraryArtPath, $"{gameKey}.png");
            File.Copy(sourcePath, destPath, overwrite: true);
            Log($"Library art saved for {gameKey}: {destPath}");
        }

        /// <summary>Removes custom artwork for a game, reverting to gradient banner.</summary>
        public void DeleteLibraryArt(string gameKey)
        {
            var path = Path.Combine(LibraryArtPath, $"{gameKey}.png");
            if (File.Exists(path)) File.Delete(path);
            Log($"Library art removed for {gameKey}");
        }

        public List<DeployManifest> GetRollbackManifests(string gameKey)
        {
            string gameBackupDir = Path.Combine(BackupsPath, gameKey);
            if (!Directory.Exists(gameBackupDir)) return [];

            var manifests = new List<DeployManifest>();
            foreach (var dir in Directory.GetDirectories(gameBackupDir).OrderByDescending(d => d))
            {
                string mPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(mPath)) continue;
                try
                {
                    var m = JsonSerializer.Deserialize<DeployManifest>(File.ReadAllText(mPath));
                    if (m is not null) manifests.Add(m);
                }
                catch { /* skip corrupt manifest */ }
            }
            return manifests;
        }

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
        }

        private void PruneOldBackups(string gameKey, int keepCount = 3)
        {
            string gameBackupDir = Path.Combine(BackupsPath, gameKey);
            if (!Directory.Exists(gameBackupDir)) return;

            var toDelete = Directory.GetDirectories(gameBackupDir)
                                    .OrderByDescending(d => d)
                                    .Skip(keepCount)
                                    .ToList();

            foreach (var dir in toDelete)
            {
                try { ForceDeleteDirectory(dir); }
                catch { /* skip if locked */ }
            }
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
        }

        /// <summary>
        /// Aggressive recursive delete: clears read-only attributes on all
        /// child files first, since mod archives often pack files with R/O set,
        /// which causes Directory.Delete to throw.
        /// </summary>
        public static void ForceDeleteDirectory(string targetDir)
        {
            var dir = new DirectoryInfo(targetDir);
            if (!dir.Exists) return;

            foreach (var file in dir.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                try { file.Attributes = FileAttributes.Normal; }
                catch { /* skip locked */ }
            }

            try { dir.Delete(true); }
            catch (IOException)
            {
                // Final attempt after a brief pause - sometimes the OS is just slow to release handles.
                Thread.Sleep(100);
                dir.Delete(true);
            }
        }

        /// <summary>
        /// Sync recursive copy (used by simple cases like drag-drop mod copy
        /// where parallelism isn't worth the overhead).
        /// </summary>
        public void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Debug.WriteLine("CopyDirectory: empty path - skipping.");
                return;
            }
            if (!Directory.Exists(sourceDirectory)) return;

            Directory.CreateDirectory(destinationDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
                File.Copy(file, Path.Combine(destinationDirectory, Path.GetFileName(file)), true);

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }

        // ==========================================================
        // ARCHIVE EXTRACTION
        // ==========================================================

        public static async Task ExtractArchiveSafeAsync(string archivePath, string destinationPath, CancellationToken ct = default)
        {
            if (Directory.Exists(destinationPath)) ForceDeleteDirectory(destinationPath);
            Directory.CreateDirectory(destinationPath);

            try
            {
                using var stream = File.OpenRead(archivePath);
                await using var reader = await ReaderFactory.OpenAsyncReader(stream);
                while (await reader.MoveToNextEntryAsync())
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(reader.Entry.Key)) continue;

                    string fullPath = Path.Combine(destinationPath, reader.Entry.Key);

                    if (reader.Entry.IsDirectory)
                    {
                        Directory.CreateDirectory(fullPath);
                    }
                    else
                    {
                        string? parent = Path.GetDirectoryName(fullPath);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                        await using var fileStream = File.Create(fullPath);
                        await using var es = await reader.OpenEntryStreamAsync();
                        await es.CopyToAsync(fileStream, ct);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new Exception($"Extraction failed: {ex.Message}", ex);
            }
        }

        // ==========================================================
        // DOWNLOADING
        // ==========================================================

        /// <summary>
        /// Streams a URL straight to disk. Previously this called
        /// <c>GetByteArrayAsync</c> which loaded the whole file into RAM -
        /// bad for big bundles like Project2DFX.
        /// </summary>
        public async Task DownloadFileAsync(string fileUrl, string destinationPath, CancellationToken ct = default)
        {
            string? parent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            using var response = await HttpClient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var src = await response.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(destinationPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 81920, useAsync: true);
            await src.CopyToAsync(dst, ct);
        }

        // ==========================================================
        // LOADOUTS  (Block D)
        // ==========================================================

        /// <summary>
        /// True when <paramref name="name"/> is safe to use as a loadout filename:
        /// non-empty after trimming and free of path separators / characters that are
        /// illegal in a Windows filename.
        /// </summary>
        public static bool IsValidLoadoutName(string? name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        public string GetLoadoutsPath(string gameKey)
        {
            string path = Path.Combine(AppDataPath, $"Loadouts_{gameKey}");
            Directory.CreateDirectory(path);
            return path;
        }

        public bool LoadoutExists(string gameKey, string loadoutName) =>
            File.Exists(Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json"));

        public IEnumerable<string> ListLoadouts(string gameKey)
        {
            string dir = GetLoadoutsPath(gameKey);
            if (!Directory.Exists(dir)) return Array.Empty<string>();
            return Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension)!;
        }

        public bool RenameLoadout(string gameKey, string oldName, string newName)
        {
            if (!IsValidLoadoutName(newName))
                throw new ArgumentException("Loadout name contains invalid characters.", nameof(newName));
            string dir = GetLoadoutsPath(gameKey);
            string src = Path.Combine(dir, $"{oldName}.json");
            string dst = Path.Combine(dir, $"{newName}.json");
            if (!File.Exists(src) || File.Exists(dst)) return false;
            File.Move(src, dst);
            return true;
        }

        public bool DeleteLoadout(string gameKey, string loadoutName)
        {
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            if (!File.Exists(path)) return false;
            File.Delete(path);
            return true;
        }

        public async Task<ModLoadout?> ReadLoadoutAsync(string gameKey, string loadoutName)
        {
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<ModLoadout>(await File.ReadAllTextAsync(path));
        }

        /// <summary>
        /// Persists a named loadout to disk. Caller is responsible for prompting if
        /// the name already exists (use <see cref="LoadoutExists"/> first).
        /// </summary>
        public async Task SaveLoadoutAsync(string gameKey, string loadoutName, IEnumerable<ModItem> mods)
        {
            if (!IsValidLoadoutName(loadoutName))
                throw new ArgumentException("Loadout name contains invalid characters.", nameof(loadoutName));
            var loadout = new ModLoadout { Name = loadoutName };
            foreach (var m in mods)
                loadout.ModStates[m.Name] = new ModLoadout.LoadoutModState(m.IsEnabled, m.LoadOrder);
            string path = Path.Combine(GetLoadoutsPath(gameKey), $"{loadoutName}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(loadout, JsonHelper.PrettyOptions));
            Logger.Info($"Saved loadout '{loadoutName}' ({loadout.ModStates.Count} mods) for {gameKey}");
        }

        public async Task ApplyLoadoutAsync(string gameKey, string loadoutName, ObservableCollection<ModItem> currentMods)
        {
            var loadout = await ReadLoadoutAsync(gameKey, loadoutName);
            if (loadout is null) return;

            foreach (var mod in currentMods)
            {
                if (loadout.ModStates.TryGetValue(mod.Name, out var state))
                {
                    mod.IsEnabled = state.IsEnabled;
                    mod.LoadOrder = state.LoadOrder;
                }
                else
                {
                    mod.IsEnabled = false;
                }
            }

            var sorted = currentMods.OrderBy(m => m.LoadOrder).ToList();
            currentMods.Clear();
            foreach (var m in sorted) currentMods.Add(m);
        }

        // ==========================================================
        // BACKUP SIZE  (quota monitoring)
        // ==========================================================

        public long GetTotalBackupSize()
        {
            if (!Directory.Exists(BackupsPath)) return 0;
            long total = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(BackupsPath, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(file).Length; } catch { }
                }
            }
            catch { }
            return total;
        }
    }
}
