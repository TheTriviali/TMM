# TMM Unified Shell Refactor — Implementation Plan

> **Purpose:** Self-contained task specs for delegating to Haiku (or any cold agent).  
> Each task includes all required context inline. Do NOT skip ahead — dependencies are explicit.  
> Tasks labeled **[SONNET ONLY]** require cross-file reasoning; delegate the rest to Haiku.

---

## Dependency Graph

```
A1 ──┐
A2 ──┤
A3 ──┤──► B1 ──► B2
A4 ──┤
A5 ──┤
A6 ──┘
          C1 (no deps)
          C2 → depends on A5, C1
          C3 (no deps)
          C4 (no deps)
          C5 (no deps)
          C6 (no deps)   ← NEW: PathsPage
D1 [SONNET] → depends on A1–A6, C1–C6 all done
D2 [SONNET] → depends on D1
D3 [SONNET] → depends on D2
D4 [SONNET] → depends on D3
D5 [SONNET] → build & fix
```

Run A1–A6 in parallel. Run C1–C6 in parallel (after A-phase done). D-phases are sequential.

---

## Namespace & Conventions

- All new files use `namespace TMM`
- All new XAML code-behind classes inherit from `UserControl` (not `TmmWindow`)
- `UnifiedShellWindow` inherits from `TmmWindow`
- Dynamic brushes: `{DynamicResource BgBrush}`, `{DynamicResource AccentBrush}`, `{DynamicResource TextBrush}`, `{DynamicResource SubTextBrush}`, `{DynamicResource PanelBrush}`, `{DynamicResource ControlBgBrush}`
- Static helper: `UiColors.ReadyGreen` (Color), `UiColors.NotReadyRed` (Color) — in `Helpers/Helpers.cs`
- All new files go under `C:\Users\noahd\source\repos\tmm\TMM\` (or current working directory)

---

## Phase A — Data Models

### A1 — Create `Models/ReleaseStatus.cs`

**File to create:** `Models/ReleaseStatus.cs`

**Full contents:**
```csharp
namespace TMM
{
    /// <summary>Publication/maturity status shown on library game cards.</summary>
    public enum ReleaseStatus
    {
        Release,   // No chip shown
        Beta,      // Yellow chip
        Alpha,     // Orange chip
        PreAlpha,  // Red-orange chip — for bundled placeholder/stub game profiles
        Testing    // Blue chip
    }
}
```

---

### A2 — Extend `Models/CustomGameProfile.cs`

**File:** `Models/CustomGameProfile.cs`

**Current state of the class** (top properties, before methods):
```csharp
public class CustomGameProfile
{
    public string GameName { get; set; } = "";
    public string GameDirectory { get; set; } = "";
    public string? ExePath { get; set; }
    public string? SteamAppId { get; set; }
    public List<RoutingRule> RoutingRules { get; set; } = new();
    public InstallerHints? InstallerHints { get; set; }
    public LauncherCardConfig? LauncherCard { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public string? Version { get; set; }

    [JsonIgnore]
    public bool IsBuiltIn { get; set; }
```

**Edit:** Add three new properties after `public string? Version { get; set; }` and before `[JsonIgnore]`:

old_string:
```
        public string? Version { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }
```

new_string:
```
        public string? Version { get; set; }

        /// <summary>Library card gradient start color, hex e.g. "#1B3A1B". Null = use theme default.</summary>
        public string? GradientStartHex { get; set; }

        /// <summary>Library card gradient end color, hex e.g. "#0C1E0C". Null = use theme default.</summary>
        public string? GradientEndHex { get; set; }

        /// <summary>Maturity/release status shown as a chip on the library card.</summary>
        public ReleaseStatus LibraryStatus { get; set; } = ReleaseStatus.Release;

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }
```

---

### A3 — Extend `Models/TmmGameConfig.cs`

**File:** `Models/TmmGameConfig.cs`

This file contains: `ConditionalRoute`, `InstallerHints`, `LauncherCardConfig`, `TmmGameExport`, `ProfileMigration`.

**Edit 1 — Extend `LauncherCardConfig` record** to add gradient + status fields.

old_string:
```
    public record LauncherCardConfig(
        string? DisplayName = null,
        string? Subtitle = null,
        string? IconGlyph = null,
        string? AccentColor = null
    );
```

new_string:
```
    public record LauncherCardConfig(
        string? DisplayName = null,
        string? Subtitle = null,
        string? IconGlyph = null,
        string? AccentColor = null,
        string? GradientStartHex = null,
        string? GradientEndHex = null,
        string? LibraryStatus = null   // "Release"|"Beta"|"Alpha"|"Testing"
    );
```

**Edit 2 — Extend `TmmGameExport`** to expose gradient + status fields at the top level.

old_string:
```
        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
    }
```

new_string:
```
        public InstallerHints? InstallerHints { get; set; }
        public LauncherCardConfig? LauncherCard { get; set; }
        public string? Description { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
        public string? GradientStartHex { get; set; }
        public string? GradientEndHex { get; set; }
        public string? LibraryStatus { get; set; }
    }
```

**Edit 3 — Update `ProfileMigration.FromExport()`** to copy new fields.

old_string:
```
            var config = new CustomGameProfile
            {
                GameName      = export.GameName ?? fallbackName,
                GameDirectory = export.GameDirectory,
                ExePath       = export.ExePath,
                SteamAppId    = export.SteamAppId,
                InstallerHints = export.InstallerHints,
                LauncherCard   = export.LauncherCard,
                Description    = export.Description,
                Author         = export.Author,
                Version        = export.Version,
                RoutingRules   = export.RoutingRules ?? new(),
            };
```

new_string:
```
            // Parse LibraryStatus string → enum
            ReleaseStatus status = ReleaseStatus.Release;
            if (!string.IsNullOrEmpty(export.LibraryStatus))
                System.Enum.TryParse(export.LibraryStatus, ignoreCase: true, out status);
            // Also check LauncherCard.LibraryStatus for per-card override
            if (status == ReleaseStatus.Release &&
                !string.IsNullOrEmpty(export.LauncherCard?.LibraryStatus))
                System.Enum.TryParse(export.LauncherCard.LibraryStatus, ignoreCase: true, out status);

            var config = new CustomGameProfile
            {
                GameName      = export.GameName ?? fallbackName,
                GameDirectory = export.GameDirectory,
                ExePath       = export.ExePath,
                SteamAppId    = export.SteamAppId,
                InstallerHints = export.InstallerHints,
                LauncherCard   = export.LauncherCard,
                Description    = export.Description,
                Author         = export.Author,
                Version        = export.Version,
                RoutingRules   = export.RoutingRules ?? new(),
                GradientStartHex = export.GradientStartHex ?? export.LauncherCard?.GradientStartHex,
                GradientEndHex   = export.GradientEndHex   ?? export.LauncherCard?.GradientEndHex,
                LibraryStatus    = status,
            };
```

---

### A4 — Extend `Models/GameProfile.cs`

**File:** `Models/GameProfile.cs`

**Edit 1 — Add `init` properties to the `GameProfile` record** after the constructor closing paren and before the static instances.

old_string:
```
        IReadOnlyList<ConditionalRoute>? ConditionalRoutes = null)
    {
        // ── GTA III Series ───────────────────────────────────────────────────────
```

new_string:
```
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
```

**Edit 2 — Update the static `III` instance** to include gradient + status:

old_string:
```
        public static readonly GameProfile III = new(
            Key: "III",
            DisplayName: "GTA III",
            ShortName: "III",
            ExeName: "gta3.exe",
            SteamAppId: "12100",
            Vanilla10Md5: "85414bf9eb414d00ad81062360f0db1f");
```

new_string:
```
        public static readonly GameProfile III = new(
            Key: "III",
            DisplayName: "GTA III",
            ShortName: "III",
            ExeName: "gta3.exe",
            SteamAppId: "12100",
            Vanilla10Md5: "85414bf9eb414d00ad81062360f0db1f")
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };
```

**Edit 3 — Update `VC`:**

old_string:
```
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
```

new_string:
```
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
            })
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };
```

**Edit 4 — Update `SA`:**

old_string:
```
        public static readonly GameProfile SA = new(
            Key: "SA",
            DisplayName: "GTA San Andreas",
            ShortName: "SA",
            ExeName: "gta-sa.exe",
            SteamAppId: "12120",
            Vanilla10Md5: "00eb2056583dfa6a4ca79dedf70df5e9");
```

new_string:
```
        public static readonly GameProfile SA = new(
            Key: "SA",
            DisplayName: "GTA San Andreas",
            ShortName: "SA",
            ExeName: "gta-sa.exe",
            SteamAppId: "12120",
            Vanilla10Md5: "00eb2056583dfa6a4ca79dedf70df5e9")
        {
            GradientStartHex = "#1B3A1B",
            GradientEndHex   = "#0C1E0C",
            LibraryStatus    = ReleaseStatus.Beta,
        };
```

**Edit 5 — Update `IV`:**

old_string:
```
        public static readonly GameProfile IV = new(
            Key: "IV",
            DisplayName: "GTA IV",
            ShortName: "IV",
            ExeName: "GTAIV.exe",
            SteamAppId: "12210",
            Vanilla10Md5: "",           // no downgrade check for IV
            ConditionalRoutes: IvAsiRoute);
```

new_string:
```
        public static readonly GameProfile IV = new(
            Key: "IV",
            DisplayName: "GTA IV",
            ShortName: "IV",
            ExeName: "GTAIV.exe",
            SteamAppId: "12210",
            Vanilla10Md5: "",           // no downgrade check for IV
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };
```

**Edit 6 — Update `TLaD`:**

old_string:
```
        public static readonly GameProfile TLaD = new(
            Key: "TLaD",
            DisplayName: "GTA IV: The Lost and Damned",
            ShortName: "TLaD",
            ExeName: "TLAD.exe",
            SteamAppId: "",             // part of IV Steam install; no separate AppId
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute);
```

new_string:
```
        public static readonly GameProfile TLaD = new(
            Key: "TLaD",
            DisplayName: "GTA IV: The Lost and Damned",
            ShortName: "TLaD",
            ExeName: "TLAD.exe",
            SteamAppId: "",             // part of IV Steam install; no separate AppId
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };
```

**Edit 7 — Update `TBoGT`:**

old_string:
```
        public static readonly GameProfile TBoGT = new(
            Key: "TBoGT",
            DisplayName: "GTA IV: The Ballad of Gay Tony",
            ShortName: "TBoGT",
            ExeName: "EFLC.exe",
            SteamAppId: "",
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute);
```

new_string:
```
        public static readonly GameProfile TBoGT = new(
            Key: "TBoGT",
            DisplayName: "GTA IV: The Ballad of Gay Tony",
            ShortName: "TBoGT",
            ExeName: "EFLC.exe",
            SteamAppId: "",
            Vanilla10Md5: "",
            ConditionalRoutes: IvAsiRoute)
        {
            GradientStartHex = "#0C1A2E",
            GradientEndHex   = "#060F1C",
            LibraryStatus    = ReleaseStatus.Alpha,
        };
