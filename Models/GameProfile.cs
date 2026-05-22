// TABLE OF CONTENTS
// -----------------------------------------------------------------
//   GameProfile RECORD
//     Known 1.0 MD5 hashes ........................................ ~18
//     Static profiles (III, VC, SA) + All list ................... ~23
//     ByKey() lookup .............................................. ~53
//     IsValidMd5() / AllValidMd5s ................................ ~57
//     Folder name helpers (RawFolderName) .......................... ~71
// -----------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace TMM
{
    /// <summary>
    /// Single source of truth for per-game data.
    /// </summary>
    public sealed record GameProfile(
        string Key,
        string DisplayName,
        string ShortName,
        string ExeName,
        string SteamAppId,
        string Vanilla10Md5,
        IReadOnlyList<string>? AdditionalValidMd5s = null)
    {
        // -- Known 1.0 hashes ----------------------------------------------------
        // Multiple variants exist: US/EU retail pressings, different downgrader
        // tools produce slightly different binaries. All listed hashes are valid
        // 1.0 builds that pass the Steam-DRM bypass test.

        public static readonly GameProfile III = new(
            Key: "III",
            DisplayName: "GTA III",
            ShortName: "III",
            ExeName: "gta3.exe",
            SteamAppId: "12100",
            Vanilla10Md5: "85414bf9eb414d00ad81062360f0db1f");

        public static readonly GameProfile VC = new(
            Key: "VC",
            DisplayName: "GTA Vice City",
            ShortName: "VC",
            ExeName: "gta-vc.exe",
            SteamAppId: "12110",
            Vanilla10Md5: "8f3707edaa361957c70f8b13998816f1",
            AdditionalValidMd5s: new[]
            {
                "167a5c8b31b3e0dbefa033ca24453d4e"   // ModDB v1.0 downgrader variant
            });

        public static readonly GameProfile SA = new(
            Key: "SA",
            DisplayName: "GTA San Andreas",
            ShortName: "SA",
            ExeName: "gta-sa.exe",
            SteamAppId: "12120",
            Vanilla10Md5: "00eb2056583dfa6a4ca79dedf70df5e9");

        public static readonly IReadOnlyList<GameProfile> All = new[] { III, VC, SA };

        public static GameProfile? ByKey(string? key) =>
            string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(p => p.Key == key);

        /// <summary>Returns true for any known-good 1.0 MD5 for this game.</summary>
        public bool IsValidMd5(string md5)
        {
            if (string.IsNullOrEmpty(md5)) return false;
            string lower = md5.ToLowerInvariant();
            if (lower == Vanilla10Md5) return true;
            return AdditionalValidMd5s?.Any(h => h == lower) ?? false;
        }

        /// <summary>All accepted 1.0 hashes, for display in diagnostics.</summary>
        public IEnumerable<string> AllValidMd5s =>
            AdditionalValidMd5s == null
                ? new[] { Vanilla10Md5 }
                : new[] { Vanilla10Md5 }.Concat(AdditionalValidMd5s);

        public string RawFolderName => $"ModsRaw{Key}";
    }
}
