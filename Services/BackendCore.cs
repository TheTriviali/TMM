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

    public partial class BackendCore
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

            // Wire the notification history/persistence + live verbose-mode provider.
            NotificationService.Initialize(AppDataPath, () => Settings.VerboseNotifications);

            // Initialize localization with saved language preference
            LocalizationService.Instance.SetLanguage(Settings.CurrentLanguage);

            foreach (var profile in GameProfile.All)
            {
                _modsDict[profile.Key] = new ObservableCollection<ModItem>();
                string modsDir = Path.Combine(AppDataPath, profile.RawFolderName);
                bool modsNew = !Directory.Exists(modsDir);
                Directory.CreateDirectory(modsDir);
                if (modsNew) NotificationService.ShowVerbose($"Created {profile.RawFolderName}", "Init");
            }
            bool cacheNew = !Directory.Exists(DownloadCachePath);
            Directory.CreateDirectory(DownloadCachePath);
            if (cacheNew) NotificationService.ShowVerbose("Created DownloadCache directory", "Init");

            bool backupsNew = !Directory.Exists(BackupsPath);
            Directory.CreateDirectory(BackupsPath);
            if (backupsNew) NotificationService.ShowVerbose("Created Backups directory", "Init");

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
        // GAME DETECTION (Quick + Deep scan)
        // ==========================================================

        public void QuickScan()
        {
            Log("--- Starting Quick Scan ---");

            ScanBuiltInsBySearchHints();

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

            ScanCustomGamesBySearchHints();

            SaveSettings();
            Log("--- Quick Scan Finished ---");
        }

        /// <summary>
        /// Detects built-in game profiles using their <see cref="GameConfig.SearchHints"/>.
        /// Runs before the legacy hardcoded-roots loop so that games already found are skipped there.
        /// Uses <see cref="SetVanillaPath"/> so that finding GTA IV auto-derives TLaD/TBoGT paths.
        /// </summary>
        private void ScanBuiltInsBySearchHints()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            foreach (var (key, config) in GameRegistry.Instance.GetBuiltInCustomGames())
            {
                if (config.SearchHints.Count == 0 || string.IsNullOrEmpty(config.ExePath)) continue;

                var profile = GameRegistry.Instance.GetGameProfile(key);
                if (profile is null) continue;
                if (!string.IsNullOrEmpty(GetVanillaPath(profile))) continue;

                string exeName = Path.GetFileName(config.ExePath);
                if (string.IsNullOrEmpty(exeName)) continue;

                string? found = ProbeSearchHints(drives, config.SearchHints, exeName);
                if (found is null) continue;

                Log($"[QUICK] Located built-in '{config.GameName}' via search hint: {found}");
                SetVanillaPath(profile, found);
            }
        }

        /// <summary>
        /// Detects custom games on this machine using each profile's <see cref="GameConfig.SearchHints"/>.
        /// This is what lets a shared .tmmgame profile auto-locate the game on another person's system:
        /// the profile travels with a list of default install locations, and Quick Scan probes them
        /// (relative to every fixed drive) for the configured executable.
        /// </summary>
        private void ScanCustomGamesBySearchHints()
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList();

            foreach (var (key, config) in GameRegistry.Instance.GetCustomGames())
            {
                // Already located on this machine — nothing to do.
                if (!string.IsNullOrEmpty(config.GameDirectory) && Directory.Exists(config.GameDirectory))
                    continue;
                if (config.SearchHints.Count == 0 || string.IsNullOrEmpty(config.ExePath))
                    continue;

                string exeName = Path.GetFileName(config.ExePath);
                if (string.IsNullOrEmpty(exeName)) continue;

                string? found = ProbeSearchHints(drives, config.SearchHints, exeName);
                if (found is null) continue;

                Log($"[QUICK] Located custom game '{config.GameName}' via search hint: {found}");
                config.GameDirectory = found;
                try { GameRegistry.Instance.SaveCustomGameSync(key, config); }
                catch (Exception ex) { Log($"[QUICK] WARN: failed to persist '{key}': {ex.Message}"); }
            }
        }

        /// <summary>
        /// Probes each <paramref name="hints"/> path (relative to every drive root) for a folder that
        /// directly contains <paramref name="exeName"/>, or whose immediate subfolder does. Returns the
        /// containing directory, or null if not found.
        /// </summary>
        private string? ProbeSearchHints(IEnumerable<DriveInfo> drives, IEnumerable<string> hints, string exeName)
        {
            foreach (var drive in drives)
            {
                foreach (var hint in hints)
                {
                    if (string.IsNullOrWhiteSpace(hint)) continue;

                    // Hints use forward slashes for portability; normalize to the platform separator.
                    string normalized = hint.Replace('/', Path.DirectorySeparatorChar)
                                            .Replace('\\', Path.DirectorySeparatorChar)
                                            .TrimStart(Path.DirectorySeparatorChar);
                    string candidate = Path.Combine(drive.Name, normalized);

                    try
                    {
                        if (!Directory.Exists(candidate)) continue;
                        if (File.Exists(Path.Combine(candidate, exeName)))
                            return candidate;

                        string nested = ScanForExe(candidate, exeName);
                        if (!string.IsNullOrEmpty(nested)) return nested;
                    }
                    catch (Exception ex) { Log($"[QUICK] Skip hint {candidate}: {ex.Message}"); }
                }
            }
            return null;
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

        /// <summary>
        /// Returns the user's card color override for a game as (start, end) hex strings,
        /// or null if none is set. Applies to both built-in and custom games.
        /// </summary>
        public (string Start, string End)? GetCardColor(string gameKey)
        {
            if (!Settings.CardColorOverrides.TryGetValue(gameKey, out var packed) || string.IsNullOrWhiteSpace(packed))
                return null;
            var parts = packed.Split('|');
            return parts.Length == 2 ? (parts[0], parts[1]) : null;
        }

        /// <summary>Sets and persists a card color override for a game.</summary>
        public void SetCardColor(string gameKey, string startHex, string endHex)
        {
            Settings.CardColorOverrides[gameKey] = $"{startHex}|{endHex}";
            SaveSettings();
            Log($"Card color set for {gameKey}: {startHex} → {endHex}");
        }

        /// <summary>Clears a card color override, reverting to the profile's gradient.</summary>
        public void ClearCardColor(string gameKey)
        {
            if (Settings.CardColorOverrides.Remove(gameKey))
            {
                SaveSettings();
                Log($"Card color reset for {gameKey}");
            }
        }

        /// <summary>Removes custom artwork for a game, reverting to gradient banner.</summary>
        public void DeleteLibraryArt(string gameKey)
        {
            var path = Path.Combine(LibraryArtPath, $"{gameKey}.png");
            if (File.Exists(path)) File.Delete(path);
            Log($"Library art removed for {gameKey}");
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