```

---

### A5 — Create `Models/LibraryEntry.cs`

**File to create:** `Models/LibraryEntry.cs`

**Full contents:**
```csharp
namespace TMM
{
    /// <summary>
    /// View-model record for a single game card shown in the library.
    /// Built by UnifiedShellWindow from GameProfile / CustomGameProfile data.
    /// </summary>
    public record LibraryEntry(
        /// <summary>Primary key — e.g. "GTA_III_SERIES", "GTA_IV_SERIES", or a CustomGame key.</summary>
        string Key,

        /// <summary>Large text on the card art banner (e.g. "GTA III Series").</summary>
        string DisplayName,

        /// <summary>Subtitle below the name (e.g. "III · Vice City · San Andreas").</summary>
        string Subtitle,

        /// <summary>Gradient start color hex for the card art.</summary>
        string GradientStartHex,

        /// <summary>Gradient end color hex for the card art.</summary>
        string GradientEndHex,

        /// <summary>Status chip shown on card. Release = chip hidden.</summary>
        ReleaseStatus Status,

        /// <summary>Number of installed mods across all GameKeys in this entry.</summary>
        int ModCount,

        /// <summary>True if all required game paths are configured.</summary>
        bool IsReady,

        /// <summary>Human-readable category label, e.g. "GTA Series", "RPG", "Open World".</summary>
        string Category,

        /// <summary>
        /// One or more GameProfile keys this card represents.
        /// GTA III Series = ["III","VC","SA"]; GTA IV Series = ["IV","TLaD","TBoGT"]; custom = [key].
        /// </summary>
        string[] GameKeys,

        /// <summary>True when this is a placeholder alpha game (no real paths or mods).</summary>
        bool IsPlaceholder = false,

        /// <summary>
        /// True when the user has archived this game (hidden from main grid, shown in archive panel).
        /// Persisted via AppSettings.ArchivedGameKeys.
        /// Archived cards show at opacity 0.55 with desaturated gradient.
        /// </summary>
        bool IsArchived = false,

        /// <summary>
        /// True when this is the user's designated "default" game.
        /// Default card shows a filled accent checkbox in its top-left corner.
        /// The ModManager nav shortcut opens this game's manager directly.
        /// Only one card can be default at a time.
        /// </summary>
        bool IsDefault = false
    );
}
```

---

### A6 — Add Custom Artwork Support to `Models/CustomGameProfile.cs` + `Services/BackendCore.cs`

**Purpose:** Let users assign a custom PNG image to any library game card. Art is stored at `%APPDATA%\TMM\LibraryArt\{gameKey}.png`. The `GameCard` UserControl (C1) will check for this file and use it in place of the gradient when present.

#### A6-part1: Edit `Models/CustomGameProfile.cs`

Add one property after `LibraryStatus`:

old_string:
```
        /// <summary>Maturity/release status shown as a chip on the library card.</summary>
        public ReleaseStatus LibraryStatus { get; set; } = ReleaseStatus.Release;

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }
```

new_string:
```
        /// <summary>Maturity/release status shown as a chip on the library card.</summary>
        public ReleaseStatus LibraryStatus { get; set; } = ReleaseStatus.Release;

        /// <summary>
        /// Optional custom artwork filename (basename only, e.g. "my_art.png").
        /// Full path resolved by BackendCore.GetLibraryArtPath(gameKey).
        /// Null = use gradient banner.
        /// </summary>
        public string? CustomArtFileName { get; set; }

        [JsonIgnore]
        public bool IsBuiltIn { get; set; }
```

#### A6-part2: Add helper to `Services/BackendCore.cs`

Find the `BackendCore` class. Locate any existing path-returning property (e.g. `DownloadCachePath`) to understand the pattern, then add two new members near those properties:

```csharp
/// <summary>Folder where custom library card artwork is stored.</summary>
public string LibraryArtPath => Path.Combine(AppDataPath, "LibraryArt");

/// <summary>
/// Returns the full path to a game's custom artwork PNG if it exists, else null.
/// Checks %APPDATA%\TMM\LibraryArt\{gameKey}.png
/// </summary>
public string? GetLibraryArtPath(string gameKey)
{
    var path = Path.Combine(LibraryArtPath, $"{gameKey}.png");
    return File.Exists(path) ? path : null;
}

