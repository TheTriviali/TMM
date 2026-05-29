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
    /// BackendCore — backup storage, first-touch baselines, and rollback manifests.
    /// </summary>
    public partial class BackendCore
    {
        public string BackupsPath => Path.Combine(AppDataPath, "Backups");

        /// <summary>
        /// Seeds the first-touch baseline for a game directory by snapshotting
        /// every file currently present. Used by import-from-install so rollback
        /// can restore the original on-disk state.
        /// </summary>
        public Task SeedBaselineAsync(string gameKey, string gameDir, CancellationToken ct = default) =>
            _baselineSnapshots.SeedExistingFilesAsync(gameKey, gameDir, ct);

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
                try
                {
                    ForceDeleteDirectory(dir);
                    NotificationService.ShowVerbose($"Pruned backup: {gameKey}/{Path.GetFileName(dir)}", "Backup");
                }
                catch { /* skip if locked */ }
            }
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
