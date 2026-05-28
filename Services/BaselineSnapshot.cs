using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TMM.Services
{
    /// <summary>
    /// Stores the first-touch baseline for a game key.
    /// Each file path is captured once, then reused for every later rollback.
    /// </summary>
    public sealed record BaselineSnapshotEntry(
        string? SnapshotFile,
        long OriginalSize,
        DateTimeOffset CapturedAt);

    /// <summary>
    /// Persists first-touch baseline snapshots under %APPDATA%\TMM\Baselines.
    /// </summary>
    public sealed class BaselineSnapshotStore
    {
        private readonly string _baselineRoot;
        private readonly Dictionary<string, Dictionary<string, BaselineSnapshotEntry>> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public BaselineSnapshotStore(string appDataPath)
        {
            _baselineRoot = Path.Combine(appDataPath, "Baselines");
            Directory.CreateDirectory(_baselineRoot);
        }

        private string GetGameRoot(string gameKey) => Path.Combine(_baselineRoot, gameKey);

        private string GetManifestPath(string gameKey) => Path.Combine(GetGameRoot(gameKey), "baseline.json");

        private string GetSnapshotsDir(string gameKey) => Path.Combine(GetGameRoot(gameKey), "snapshots");

        public string GetSnapshotPath(string gameKey, string snapshotFileName) =>
            Path.Combine(GetSnapshotsDir(gameKey), snapshotFileName);

        public async Task EnsureCapturedAsync(
            string gameKey,
            string gameDir,
            string relativePath,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(gameKey))
                throw new ArgumentException("gameKey cannot be empty.", nameof(gameKey));
            if (string.IsNullOrWhiteSpace(gameDir))
                throw new ArgumentException("gameDir cannot be empty.", nameof(gameDir));
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            string normalizedRelativePath = NormalizeRelativePath(relativePath);
            var entries = LoadEntries(gameKey);
            if (entries.ContainsKey(normalizedRelativePath))
                return;

            Directory.CreateDirectory(GetGameRoot(gameKey));
            Directory.CreateDirectory(GetSnapshotsDir(gameKey));

            BaselineSnapshotEntry entry;
            string destFile = Path.Combine(gameDir, relativePath);
            if (File.Exists(destFile))
            {
                string snapshotFile = $"{CreateSnapshotName(relativePath)}.bin";
                string snapshotPath = GetSnapshotPath(gameKey, snapshotFile);
                string? snapshotParent = Path.GetDirectoryName(snapshotPath);
                if (!string.IsNullOrEmpty(snapshotParent))
                    Directory.CreateDirectory(snapshotParent);

                await using var src = new FileStream(destFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                    81920, useAsync: true);
                await using var dst = new FileStream(snapshotPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    81920, useAsync: true);
                await src.CopyToAsync(dst, ct);

                entry = new BaselineSnapshotEntry(
                    snapshotFile,
                    new FileInfo(destFile).Length,
                    DateTimeOffset.UtcNow);
            }
            else
            {
                entry = new BaselineSnapshotEntry(
                    SnapshotFile: null,
                    OriginalSize: 0,
                    CapturedAt: DateTimeOffset.UtcNow);
            }

            lock (_lock)
            {
                entries[normalizedRelativePath] = entry;
            }

            await SaveEntriesAsync(gameKey, entries, ct);
        }

        public async Task SeedExistingFilesAsync(string gameKey, string gameDir, CancellationToken ct = default)
        {
            if (!Directory.Exists(gameDir))
                return;

            foreach (var file in Directory.EnumerateFiles(gameDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                string relativePath = Path.GetRelativePath(gameDir, file);
                await EnsureCapturedAsync(gameKey, gameDir, relativePath, ct);
            }
        }

        public bool TryGetEntry(string gameKey, string relativePath, out BaselineSnapshotEntry? entry)
        {
            var entries = LoadEntries(gameKey);
            return entries.TryGetValue(NormalizeRelativePath(relativePath), out entry);
        }

        private Dictionary<string, BaselineSnapshotEntry> LoadEntries(string gameKey)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(gameKey, out var cached))
                    return cached;

                var entries = new Dictionary<string, BaselineSnapshotEntry>(StringComparer.OrdinalIgnoreCase);
                string manifestPath = GetManifestPath(gameKey);
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var loaded = JsonSerializer.Deserialize<Dictionary<string, BaselineSnapshotEntry>>(
                            File.ReadAllText(manifestPath),
                            JsonHelper.PrettyOptions);
                        if (loaded is not null)
                        {
                            foreach (var (key, value) in loaded)
                                entries[NormalizeRelativePath(key)] = value;
                        }
                    }
                    catch
                    {
                        // Corrupt baseline manifests should not crash deploys; callers
                        // will fall back to per-deploy backup manifests if needed.
                    }
                }

                _cache[gameKey] = entries;
                return entries;
            }
        }

        private async Task SaveEntriesAsync(
            string gameKey,
            Dictionary<string, BaselineSnapshotEntry> entries,
            CancellationToken ct)
        {
            string manifestPath = GetManifestPath(gameKey);
            string? manifestDir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(manifestDir))
                Directory.CreateDirectory(manifestDir);

            string tempPath = manifestPath + ".tmp";
            await File.WriteAllTextAsync(
                tempPath,
                JsonSerializer.Serialize(entries, JsonHelper.PrettyOptions),
                ct);

            File.Move(tempPath, manifestPath, overwrite: true);
        }

        private static string NormalizeRelativePath(string relativePath) =>
            relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        private static string CreateSnapshotName(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath).ToUpperInvariant();
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                builder.Append(b.ToString("x2"));
            return builder.ToString();
        }
    }
}