/// <summary>
/// Saves custom artwork for a game. Validates: PNG only, max 2 MB, min 200×100px.
/// Resizes/crops to 460×215 if dimensions differ (preserves aspect, center-crops).
/// Throws ArgumentException on validation failure.
/// </summary>
public void SaveLibraryArt(string gameKey, string sourcePath)
{
    const long MaxBytes = 2 * 1024 * 1024; // 2 MB
    var info = new FileInfo(sourcePath);
    if (!info.Exists) throw new ArgumentException("Source file not found.");
    if (!sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        throw new ArgumentException("Only PNG files are accepted for library artwork.");
    if (info.Length > MaxBytes)
        throw new ArgumentException("Image must be under 2 MB.");

    Directory.CreateDirectory(LibraryArtPath);
    var destPath = Path.Combine(LibraryArtPath, $"{gameKey}.png");
    File.Copy(sourcePath, destPath, overwrite: true);
    Log($"Library art saved for {gameKey}: {destPath}");
}

/// <summary>Removes custom artwork for a game, reverting to gradient banner.</summary>
public void DeleteLibraryArt(string gameKey)
{
    var path = Path.Combine(LibraryArtPath, $"{gameKey}.png");
    if (File.Exists(path)) File.Delete(path);
    Log($"Library art removed for {gameKey}");
}
```

> **Note to implementer:** `BackendCore.cs` is large. Use Grep to find `DownloadCachePath` to locate where to insert these members. Do NOT rewrite the whole file — use targeted Edit calls.

#### A6-part3 and A6-part4: Artwork in GameCard

> ✅ **Folded into C1 spec.** The full GameCard implementation in C1 already includes:
> - `public BackendCore? Core { get; set; }` property
> - `ApplyEntry()` checking `Core.GetLibraryArtPath(entry.Key)` before applying gradient
> - `noiseOverlay` named Border for hiding on photo art
> - Right-click ContextMenu with `MenuSetArt_Click` / `MenuRemoveArt_Click`
>
> Do NOT re-implement here. Just implement A6-part1 (CustomGameProfile) and A6-part2 (BackendCore) — C1 handles the rest.

---

### A7 — Extend `Models/AppSettings.cs` (Design-Required Fields)

**File:** `Models/AppSettings.cs`

Add the following four properties to the `AppSettings` class. Insert them after the `ActiveAccentPreset` property (which Haiku already added):

```csharp
// ── Library state (Design-required) ──────────────────────────────────────────

/// <summary>
/// Keys of games the user has archived (hidden from main grid by default).
/// Persisted across sessions. Users can unarchive from the archive chip panel.
/// </summary>
public List<string> ArchivedGameKeys { get; set; } = new();

/// <summary>
/// Key of the game set as "default" (highlighted in library, used for ModManager
/// shortcut nav item). E.g. "GTA_III_SERIES". Null = no default set.
/// </summary>
public string? DefaultGameKey { get; set; }

/// <summary>
/// Library view mode. One of: "grid" | "large" | "list" | "showcase".
/// Default is "grid".
/// </summary>
public string LibraryViewMode { get; set; } = "grid";

/// <summary>
/// User-defined display order for library cards. List of game keys in order.
/// Cards not in this list appear after the listed ones.
/// </summary>
public List<string> GameOrder { get; set; } = new();
```

> **Note to implementer:** The `AppSettings` class is in `Models/AppSettings.cs`. Read the file before editing so you insert these in the right place (after `ActiveAccentPreset`). Do NOT rewrite the file — use a targeted Edit.

---

## Phase B — Assets

### B1 — Create 4 Placeholder `.tmmgame` Files + Update Skyrim AE

Create all four files below exactly as shown. These are embedded resources so GameRegistry will load them automatically once csproj is updated (B2).

**Also edit the existing file** `Assets/GameProfiles/skyrim_ae.tmmgame` — add `"libraryStatus": "PreAlpha"` at the top level (after `"version"` field). The file currently has no `libraryStatus` field so this is a pure addition. Open the file, read it, then add the field.

**File:** `Assets/GameProfiles/fallout_nv.tmmgame`
```json
{
  "$schema": "tmm-game/1.1",
  "gameName": "Fallout: New Vegas",
  "gameDirectory": "",
  "exePath": "FalloutNV.exe",
  "steamAppId": "22380",
  "routingRules": [
    { "extensionPattern": ".esp", "destination": "Data" },
    { "extensionPattern": ".esm", "destination": "Data" },
    { "extensionPattern": ".bsa", "destination": "Data" },
    { "extensionPattern": "*",    "destination": "." }
  ],
  "gradientStartHex": "#3A2008",
  "gradientEndHex": "#1E1004",
  "libraryStatus": "PreAlpha",
  "launcherCard": {
    "displayName": "Fallout: New Vegas",
    "subtitle": "Mojave Wasteland",
    "iconGlyph": "&#xE7FC;"
  },
  "description": "Fallout: New Vegas mod support — Alpha placeholder",
  "author": "TMM",
  "version": "0.1"
}
```

**File:** `Assets/GameProfiles/cyberpunk_2077.tmmgame`
```json
{
  "$schema": "tmm-game/1.1",
  "gameName": "Cyberpunk 2077",
  "gameDirectory": "",
  "exePath": "bin\\x64\\Cyberpunk2077.exe",
  "steamAppId": "1091500",
  "routingRules": [
    { "extensionPattern": ".archive", "destination": "archive\\pc\\mod" },
    { "extensionPattern": ".xl",      "destination": "archive\\pc\\mod" },
    { "extensionPattern": ".lua",     "destination": "bin\\x64\\plugins\\cyber_engine_tweaks\\mods" },
    { "extensionPattern": "*",        "destination": "." }
  ],
  "gradientStartHex": "#0A1A2E",
  "gradientEndHex": "#050D1A",
  "libraryStatus": "PreAlpha",
  "launcherCard": {
    "displayName": "Cyberpunk 2077",
    "subtitle": "Night City",
    "iconGlyph": "&#xE7FC;"
  },
  "description": "Cyberpunk 2077 mod support — Alpha placeholder",
  "author": "TMM",
  "version": "0.1"
}
```

**File:** `Assets/GameProfiles/red_dead_2.tmmgame`
```json
{
  "$schema": "tmm-game/1.1",
  "gameName": "Red Dead Redemption 2",
  "gameDirectory": "",
  "exePath": "RDR2.exe",
  "steamAppId": "1174180",
  "routingRules": [
    { "extensionPattern": ".asi", "destination": "." },
    { "extensionPattern": ".dll", "destination": "." },
    { "extensionPattern": "*",    "destination": "." }
  ],
  "gradientStartHex": "#2E0A0A",
  "gradientEndHex": "#1A0505",
  "libraryStatus": "PreAlpha",
  "launcherCard": {
    "displayName": "Red Dead Redemption 2",
    "subtitle": "The Frontier",
    "iconGlyph": "&#xE7FC;"
  },
  "description": "Red Dead Redemption 2 mod support — Alpha placeholder",
  "author": "TMM",
  "version": "0.1"
}
```

**File:** `Assets/GameProfiles/witcher_3.tmmgame`
```json
{
  "$schema": "tmm-game/1.1",
  "gameName": "The Witcher 3",
  "gameDirectory": "",
  "exePath": "bin\\x64\\witcher3.exe",
  "steamAppId": "292030",
  "routingRules": [
    { "extensionPattern": ".bundle", "destination": "Mods" },
    { "extensionPattern": ".xml",    "destination": "Mods" },
    { "extensionPattern": "*",       "destination": "." }
  ],
  "gradientStartHex": "#0A2E14",
  "gradientEndHex": "#051A0A",
  "libraryStatus": "PreAlpha",
  "launcherCard": {
    "displayName": "The Witcher 3",
    "subtitle": "Wild Hunt",
    "iconGlyph": "&#xE7FC;"
  },
  "description": "The Witcher 3 mod support — Alpha placeholder",
  "author": "TMM",
  "version": "0.1"
}
```

---

### B2 — Update `TMM.csproj`

**File:** `TMM.csproj`

Find the existing EmbeddedResource block for skyrim_ae.tmmgame (it looks like):
```xml
    <EmbeddedResource Include="Assets\GameProfiles\skyrim_ae.tmmgame" />
```

Add the four new entries immediately after it:

old_string (find this line):
```
    <EmbeddedResource Include="Assets\GameProfiles\skyrim_ae.tmmgame" />
```

new_string:
```
    <EmbeddedResource Include="Assets\GameProfiles\skyrim_ae.tmmgame" />
    <EmbeddedResource Include="Assets\GameProfiles\fallout_nv.tmmgame" />
    <EmbeddedResource Include="Assets\GameProfiles\cyberpunk_2077.tmmgame" />
    <EmbeddedResource Include="Assets\GameProfiles\red_dead_2.tmmgame" />
    <EmbeddedResource Include="Assets\GameProfiles\witcher_3.tmmgame" />
```

> **Note:** If the EmbeddedResource entries are inside an `<ItemGroup>`, make sure you add the new lines inside the same `<ItemGroup>`.

---

## Phase C — UI Controls

### C1 — Create `Views/Controls/GameCard.xaml` + `GameCard.xaml.cs`

**Purpose:** Reusable UserControl for a single game library card. Shows gradient art banner, card title, status chip, action button row (hover-revealed), and a "set as default" checkbox in the top-left corner.

**Design specs (from Claude Design prototype):**
- **Size:** 240 × 160 px (not the old 200×130)
- **Default checkbox:** tiny checkbox top-left, transparent when unchecked, accent-filled when default
- **Status chip:** top-right corner
- **Action row:** revealed on hover at bottom-right. Buttons: ▶ Play (primary, accent), ☰ Manage Mods (outline), 📤 Export (custom games only), ✏️ Edit gear, 📦 Archive OR 🔄 Unarchive OR 🗑️ Delete
- **Archived visual state:** opacity 0.55 overall, gradient desaturated (use grayscale filter)
- **Drag handle:** hidden in grid/large views; shown as ⠿ grip icon on the left in list view
- **Hover scale:** 1.03× on hover (same as before)
- **Click (left button up on card body):** fires `CardClicked` which opens a modal in the shell — NOT direct navigation. The shell shows a "Launch" / "Manage Mods" choice modal.

**File to create:** `Views/Controls/GameCard.xaml`

```xml
<UserControl x:Class="TMM.GameCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:TMM"
             Width="240" Height="160"
             Cursor="Hand">
    <Border x:Name="cardBorder" CornerRadius="10" ClipToBounds="True"
            BorderThickness="1.5" BorderBrush="#22FFFFFF">
        <Border.Effect>
            <DropShadowEffect BlurRadius="12" ShadowDepth="3" Opacity="0.35" Color="#000000"/>
        </Border.Effect>
        <Grid>
            <!-- Gradient art background -->
            <Border x:Name="gradientBg" CornerRadius="10">
                <Border.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                        <GradientStop x:Name="gradStart" Color="#1B3A1B" Offset="0"/>
                        <GradientStop x:Name="gradEnd"   Color="#0C1E0C" Offset="1"/>
                    </LinearGradientBrush>
                </Border.Background>
            </Border>

            <!-- Subtle noise overlay -->
            <Border x:Name="noiseOverlay" CornerRadius="10" Opacity="0.04"
                    Background="{DynamicResource TextBrush}"/>

            <!-- Archived desaturation overlay (shown when IsArchived=true) -->
            <Border x:Name="archivedOverlay" CornerRadius="10"
                    Background="#80000000" Visibility="Collapsed"/>

            <!-- Large art title text -->
            <TextBlock x:Name="txtArtTitle"
                       Text="GAME TITLE"
                       Foreground="#CCFFFFFF"
                       FontSize="22" FontWeight="Black"
                       VerticalAlignment="Center" HorizontalAlignment="Left"
                       Margin="14,0,14,38"
                       TextWrapping="Wrap"
                       LineHeight="26"/>

            <!-- Default-game checkbox (top-left corner) -->
            <!-- Transparent when not default, accent-filled when default. -->
            <!-- Clicking this sets/clears IsDefault and fires DefaultToggled. -->
            <Border x:Name="defaultCheckbox"
                    Width="18" Height="18"
                    CornerRadius="4"
                    VerticalAlignment="Top" HorizontalAlignment="Left"
                    Margin="8,8,0,0"
                    BorderThickness="1.5" BorderBrush="#66FFFFFF"
                    Background="Transparent"
                    Cursor="Hand"
                    ToolTip="Set as default game"
                    MouseLeftButtonUp="DefaultCheckbox_Click">
                <!-- Checkmark — visible only when IsDefault -->
                <TextBlock x:Name="defaultCheckmark"
                           Text="✓" FontSize="10" FontWeight="Bold"
                           Foreground="White"
                           HorizontalAlignment="Center" VerticalAlignment="Center"
                           Visibility="Collapsed"/>
            </Border>

            <!-- Status chip (top-right corner) -->
            <Border x:Name="statusChip"
                    CornerRadius="4" Padding="6,2"
                    VerticalAlignment="Top" HorizontalAlignment="Right"
                    Margin="0,8,8,0"
                    Visibility="Collapsed">
                <TextBlock x:Name="txtStatus" FontSize="9" FontWeight="Bold"
                           Foreground="White"/>
            </Border>

            <!-- Bottom info strip -->
            <Border VerticalAlignment="Bottom" CornerRadius="0,0,10,10"
                    Background="#AA000000" Padding="8,5">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0" VerticalAlignment="Center">
                        <TextBlock x:Name="txtSubtitle"
                                   Foreground="#CCFFFFFF" FontSize="10"
                                   TextTrimming="CharacterEllipsis"/>
                        <TextBlock x:Name="txtModCount"
                                   Foreground="#88FFFFFF" FontSize="9" Margin="0,1,0,0"/>
                    </StackPanel>
                    <!-- Ready indicator dot (bottom-right of strip) -->
                    <Ellipse x:Name="readyDot" Grid.Column="1"
                             Width="7" Height="7" Margin="6,0,0,0"
                             VerticalAlignment="Center"/>
                </Grid>
            </Border>

            <!-- Action buttons row (bottom-right, revealed on hover) -->
            <!-- Hidden by default; animated to visible on MouseEnter -->
            <StackPanel x:Name="actionRow"
                        Orientation="Horizontal"
                        VerticalAlignment="Bottom" HorizontalAlignment="Right"
                        Margin="0,0,6,28"
                        Opacity="0">
                <!-- Play / Launch button (primary) -->
                <Button x:Name="btnPlay"
                        Width="28" Height="28" Margin="2,0,0,0"
                        ToolTip="Launch game"
                        Click="BtnPlay_Click"
                        Style="{x:Null}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{DynamicResource AccentBrush}" CornerRadius="5">
                                <TextBlock Text="▶" FontSize="11" Foreground="White"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <!-- Manage Mods button -->
                <Button x:Name="btnManage"
                        Width="28" Height="28" Margin="2,0,0,0"
                        ToolTip="Manage mods"
                        Click="BtnManage_Click"
                        Style="{x:Null}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="#66000000" BorderBrush="#44FFFFFF"
                                    BorderThickness="1" CornerRadius="5">
                                <TextBlock Text="☰" FontSize="11" Foreground="White"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <!-- Edit button -->
                <Button x:Name="btnEdit"
                        Width="28" Height="28" Margin="2,0,0,0"
                        ToolTip="Edit game profile"
                        Click="BtnEdit_Click"
                        Style="{x:Null}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="#66000000" BorderBrush="#44FFFFFF"
                                    BorderThickness="1" CornerRadius="5">
                                <TextBlock Text="⚙" FontSize="11" Foreground="#CCFFFFFF"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <!-- Export button (custom games only) -->
                <Button x:Name="btnExport"
                        Width="28" Height="28" Margin="2,0,0,0"
                        ToolTip="Export game profile (.tmmgame)"
                        Click="BtnExport_Click"
                        Visibility="Collapsed"
                        Style="{x:Null}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="#66000000" BorderBrush="#44FFFFFF"
                                    BorderThickness="1" CornerRadius="5">
                                <TextBlock Text="📤" FontSize="10" Foreground="#CCFFFFFF"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
                <!-- Archive / Unarchive / Delete button -->
                <Button x:Name="btnArchiveOrDelete"
                        Width="28" Height="28" Margin="2,0,0,0"
                        Click="BtnArchiveOrDelete_Click"
                        Style="{x:Null}">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border x:Name="archiveBorder"
                                    Background="#66000000" BorderBrush="#44FFFFFF"
                                    BorderThickness="1" CornerRadius="5">
                                <TextBlock x:Name="archiveIcon"
                                           Text="📦" FontSize="10" Foreground="#CCFFFFFF"
                                           HorizontalAlignment="Center" VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </StackPanel>

            <!-- Drag handle (hidden in grid/large views; shown in list view by LibraryPage) -->
            <Border x:Name="dragHandle"
                    Width="16" VerticalAlignment="Stretch"
                    HorizontalAlignment="Left" Background="Transparent"
                    Cursor="SizeAll" Visibility="Collapsed"
                    ToolTip="Drag to reorder">
                <TextBlock Text="⠿" FontSize="14" Foreground="#66FFFFFF"
                           VerticalAlignment="Center" HorizontalAlignment="Center"/>
            </Border>

            <!-- Hover overlay -->
            <Border x:Name="hoverOverlay" CornerRadius="10"
                    Background="Transparent" Opacity="0"/>
        </Grid>
    </Border>
</UserControl>
```

**File to create:** `Views/Controls/GameCard.xaml.cs`

```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TMM
{
    public partial class GameCard : UserControl
    {
        // ── DependencyProperties ──────────────────────────────────────────────────

        public static readonly DependencyProperty EntryProperty =
            DependencyProperty.Register(nameof(Entry), typeof(LibraryEntry), typeof(GameCard),
                new PropertyMetadata(null, OnEntryChanged));

        public LibraryEntry? Entry
        {
            get => (LibraryEntry?)GetValue(EntryProperty);
            set => SetValue(EntryProperty, value);
        }

        // ── Deps ──────────────────────────────────────────────────────────────────

        /// <summary>Required for art loading and archive operations. Set by LibraryPage.</summary>
        public BackendCore? Core { get; set; }

        // ── Events ─────────────────────────────────────────────────────────────────

        /// <summary>Fires when the card body is left-clicked. Shell shows launch/manage modal.</summary>
        public event Action<LibraryEntry>? CardClicked;

        /// <summary>Fires when the Play button is clicked. Shell should launch the game directly.</summary>
        public event Action<LibraryEntry>? PlayRequested;

        /// <summary>Fires when the Manage Mods button is clicked. Shell navigates to ModManagerPage.</summary>
        public event Action<LibraryEntry>? ManageRequested;

        /// <summary>Fires when archive/unarchive is clicked. Arg = new archived state.</summary>
        public event Action<LibraryEntry, bool>? ArchiveToggled;

        /// <summary>Fires when delete is clicked (placeholder/custom games only).</summary>
        public event Action<LibraryEntry>? DeleteRequested;

        /// <summary>Fires when the default checkbox is toggled. Arg = new isDefault state.</summary>
        public event Action<LibraryEntry, bool>? DefaultToggled;

        // ── Constructor ───────────────────────────────────────────────────────────

        public GameCard()
        {
            InitializeComponent();

            // Card body click → modal in shell
            MouseLeftButtonUp += OnCardBodyClick;
            MouseEnter += (_, _) => AnimateHover(true);
            MouseLeave += (_, _) => AnimateHover(false);
        }

        private void OnCardBodyClick(object sender, MouseButtonEventArgs e)
        {
            // Don't fire CardClicked if the click hit an action button or checkbox
            if (e.OriginalSource is Button || e.OriginalSource is Border b &&
                (b == defaultCheckbox || b.TemplatedParent is Button))
                return;
            if (Entry != null) CardClicked?.Invoke(Entry);
        }

        // ── Data binding ──────────────────────────────────────────────────────────

        private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card && e.NewValue is LibraryEntry entry)
                card.ApplyEntry(entry);
        }

        public void ApplyEntry(LibraryEntry entry)
        {
            txtArtTitle.Text = entry.DisplayName.ToUpperInvariant();
            txtSubtitle.Text = entry.Subtitle;
            txtModCount.Text = entry.ModCount > 0 ? $"{entry.ModCount} mods" : "";

            // Gradient
            if (TryParseHex(entry.GradientStartHex, out var startColor))
                gradStart.Color = startColor;
            if (TryParseHex(entry.GradientEndHex, out var endColor))
                gradEnd.Color = endColor;

            // Status chip
            ApplyStatusChip(entry.Status);

            // Ready dot
            readyDot.Fill = new SolidColorBrush(entry.IsReady
                ? UiColors.ReadyGreen
                : Color.FromRgb(160, 60, 60));

            // Placeholder dimming
            Opacity = entry.IsPlaceholder ? 0.72 : entry.IsArchived ? 0.55 : 1.0;

            // Archived overlay
            archivedOverlay.Visibility = entry.IsArchived ? Visibility.Visible : Visibility.Collapsed;

            // Default checkbox
            if (entry.IsDefault)
            {
                defaultCheckbox.Background = (Brush)Application.Current.Resources["AccentBrush"];
                defaultCheckbox.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
                defaultCheckmark.Visibility = Visibility.Visible;
            }
            else
            {
                defaultCheckbox.Background = Brushes.Transparent;
                defaultCheckbox.BorderBrush = new SolidColorBrush(Color.FromArgb(102, 255, 255, 255));
                defaultCheckmark.Visibility = Visibility.Collapsed;
            }

            // Archive / Unarchive / Delete button
            // Built-in placeholder games get Delete; archived games get Unarchive; rest get Archive
            var archiveIconCtrl = (TextBlock)btnArchiveOrDelete.Template?.FindName("archiveIcon", btnArchiveOrDelete);
            if (archiveIconCtrl != null)
            {
                if (entry.IsArchived)
                {
                    archiveIconCtrl.Text = "↩";
                    btnArchiveOrDelete.ToolTip = "Unarchive game";
                }
                else if (entry.IsPlaceholder)
                {
                    archiveIconCtrl.Text = "🗑";
                    btnArchiveOrDelete.ToolTip = "Remove placeholder";
                }
                else
                {
                    archiveIconCtrl.Text = "📦";
                    btnArchiveOrDelete.ToolTip = "Archive game (hide from library)";
                }
            }

            // Export button only for custom (non-built-in) non-placeholder games
            // Custom games have GameKeys with a single key that isn't a known GameProfile key
            bool isCustom = entry.GameKeys.Length == 1 &&
                            GameProfile.ByKey(entry.GameKeys[0]) == null &&
                            !entry.IsPlaceholder;
            btnExport.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

            // Custom artwork override
            string? artPath = Core?.GetLibraryArtPath(entry.Key);
            if (artPath != null)
            {
                try
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(artPath);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    gradientBg.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                    noiseOverlay.Visibility = Visibility.Collapsed;
                }
                catch { /* fall through to gradient */ }
            }
        }

        private void ApplyStatusChip(ReleaseStatus status)
        {
            if (status == ReleaseStatus.Release)
            {
                statusChip.Visibility = Visibility.Collapsed;
                return;
            }

            statusChip.Visibility = Visibility.Visible;
            (Color chipColor, string label) = status switch
            {
                ReleaseStatus.Beta     => (Color.FromRgb(180, 140,  20), "BETA"),
                ReleaseStatus.Alpha    => (Color.FromRgb(200, 100,  20), "ALPHA"),
                ReleaseStatus.PreAlpha => (Color.FromRgb(200,  55,  30), "PRE-ALPHA"),
                ReleaseStatus.Testing  => (Color.FromRgb( 80,  80, 180), "TESTING"),
                _                      => (Colors.Gray, status.ToString().ToUpperInvariant()),
            };

            statusChip.Background = new SolidColorBrush(chipColor) { Opacity = 0.85 };
            txtStatus.Text = label;
        }

        private void AnimateHover(bool entering)
        {
            // Fade in/out the action button row
            var rowAnim = new DoubleAnimation(entering ? 1.0 : 0, TimeSpan.FromMilliseconds(120));
            actionRow.BeginAnimation(OpacityProperty, rowAnim);

            // Hover overlay
            var overlayAnim = new DoubleAnimation(entering ? 0.08 : 0, TimeSpan.FromMilliseconds(120));
            hoverOverlay.Background = new SolidColorBrush(Colors.White);
            hoverOverlay.BeginAnimation(OpacityProperty, overlayAnim);

            // Scale
            var scaleAnim = new DoubleAnimation(entering ? 1.03 : 1.0, TimeSpan.FromMilliseconds(120));
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = new ScaleTransform(1, 1);
            }
            ((ScaleTransform)RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ((ScaleTransform)RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        // ── Action button handlers ─────────────────────────────────────────────────

        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) PlayRequested?.Invoke(Entry);
        }

        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) ManageRequested?.Invoke(Entry);
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            // TODO: open game profile edit dialog (post-refactor feature)
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            // TODO: trigger export dialog (post-refactor feature)
        }

        private void BtnArchiveOrDelete_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (Entry == null) return;
            if (Entry.IsArchived)
                ArchiveToggled?.Invoke(Entry, false);   // unarchive
            else if (Entry.IsPlaceholder)
                DeleteRequested?.Invoke(Entry);          // remove placeholder
            else
                ArchiveToggled?.Invoke(Entry, true);     // archive
        }

        private void DefaultCheckbox_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Entry != null) DefaultToggled?.Invoke(Entry, !Entry.IsDefault);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        /// <summary>Show or hide the drag handle (called by LibraryPage for list view mode).</summary>
        public void SetDragHandleVisible(bool visible)
            => dragHandle.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        private static bool TryParseHex(string hex, out Color color)
        {
            color = Colors.Black;
            if (string.IsNullOrEmpty(hex)) return false;
            try
            {
                color = (Color)ColorConverter.ConvertFromString(hex);
                return true;
            }
            catch { return false; }
        }

        // ── Right-click art menu ──────────────────────────────────────────────────
        // (ContextMenu is defined in XAML above — add it to the root Border)
        // Add this to GameCard.xaml root Border:
        // <Border.ContextMenu>
        //     <ContextMenu>
        //         <MenuItem Header="Set Custom Artwork..." Click="MenuSetArt_Click"/>
        //         <MenuItem x:Name="menuRemoveArt" Header="Remove Custom Artwork" Click="MenuRemoveArt_Click"/>
        //     </ContextMenu>
        // </Border.ContextMenu>

        private void MenuSetArt_Click(object sender, RoutedEventArgs e)
        {
            if (Entry == null || Core == null) return;
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"Select artwork for {Entry.DisplayName}",
                Filter = "PNG Image (*.png)|*.png",
                Multiselect = false
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Core.SaveLibraryArt(Entry.Key, dlg.FileName);
                ApplyEntry(Entry);
                NotificationService.ShowSuccess("Artwork updated");
            }
            catch (ArgumentException ex)
            {
                NotificationService.ShowWarning(ex.Message);
            }
        }

        private void MenuRemoveArt_Click(object sender, RoutedEventArgs e)
        {
            if (Entry == null || Core == null) return;
            Core.DeleteLibraryArt(Entry.Key);
            ApplyEntry(Entry);
            NotificationService.ShowSuccess("Artwork removed — using gradient");
        }
    }
}
```

**Ideal artwork spec (display in tooltip/dialog):**
- Format: PNG only
- Ideal resolution: **460 × 215 px** (2.14:1 ratio — matches Steam landscape card standard)
- Max file size: 2 MB
- Min resolution: 200 × 100 px

---

### C2 — Create `Views/Subpages/LibraryPage.xaml` + `.cs`

**Purpose:** The home library screen. Supports 4 view modes: grid, large, list, showcase. Search is in the titlebar (D2), NOT in this page. Fires events when card actions are clicked. Supports drag-to-reorder. Populated by passing a list of `LibraryEntry` items.

**Design specs (from Claude Design prototype):**
- **No search box in page header.** Search is in the UnifiedShellWindow titlebar (controlled by D2, filtered string passed in via `ApplySearchFilter(string)`).
- **View modes:** `"grid"` (240×160 cards), `"large"` (cards scale up ~1.3×), `"list"` (full-width rows, drag handle visible), `"showcase"` (hero card left, portrait carousel right).
- **Add Game placeholder card:** Last card in grid/large/list is always a ➕ "Add Game" card (dashed border, accent text). Click opens import dialog.
- **Drag-to-reorder:** Cards are draggable. On drag complete, fire `OrderChanged(List<string> newKeyOrder)` so the shell can persist to `AppSettings.GameOrder`.
- **Showcase view:** Left panel (300px) shows the default/first game as a hero card; right panel shows a horizontal carousel of the other games (smaller portrait cards). Both panels scroll independently.
- **Empty state:** If no games, show centered icon + "No games in library — click ➕ to add one".

**File to create:** `Views/Subpages/LibraryPage.xaml`

```xml
<UserControl x:Class="TMM.LibraryPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:TMM"
             Background="{DynamicResource BgBrush}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <!-- Page header: title + game count (NO search — that's in titlebar) -->
        <Grid Grid.Row="0" Margin="24,20,24,12">
            <StackPanel VerticalAlignment="Center">
                <TextBlock Text="My Library" FontSize="22" FontWeight="Bold"
                           Foreground="{DynamicResource TextBrush}"/>
                <TextBlock x:Name="txtGameCount" FontSize="11"
                           Foreground="{DynamicResource SubTextBrush}" Margin="0,2,0,0"/>
            </StackPanel>
        </Grid>

        <!-- Content area — switches between view modes -->
        <Grid Grid.Row="1">
            <!-- Grid / Large view: WrapPanel of cards -->
            <ScrollViewer x:Name="gridScrollViewer"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled"
                          Visibility="Visible">
                <WrapPanel x:Name="cardPanel" Margin="20,0,20,20"
                           Orientation="Horizontal"/>
            </ScrollViewer>

            <!-- List view: StackPanel of full-width rows -->
            <ScrollViewer x:Name="listScrollViewer"
                          VerticalScrollBarVisibility="Auto"
                          HorizontalScrollBarVisibility="Disabled"
                          Visibility="Collapsed">
                <StackPanel x:Name="listPanel" Margin="20,0,20,20"/>
            </ScrollViewer>

            <!-- Showcase view: hero left + carousel right -->
            <Grid x:Name="showcasePanel" Visibility="Collapsed">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="300"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <!-- Hero card area (left) -->
                <Border Grid.Column="0" Background="{DynamicResource PanelBrush}"
                        Margin="20,0,10,20" CornerRadius="12">
                    <ContentPresenter x:Name="heroCardHost" Margin="20"/>
                </Border>
                <!-- Carousel (right) -->
                <ScrollViewer Grid.Column="1"
                              HorizontalScrollBarVisibility="Auto"
                              VerticalScrollBarVisibility="Disabled"
                              Margin="10,0,20,20">
                    <StackPanel x:Name="carouselPanel" Orientation="Horizontal"/>
                </ScrollViewer>
            </Grid>

            <!-- Empty state -->
            <StackPanel x:Name="emptyState"
                        VerticalAlignment="Center" HorizontalAlignment="Center"
                        Opacity="0.4" Visibility="Collapsed">
                <TextBlock Text="📚" FontSize="48"
                           HorizontalAlignment="Center"/>
                <TextBlock Text="No games in library" FontSize="14"
                           Foreground="{DynamicResource SubTextBrush}"
                           HorizontalAlignment="Center" Margin="0,12,0,4"/>
                <TextBlock Text="Click ➕ to add your first game" FontSize="11"
                           Foreground="{DynamicResource SubTextBrush}"
                           HorizontalAlignment="Center"/>
            </StackPanel>
        </Grid>
    </Grid>
