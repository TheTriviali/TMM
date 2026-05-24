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
D1 [SONNET] → depends on A1–A6, C1–C5 all done
D2 [SONNET] → depends on D1
D3 [SONNET] → depends on D2
D4 [SONNET] → depends on D3
D5 [SONNET] → build & fix
```

Run A1–A6 in parallel. Run C1–C5 in parallel (after A-phase done). D-phases are sequential.

---

## Namespace & Conventions

- All new files use `namespace TMM`
- All new XAML code-behind classes inherit from `UserControl` (not `TmmWindow`)
- `UnifiedShellWindow` inherits from `TmmWindow`
- Dynamic brushes: `{DynamicResource BgBrush}`, `{DynamicResource AccentBrush}`, `{DynamicResource TextBrush}`, `{DynamicResource SubTextBrush}`, `{DynamicResource PanelBrush}`, `{DynamicResource ControlBgBrush}`
- Static helper: `UiColors.ReadyGreen` (Color), `UiColors.NotReadyRed` (Color) — in `Helpers/Helpers.cs`
- All new files go under `C:\Users\noahd\source\repos\tgtamm\TGTAMM\`

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
        bool IsPlaceholder = false
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

#### A6-part3: Update `Views/Controls/GameCard.xaml.cs` (C1) to use artwork

In `ApplyEntry()`, before the gradient is applied, check for a custom art PNG:

```csharp
// Custom artwork overrides gradient
string? artPath = null;
// BackendCore reference must be passed in; see note below
if (_core != null)
    artPath = _core.GetLibraryArtPath(entry.Key);

if (artPath != null)
{
    try
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.UriSource = new Uri(artPath);
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.EndInit();
        gradientBg.Background = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
        // Hide noise overlay when using photo
        // (the noise Border has x:Name="noiseOverlay" — add that name in C1 XAML)
    }
    catch { /* fall through to gradient */ }
}
```

> **Note:** `GameCard` needs a `BackendCore? _core` field. Expose it as a property:
> `public BackendCore? Core { get; set; }` — set by LibraryPage when creating each card.

#### A6-part4: Wire right-click context menu in `Views/Controls/GameCard.xaml`

Add a ContextMenu to the root Border in GameCard.xaml:

```xml
<Border.ContextMenu>
    <ContextMenu>
        <MenuItem Header="Set Custom Artwork..." Click="MenuSetArt_Click"/>
        <MenuItem x:Name="menuRemoveArt" Header="Remove Custom Artwork" Click="MenuRemoveArt_Click"/>
    </ContextMenu>
