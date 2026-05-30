using System.Collections.Generic;

namespace TMM.Services
{
    /// <summary>An entry in the structured error catalog.</summary>
    public sealed record TmmErrorEntry(string Code, string Source, string TitleKey, string CauseKey, string FixKey);

    /// <summary>
    /// Static catalog of structured TMM error codes. Each entry maps a short code
    /// (e.g. "TMM-E001") to locale keys for its title, cause, and fix so the
    /// Troubleshooting page can render them in any language.
    /// Passes G3–G9 append their own codes here.
    /// </summary>
    public static class TmmError
    {
        private static readonly Dictionary<string, TmmErrorEntry> _catalog = new();

        /// <summary>All registered error entries, keyed by code.</summary>
        public static IReadOnlyDictionary<string, TmmErrorEntry> All => _catalog;

        /// <summary>Look up an entry by code; returns null if not registered.</summary>
        public static TmmErrorEntry? Get(string? code)
            => code is not null && _catalog.TryGetValue(code, out var e) ? e : null;

        static TmmError()
        {
            // ── G3: Deploy / rollback ─────────────────────────────────────────────
            Register("TMM-E001", "Deploy",   "TmmError_E001_Title", "TmmError_E001_Cause", "TmmError_E001_Fix");
            Register("TMM-E002", "Deploy",   "TmmError_E002_Title", "TmmError_E002_Cause", "TmmError_E002_Fix");

            // ── G4: Install / import ──────────────────────────────────────────────
            Register("TMM-E003", "Install",  "TmmError_E003_Title", "TmmError_E003_Cause", "TmmError_E003_Fix");
            Register("TMM-E007", "Install",  "TmmError_E007_Title", "TmmError_E007_Cause", "TmmError_E007_Fix");
            Register("TMM-E008", "Install",  "TmmError_E008_Title", "TmmError_E008_Cause", "TmmError_E008_Fix");

            // ── G5: Backup ────────────────────────────────────────────────────────
            Register("TMM-E004", "Backup",   "TmmError_E004_Title", "TmmError_E004_Cause", "TmmError_E004_Fix");
            Register("TMM-E009", "Backup",   "TmmError_E009_Title", "TmmError_E009_Cause", "TmmError_E009_Fix");

            // ── G6: Loadouts ──────────────────────────────────────────────────────
            Register("TMM-E010", "Loadouts", "TmmError_E010_Title", "TmmError_E010_Cause", "TmmError_E010_Fix");
            Register("TMM-E011", "Loadouts", "TmmError_E011_Title", "TmmError_E011_Cause", "TmmError_E011_Fix");

            // ── G7: Paths / settings ──────────────────────────────────────────────
            Register("TMM-E005", "GamePath", "TmmError_E005_Title", "TmmError_E005_Cause", "TmmError_E005_Fix");
            Register("TMM-E006", "Settings", "TmmError_E006_Title", "TmmError_E006_Cause", "TmmError_E006_Fix");
        }

        private static void Register(string code, string source, string titleKey, string causeKey, string fixKey)
            => _catalog[code] = new TmmErrorEntry(code, source, titleKey, causeKey, fixKey);
    }
}