</UserControl>
```

**File to create:** `Views/Subpages/LibraryPage.xaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TMM
{
    public partial class LibraryPage : UserControl
    {
        private List<LibraryEntry> _allEntries = new();
        private List<LibraryEntry> _filteredEntries = new();
        private string _viewMode = "grid";
        private BackendCore? _core;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires when card body is clicked — shell shows launch/manage modal.</summary>
        public event Action<LibraryEntry>? CardClicked;

        /// <summary>Fires when Play button on card is clicked — direct launch.</summary>
        public event Action<LibraryEntry>? PlayRequested;

        /// <summary>Fires when Manage button on card is clicked — navigate to ModManagerPage.</summary>
        public event Action<LibraryEntry>? ManageRequested;

        /// <summary>Fires when archive state changes. Shell updates AppSettings.ArchivedGameKeys.</summary>
        public event Action<LibraryEntry, bool>? ArchiveToggled;

        /// <summary>Fires when user sets/clears a default game. Shell updates AppSettings.DefaultGameKey.</summary>
        public event Action<LibraryEntry, bool>? DefaultToggled;

        /// <summary>Fires when user reorders cards. Shell updates AppSettings.GameOrder.</summary>
        public event Action<List<string>>? OrderChanged;

        /// <summary>Fires when the ➕ Add Game card is clicked. Shell opens import dialog.</summary>
        public event Action? AddGameRequested;

        // ── Constructor ───────────────────────────────────────────────────────────

        public LibraryPage()
        {
            InitializeComponent();
        }

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>Set the BackendCore reference (needed for art loading).</summary>
        public void Initialize(BackendCore core) => _core = core;

        /// <summary>
        /// Populate or refresh the card display.
        /// Pass all entries including archived ones — this method handles visibility.
        /// </summary>
        public void LoadEntries(IEnumerable<LibraryEntry> entries)
        {
            _allEntries = entries.ToList();
            var visible = _allEntries.Where(e => !e.IsArchived).ToList();
            txtGameCount.Text = $"{visible.Count} game{(visible.Count != 1 ? "s" : "")}";
            ApplyFilter("");
        }

        /// <summary>Called by UnifiedShellWindow when the titlebar search text changes.</summary>
        public void ApplySearchFilter(string query)
        {
            _filteredEntries = string.IsNullOrWhiteSpace(query)
                ? _allEntries.Where(e => !e.IsArchived).ToList()
                : _allEntries.Where(e => !e.IsArchived && (
                    e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Category.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
            RenderCurrentView();
        }

        /// <summary>Switch view mode. Called by UnifiedShellWindow titlebar view-mode buttons.</summary>
        public void SetViewMode(string mode)
        {
            _viewMode = mode;
            RenderCurrentView();
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void ApplyFilter(string query)
        {
            ApplySearchFilter(query);
        }

        private void RenderCurrentView()
        {
            // Hide all panels
            gridScrollViewer.Visibility = Visibility.Collapsed;
            listScrollViewer.Visibility = Visibility.Collapsed;
            showcasePanel.Visibility    = Visibility.Collapsed;
            emptyState.Visibility       = Visibility.Collapsed;

            if (_filteredEntries.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                return;
            }

            switch (_viewMode)
            {
                case "grid":
                case "large":
                    RenderGridView();
                    break;
                case "list":
                    RenderListView();
                    break;
                case "showcase":
                    RenderShowcaseView();
                    break;
                default:
                    RenderGridView();
                    break;
            }
        }

        private void RenderGridView()
        {
            gridScrollViewer.Visibility = Visibility.Visible;
            cardPanel.Children.Clear();

            double scale = _viewMode == "large" ? 1.3 : 1.0;

            foreach (var entry in _filteredEntries)
            {
                var card = CreateCard(entry, scale);
                card.Margin = new Thickness(6);
                cardPanel.Children.Add(card);
            }

            // Add Game placeholder card
            cardPanel.Children.Add(CreateAddGameCard(scale));
        }

        private void RenderListView()
        {
            listScrollViewer.Visibility = Visibility.Visible;
            listPanel.Children.Clear();

            foreach (var entry in _filteredEntries)
            {
                var card = CreateCard(entry, 1.0);
                card.SetDragHandleVisible(true);
                // In list view, constrain width and make cards full-width rows
                card.Width = double.NaN;
                card.Height = 64;
                card.HorizontalAlignment = HorizontalAlignment.Stretch;
                card.Margin = new Thickness(0, 3, 0, 3);
                listPanel.Children.Add(card);
            }

            // Add Game row
            listPanel.Children.Add(CreateAddGameCard(1.0));
        }

        private void RenderShowcaseView()
        {
            showcasePanel.Visibility = Visibility.Visible;
            carouselPanel.Children.Clear();

            if (_filteredEntries.Count == 0) return;

            // Hero = default game, or first entry
            var hero = _filteredEntries.FirstOrDefault(e => e.IsDefault) ?? _filteredEntries[0];
            var heroCard = CreateCard(hero, 1.6);
            heroCard.Width = double.NaN;
            heroCard.Height = double.NaN;
            heroCard.HorizontalAlignment = HorizontalAlignment.Stretch;
            heroCard.VerticalAlignment = VerticalAlignment.Stretch;
            heroCardHost.Content = heroCard;

            // Carousel: remaining games
            foreach (var entry in _filteredEntries.Where(e => e.Key != hero.Key))
            {
                var card = CreateCard(entry, 0.9);
                card.Margin = new Thickness(6);
                carouselPanel.Children.Add(card);
            }
        }

        private GameCard CreateCard(LibraryEntry entry, double scale)
        {
            var card = new GameCard
            {
                Entry = entry,
                Core  = _core,
                Width  = 240 * scale,
                Height = 160 * scale,
            };
            card.CardClicked      += e => CardClicked?.Invoke(e);
            card.PlayRequested    += e => PlayRequested?.Invoke(e);
            card.ManageRequested  += e => ManageRequested?.Invoke(e);
            card.ArchiveToggled   += (e, archived) => ArchiveToggled?.Invoke(e, archived);
            card.DefaultToggled   += (e, isDefault) => DefaultToggled?.Invoke(e, isDefault);
            return card;
        }

        private UIElement CreateAddGameCard(double scale)
        {
            double w = 240 * scale;
            double h = 160 * scale;

            var border = new Border
            {
                Width = w, Height = h,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(6),
            };
            border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

            var content = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var icon = new TextBlock
            {
                Text = "➕", FontSize = 24 * scale,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            var label = new TextBlock
            {
                Text = "Add Game", FontSize = 12 * scale,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
            content.Children.Add(icon);
            content.Children.Add(label);
            border.Child = content;

            border.MouseLeftButtonUp += (_, _) => AddGameRequested?.Invoke();
            return border;
        }
    }
}
```

---

### C3 — Create `Views/Subpages/SettingsPage.xaml` + `.cs`

**Purpose:** Port of SettingsWindow content as a UserControl for embedding inside UnifiedShellWindow.

**File to create:** `Views/Subpages/SettingsPage.xaml`

```xml
<UserControl x:Class="TMM.SettingsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:TMM"
             Background="{DynamicResource BgBrush}">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="28,24,28,24" MaxWidth="540">

            <TextBlock Text="Settings" FontSize="22" FontWeight="Bold"
                       Foreground="{DynamicResource TextBrush}" Margin="0,0,0,20"/>

            <!-- Appearance: 2-Tone Accent Colors -->
            <Border BorderBrush="{DynamicResource SubTextBrush}" BorderThickness="0,1,0,0"
                    Padding="0,12,0,0" Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Appearance" FontSize="13" FontWeight="SemiBold"
                               Foreground="#FF88EE" Margin="0,0,0,8"/>
                    <TextBlock Text="Accent Preset" FontSize="11" Foreground="{DynamicResource SubTextBrush}"
                               Margin="0,0,0,4"/>
                    <ComboBox x:Name="cmbAccentPreset" Height="28" Margin="0,0,0,12"
                              Background="{DynamicResource ControlBgBrush}"
                              Foreground="{DynamicResource TextBrush}"
                              BorderThickness="1" BorderBrush="{DynamicResource SubTextBrush}"
                              Padding="8,4" SelectionChanged="CmbAccentPreset_SelectionChanged"/>
                    <Grid Margin="0,0,0,12">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="*"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0" Margin="0,0,6,0">
                            <TextBlock Text="Primary Color" FontSize="10" Foreground="{DynamicResource SubTextBrush}"
                                       Margin="0,0,0,4"/>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="40"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Border Grid.Column="0" x:Name="primaryColorPreview" Width="40" Height="32"
                                        CornerRadius="4" Background="{DynamicResource AccentBrush}"
                                        BorderThickness="1" BorderBrush="{DynamicResource SubTextBrush}"/>
                                <TextBox Grid.Column="1" x:Name="txtPrimaryColor" Margin="8,0,0,0"
                                         Background="{DynamicResource ControlBgBrush}"
                                         Foreground="{DynamicResource TextBrush}"
                                         BorderThickness="1" BorderBrush="{DynamicResource SubTextBrush}"
                                         Padding="6,4" VerticalContentAlignment="Center"
                                         Text="#0883FF" TextChanged="TxtPrimaryColor_TextChanged"/>
                            </Grid>
                        </StackPanel>
                        <StackPanel Grid.Column="1" Margin="6,0,0,0">
                            <TextBlock Text="Secondary Color" FontSize="10" Foreground="{DynamicResource SubTextBrush}"
                                       Margin="0,0,0,4"/>
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="40"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Border Grid.Column="0" x:Name="secondaryColorPreview" Width="40" Height="32"
                                        CornerRadius="4" Background="{DynamicResource AccentBrush2}"
                                        BorderThickness="1" BorderBrush="{DynamicResource SubTextBrush}"/>
                                <TextBox Grid.Column="1" x:Name="txtSecondaryColor" Margin="8,0,0,0"
                                         Background="{DynamicResource ControlBgBrush}"
                                         Foreground="{DynamicResource TextBrush}"
                                         BorderThickness="1" BorderBrush="{DynamicResource SubTextBrush}"
                                         Padding="6,4" VerticalContentAlignment="Center"
                                         Text="#00D9FF" TextChanged="TxtSecondaryColor_TextChanged"/>
                            </Grid>
                        </StackPanel>
                    </Grid>
                    <Button Content="Apply" Click="BtnApplyAccent_Click"
                            Height="28" Background="{DynamicResource PanelBrush}"
                            Foreground="{DynamicResource TextBrush}" BorderThickness="0" Cursor="Hand"/>
                </StackPanel>
            </Border>

            <!-- Steam Controls -->
            <Border BorderBrush="{DynamicResource SubTextBrush}" BorderThickness="0,1,0,0"
                    Padding="0,12,0,0" Margin="0,0,0,16">
                <StackPanel>
                    <Grid Margin="0,0,0,8">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <TextBlock Text="Steam Controls" FontSize="13" FontWeight="SemiBold"
                                   Foreground="#FF88EE" VerticalAlignment="Center"/>
                        <ComboBox x:Name="cmbSteamGame" Grid.Column="1" Width="150" SelectedIndex="0"
                                  Background="{DynamicResource ControlBgBrush}"
                                  Foreground="{DynamicResource TextBrush}"
                                  BorderThickness="0" Padding="6,3">
                            <ComboBoxItem Content="GTA III"        Tag="III"/>
                            <ComboBoxItem Content="GTA Vice City"  Tag="VC"/>
                            <ComboBoxItem Content="GTA San Andreas" Tag="SA"/>
                            <ComboBoxItem Content="GTA IV"          Tag="IV"/>
                        </ComboBox>
                    </Grid>
                    <UniformGrid Columns="3">
                        <Button Content="[OK] Verify" Click="BtnSteamAction_Click" Tag="validate"
                                Height="26" Margin="0,0,3,0" Background="{DynamicResource PanelBrush}"
                                Foreground="{DynamicResource TextBrush}" BorderThickness="0" Cursor="Hand"
                                ToolTip="Force Steam to verify file integrity"/>
                        <Button Content="Install" Click="BtnSteamAction_Click" Tag="install"
                                Height="26" Margin="3,0,3,0" Background="{DynamicResource PanelBrush}"
                                Foreground="{DynamicResource TextBrush}" BorderThickness="0" Cursor="Hand"/>
                        <Button Content="Uninstall" Click="BtnSteamAction_Click" Tag="uninstall"
                                Height="26" Margin="3,0,0,0" Background="#442222"
                                Foreground="#FF5555" BorderThickness="0" Cursor="Hand"/>
                    </UniformGrid>
                </StackPanel>
            </Border>

            <!-- Diagnostics -->
            <Border BorderBrush="{DynamicResource SubTextBrush}" BorderThickness="0,1,0,0"
                    Padding="0,12,0,0" Margin="0,0,0,16">
                <StackPanel>
                    <TextBlock Text="Diagnostics" FontSize="13" FontWeight="SemiBold"
                               Foreground="#FF88EE" Margin="0,0,0,8"/>
                    <UniformGrid Columns="3">
                        <Button Content="MD5 Check" Click="BtnMd5Check_Click"
                                Height="26" Margin="0,0,3,0" Background="{DynamicResource PanelBrush}"
                                Foreground="{DynamicResource AccentBrush}" BorderThickness="0" Cursor="Hand"
                                ToolTip="Check exe MD5 — detects downgrade status"/>
                        <Button Content="Error Log" Click="BtnOpenLog_Click"
                                Height="26" Margin="3,0,3,0" Background="{DynamicResource PanelBrush}"
                                Foreground="{DynamicResource TextBrush}" BorderThickness="0" Cursor="Hand"/>
                        <Button Content="Wipe Cache" Click="BtnWipeCache_Click"
                                Height="26" Margin="3,0,0,0" Background="{DynamicResource PanelBrush}"
                                Foreground="{DynamicResource TextBrush}" BorderThickness="0" Cursor="Hand"/>
                    </UniformGrid>
                </StackPanel>
            </Border>

            <!-- Danger Zone -->
            <Border BorderBrush="#FF5555" BorderThickness="0,1,0,0" Padding="0,12,0,0">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="Danger Zone" FontSize="12" FontWeight="SemiBold"
                               Foreground="#FF5555" VerticalAlignment="Center"/>
                    <TextBlock x:Name="lblVersion" Grid.Column="1"
                               Foreground="{DynamicResource SubTextBrush}"
                               FontSize="10" VerticalAlignment="Center" Margin="0,0,12,0"
                               Text="v1.2 Alpha"/>
                    <Button Grid.Column="2" Content="Factory Reset" Click="BtnFactoryReset_Click"
                            Height="28" Padding="16,0" Background="#442222" Foreground="#FF5555"
                            BorderThickness="0" Cursor="Hand"/>
                </Grid>
            </Border>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

**File to create:** `Views/Subpages/SettingsPage.xaml.cs`

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TMM
{
    public partial class SettingsPage : UserControl
    {
        private readonly BackendCore _core;
        private bool _isUpdating = false;

        public SettingsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
            InitializeAccentPresets();
        }

        private void InitializeAccentPresets()
        {
            _isUpdating = true;

            // Populate preset combo
            cmbAccentPreset.Items.Add("- Custom -");
            foreach (var preset in AccentPresets.All)
                cmbAccentPreset.Items.Add(preset.Name);

            // Load current colors
            txtPrimaryColor.Text = _core.Settings.AccentColor;
            txtSecondaryColor.Text = _core.Settings.AccentColor2;

            // Select preset if it matches
            int presetIdx = AccentPresets.All.FindIndex(p => p.Name == _core.Settings.ActiveAccentPreset);
            cmbAccentPreset.SelectedIndex = presetIdx >= 0 ? presetIdx + 1 : 0;

            _isUpdating = false;
            UpdateColorPreviews();
        }

        private void CmbAccentPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (cmbAccentPreset.SelectedIndex <= 0) return;

            var preset = AccentPresets.All[cmbAccentPreset.SelectedIndex - 1];
            txtPrimaryColor.Text = preset.PrimaryHex;
            txtSecondaryColor.Text = preset.SecondaryHex;
            _core.Settings.ActiveAccentPreset = preset.Name;
            ApplyAccentColors();
        }

        private void TxtPrimaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
            cmbAccentPreset.SelectedIndex = 0; // Mark as custom
        }

        private void TxtSecondaryColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateColorPreviews();
            cmbAccentPreset.SelectedIndex = 0; // Mark as custom
        }

        private void UpdateColorPreviews()
        {
            try
            {
                var primary = (Color)ColorConverter.ConvertFromString(txtPrimaryColor.Text);
                primaryColorPreview.Background = new SolidColorBrush(primary);
            }
            catch { }

            try
            {
                var secondary = (Color)ColorConverter.ConvertFromString(txtSecondaryColor.Text);
                secondaryColorPreview.Background = new SolidColorBrush(secondary);
            }
            catch { }
        }

        private void BtnApplyAccent_Click(object sender, RoutedEventArgs e)
        {
            ApplyAccentColors();
        }

        private void ApplyAccentColors()
        {
            try
            {
                // Validate hex colors
                var primary = (Color)ColorConverter.ConvertFromString(txtPrimaryColor.Text);
                var secondary = (Color)ColorConverter.ConvertFromString(txtSecondaryColor.Text);

                _core.Settings.AccentColor = txtPrimaryColor.Text;
                _core.Settings.AccentColor2 = txtSecondaryColor.Text;
                _core.SaveSettings();

                // Apply immediately to UI
                ThemeEngine.ApplyTheme(_core.Settings);

                NotificationService.ShowSuccess("Accent colors updated");
            }
            catch (Exception ex)
            {
                NotificationService.ShowWarning($"Invalid color format: {ex.Message}");
            }
        }

        private void BtnSteamAction_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            if (cmbSteamGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;
            SteamLauncher.Invoke(btn.Tag.ToString()!, profile.SteamAppId, _core.Log);
        }

        private async void BtnMd5Check_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSteamGame.SelectedItem is not ComboBoxItem item) return;
            var profile = GameProfile.ByKey(item.Tag.ToString());
            if (profile == null) return;
            string result = await _core.GetMd5DiagnosticsAsync(profile);
            MessageBox.Show(result, $"MD5 Check - {profile.DisplayName}",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnOpenLog_Click(object sender, RoutedEventArgs e)
        {
            string logPath = Path.Combine(_core.AppDataPath, "TMM.log");
            if (File.Exists(logPath))
                Process.Start(new ProcessStartInfo(logPath) { UseShellExecute = true });
            else
                MessageBox.Show("Log file does not exist yet.", "No Logs",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnWipeCache_Click(object sender, RoutedEventArgs e)
        {
            _core.Log("User initiated manual download cache wipe.");
            try
            {
                _core.WipeDownloadCache();
                Directory.CreateDirectory(_core.DownloadCachePath);
                NotificationService.ShowSuccess("Temporary cache wiped successfully");
            }
            catch (Exception ex)
            {
                _core.Log($"Cache wipe failed: {ex.Message}");
                NotificationService.ShowWarning("Cache wipe failed — close any open mod folders and try again");
            }
        }

        private void BtnFactoryReset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "This will wipe all settings and game paths. The app will relaunch.\n\nContinue?",
                "Factory Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            _core.Log("Initiating Factory Reset...");
            try { _core.FactoryReset(); }
            catch (Exception ex) { _core.Log($"FactoryReset error (non-fatal): {ex.Message}"); }

            string? exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                try { Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true }); }
                catch (Exception ex) { _core.Log($"Restart failed: {ex.Message}"); }
            }
            Environment.Exit(0);
        }
    }
}
```

---

### C4 — Create `Views/Subpages/DownloadsPage.xaml` + `.cs`

**Purpose:** Downloads queue page. Initially shows a stub UI with placeholder text — real download queue wiring is a future task. The page must compile and look reasonable.

**File to create:** `Views/Subpages/DownloadsPage.xaml`

```xml
<UserControl x:Class="TMM.DownloadsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource BgBrush}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Downloads" FontSize="22" FontWeight="Bold"
                   Foreground="{DynamicResource TextBrush}" Margin="28,24,28,16"/>

        <StackPanel Grid.Row="1" VerticalAlignment="Center" HorizontalAlignment="Center"
                    Opacity="0.4">
            <TextBlock Text="&#xE896;" FontFamily="Segoe MDL2 Assets" FontSize="48"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="No active downloads" FontSize="14"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center" Margin="0,12,0,4"/>
            <TextBlock Text="One-click essential downloads appear here" FontSize="11"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</UserControl>
