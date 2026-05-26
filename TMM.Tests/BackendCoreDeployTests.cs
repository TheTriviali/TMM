using TMM.Tests.Helpers;

namespace TMM.Tests;

/// <summary>
/// Real I/O integration tests for the deploy/rollback pipeline in
/// <see cref="BackendCore"/>.
///
/// <para>
/// Each test creates isolated temp directories for the "game directory" and
/// "mod files". <see cref="BackendCore.DeployFilesToGameDirAsync"/> is called
/// directly with an explicit <c>fileMap</c>, bypassing the routing-rule
/// pipeline so that these tests stay focused on the copy/backup/manifest
/// lifecycle.
/// </para>
///
/// <para>
/// Backup snapshots land in <c>%APPDATA%\TMM\Backups\{testGameKey}\</c>.
/// Each test uses a UUID-suffix game key and cleans up the backup directory
/// in a <c>finally</c> block, so the real TMM install is not polluted.
/// </para>
///
/// Coverage:
///   • New file is copied to the game directory.
///   • Pre-existing file is backed up before being overwritten.
///   • <see cref="DeployManifest"/> is saved with correct metadata.
///   • <see cref="BackendCore.RollbackDeployAsync"/> restores backed-up content.
///   • <see cref="BackendCore.RollbackDeployAsync"/> removes a newly deployed file
///     that had no pre-existing counterpart.
///   • Files not referenced by the deploy are left untouched by rollback.
/// </summary>
public class BackendCoreDeployTests
{
    // We construct BackendCore once. Its constructor is safe in a headless
    // context: it creates dirs under %APPDATA%\TMM and loads settings, but
    // does not open any WPF windows or use the Dispatcher.
    private static readonly BackendCore Backend = new();

    /// <summary>
    /// Returns a unique game key scoped to this test run so backup cleanup is
    /// guaranteed not to touch any real TMM data.
    /// </summary>
    private static string UniqueKey() => "ITEST_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    // ── Deploy: new file copied ───────────────────────────────────────────────

