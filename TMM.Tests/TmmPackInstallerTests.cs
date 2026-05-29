using System.IO.Compression;
using System.Text.Json;
using TMM.Services;
using TMM.Tests.Helpers;

namespace TMM.Tests;

/// <summary>
/// Unit tests for <see cref="TmmPackInstaller"/> — manifest parsing and the
/// forward-compatibility guard. The guard throws before any filesystem writes,
/// so these tests need no registered game and pollute no real AppData.
/// </summary>
public class TmmPackInstallerTests
{
    private static string WritePack(string path, TmmPackBuilder.Manifest manifest)
    {
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("manifest.json");
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, manifest, JsonHelper.PrettyOptions);
        return path;
    }

    [Fact]
    public void ReadManifest_ReturnsBundledMetadata()
    {
        using var tmp = new TempDirectory();
        string pack = WritePack(Path.Combine(tmp.Path, "test.tmmpack"), new TmmPackBuilder.Manifest
        {
            GameName    = "Some Game",
            LoadoutName = "My Loadout",
            ModNames    = ["ModA", "ModB"],
        });

        var manifest = TmmPackInstaller.ReadManifest(pack);

        Assert.Equal("Some Game", manifest.GameName);
        Assert.Equal("My Loadout", manifest.LoadoutName);
        Assert.Equal(2, manifest.ModNames.Count);
    }

    [Fact]
    public async Task ImportAsync_RejectsForwardIncompatiblePack()
    {
        using var tmp = new TempDirectory();
        string pack = WritePack(Path.Combine(tmp.Path, "future.tmmpack"), new TmmPackBuilder.Manifest
        {
            Version     = TmmPackBuilder.CurrentVersion + 1,
            GameName    = "Future Game",
            LoadoutName = "Future",
        });

        var backend = new BackendCore();

        await Assert.ThrowsAsync<InvalidDataException>(
            () => TmmPackInstaller.ImportAsync(backend, pack, "III"));
    }

    [Fact]
    public void ReadManifest_ThrowsOnNonPack()
    {
        using var tmp = new TempDirectory();
        string notAPack = Path.Combine(tmp.Path, "empty.tmmpack");
        using (var fs = File.Create(notAPack))
        using (new ZipArchive(fs, ZipArchiveMode.Create)) { /* no manifest entry */ }

        Assert.Throws<InvalidDataException>(() => TmmPackInstaller.ReadManifest(notAPack));
    }
}
