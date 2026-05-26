namespace TMM.Tests.Helpers;

/// <summary>
/// Creates an isolated temporary directory for a single test and deletes it (recursively)
/// on <see cref="Dispose"/>. Use inside a <c>using</c> block to guarantee cleanup even when
/// the test throws.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    /// <summary>Root path of the temporary directory.</summary>
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                               "TMM_Tests_" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    /// <summary>Creates a named sub-directory inside the temp root and returns its full path.</summary>
    public string CreateSubDir(string name)
    {
        string full = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>Creates a file at <paramref name="relativePath"/> inside the temp root.</summary>
    public string WriteFile(string relativePath, string contents = "")
    {
        string full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, contents);
        return full;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                BackendCore.ForceDeleteDirectory(Path);
        }
        catch { /* best-effort — leftover temp files are harmless */ }
    }
}
