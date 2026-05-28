using System.Collections.Generic;

namespace TMM
{
    /// <summary>
    /// One detected candidate during import-from-install analysis.
    /// Files are moved into a dedicated mod folder, then restored via the
    /// generated deploy plan.
    /// </summary>
    public sealed class ModImportCandidate
    {
        public bool IsSelected { get; set; } = true;

        public string Name { get; set; } = string.Empty;

        public string? GroupName { get; set; }

        public string SourceSummary { get; set; } = string.Empty;

        public string? Warning { get; set; }

        public List<string> FilePaths { get; set; } = new();

        public int FileCount => FilePaths.Count;
    }
}