```

**File to create:** `Views/Subpages/DownloadsPage.xaml.cs`

```csharp
using System.Windows.Controls;

namespace TMM
{
    public partial class DownloadsPage : UserControl
    {
        public DownloadsPage()
        {
            InitializeComponent();
        }
    }
}
```

---

### C5 — Create `Views/Subpages/BackupsPage.xaml` + `.cs`

**Purpose:** Backups overview stub page.

**File to create:** `Views/Subpages/BackupsPage.xaml`

```xml
<UserControl x:Class="TMM.BackupsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource BgBrush}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" Text="Backups" FontSize="22" FontWeight="Bold"
                   Foreground="{DynamicResource TextBrush}" Margin="28,24,28,16"/>

        <StackPanel Grid.Row="1" VerticalAlignment="Center" HorizontalAlignment="Center"
                    Opacity="0.4">
            <TextBlock Text="&#xE777;" FontFamily="Segoe MDL2 Assets" FontSize="48"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center"/>
            <TextBlock Text="No backups yet" FontSize="14"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center" Margin="0,12,0,4"/>
            <TextBlock Text="Backups are created automatically when you deploy mods" FontSize="11"
                       Foreground="{DynamicResource SubTextBrush}"
                       HorizontalAlignment="Center"/>
        </StackPanel>
    </Grid>
