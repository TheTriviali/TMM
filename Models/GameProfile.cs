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
        IReadOnlyList<ConditionalRoute>? ConditionalRoutes = null)
    {
        // ── Library display properties ─────────────────────────────────────────
        /// <summary>Library card gradient start color hex, e.g. "#1B3A1B".</summary>
        public string GradientStartHex { get; init; } = "#1A1A2E";
        /// <summary>Library card gradient end color hex, e.g. "#0C1E0C".</summary>
        public string GradientEndHex { get; init; } = "#0D0D1A";
        /// <summary>Maturity status shown as chip on library card.</summary>
        public ReleaseStatus LibraryStatus { get; init; } = ReleaseStatus.Release;

        // ── GTA III Series ───────────────────────────────────────────────────────

        public static readonly GameProfile III = new(
            Key: "III",
            DisplayName: "GTA III",
            ShortName: "III",
            ExeName: "gta3.exe",
            SteamAppId: "12100")
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };

        public static readonly GameProfile VC = new(
            Key: "VC",
            DisplayName: "GTA Vice City",
            ShortName: "VC",
            ExeName: "gta-vc.exe",
            SteamAppId: "12110")
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };

        public static readonly GameProfile SA = new(
            Key: "SA",
            DisplayName: "GTA San Andreas",
            ShortName: "SA",
            ExeName: "gta-sa.exe",
            SteamAppId: "12120")
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };

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
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };

        public static readonly GameProfile TLaD = new(
            Key: "TLaD",
            DisplayName: "GTA IV: The Lost and Damned",
            ShortName: "TLaD",
            ExeName: "TLAD.exe",
            SteamAppId: "",             // part of IV Steam install; no separate AppId
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };

        public static readonly GameProfile TBoGT = new(
            Key: "TBoGT",
            DisplayName: "GTA IV: The Ballad of Gay Tony",
            ShortName: "TBoGT",
            ExeName: "EFLC.exe",
            SteamAppId: "",
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };

        // ── Collections ──────────────────────────────────────────────────────────

        public static readonly IReadOnlyList<GameProfile> All =
            new[] { III, VC, SA, IV, TLaD, TBoGT };

        /// <summary>Keys that belong to the GTA IV episode family (used for path auto-derivation).</summary>
        public static readonly IReadOnlyCollection<string> IvFamilyKeys =
            new HashSet<string> { "IV", "TLaD", "TBoGT" };

        // ── Lookups ───────────────────────────────────────────────────────────

        public static GameProfile? ByKey(string? key) =>
            string.IsNullOrEmpty(key) ? null : All.FirstOrDefault(p => p.Key == key);

        public string RawFolderName => $"ModsRaw{Key}";
    }
}
