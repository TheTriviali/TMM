using System.Collections.Generic;

namespace TMM
{
    public record DeployManifest(
        string Timestamp,
        string GameKey,
        string GameDirectory,
        List<string> ModNames,
        List<BackupEntry> Entries,
        List<string>? Directories = null);

    public record BackupEntry(
        string RelativePath,
        string? BackupFilePath,
        long OriginalSize);
}