</UserControl>
```

**File to create:** `Views/Subpages/BackupsPage.xaml.cs`

```csharp
using System.Windows.Controls;

namespace TMM
{
    public partial class BackupsPage : UserControl
    {
        public BackupsPage()
        {
            InitializeComponent();
        }
    }
}
```

---

### C6 — Create `Views/Subpages/PathsPage.xaml` + `.cs`

**Purpose:** File locations manager page. Shows 5 configurable folder paths. Each has an Open button (opens Explorer) and a Change button (folder picker). Added from Claude Design prototype.

**Paths managed:**
1. **Library art** — `%APPDATA%\TMM\LibraryArt\` — custom card artwork
2. **Tmmpack archive** — `%APPDATA%\TMM\Packs\` — exported .tmmpack files
3. **Backups** — `%APPDATA%\TMM\Backups\` — mod backups
4. **Downloads cache** — `%APPDATA%\TMM\Downloads\` — temporary download cache
5. **Log files** — `%APPDATA%\TMM\` — TMM.log location

**File to create:** `Views/Subpages/PathsPage.xaml`

```xml
<UserControl x:Class="TMM.PathsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource BgBrush}">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="28,24,28,24" MaxWidth="600">

            <TextBlock Text="File Locations" FontSize="22" FontWeight="Bold"
                       Foreground="{DynamicResource TextBrush}" Margin="0,0,0,6"/>
            <TextBlock Text="Configure where TMM stores its data. Changes take effect immediately."
                       FontSize="12" Foreground="{DynamicResource SubTextBrush}" Margin="0,0,0,24"/>

            <!-- Path rows defined in code-behind via PathRows list -->
            <StackPanel x:Name="pathRowsPanel"/>

        </StackPanel>
    </ScrollViewer>