</Border.ContextMenu>
```

In `GameCard.xaml.cs` add:
```csharp
private void MenuSetArt_Click(object sender, RoutedEventArgs e)
{
    if (Entry == null || _core == null) return;
    var dlg = new Microsoft.Win32.OpenFileDialog
    {
        Title = $"Select artwork for {Entry.DisplayName}",
        Filter = "PNG Image (*.png)|*.png",
        Multiselect = false
    };
    if (dlg.ShowDialog() != true) return;
    try
    {
        _core.SaveLibraryArt(Entry.Key, dlg.FileName);
        // Re-apply to refresh the card
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
    if (Entry == null || _core == null) return;
    _core.DeleteLibraryArt(Entry.Key);
    ApplyEntry(Entry);
    NotificationService.ShowSuccess("Artwork removed — using gradient");
}
```

**Ideal artwork spec (display in tooltip/dialog):**
- Format: PNG only
- Ideal resolution: **460 × 215 px** (2.14:1 ratio — matches Steam landscape card standard)
- Max file size: 2 MB
- Min resolution: 200 × 100 px

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

### B2 — Update `TGTAMM.csproj`

**File:** `TGTAMM.csproj`

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

**Purpose:** Reusable UserControl for a single game library card. Shows gradient art banner with game title, status chip in corner, and a bottom info strip with subtitle + mod count.

**File to create:** `Views/Controls/GameCard.xaml`

```xml
<UserControl x:Class="TMM.GameCard"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:TMM"
             Width="200" Height="130"
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
            <Border CornerRadius="10" Opacity="0.04"
                    Background="{DynamicResource TextBrush}"/>

            <!-- Large art title text -->
            <TextBlock x:Name="txtArtTitle"
                       Text="GAME TITLE"
                       Foreground="#CCFFFFFF"
                       FontSize="22" FontWeight="Black"
                       VerticalAlignment="Center" HorizontalAlignment="Left"
                       Margin="14,0,14,24"
                       TextWrapping="Wrap"
                       LineHeight="26"/>

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
                    Background="#AA000000" Padding="10,6">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="Auto"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock x:Name="txtSubtitle" Grid.Column="0"
                               Foreground="#CCFFFFFF" FontSize="10"
                               TextTrimming="CharacterEllipsis"
                               VerticalAlignment="Center"/>
                    <TextBlock x:Name="txtModCount" Grid.Column="1"
                               Foreground="#88FFFFFF" FontSize="9"
                               VerticalAlignment="Center" Margin="8,0,0,0"/>
                </Grid>
            </Border>

            <!-- Ready indicator dot (bottom-left) -->
            <Ellipse x:Name="readyDot" Width="7" Height="7"
                     VerticalAlignment="Bottom" HorizontalAlignment="Left"
                     Margin="8,0,0,8"/>

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

        // ── Events ─────────────────────────────────────────────────────────────────

        public event Action<LibraryEntry>? CardClicked;

        // ── Constructor ───────────────────────────────────────────────────────────

        public GameCard()
        {
            InitializeComponent();
            MouseLeftButtonUp += (_, _) =>
            {
                if (Entry != null) CardClicked?.Invoke(Entry);
            };
            MouseEnter += (_, _) => AnimateHover(true);
            MouseLeave += (_, _) => AnimateHover(false);
        }

        // ── Data binding ──────────────────────────────────────────────────────────

        private static void OnEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GameCard card && e.NewValue is LibraryEntry entry)
                card.ApplyEntry(entry);
        }

        private void ApplyEntry(LibraryEntry entry)
        {
            txtArtTitle.Text  = entry.DisplayName.ToUpperInvariant();
            txtSubtitle.Text  = entry.Subtitle;
            txtModCount.Text  = entry.ModCount > 0 ? $"{entry.ModCount} mods" : "";

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
            Opacity = entry.IsPlaceholder ? 0.72 : 1.0;
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
                ReleaseStatus.Beta    => (Color.FromRgb(180, 140, 20),  "BETA"),
                ReleaseStatus.Alpha   => (Color.FromRgb(200, 100, 20),  "ALPHA"),
                ReleaseStatus.Testing => (Color.FromRgb(80,  80,  180), "TESTING"),
                _                     => (Colors.Gray, status.ToString().ToUpperInvariant()),
            };

            statusChip.Background = new SolidColorBrush(chipColor) { Opacity = 0.85 };
            txtStatus.Text = label;
        }

        private void AnimateHover(bool entering)
        {
            var anim = new DoubleAnimation(entering ? 0.12 : 0, TimeSpan.FromMilliseconds(120));
            hoverOverlay.Background = new SolidColorBrush(Colors.White);
            hoverOverlay.BeginAnimation(OpacityProperty, anim);

            var scaleAnim = new DoubleAnimation(entering ? 1.03 : 1.0, TimeSpan.FromMilliseconds(120));
            if (RenderTransform is not ScaleTransform)
            {
                RenderTransformOrigin = new Point(0.5, 0.5);
                RenderTransform = new ScaleTransform(1, 1);
            }
            ((ScaleTransform)RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            ((ScaleTransform)RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

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
    }
}
```

---

### C2 — Create `Views/Subpages/LibraryPage.xaml` + `.cs`

**Purpose:** The home library screen — search bar + WrapPanel of GameCards. Fires `GameSelected` event when a card is clicked. Populated by passing a list of `LibraryEntry` items.

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

        <!-- Header bar: title + search -->
        <Grid Grid.Row="0" Margin="24,20,24,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>

            <StackPanel Grid.Column="0" VerticalAlignment="Center">
                <TextBlock Text="My Library" FontSize="22" FontWeight="Bold"
                           Foreground="{DynamicResource TextBrush}"/>
                <TextBlock x:Name="txtGameCount" FontSize="11"
                           Foreground="{DynamicResource SubTextBrush}" Margin="0,2,0,0"/>
            </StackPanel>

            <!-- Search box -->
            <Border Grid.Column="1" CornerRadius="6" Background="{DynamicResource ControlBgBrush}"
                    Padding="8,5" MinWidth="200">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="Auto"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="&#xE721;" FontFamily="Segoe MDL2 Assets" FontSize="12"
                               Foreground="{DynamicResource SubTextBrush}"
                               VerticalAlignment="Center" Margin="0,0,6,0"/>
                    <TextBox x:Name="txtSearch" Grid.Column="1"
                             Background="Transparent" BorderThickness="0"
                             Foreground="{DynamicResource TextBrush}" CaretBrush="{DynamicResource AccentBrush}"
                             FontSize="12" VerticalContentAlignment="Center"
                             TextChanged="TxtSearch_TextChanged"
                             Text="">
                        <TextBox.Style>
                            <Style TargetType="TextBox">
                                <Style.Resources>
                                    <VisualBrush x:Key="HintBrush" AlignmentX="Left" AlignmentY="Center" Stretch="None">
                                        <VisualBrush.Visual>
                                            <TextBlock Text="Search games..." Foreground="#66FFFFFF" FontSize="12"/>
                                        </VisualBrush.Visual>
                                    </VisualBrush>
                                </Style.Resources>
                                <Style.Triggers>
                                    <Trigger Property="Text" Value="">
                                        <Setter Property="Background" Value="{StaticResource HintBrush}"/>
                                    </Trigger>
                                    <Trigger Property="IsKeyboardFocused" Value="True">
                                        <Setter Property="Background" Value="Transparent"/>
                                    </Trigger>
                                </Style.Triggers>
                            </Style>
                        </TextBox.Style>
                    </TextBox>
                </Grid>
            </Border>
        </Grid>

        <!-- Card grid -->
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"
                      HorizontalScrollBarVisibility="Disabled">
            <WrapPanel x:Name="cardPanel" Margin="20,0,20,20"
                       Orientation="Horizontal"/>
        </ScrollViewer>
    </Grid>
</UserControl>
```

**File to create:** `Views/Subpages/LibraryPage.xaml.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace TMM
{
    public partial class LibraryPage : UserControl
    {
        private List<LibraryEntry> _allEntries = new();

        /// <summary>Fired when the user clicks a game card.</summary>
        public event Action<LibraryEntry>? GameSelected;

        public LibraryPage()
        {
            InitializeComponent();
        }

        /// <summary>Populate (or refresh) the card grid. Call this from UnifiedShellWindow.</summary>
        public void LoadEntries(IEnumerable<LibraryEntry> entries)
        {
            _allEntries = entries.ToList();
            txtGameCount.Text = $"{_allEntries.Count} game{(_allEntries.Count != 1 ? "s" : "")}";
            ApplyFilter(txtSearch.Text);
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(txtSearch.Text);
        }

        private void ApplyFilter(string query)
        {
            cardPanel.Children.Clear();
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allEntries
                : _allEntries.Where(e =>
                    e.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    e.Category.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var entry in filtered)
            {
                var card = new GameCard
                {
                    Entry = entry,
                    Margin = new System.Windows.Thickness(6)
                };
                card.CardClicked += e => GameSelected?.Invoke(e);
                cardPanel.Children.Add(card);
            }
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

namespace TMM
{
    public partial class SettingsPage : UserControl
    {
        private readonly BackendCore _core;

        public SettingsPage(BackendCore core)
        {
            _core = core;
            InitializeComponent();
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
┌─────────────────────────────────────────────────────────┐
│  TitleBar (drag, close, min, max)                       │
├────┬────────────────────────────────────────────────────┤
│    │                                                    │
│ N  │  Content area (LibraryPage / ModManagerPage /      │
│ a  │   SettingsPage / DownloadsPage / BackupsPage)      │
│ v  │                                                    │
│    │                                                    │
└────┴────────────────────────────────────────────────────┘
  ~50px
```

**Nav strip icons (top to bottom, using Segoe MDL2 Assets glyphs):**
| Glyph hex | Label      | Page         |
|-----------|------------|--------------|
| `&#xE80F;` | Home       | LibraryPage  |
| `&#xE896;` | Downloads  | DownloadsPage |
| `&#xE777;` | Backups    | BackupsPage  |
| `&#xE713;` | Settings   | SettingsPage |
| `&#xE7E8;` | Quit       | (closes app) |

> Note: Quit button at bottom of nav strip, other icons at top.

**Navigation rules:**
- Clicking Home → show LibraryPage
- Clicking a GameCard in LibraryPage → show ModManagerPage (slide in from right, back arrow in top-left of ModManagerPage returns to LibraryPage)
- Active nav icon highlighted with AccentBrush

**LibraryEntry construction (inside UnifiedShellWindow):**

Build these entries on load:

```csharp
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
    GameKeys: new[] { "III", "VC", "SA" }
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
    GameKeys: new[] { "IV", "TLaD", "TBoGT" }
);

// Built-in custom games from GameRegistry (e.g. Skyrim AE, Fallout NV, etc.)
// Use config.GradientStartHex/GradientEndHex, config.LibraryStatus, config.LauncherCard
// IsPlaceholder = config.IsBuiltIn && string.IsNullOrEmpty(config.GameDirectory) — i.e. no path set AND is a bundled placeholder

// User custom games from GameRegistry.GetCustomGames()
// IsPlaceholder = false always for user-added games
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
Models/LibraryEntry.cs                        (A5 — new)          record for library card data
Models/CustomGameProfile.cs                   (A2+A6 — edit)      +GradientStartHex/GradientEndHex/LibraryStatus/CustomArtFileName
Models/TmmGameConfig.cs                       (A3 — edit)         +gradient/status fields in LauncherCardConfig+TmmGameExport
Models/GameProfile.cs                         (A4 — edit)         +GradientStartHex/GradientEndHex/LibraryStatus init props
Services/BackendCore.cs                       (A6 — edit)         +LibraryArtPath/GetLibraryArtPath/SaveLibraryArt/DeleteLibraryArt
Assets/GameProfiles/skyrim_ae.tmmgame         (B1 — edit)         add "libraryStatus": "PreAlpha"
Assets/GameProfiles/fallout_nv.tmmgame        (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/cyberpunk_2077.tmmgame    (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/red_dead_2.tmmgame        (B1 — new)          PreAlpha placeholder
Assets/GameProfiles/witcher_3.tmmgame         (B1 — new)          PreAlpha placeholder
TGTAMM.csproj                                 (B2 — edit)         4 new EmbeddedResource entries
Views/Controls/GameCard.xaml + .cs            (C1+A6 — new)       gradient card + art override + right-click menu
Views/Subpages/LibraryPage.xaml + .cs         (C2 — new)          search + WrapPanel of GameCards
Views/Subpages/SettingsPage.xaml + .cs        (C3 — new)          SettingsWindow ported to UserControl
Views/Subpages/DownloadsPage.xaml + .cs       (C4 — new)          downloads stub
Views/Subpages/BackupsPage.xaml + .cs         (C5 — new)          backups stub
Views/Subpages/ModManagerPage.xaml + .cs      (D1 — SONNET ONLY)  unified dashboard replacing 3 old windows
Views/UnifiedShellWindow.xaml + .cs           (D2 — SONNET ONLY)  icon nav strip + content area
App.xaml.cs                                   (D3 — SONNET ONLY)  1-line entry point change
[DELETE 5 old window files]                   (D4 — SONNET ONLY)
```