    [Fact]
    public async Task DeployFilesToGameDir_CopiesNewFileToGameDirectory()
    {
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");
        string srcFile = tmp.WriteFile("ModFiles/plugin.asi", "fake asi content");

        string gameKey  = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["plugin.asi"] = srcFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            string deployed = Path.Combine(gameDir, "plugin.asi");
            Assert.True(File.Exists(deployed), "Deployed file should exist in game directory.");
            Assert.Equal("fake asi content", File.ReadAllText(deployed));
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Deploy: pre-existing file is backed up ────────────────────────────────

    [Fact]
    public async Task DeployFilesToGameDir_BacksUpExistingFileBeforeOverwrite()
    {
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");

        // Pre-existing game file with known content
        File.WriteAllText(Path.Combine(gameDir, "d3d9.dll"), "ORIGINAL_DLL");
        string srcFile = tmp.WriteFile("ModFiles/d3d9.dll", "MOD_DLL");

        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["d3d9.dll"] = srcFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            // Game file should now contain mod content
            Assert.Equal("MOD_DLL", File.ReadAllText(Path.Combine(gameDir, "d3d9.dll")));

            // A backup entry with the original content must exist
            var manifests = Backend.GetRollbackManifests(gameKey);
            var entry     = Assert.Single(manifests.Single().Entries);
            Assert.NotNull(entry.BackupFilePath);
            Assert.Equal("ORIGINAL_DLL", File.ReadAllText(entry.BackupFilePath!));
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Deploy: manifest metadata ─────────────────────────────────────────────

    [Fact]
    public async Task DeployFilesToGameDir_CreatesManifestWithCorrectMetadata()
    {
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");
        string f1 = tmp.WriteFile("Mod/file1.asi", "a");
        string f2 = tmp.WriteFile("Mod/file2.cs",  "b");

        var modNames = new List<string> { "MyMod1", "MyMod2" };
        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["file1.asi"] = f1,
                ["file2.cs"]  = f2,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, modNames);

            var manifests = Backend.GetRollbackManifests(gameKey);

            // Exactly one manifest written per deploy call
            var manifest = Assert.Single(manifests);
            Assert.Equal(gameKey, manifest.GameKey);
            Assert.Equal(gameDir, manifest.GameDirectory);
            Assert.Equal(modNames, manifest.ModNames);

            // Two files deployed → two entries
            Assert.Equal(2, manifest.Entries.Count);
            Assert.Contains(manifest.Entries, e => e.RelativePath == "file1.asi");
            Assert.Contains(manifest.Entries, e => e.RelativePath == "file2.cs");
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Rollback: restore backed-up content ──────────────────────────────────

    [Fact]
    public async Task RollbackDeployAsync_RestoresOriginalFileContent()
    {
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");

        // Pre-existing file with original content
        File.WriteAllText(Path.Combine(gameDir, "existing.dll"), "ORIGINAL");
        string modFile = tmp.WriteFile("Mod/existing.dll", "MODDED");

        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["existing.dll"] = modFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            // Verify overwrite happened
            Assert.Equal("MODDED", File.ReadAllText(Path.Combine(gameDir, "existing.dll")));

            // Rollback
            var manifest = Backend.GetRollbackManifests(gameKey).Single();
            await Backend.RollbackDeployAsync(manifest);

            // Original content must be restored
            Assert.Equal("ORIGINAL", File.ReadAllText(Path.Combine(gameDir, "existing.dll")));
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Rollback: remove newly deployed file ─────────────────────────────────

    [Fact]
    public async Task RollbackDeployAsync_RemovesNewlyDeployedFile()
    {
        // A file that did not exist before deployment (BackupFilePath=null in the
        // manifest entry) must be deleted by rollback, not just overwritten.
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");
        string modFile = tmp.WriteFile("Mod/newfile.asi", "MOD CONTENT");

        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["newfile.asi"] = modFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            Assert.True(File.Exists(Path.Combine(gameDir, "newfile.asi")),
                "File should exist after deploy.");

            var manifest = Backend.GetRollbackManifests(gameKey).Single();

            // Verify manifest recorded this as a new file (no backup)
            var entry = Assert.Single(manifest.Entries);
            Assert.Null(entry.BackupFilePath);

            // Rollback
            await Backend.RollbackDeployAsync(manifest);

            Assert.False(File.Exists(Path.Combine(gameDir, "newfile.asi")),
                "New file should be removed by rollback.");
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Rollback: unrelated files untouched ──────────────────────────────────

    [Fact]
    public async Task RollbackDeployAsync_LeavesUnrelatedFilesIntact()
    {
        // A file that was in the game directory but not part of the deploy must
        // remain exactly as-is after rollback.
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");

        // File not touched by this deploy
        File.WriteAllText(Path.Combine(gameDir, "untouched.txt"), "UNTOUCHED");

        string modFile = tmp.WriteFile("Mod/plugin.asi", "ASI");

        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["plugin.asi"] = modFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            var manifest = Backend.GetRollbackManifests(gameKey).Single();
            await Backend.RollbackDeployAsync(manifest);

            // Untouched file must still be there with original content
            Assert.True(File.Exists(Path.Combine(gameDir, "untouched.txt")));
            Assert.Equal("UNTOUCHED", File.ReadAllText(Path.Combine(gameDir, "untouched.txt")));
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }

    // ── Rollback: subdirectory file paths ────────────────────────────────────

    [Fact]
    public async Task RollbackDeployAsync_HandlesSubdirectoryPaths()
    {
        // Files deployed into a subdirectory of the game dir (e.g. scripts\)
        // must have their relative path reconstructed correctly by rollback.
        using var tmp = new TempDirectory();
        string gameDir = tmp.CreateSubDir("GameDir");
        Directory.CreateDirectory(Path.Combine(gameDir, "scripts"));
        File.WriteAllText(Path.Combine(gameDir, "scripts", "existing.asi"), "ORIGINAL_ASI");

        string modFile = tmp.WriteFile("Mod/existing.asi", "MODDED_ASI");

        string gameKey   = UniqueKey();
        string backupDir = Path.Combine(Backend.BackupsPath, gameKey);
        try
        {
            // fileMap key uses the relative path the planner would produce
            var fileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.Combine("scripts", "existing.asi")] = modFile,
            };
            await Backend.DeployFilesToGameDirAsync(gameKey, gameDir, fileMap, ["TestMod"]);

            Assert.Equal("MODDED_ASI",
                File.ReadAllText(Path.Combine(gameDir, "scripts", "existing.asi")));

            var manifest = Backend.GetRollbackManifests(gameKey).Single();
            await Backend.RollbackDeployAsync(manifest);

            Assert.Equal("ORIGINAL_ASI",
                File.ReadAllText(Path.Combine(gameDir, "scripts", "existing.asi")));
        }
        finally
        {
            if (Directory.Exists(backupDir)) BackendCore.ForceDeleteDirectory(backupDir);
        }
    }
}