</UserControl>
```

**File to create:** `Views/Subpages/PathsPage.xaml.cs`

```csharp
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;   // FolderBrowserDialog — add reference to System.Windows.Forms

namespace TMM
{
    public partial class PathsPage : UserControl
    {
        private readonly BackendCore _core;

        public PathsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
            BuildPathRows();
        }

        private record PathRowDef(
            string Label,
            string Description,
            Func<string> GetPath,
            Action<string>? SetPath = null   // null = read-only (log dir)
        );

        private void BuildPathRows()
        {
            var rows = new[]
            {
                new PathRowDef(
                    "Library Art",
                    "Custom artwork for game cards (PNG files)",
                    () => _core.LibraryArtPath,
                    null   // Always under AppData — not user-configurable in v1
                ),
                new PathRowDef(
                    "Tmmpack Archive",
                    "Exported .tmmpack bundles",
                    () => Path.Combine(_core.AppDataPath, "Packs"),
                    null
                ),
                new PathRowDef(
                    "Backups",
                    "Automatic mod backups before deploy",
                    () => _core.BackupPath,
                    null
                ),
                new PathRowDef(
                    "Downloads Cache",
                    "Temporary files during downloads",
                    () => _core.DownloadCachePath,
                    null
                ),
                new PathRowDef(
                    "Log Files",
                    "TMM.log and diagnostic output",
                    () => _core.AppDataPath,
                    null
                ),
            };

            foreach (var row in rows)
                pathRowsPanel.Children.Add(BuildRow(row));
        }

        private UIElement BuildRow(PathRowDef def)
        {
            var container = new Border
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16),
                CornerRadius = new System.Windows.CornerRadius(8),
            };
            container.SetResourceReference(Border.BackgroundProperty, "PanelBrush");

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Label
            var label = new TextBlock
            {
                Text = def.Label, FontSize = 13, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetRow(label, 0); Grid.SetColumn(label, 0); Grid.SetColumnSpan(label, 3);

            // Description
            var desc = new TextBlock
            {
                Text = def.Description, FontSize = 11,
                Margin = new Thickness(0, 0, 0, 8)
            };
            desc.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
            Grid.SetRow(desc, 1); Grid.SetColumn(desc, 0); Grid.SetColumnSpan(desc, 3);

            // Path text
            var pathText = new TextBlock
            {
                Text = def.GetPath(), FontSize = 11,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            pathText.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");
            Grid.SetRow(pathText, 2); Grid.SetColumn(pathText, 0);

            // Open button
            var btnOpen = new System.Windows.Controls.Button
            {
                Content = "Open", Height = 28, Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(8, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand,
            };
            btnOpen.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "ControlBgBrush");
            btnOpen.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "TextBrush");
            btnOpen.BorderThickness = new Thickness(0);
            btnOpen.Click += (_, _) =>
            {
                var path = def.GetPath();
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
            };
            Grid.SetRow(btnOpen, 2); Grid.SetColumn(btnOpen, 1);

            grid.Children.Add(label);
            grid.Children.Add(desc);
            grid.Children.Add(pathText);
            grid.Children.Add(btnOpen);

            container.Child = grid;
            return container;
        }
    }
}
```

> **Note to implementer:** `BackendCore` properties used: `LibraryArtPath`, `AppDataPath`, `BackupPath`, `DownloadCachePath`. Grep these to confirm they exist before implementing. If `BackupPath` doesn't exist, use `Path.Combine(_core.AppDataPath, "Backups")` as a fallback. Add `System.Windows.Forms` reference to csproj if not already present (needed for `FolderBrowserDialog` — or use `Microsoft.Win32` folder picking alternative).

---

## Phase D — Architecture [SONNET ONLY]

> The D-phase tasks require deep understanding of the existing codebase. Do NOT delegate to Haiku.
> Read all referenced existing files before writing. Build after each step.

---

### D1 — Create `Views/Subpages/ModManagerPage.xaml` + `.cs` [SONNET ONLY]

**Purpose:** A unified mod manager page that replaces MainDashboardWindow, Gta4DashboardWindow, and CustomGameDashboardWindow. Receives a `LibraryEntry` and renders the appropriate dashboard layout inside the unified shell.

**Implementation notes:**
- Accept a `LibraryEntry` via `public void LoadEntry(LibraryEntry entry, BackendCore core)`
- For `GameKeys = ["III","VC","SA"]`: render a 3-column layout (one column per game) replicating MainDashboardWindow content without the outer chrome
- For `GameKeys = ["IV","TLaD","TBoGT"]`: render 3-column layout replicating Gta4DashboardWindow
- For custom game entries: single-column layout replicating CustomGameDashboardWindow
- For placeholder entries: show a "coming soon" overlay
- The toolbar (Deploy, Rollback, Play) moves into the top of ModManagerPage (not in the shell nav strip)
- Sidebar (game paths, essentials) remains inline as it is in the current dashboards
- Fire a `BackRequested` event when back arrow is clicked (so shell can return to library)
- Keep all existing event handlers; only eliminate the Window chrome / `TmmWindow` base

**Files to read first:**
- `Views/MainDashboardWindow.xaml` and `.cs`
- `Views/Gta4DashboardWindow.xaml` and `.cs`
- `Views/CustomGameDashboardWindow.xaml` and `.cs`

---

### D2 — Create `Views/UnifiedShellWindow.xaml` + `.cs` [SONNET ONLY]

**Purpose:** The new single application window that contains everything.

**Layout:**
```
┌────────────────────────────────────────────────────────────────────────┐
│  TitleBar: [drag area][Title][search box*][archive chip][min/max/close]│
│             * search only visible on LibraryPage                       │
├────┬───────────────────────────────────────────────────────────────────┤
│    │                                                                   │
│ N  │  Content area (LibraryPage / ModManagerPage /                     │
│ a  │   SettingsPage / DownloadsPage / BackupsPage / PathsPage)        │
│ v  │                                                                   │
│    │                                                                   │
└────┴───────────────────────────────────────────────────────────────────┘
  ~50px
```

**Titlebar elements (left → right):**
1. Drag area (app title text "TMM")
2. **Search box** — `ControlBgBrush` background, 🔍 icon, text input. Visible ONLY when LibraryPage is active. Passes text changes to `libraryPage.ApplySearchFilter(text)`.
3. **View-mode switcher** — 4 small icon buttons (⊞ grid, ⊟ large, ☰ list, ✦ showcase). Visible ONLY when LibraryPage is active. Active mode button highlighted with AccentBrush. Calls `libraryPage.SetViewMode(mode)`.
4. **Archive chip** — compact eye-off icon (👁‍🗨 or use Unicode ⊘) that shows archived game count on hover/expansion. Positioned just left of the window controls (min/max/close). Behavior:
   - **Default (collapsed):** shows only the eye-off icon (~24×24px), muted color
   - **Hover:** expands to show "N archived" text with a subtle slide animation
   - **If ArchivedGameKeys.Count == 0:** chip is hidden entirely
   - **Click:** opens a dropdown/flyout panel listing archived games with unarchive buttons
   - User's explicit spec: "crossed out eye icon that expands by the minimize button"
5. Min / Max / Close buttons (standard, inherited from TmmWindow)

**Nav strip icons (top to bottom, using Segoe MDL2 Assets glyphs):**
| Glyph hex   | Label         | Page/Action        |
|-------------|---------------|--------------------|
| `&#xE80F;`  | Library       | LibraryPage        |
| `&#xE896;`  | Downloads     | DownloadsPage      |
| `&#xE7FC;`  | Mod Manager   | ModManagerPage (opens last/default game directly) |
| `&#xE777;`  | Backups       | BackupsPage        |
| `&#xE713;`  | Settings      | SettingsPage       |
| `&#xE9CE;`  | Paths         | PathsPage (NEW)    |
| *(spacer)*  |               |                    |
| `&#xE7E8;`  | Quit          | (closes app)       |

> Note: Quit and Paths at bottom of nav strip separated by spacer. Library, Downloads, Mod Manager, Backups, Settings at top.
> The "Mod Manager" nav item directly opens the default game's ModManagerPage (from `AppSettings.DefaultGameKey`). If no default is set, clicking it navigates to LibraryPage with a toast "Set a default game first".

**Navigation rules:**
- Clicking Library → show LibraryPage; show titlebar search + view switcher
- **Clicking a GameCard body** → show a **modal choice dialog** (in-window overlay, not a new Window):
  - Two large buttons: "▶ Launch Game" and "☰ Manage Mods"
  - Launch → calls game launcher logic directly, dismisses modal
  - Manage Mods → navigates to ModManagerPage
  - Modal dismisses on click-outside or Escape
- **Clicking Play button on card** → skip modal, launch directly
- **Clicking Manage button on card** → skip modal, navigate to ModManagerPage directly
- ModManagerPage has back arrow top-left → returns to LibraryPage
- Leaving LibraryPage → hide titlebar search + view switcher
- Active nav icon highlighted with AccentBrush

**LibraryEntry construction (inside UnifiedShellWindow):**

Build these entries on load:

