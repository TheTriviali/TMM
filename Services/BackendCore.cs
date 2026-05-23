// TABLE OF CONTENTS
// -----------------------------------------------------------------
//   STATE & FIELDS  (AppDataPath, Settings, Mods, HttpClient)
//   INIT  (BackendCore constructor)
//   LOGGING  (Log)
//   SETTINGS  (LoadSettings, SaveSettings, FactoryReset)
//   GAME PATH ACCESS  (GetVanillaPath, SetVanillaPath, IsGameReady)
//   GAME DETECTION  (QuickScan, ScanForExe)
//   GAME STATUS & EXE VERIFICATION
//     VerifyGameStatusAsync / HasExeModOverride / FindExeInMod
//     GetFileMD5Async / GetEffectiveMd5Async / GetMd5DiagnosticsAsync
//   MOD LIST LOAD/SAVE  (RefreshAllModListsAsync / RefreshModListForGame)
//   DOWNLOAD CACHE  (DownloadCachePath / WipeDownloadCache / GetDriveSpaceInfo)
//   BACKUP / ROLLBACK  (GetRollbackManifests / RollbackDeployAsync / PruneOldBackups)
//   DEPLOYMENT  (DeployModsAsync / DeployCustomGameModsAsync / DeployFilesToGameDirAsync)
//   ARCHIVE EXTRACTION  (ExtractArchiveSafeAsync)
//   DOWNLOADING  (DownloadFileAsync)
// -----------------------------------------------------------------

