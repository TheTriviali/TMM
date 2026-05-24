using System.Collections.Generic;
using System.Linq;

namespace TMM
{
    /// <summary>Detected state of a game executable.</summary>
    public enum ExeStatus { Unknown, Vanilla, Downgraded }

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
        IReadOnlyList<string>? AdditionalValidMd5s = null,
        IReadOnlyList<ConditionalRoute>? ConditionalRoutes = null)
    {
        // ── GTA III Series ───────────────────────────────────────────────────────

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

        // ── GTA IV Series ─────────────────────────────────────────────────────
        // Complete Edition layout (Steam):
        //   …\Grand Theft Auto IV\GTAIV\           ← IV exe lives here
        //   …\Grand Theft Auto IV\GTAIV\TLAD\      ← TLaD
        //   …\Grand Theft Auto IV\GTAIV\EFLC\      ← TBoGT
        //
        // ASI routing: if a "plugins\" folder exists inside the episode dir,
        // put .asi files there; otherwise drop them in the episode root.

        private static readonly IReadOnlyList<ConditionalRoute> IvAsiRoute = new[]
        {
            new ConditionalRoute(".asi", "plugins", "plugins", ".")
        };

        public static readonly GameProfile IV = new(
            Key: "IV",
            DisplayName: "GTA IV",
            ShortName: "IV",
            ExeName: "GTAIV.exe",
            SteamAppId: "12210",
            Vanilla10Md5: "",           // no downgrade check for IV
            ConditionalRoutes: IvAsiRoute);

        public static readonly GameProfile TLaD = new(
            Key: "TLaD",
            DisplayName: "GTA IV: The Lost and Damned",
            ShortName: "TLaD",
            ExeName: "TLAD.exe",
            SteamAppId: "",             // part of IV Steam install; no separate AppId
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute);

        public static readonly GameProfile TBoGT = new(
            Key: "TBoGT",
            DisplayName: "GTA IV: The Ballad of Gay Tony",
            ShortName: "TBoGT",
            ExeName: "EFLC.exe",
            SteamAppId: "",
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute);

        // ── Collections ──────────────────────────────────────────────────────────

        public static readonly IReadOnlyList<GameProfile> All =
            new[] { III, VC, SA, IV, TLaD, TBoGT };

        /// <summary>Keys that belong to the GTA IV episode family.</summary>
        public static readonly IReadOnlyCollection<string> IvFamilyKeys =
            new HashSet<string> { "IV", "TLaD", "TBoGT" };

        // ── Lookups ───────────────────────────────────────────────────────────

        public static GameProfile? ByKey(string? key) =>
            string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(p => p.Key == key);

        /// <summary>
        /// True when this profile has a known vanilla MD5 to check against.
        /// False for IV-family and custom games — no downgrade check is performed.
        /// </summary>
        public bool HasExeCheck => !string.IsNullOrEmpty(Vanilla10Md5);

        /// <summary>Returns true for any known-good 1.0 MD5 for this game.</summary>
        public bool IsValidMd5(string md5)
        {
            if (string.IsNullOrEmpty(md5) || string.IsNullOrEmpty(Vanilla10Md5)) return false;
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