```csharp
// Helper: check archive + default state from settings
bool IsArchived(string key) => core.Settings.ArchivedGameKeys.Contains(key);
bool IsDefault(string key)  => core.Settings.DefaultGameKey == key;

// Apply GameOrder sort: entries in GameOrder come first in that order,
// remaining entries appended in their natural order.
// (Do this after building all entries, before passing to LibraryPage.LoadEntries)

// GTA III Series — groups III, VC, SA into one card
var gtaIIISeries = new LibraryEntry(
    Key: "GTA_III_SERIES",
    DisplayName: "GTA III Series",
    Subtitle: "III · Vice City · San Andreas",
    GradientStartHex: "#1B3A1B",
    GradientEndHex: "#0C1E0C",
    Status: ReleaseStatus.Beta,
    ModCount: CountMods(core, "III", "VC", "SA"),
    IsReady: AnyPathSet(core, "III", "VC", "SA"),
    Category: "GTA Series",
    GameKeys: new[] { "III", "VC", "SA" },
    IsArchived: IsArchived("GTA_III_SERIES"),
    IsDefault:  IsDefault("GTA_III_SERIES")
);

// GTA IV Series — groups IV, TLaD, TBoGT into one card
var gtaIVSeries = new LibraryEntry(
    Key: "GTA_IV_SERIES",
    DisplayName: "GTA IV Series",
    Subtitle: "IV · TLaD · TBoGT",
    GradientStartHex: "#0C1A2E",
    GradientEndHex: "#060F1C",
    Status: ReleaseStatus.Alpha,
    ModCount: CountMods(core, "IV", "TLaD", "TBoGT"),
    IsReady: AnyPathSet(core, "IV", "TLaD", "TBoGT"),
    Category: "GTA Series",
    GameKeys: new[] { "IV", "TLaD", "TBoGT" },
    IsArchived: IsArchived("GTA_IV_SERIES"),
    IsDefault:  IsDefault("GTA_IV_SERIES")
);

// Built-in custom games from GameRegistry (e.g. Skyrim AE, Fallout NV, etc.)
// Use config.GradientStartHex/GradientEndHex, config.LibraryStatus, config.LauncherCard
// IsPlaceholder = config.IsBuiltIn && string.IsNullOrEmpty(config.GameDirectory) — i.e. no path set AND is a bundled placeholder
// IsArchived = IsArchived(config.GameName) (use GameName as key for custom games)
// IsDefault  = IsDefault(config.GameName)

// User custom games from GameRegistry.GetCustomGames()
// IsPlaceholder = false always for user-added games
// IsArchived and IsDefault: same pattern as above

// After building all entries, sort by GameOrder:
// var ordered = entries.OrderBy(e =>
// {
//     var idx = core.Settings.GameOrder.IndexOf(e.Key);
//     return idx < 0 ? int.MaxValue : idx;
// }).ToList();
```

**Helper methods to implement:**
```csharp
private static int CountMods(BackendCore core, params string[] keys)
{
    int total = 0;
    foreach (var key in keys)
    {
        var profile = GameProfile.ByKey(key);
        if (profile != null) total += core.GetMods(profile).Count(m => m.IsEnabled);
    }
    return total;
}

private static bool AnyPathSet(BackendCore core, params string[] keys)
{
    foreach (var key in keys)
    {
        var profile = GameProfile.ByKey(key);
        if (profile != null)
        {
            var path = core.GetVanillaPath(profile);
            if (!string.IsNullOrEmpty(path) && System.IO.Directory.Exists(path))
                return true;
        }
    }
    return false;
}
```

**Window size:** `Width="1100" Height="720" MinWidth="900" MinHeight="600"`

**Files to read first:**
- `Views/GameLauncherWindow.xaml` and `.cs` (for pattern reference)
- `App.xaml.cs`
- `Services/BackendCore.cs` (to find `GetMods()` and `GetVanillaPath()` signatures)

---

### D3 — Update `App.xaml.cs` [SONNET ONLY]

**Change entry point from `GameLauncherWindow` to `UnifiedShellWindow`.**

In `App.xaml.cs`, find the line:
```csharp
new GameLauncherWindow(Core).Show();
```
Replace with:
```csharp
new UnifiedShellWindow(Core).Show();
```

Read `App.xaml.cs` before editing to ensure you get the exact context.

---

### D4 — Delete Old Windows [SONNET ONLY]

After D3 builds cleanly, delete these files:
- `Views/GameLauncherWindow.xaml`
- `Views/GameLauncherWindow.xaml.cs`
- `Views/MainDashboardWindow.xaml`
- `Views/MainDashboardWindow.xaml.cs`
- `Views/Gta4DashboardWindow.xaml`
- `Views/Gta4DashboardWindow.xaml.cs`
- `Views/CustomGameDashboardWindow.xaml`
- `Views/CustomGameDashboardWindow.xaml.cs`
- `Views/SettingsWindow.xaml`
- `Views/SettingsWindow.xaml.cs`

> **Also remove** the `GameSetupRow` UserControl if it was only used by SettingsWindow/MainDashboardWindow.
> Check `Views/Controls/GameSetupRow.xaml` — if no references remain, delete it too.

---

### D5 — Build and Fix [SONNET ONLY]

Run `dotnet build` and fix all compilation errors. Common expected issues:
- Missing using directives in new files
- References to deleted window types (scan for `GameLauncherWindow`, `MainDashboardWindow`, etc.)
- Missing `BackendCore` method signatures (e.g. `GetMods` — look up actual signature in `Services/BackendCore.cs`)
- `NotificationService` may need an `Owner` window reference; pass `UnifiedShellWindow` instance
- Theme picker (`ThemeManagerWindow`) may open via button — ensure it still receives a valid `Window` owner

---

## Quick Reference — Status Chip Colors

```csharp
ReleaseStatus.Beta     → Color.FromRgb(180, 140,  20)  // yellow
ReleaseStatus.Alpha    → Color.FromRgb(200, 100,  20)  // orange
ReleaseStatus.PreAlpha → Color.FromRgb(200,  55,  30)  // red-orange
ReleaseStatus.Testing  → Color.FromRgb( 80,  80, 180)  // blue
ReleaseStatus.Release  → Chip hidden (Visibility.Collapsed)
```

## Quick Reference — Gradient Pairs

| Entry            | Start      | End        |
|------------------|------------|------------|
| GTA III Series   | `#1B3A1B`  | `#0C1E0C`  |
| GTA IV Series    | `#0C1A2E`  | `#060F1C`  |
| Skyrim AE        | `#1E0A3C`  | `#10051E`  |
| Fallout NV       | `#3A2008`  | `#1E1004`  |
| Cyberpunk 2077   | `#0A1A2E`  | `#050D1A`  |
| RDR2             | `#2E0A0A`  | `#1A0505`  |
| Witcher 3        | `#0A2E14`  | `#051A0A`  |

## Quick Reference — New Files Summary

```
Models/ReleaseStatus.cs                       (A1 — new)          enum: Release/Beta/Alpha/PreAlpha/Testing
Models/LibraryEntry.cs                        (A5 — new)          record: +IsArchived, +IsDefault
Models/AppSettings.cs                         (A7 — edit)         +ArchivedGameKeys/DefaultGameKey/LibraryViewMode/GameOrder
Models/CustomGameProfile.cs                   (A2+A6 — edit)      +GradientStartHex/GradientEndHex/LibraryStatus/CustomArtFileName
Models/TmmGameConfig.cs                       (A3 — edit)         +gradient/status fields in LauncherCardConfig+TmmGameExport
Models/GameProfile.cs                         (A4 — edit)         +GradientStartHex/GradientEndHex/LibraryStatus init props
Services/BackendCore.cs                       (A6 — edit)         +LibraryArtPath/GetLibraryArtPath/SaveLibraryArt/DeleteLibraryArt
Assets/GameProfiles/skyrim_ae.tmmgame         (B1 — edit)         add "libraryStatus": "PreAlpha"
Assets/GameProfiles/fallout_nv.tmmgame        (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/cyberpunk_2077.tmmgame    (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/red_dead_2.tmmgame        (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/witcher_3.tmmgame         (B1 — new)          PreAlpha placeholder
TMM.csproj                                 (B2 — edit)         4 new EmbeddedResource entries
Views/Controls/GameCard.xaml + .cs            (C1 — new)          240×160 card, action buttons, default checkbox, archived state
Views/Subpages/LibraryPage.xaml + .cs         (C2 — new)          4 view modes, drag reorder, Add Game card (NO search in header)
Views/Subpages/SettingsPage.xaml + .cs        (C3 — new)          SettingsWindow ported to UserControl + accent color picker
Views/Subpages/DownloadsPage.xaml + .cs       (C4 — new)          downloads stub
Views/Subpages/BackupsPage.xaml + .cs         (C5 — new)          backups stub
Views/Subpages/PathsPage.xaml + .cs           (C6 — new)          file locations manager (NEW from Design)
Views/Subpages/ModManagerPage.xaml + .cs      (D1 — SONNET ONLY)  unified dashboard replacing 3 old windows
Views/UnifiedShellWindow.xaml + .cs           (D2 — SONNET ONLY)  nav strip + titlebar search + archive chip + click modal
App.xaml.cs                                   (D3 — SONNET ONLY)  1-line entry point change
[DELETE old window files]                     (D4 — SONNET ONLY)
```

---

## Design Notes (from Claude Design Prototype)

> These are implementation guidance notes derived from the Claude Design HTML prototype review.

### Card Click Behavior
- **Card body click** → opens an **in-window modal overlay** (not a new Window) with two choices:
  - "▶ Launch Game" — calls game launcher, dismisses modal
  - "☰ Manage Mods" — navigates to ModManagerPage
- **Card Play button** → launches directly (skip modal)
- **Card Manage button** → navigates directly (skip modal)

### Archive Chip (Titlebar)
- Located in titlebar, just left of window control buttons (min/max/close)
- **Collapsed state:** small eye-off icon only (~24px), subdued color (`SubTextBrush`)
- **Hover/expanded state:** slides out to show "N archived" label
- **Hidden entirely** when `AppSettings.ArchivedGameKeys.Count == 0`
- **Click:** opens a dropdown or flyout listing archived games; each has an "Unarchive" button
- User's exact words: "crossed out eye icon that expands by the minimize button"

### View Mode Switcher (Titlebar)
- 4 compact icon buttons visible in titlebar when LibraryPage is active:
  - ⊞ Grid (240×160 cards, WrapPanel)
  - ⊟ Large (240×160 × 1.3 scale, WrapPanel)
  - ☰ List (full-width rows, StackPanel, drag handle visible)
  - ✦ Showcase (hero left, carousel right)
- Active mode button uses `AccentBrush` foreground
- State persisted to `AppSettings.LibraryViewMode`

### Archived Card Visual State
- `Opacity = 0.55` on the whole card
- `archivedOverlay` Border (`#80000000`) darkens the gradient further
- Archived cards still appear in the list (shown at reduced opacity), so users can see and unarchive them from the main grid OR from the archive chip flyout
- *(Alternatively: hide archived cards from main grid entirely, only accessible via chip flyout — Sonnet to decide during D2 implementation based on UX preference)*

### Drag-to-Reorder
- Supported in grid, large, and list views (not showcase — hero is fixed)
- Drag handle `⠿` shown on left side of card in list view; invisible but functional in grid/large view
- On drop, fire `LibraryPage.OrderChanged(List<string> newKeyOrder)` → shell saves to `AppSettings.GameOrder`
- WPF drag/drop implementation: use `AllowDrop=True` + `DragOver` / `Drop` events on the container panel

### Design Color Palette Note
The Claude Design prototype used slightly darker base colors than Haiku implemented:
- Design `--bg`: `#1a1a1d` | Haiku `BgBrush`: `#202020`
- Design `--panel`: `#26262b` | Haiku `PanelBrush`: `#2D2D30`
- Design `--accent`: `#4cc2ff` (single tone) vs our 2-tone system

**Decision:** Keep Haiku's WinUI-standard colors (#202020 etc.) — they are correct for Win11 consistency. The Design's darker colors were aesthetic preference only; WinUI standard is the right call for a Windows app.