using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace TMM
{
    /// <summary>
    /// Core backend. Owns persisted settings, mod lists per game, and the
    /// staging/deploy pipeline. UI windows talk to this; this talks to disk.
    /// </summary>
    public class BackendCore
    {
        // ==========================================================
        // STATE
        // ==========================================================

        public string AppDataPath { get; }
        public AppSettings Settings { get; private set; } = new();
        public string Version { get; } = "2.0";

        // Per-game mod lists, looked up by GameProfile.Key.
        // Exposed as read-only; mutated internally via _modsDict.
        private readonly Dictionary<string, ObservableCollection<ModItem>> _modsDict = new();
        public IReadOnlyDictionary<string, ObservableCollection<ModItem>> Mods => _modsDict;

        private static readonly HttpClient HttpClient = new();

        // ==========================================================
        // INIT
        // ==========================================================

        public BackendCore()
        {
            AppDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TMM");

            // Migrate existing TGTAMM data if the old folder exists and TMM doesn't yet
            string oldPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TGTAMM");
            if (Directory.Exists(oldPath) && !Directory.Exists(AppDataPath))
            {
                try { Directory.Move(oldPath, AppDataPath); }
                catch { Directory.CreateDirectory(AppDataPath); }
            }
            else
            {
                Directory.CreateDirectory(AppDataPath);
            }

            LoadSettings();

            // Seed mod lists and raw folders for built-in games
            foreach (var profile in GameProfile.All)
            {
                _modsDict[profile.Key] = new ObservableCollection<ModItem>();
                Directory.CreateDirectory(Path.Combine(AppDataPath, profile.RawFolderName));
            }
            Directory.CreateDirectory(DownloadCachePath);
            Directory.CreateDirectory(BackupsPath);

            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "TMM-Mod-Manager");
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
                Settings.DeployOverrides.TryAdd(key, false);
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
                if (loaded != null) Settings = loaded;

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
        // GAME STATE VERIFICATION
        // ==========================================================

        public async Task<ExeStatus> VerifyGameStatusAsync(GameProfile profile)
        {
            // If an enabled mod in the modlist contains the game exe, use that
            // for verification - it means the user has installed a downgraded exe
            // as a mod, which is fully valid.
            var list = Mods[profile.Key];
            var exeMod = list
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.LoadOrder)
                .LastOrDefault(m => FindExeInMod(m.RawFolderPath, profile.ExeName) != null);

            if (exeMod != null)
            {
                string modExePath = FindExeInMod(exeMod.RawFolderPath, profile.ExeName)!;
                string modMd5 = await GetFileMD5Async(modExePath);
                return profile.IsValidMd5(modMd5) ? ExeStatus.Downgraded : ExeStatus.Vanilla;
            }

            string? path = GetVanillaPath(profile);
            if (string.IsNullOrEmpty(path)) return ExeStatus.Unknown;

            string fullPath = Path.Combine(path, profile.ExeName);
            if (!File.Exists(fullPath)) return ExeStatus.Unknown;

            string md5 = await GetFileMD5Async(fullPath);
            return profile.IsValidMd5(md5) ? ExeStatus.Downgraded : ExeStatus.Vanilla;
        }

        /// <summary>
        /// Returns true if the game can be deployed even if the vanilla path is
        /// a Steam install. True when either:
        ///   (a) an enabled exe mod is in the modlist (provides the 1.0 binary), or
        ///   (b) the user has explicitly toggled the per-game DeployOverride flag.
        /// </summary>
        public bool HasExeModOverride(GameProfile profile)
        {
            if (Settings.DeployOverrides.TryGetValue(profile.Key, out bool forced) && forced)
                return true;

            var list = Mods[profile.Key];
            return list.Any(m => m.IsEnabled && FindExeInMod(m.RawFolderPath, profile.ExeName) != null);
        }

        /// <summary>Toggles the per-game force-deploy override and persists settings.</summary>
        public bool ToggleDeployOverride(GameProfile profile)
        {
            Settings.DeployOverrides.TryAdd(profile.Key, false);
            Settings.DeployOverrides[profile.Key] = !Settings.DeployOverrides[profile.Key];
            SaveSettings();
            return Settings.DeployOverrides[profile.Key];
        }

        /// <summary>
        /// Searches the mod folder recursively (up to 3 levels) for the game exe.
        /// Handles zip archives that wrap content in a single top-level subdirectory.
        /// </summary>
        public static string? FindExeInMod(string modRoot, string exeName)
        {
            if (!Directory.Exists(modRoot)) return null;

            // Check root first (most common case).
            string rootExe = Path.Combine(modRoot, exeName);
            if (File.Exists(rootExe)) return rootExe;

            // Check one level deep (single-directory wrapper pattern common in zip archives).
            foreach (var sub in Directory.GetDirectories(modRoot))
            {
                string sub1 = Path.Combine(sub, exeName);
                if (File.Exists(sub1)) return sub1;

                // Two levels deep (rare but covered).
                foreach (var sub2dir in Directory.GetDirectories(sub))
                {
                    string sub2 = Path.Combine(sub2dir, exeName);
                    if (File.Exists(sub2)) return sub2;
                }
            }
            return null;
        }

        private static async Task<string> GetFileMD5Async(string filePath)
        {
            using var md5 = MD5.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Returns the MD5 of the exe that will actually run during deployment.
        /// If the modlist has an enabled mod whose folder contains the game exe,
        /// that file's hash is returned instead of the vanilla exe's hash.
        /// This lets diagnostics show whether the active downgraded exe is valid.
        /// </summary>
        public async Task<string> GetEffectiveMd5Async(GameProfile profile)
        {
            // Check modlist for an enabled mod that contains the game exe.
            var list = Mods[profile.Key];
            var exeMod = list
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.LoadOrder)
                .LastOrDefault(m => FindExeInMod(m.RawFolderPath, profile.ExeName) != null);

            if (exeMod != null)
            {
                string modExe = FindExeInMod(exeMod.RawFolderPath, profile.ExeName)!;
                return await GetFileMD5Async(modExe);
            }

            // Fall back to the vanilla path exe.
            string? vanillaPath = GetVanillaPath(profile);
            if (string.IsNullOrEmpty(vanillaPath)) return "(no path set)";
            string exePath = Path.Combine(vanillaPath, profile.ExeName);
            if (!File.Exists(exePath)) return "(exe not found)";
            return await GetFileMD5Async(exePath);
        }

        /// <summary>
        /// Returns a multi-line diagnostic string for the developer console.
        /// Shows the effective exe source, its MD5, and whether it matches the
        /// expected 1.0 hash.
        /// </summary>
        public async Task<string> GetMd5DiagnosticsAsync(GameProfile profile)
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"-- {profile.DisplayName} MD5 Diagnostics --");
            // Show all accepted hashes so the user can verify their downgrader variant
            foreach (var h in profile.AllValidMd5s)
                lines.AppendLine($"  Accepted 1.0 MD5 : {h}");

            string effective = await GetEffectiveMd5Async(profile);
            lines.AppendLine($"  Active exe MD5   : {effective}");

            bool match = profile.IsValidMd5(effective);
            lines.AppendLine($"  Status           : {(match ? "[OK] MATCH - ready for direct deploy" : "[FAIL] MISMATCH - Steam or unknown build")}");

            // Show which mod is providing the exe (if any).
            var list = Mods[profile.Key];
            var exeMod = list
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.LoadOrder)
                .LastOrDefault(m => FindExeInMod(m.RawFolderPath, profile.ExeName) != null);

            if (exeMod != null)
            {
                string exePath = FindExeInMod(exeMod.RawFolderPath, profile.ExeName)!;
                lines.AppendLine($"  Exe source mod   : [{exeMod.LoadOrder}] {exeMod.Name}");
                lines.AppendLine($"  Exe path in mod  : {exePath.Replace(exeMod.RawFolderPath, "")}");
            }
            else
                lines.AppendLine($"  Exe source       : Vanilla path ({GetVanillaPath(profile) ?? "unset"})");

            return lines.ToString();
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
                    string infoPath = Path.Combine(subFolder, "modinfo.txt");
                    if (File.Exists(infoPath))
                    {
                        try
                        {
                            var loaded = JsonSerializer.Deserialize<ModItem>(File.ReadAllText(infoPath), JsonHelper.PrettyOptions);
                            if (loaded != null)
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
            if (app == null) return;
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

        public List<DeployManifest> GetRollbackManifests(string gameKey)
        {
            string gameBackupDir = Path.Combine(BackupsPath, gameKey);
            if (!Directory.Exists(gameBackupDir)) return new List<DeployManifest>();

            var manifests = new List<DeployManifest>();
            foreach (var dir in Directory.GetDirectories(gameBackupDir).OrderByDescending(d => d))
            {
                string mPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(mPath)) continue;
                try
                {
                    var m = JsonSerializer.Deserialize<DeployManifest>(File.ReadAllText(mPath));
                    if (m != null) manifests.Add(m);
                }
                catch { /* skip corrupt manifest */ }
            }
            return manifests;
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

                if (entry.BackupFilePath != null && File.Exists(entry.BackupFilePath))
                {
                    // Restore the backed-up original.
                    string? destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                    try { File.SetAttributes(destFile, FileAttributes.Normal); } catch { }
                    await Task.Run(() => File.Copy(entry.BackupFilePath, destFile, overwrite: true), ct);
                }
                else if (entry.BackupFilePath == null && File.Exists(destFile))
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
        /// Deploy enabled mods in load order directly to the game's installation
        /// directory. Files that would be overwritten are backed up first so the
        /// deploy can be rolled back. Higher LoadOrder wins on conflict.
        /// </summary>
        public async Task DeployModsAsync(
            GameProfile profile,
            IEnumerable<ModItem> mods,
            IProgress<DeploymentProgress>? progress = null,
            CancellationToken ct = default)
        {
            string? gameDir = GetVanillaPath(profile);
            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
                throw new InvalidOperationException($"Game directory for {profile.DisplayName} is missing.");

            var allMods = mods.ToList();
            var enabled = allMods.Where(m => m.IsEnabled).OrderBy(m => m.LoadOrder).ToList();
            var disabled = allMods.Where(m => !m.IsEnabled).ToList();

            Log($"[Deploy:{profile.Key}] Starting - {enabled.Count} enabled, {disabled.Count} disabled");
            foreach (var m in disabled)
                Log($"[Deploy:{profile.Key}]   SKIP (disabled) [{m.LoadOrder}] {m.Name}");

            // Build flat file map: relative game-dir path -> winning source file.
            // ConditionalRoutes on the profile are applied here (e.g. .asi → plugins\).
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in enabled)
            {
                if (!Directory.Exists(mod.RawFolderPath))
                {
                    Log($"[Deploy:{profile.Key}]   WARN: folder missing for [{mod.LoadOrder}] {mod.Name}");
                    continue;
                }
                foreach (var file in Directory.EnumerateFiles(mod.RawFolderPath, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(mod.RawFolderPath, file);
                    rel = ApplyConditionalRoutes(profile.ConditionalRoutes, gameDir, rel);
                    fileMap[rel] = file;
                }
            }

            await DeployFilesToGameDirAsync(profile.Key, gameDir, fileMap,
                enabled.Select(m => m.Name).ToList(), progress, ct);
        }

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

            var allMods = mods.ToList();
            var enabled = allMods.Where(m => m.IsEnabled).OrderBy(m => m.LoadOrder).ToList();
            var disabled = allMods.Where(m => !m.IsEnabled).ToList();

            Log($"[CustomDeploy:{profile.Key}] Starting - {enabled.Count} enabled, {disabled.Count} disabled");

            // Build file map with routing rules (first-match-wins, extension + optional name filter).
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in enabled)
            {
                if (!Directory.Exists(mod.RawFolderPath)) continue;
                foreach (var file in Directory.EnumerateFiles(mod.RawFolderPath, "*", SearchOption.AllDirectories))
                {
                    string ext      = Path.GetExtension(file).ToLowerInvariant();
                    string fileName = Path.GetFileName(file);
                    string outSubDir = config.ResolveOutputDirectory(ext, fileName, gameDir);
                    string rel = outSubDir == "." ? fileName : Path.Combine(outSubDir, fileName);
                    fileMap[rel] = file;
                }
            }

            await DeployFilesToGameDirAsync(profile.Key, gameDir, fileMap,
                enabled.Select(m => m.Name).ToList(), progress, ct);
        }

        /// <summary>
        /// Applies profile-level ConditionalRoutes to a relative file path.
        /// If the target extension matches a rule, the output path is re-routed
        /// based on whether the specified sub-directory exists in the game dir.
        /// </summary>
        private static string ApplyConditionalRoutes(
            IReadOnlyList<ConditionalRoute>? routes,
            string gameDir,
            string relPath)
        {
            if (routes == null || routes.Count == 0) return relPath;
            string ext = Path.GetExtension(relPath).ToLowerInvariant();
            foreach (var cond in routes)
            {
                if (!cond.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)) continue;
                string check = Path.Combine(gameDir, cond.CheckSubdir);
                string routeTo = Directory.Exists(check) ? cond.RouteIfExists : cond.RouteIfMissing;
                string fileName = Path.GetFileName(relPath);
                return routeTo == "." ? fileName : Path.Combine(routeTo, fileName);
            }
            return relPath;
        }

        /// <summary>
        /// Shared deploy core: backs up files that will be overwritten, writes
        /// new files, saves a rollback manifest, and prunes old backups.
        /// fileMap key = path relative to gameDir, value = absolute source path.
        /// </summary>
        private async Task DeployFilesToGameDirAsync(
            string gameKey,
            string gameDir,
            Dictionary<string, string> fileMap,
            List<string> modNames,
            IProgress<DeploymentProgress>? progress = null,
            CancellationToken ct = default)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(BackupsPath, gameKey, timestamp);
            Directory.CreateDirectory(backupDir);

            var entries = new List<BackupEntry>();
            int total = fileMap.Count, done = 0;

            progress?.Report(new($"Deploying {total} files...", 0, total));

            foreach (var (rel, srcFile) in fileMap)
            {
                ct.ThrowIfCancellationRequested();

                string destFile = Path.Combine(gameDir, rel);
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

                long originalSize = backupFilePath != null ? new FileInfo(backupFilePath).Length : 0;
                entries.Add(new BackupEntry(rel, backupFilePath, originalSize));

                done++;
                if (done % 10 == 0 || done == total)
                    progress?.Report(new($"Writing file {done}/{total}", done, total));
            }

            var manifest = new DeployManifest(timestamp, gameKey, gameDir, modNames, entries);
            File.WriteAllText(
                Path.Combine(backupDir, "manifest.json"),
                JsonSerializer.Serialize(manifest, JsonHelper.PrettyOptions));

            PruneOldBackups(gameKey);
            int backedUp = entries.Count(e => e.BackupFilePath != null);
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

    }
}
