# TMM — Implementation Plans
*Last updated 2026-05-23. Send this file to a new Claude session along with CODEBASE_GUIDE.md to resume work.*

---

## 0 — Current Shipped State (as of this document)

Everything below is **live on `master`** and working:

| Area | Status |
|------|--------|
| Direct deploy to game dir | ✅ |
| Backup & rollback (5-deep) | ✅ |
| Smart archive extraction | ✅ |
| GTA III/VC/SA full dashboard | ✅ |
| GTA IV/TLaD/TBoGT dashboard + wizard | ✅ |
| Multi-game launcher (GameLauncherWindow) | ✅ |
| Custom game support (Add/Edit/Delete) | ✅ |
| Sentence-builder routing rules + presets | ✅ |
| Context-aware SettingsWindow (Full / GtaIvOnly / CustomGame) | ✅ |
| Themes + dice button in all three dashboards | ✅ |
| Edit config (pencil) button in Custom Game dashboard | ✅ |
| CrashReportWindow with copy-to-clipboard | ✅ |
| App icon (T on dark, multi-size .ico) | ✅ |
| CODEBASE_GUIDE.md | ✅ |
| WindowBorderBrush on all windows (no forced accent border) | ✅ |
| macOS traffic-light buttons as default titlebar | ✅ |
| OpenFolder via explorer.exe (fixes newly-created dir error) | ✅ |

**Known gaps in shipped code (fix before new features):**
- `ToolbarShowLabels` only wired in MainDashboard — GTA IV and Custom dashboards have no label toggle
- Steam protocol launch (`steam://run/{id}`) not wired up in CustomGameDashboard despite `SteamAppId` being stored
- ThemeManagerWindow has a MainDashboard-specific callback; after theme change from IV/Custom dashboards the ThemeManager's internal refresh is partially broken
- "Open Mods Store" in GTA IV context menu is a stub (no URL wired)
- Deploy button color state (grey/accent/orange pending-changes indicator) only in MainDashboard; GTA IV and Custom use static icons
- Backup Folder context menu item absent from Custom Game dashboard

---

## 1 — .tmmgame Export/Import Format

### 1.1 File format spec

Extension: `.tmmgame`  
MIME type: `application/json`  
Encoding: UTF-8, pretty-printed  

```json
{
  "$schema": "tmm-game/1.0",
  "gameName": "Skyrim Anniversary Edition",
  "gameDirectory": "",
  "exePath": "SkyrimSE.exe",
  "steamAppId": "489830",
  "modFileTypes": ".zip, .rar, .7z, .esp, .esm, .esl, .dll, .bsa, .ba2, .ini, .pex",
  "outputDirectories": {
    ".esp":  "Data",
    ".esm":  "Data",
    ".esl":  "Data",
    ".bsa":  "Data",
    ".ba2":  "Data",
    ".pex":  "Data\\Scripts",
    ".psc":  "Data\\Scripts\\Source",
    ".ini":  ".",
    ".toml": ".",
    ".json": "."
  },
  "conditionalRoutes": [
    {
      "extension": ".dll",
      "checkSubdir": "Data\\SKSE",
      "routeIfExists": "Data\\SKSE\\Plugins",
      "routeIfMissing": "."
    }
  ],
  "installerHints": {
    "engineProxyNames": ["d3d11.dll", "d3d9.dll", "dxgi.dll", "d3d12.dll", "dinput8.dll"],
    "dxVersionTarget": "dx11",
    "smartDllWizard": true
  },
  "launcherCard": {
    "displayName": "Skyrim AE",
    "subtitle": "Anniversary Edition",
    "iconGlyph": "&#xE7FC;",
    "accentColor": "#4A9EFF"
  },
  "description": "Community config for Skyrim Anniversary Edition. Handles SKSE plugins, ESP/ESM data files, and engine proxies (DXVK, ENB, ReShade).",
  "author": "",
  "version": "1.0"
}
```

**Key fields:**
- `gameDirectory` — intentionally blank in exported files so each user fills in their own path
- `installerHints.engineProxyNames` — list of DLL filenames that trigger engine-proxy routing (not SKSE)
- `installerHints.dxVersionTarget` — when a DXVK archive contains multiple variants (d3d9/d3d11/d3d12), pick only the matching one
- `installerHints.smartDllWizard` — if true, the DLL installer wizard fires for this game
- `launcherCard` — optional; if present, a card is shown in GameLauncherWindow for this game
- `$schema` version allows forward-compatible parsing

### 1.2 Model changes

**`CustomGameProfile.cs`** — add new fields:
```csharp
public InstallerHints? InstallerHints { get; set; }
public LauncherCardConfig? LauncherCard { get; set; }
public string? Description { get; set; }
public string? Author { get; set; }
public string? Version { get; set; }
```

New record types (same file or `Models/TmmGameConfig.cs`):
```csharp
public record InstallerHints(
    List<string> EngineProxyNames,
    string DxVersionTarget,       // "dx9" | "dx11" | "dx12"
    bool SmartDllWizard
);

public record LauncherCardConfig(
    string DisplayName,
    string? Subtitle,
    string? IconGlyph,
    string? AccentColor
);
```

**Serialization:** Use `System.Text.Json` with `WriteIndented = true`. The existing `GameRegistry` already uses this. Add `ExportAsync(string path)` and `static ImportAsync(string path)` to `GameRegistry`.

### 1.3 Export flow

1. In `CustomGameConfigWindow`: add **Export** button in the footer bar (left of Save/Cancel)
2. Opens `SaveFileDialog` with filter `"TMM Game Config (*.tmmgame)|*.tmmgame"`
3. Serializes `CustomGameProfile` → JSON, blanks out `GameDirectory`, writes file
4. Shows success toast

### 1.4 Import flow

**From launcher (drag-and-drop):**
- Handle `Drop` event on `GameLauncherWindow` root
- Accept files with `.tmmgame` extension
- Parse, show `CustomGameConfigWindow` pre-filled (edit mode, no existing key), user sets `GameDirectory` and saves

**From launcher (button):**
- Add "Import Game Config" option to the + / New Game button menu (or a dedicated import button)
- Same flow as drag-and-drop after file is selected

**From `CustomGameConfigWindow` (Import button):**
- Opens `OpenFileDialog` filter `*.tmmgame`
- Loads fields into the open form (replacing current values with a confirm dialog if form is dirty)

---

## 2 — CustomGameConfigWindow Refinements

### 2.1 Import/Export buttons in the window

**XAML:** Add a `StackPanel` with `Import` and `Export` buttons on the LEFT side of the footer:
```xml
<Button Content="Import Config..." Click="BtnImportConfig_Click" .../>
<Button Content="Export Config..." Click="BtnExportConfig_Click" .../>
```
The existing `Cancel` and `Save` buttons stay on the right.

**Code-behind:**
- `BtnImportConfig_Click` — OpenFileDialog → parse .tmmgame → fill all fields (with dirty-check prompt)
- `BtnExportConfig_Click` — same as §1.3 export flow but can also export a *partially configured* config (useful for creating community templates before knowing the actual game path)

### 2.2 Rule drag-to-reorder

The `ItemsControl icCondRoutes` needs to become a drag-reorderable list.

**Approach:** Replace with a `ListBox` that has a transparent item container style matching the current card look, plus `PreviewMouseLeftButtonDown` / `MouseMove` / `Drop` handlers (same pattern as the mod list in the dashboards).

OR: Simpler — add Up/Down arrow buttons on each card next to the Remove (×) button. Arrows swap the item with the one above/below in `_condRoutes`. Less drag-drop complexity, works fine for small lists.

**Recommended:** Up/Down arrow buttons for now. Drag handles can be added later.

**XAML change on each card:**
```xml
<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
    <Button Content="▲" Click="BtnMoveRuleUp_Click" .../>
    <Button Content="▼" Click="BtnMoveRuleDown_Click" .../>
    <Button Content="&#xE8BB;" Click="BtnRemoveCondRouteItem_Click" .../>
</StackPanel>
```

**Code-behind:**
```csharp
private void BtnMoveRuleUp_Click(object sender, RoutedEventArgs e)
{
    if (sender is Button btn && btn.DataContext is CondRouteRow row)
    {
        int idx = _condRoutes.IndexOf(row);
        if (idx > 0) _condRoutes.Move(idx, idx - 1);
    }
}
// BtnMoveRuleDown_Click: same, idx < _condRoutes.Count - 1, Move(idx, idx + 1)
```

### 2.3 Rule conflict highlight

After any field change in a `CondRouteRow`, validate all rules. If two rows share the same extension, set a flag and show a red border on the conflicting cards.

**Approach:** Add a `bool HasConflict` property to `CondRouteRow` (with `INotifyPropertyChanged`). After each `TextBox` change, run a sweep:
```csharp
private void ValidateRuleConflicts()
{
    var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    foreach (var r in _condRoutes) r.HasConflict = false;
    foreach (var r in _condRoutes)
    {
        string ext = r.Extension.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(ext)) continue;
        if (seen.ContainsKey(ext))
        {
            r.HasConflict = true;
            _condRoutes[seen[ext]].HasConflict = true;
        }
        else seen[ext] = _condRoutes.IndexOf(r);
    }
}
```

**XAML trigger:** In the DataTemplate Border, add a `DataTrigger` on `HasConflict`:
```xml
<Border.Style>
    <Style TargetType="Border">
        <Setter Property="BorderBrush" Value="{DynamicResource SubTextBrush}"/>
        <Style.Triggers>
            <DataTrigger Binding="{Binding HasConflict}" Value="True">
                <Setter Property="BorderBrush" Value="#FF5555"/>
                <Setter Property="BorderThickness" Value="1.5"/>
            </DataTrigger>
        </Style.Triggers>
    </Style>
</Border.Style>
```

Wire `ValidateRuleConflicts()` to the `PropertyChanged` event on each row when rows are added.

### 2.4 Test routing dry-run

**Button:** `"Test Routing..."` in the conditional routes section header.

**Flow:**
1. Opens a small modal (`TestRoutingWindow`) or inline panel
2. User picks a file (OpenFileDialog, no filter)
3. System evaluates:
   - Is the extension in `outputDirectories`? → show static route
   - Does any `conditionalRoute` match the extension?
     - If yes: check whether `CheckSubdir` exists in `GameDirectory` (if directory is set)
     - Show result: `"→ Data\SKSE\Plugins\  (because Data\SKSE\ exists)"` or `"→ .  (Data\SKSE\ not found)"`
4. Display result as a simple info panel within the config window (no separate dialog needed — just a collapsible result area below the routing section)

**Code logic:**
```csharp
private string SimulateRoute(string filePath)
{
    string ext = Path.GetExtension(filePath).ToLowerInvariant();
    string gameDir = txtGameDir.Text.Trim();

    // Check conditional routes first (order matters - first match wins)
    foreach (var r in _condRoutes)
    {
        if (!r.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase)) continue;
        bool exists = !string.IsNullOrEmpty(gameDir) &&
                      Directory.Exists(Path.Combine(gameDir, r.CheckSubdir));
        string dest = exists ? r.RouteIfExists : r.RouteIfMissing;
        string reason = exists ? $"({r.CheckSubdir}\\ exists)" : $"({r.CheckSubdir}\\ not found)";
        return $"→  {(dest == "." ? "(game root)" : dest)}  {reason}";
    }

    // Static mapping
    if (_mappings.FirstOrDefault(m => m.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase))
            is MappingRow match)
        return $"→  {(match.OutputFolder == "." ? "(game root)" : match.OutputFolder)}  (static rule)";

    return $"→  (game root)  (no rule — default)";
}
```

### 2.5 Additional QoL (smaller items)

- **Description field** — optional free-text below game name; shown as tooltip on launcher card
- **Author/Version fields** — only shown when exporting (collapsible "Metadata" section)
- **Recent directories** — game directory field remembers last 5 used paths (dropdown arrow)
- **File type quick-add chips** — below the file types text box, show clickable chips for common extensions: [.zip] [.rar] [.7z] [.esp] [.dll] [.jar] — clicking appends to the field
- **Empty-state routing hint** — already implemented; ensure the hint text updates when game name is filled (personalize it: "Mods for *{gameName}* go to the game root by default")
- **"Open game dir" button** — small folder icon next to the Game Directory field to open it in Explorer (useful for verifying the path is right)

---

## 3 — Smart DLL Installer Wizard

Applies to **any** custom game (and built-in GTA IV / Skyrim / future games) that lists `.dll` in its supported mod file types.

### 3.1 Trigger conditions

The wizard fires during `BtnInstallMod_Click` (or the archive extraction completion handler) when:
- The installed archive contains one or more `.dll` files at its root or in a single subdirectory
- The game profile has `installerHints.smartDllWizard = true`  
  OR the game's mod file types include `.dll`

### 3.2 Wizard decision tree

```
Archive arrives containing .dll(s)
│
├─ Any .dll filename matches engineProxyNames?
│   (d3d11.dll, dxgi.dll, d3d9.dll, d3d12.dll, dinput8.dll, etc.)
│   │
│   ├─ YES → Engine proxy route
│   │   ├─ Multiple variants present (d3d9 + d3d11 + d3d12)?
│   │   │   └─ Auto-select based on dxVersionTarget:
│   │   │       "dx11" → keep d3d11.dll only
│   │   │       "dx9"  → keep d3d9.dll only
│   │   │       "dx12" → keep d3d12.dll only
│   │   │       null   → ask user which variant
│   │   └─ Route all kept files → game root (.)
│   │       Show toast: "DXVK (d3d11.dll) installed to game root"
│   │
│   └─ NO → Check archive directory structure
│       │
│       ├─ DLLs are in an SKSE\Plugins\ subfolder in the archive?
│       │   └─ Auto-route to Data\SKSE\Plugins\ (no prompt needed)
│       │
│       ├─ DLLs are in a plugins\ or scripts\ subfolder?
│       │   └─ Route to matching output dir from conditionalRoutes
│       │
│       └─ DLLs are loose at archive root?
│           └─ Show SmartDllDialog:
│               "What kind of mod is this?"
│               [Script Extender Plugin]  → Data\SKSE\Plugins\  (if SKSE route exists)
│               [Engine Proxy / Injector] → game root
│               [Other / Manual]          → pick destination
```

### 3.3 SmartDllDialog UI

Simple modal (no new XAML file needed — use `AskUserWindow` pattern or inline `MessageBox` with custom buttons):

```
Installing: ENBSeries.dll
────────────────────────────────────────────────
What kind of mod is this?

○  Script Extender Plugin  (Data\SKSE\Plugins\)
   e.g. SkyUI, iEquip, PapyrusUtil

○  Engine Proxy / Injector  (game root)
   e.g. DXVK, ENBSeries, ReShade, SpecialK

○  Let me choose the destination...

[ Cancel ]                              [ Install ]
```

New file: `Views/SmartDllDialog.xaml` + `.xaml.cs`

### 3.4 Engine proxy name list (built into BackendCore)

```csharp
public static readonly HashSet<string> KnownEngineProxyNames = new(StringComparer.OrdinalIgnoreCase)
{
    "d3d8.dll", "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll",
    "dxgi.dll", "dinput8.dll", "dsound.dll", "winmm.dll",
    "version.dll", "binkw32.dll"
};
```

A game profile's `InstallerHints.EngineProxyNames` overrides/extends this list.

---

## 4 — Built-in Game Profiles: Skyrim AE

### 4.1 Launcher card

`GameLauncherWindow` currently hard-codes the GTA card and iterates `GameRegistry` for custom games. Add a new section: **built-in extended profiles** loaded from embedded `.tmmgame` resource files.

**Implementation:**
1. Add folder `Assets/GameProfiles/` 
2. Add `skyrim_ae.tmmgame` as an `EmbeddedResource` in the .csproj
3. On `GameRegistry.InitializeAsync()`, load all embedded profiles as "built-in custom games" (same `CustomGameProfile` model, flagged `IsBuiltIn = true`)
4. Launcher shows them in a dedicated "Supported Games" section between the GTA card and user-added custom games

`IsBuiltIn` flag prevents Delete from appearing on the card (Edit is still allowed to set the game directory).

**skyrim_ae.tmmgame** (embed in Assets/GameProfiles/):
```json
{
  "$schema": "tmm-game/1.0",
  "gameName": "Skyrim Anniversary Edition",
  "exePath": "SkyrimSE.exe",
  "steamAppId": "489830",
  "modFileTypes": ".zip, .rar, .7z, .esp, .esm, .esl, .dll, .bsa, .ba2, .ini, .json, .pex, .psc",
  "outputDirectories": {
    ".esp":  "Data",
    ".esm":  "Data",
    ".esl":  "Data",
    ".bsa":  "Data",
    ".ba2":  "Data",
    ".pex":  "Data\\Scripts",
    ".psc":  "Data\\Scripts\\Source"
  },
  "conditionalRoutes": [
    {
      "extension": ".dll",
      "checkSubdir": "Data\\SKSE",
      "routeIfExists": "Data\\SKSE\\Plugins",
      "routeIfMissing": "."
    }
  ],
  "installerHints": {
    "engineProxyNames": ["d3d11.dll","d3d9.dll","dxgi.dll","d3d12.dll","dinput8.dll"],
    "dxVersionTarget": "dx11",
    "smartDllWizard": true
  },
  "launcherCard": {
    "displayName": "Skyrim AE",
    "subtitle": "Anniversary Edition",
    "iconGlyph": "&#xE7FC;",
    "accentColor": null
  },
  "description": "Built-in support for Skyrim Anniversary Edition (v1.6+). Routes ESP/ESM to Data\\, SKSE plugins to Data\\SKSE\\Plugins\\, and detects engine proxies (DXVK, ENB, ReShade) automatically.",
  "version": "1.0"
}
```

### 4.2 First-use setup (no wizard needed)

When the user clicks the Skyrim card and `GameDirectory` is empty, show a simple directory-picker prompt (reuse the `InitialSetupWindow` pattern or a minimal inline prompt). No full wizard needed since the routing is pre-configured.

### 4.3 Deploy verification panel

At deploy time, before writing files, show a summary:
```
Deploying 12 files to Skyrim AE:
  4 files  →  Data\          (.esp, .esm)
  6 files  →  Data\SKSE\Plugins\   (.dll)
  1 file   →  game root      (d3d11.dll — DXVK)
  1 file   →  Data\Scripts\  (.pex)
                    [ Deploy ]  [ Cancel ]
```

This is the **deploy preview panel** — show it when `_hasPendingChanges` is true and user hits Deploy, for any game with `installerHints` set. After confirmation deploy proceeds as normal.

**Implementation:** `DeployPreviewWindow.xaml` — simple modal listing destination groups. Passes `IEnumerable<(string dest, int count, string exts)>` from a new `BackendCore.SimulateDeployAsync()` method.

---

## 5 — Built-in Game Profiles: Minecraft

### 5.1 Java version table (for UI display)

| Label in TMM | Java version | Minecraft versions | Notes |
|---|---|---|---|
| Classic / Legacy | Java 8 | 1.0 – 1.16.5 | Old Forge mods |
| Modern | Java 17 | 1.17 – 1.20.4 | Most active modpacks |
| Current | Java 21 | 1.20.5 – 1.21.x | Fabric/NeoForge today |
| Future | Java 25 | 1.22+ | EA at time of writing |

TMM does not need to manage Java itself — just display the version requirement as a note next to the selected MC version.

### 5.2 minecraft.tmmgame (embed in Assets/GameProfiles/)

```json
{
  "$schema": "tmm-game/1.0",
  "gameName": "Minecraft Java Edition",
  "gameDirectory": "",
  "exePath": null,
  "steamAppId": null,
  "modFileTypes": ".zip, .jar, .json",
  "outputDirectories": {
    ".jar":  "mods",
    ".zip":  "resourcepacks"
  },
  "conditionalRoutes": [],
  "installerHints": {
    "smartDllWizard": false,
    "minecraftMode": true
  },
  "launcherCard": {
    "displayName": "Minecraft",
    "subtitle": "Java Edition",
    "iconGlyph": "&#xE7BE;",
    "accentColor": "#5DA832"
  },
  "description": "Built-in support for Minecraft Java Edition. Routes .jar mods to mods\\, resource packs to resourcepacks\\, shader packs to shaderpacks\\. Datapacks require world selection at install time.",
  "version": "1.0"
}
```

### 5.3 Minecraft-specific launcher card behavior

The Minecraft card in `GameLauncherWindow`:
- Shows a **MC version selector** (text field or dropdown) below the card title: "MC Version: [1.21.1]" → shows required Java version
- Shows a link button: `"Use Prism Launcher ↗"` → opens `https://prismlauncher.org`
- Default game directory: `%APPDATA%\.minecraft`
- If custom directory selected, TMM stores it as `GameDirectory` in the profile (same as any other game)

### 5.4 Datapack install flow (special case)

**Trigger:** User installs a mod archive that contains a `pack.mcmeta` file at its root → it's a datapack.

**Flow:**
1. Detect `pack.mcmeta` in archive root during extraction
2. Open `MinecraftWorldPickerDialog`:
   - Lists subdirectories of `saves\` in the game directory
   - Each row shows world name + icon (if `icon.png` exists in the save)
   - Checkboxes for multi-world install
   - "Install to all worlds" button
3. For each selected world: extract datapack to `saves\{worldName}\datapacks\{packName}\`

**`MinecraftWorldPickerDialog.xaml`** — new file, similar to `SmartDllDialog`

### 5.5 Mod loader detection

Before deploying `.jar` files, check:
```csharp
bool modsFolder = Directory.Exists(Path.Combine(gameDir, "mods"));
bool fabricJson  = File.Exists(Path.Combine(gameDir, "fabric.json")); // rough check
bool forgeInst   = Directory.GetFiles(gameDir, "forge-*.jar").Any();
```

If `mods\` doesn't exist, warn: *"No mod loader detected. Install Fabric or Forge first, then deploy."*  
Do not block deploy — just warn.

### 5.6 Shader pack detection

Archive containing a `shaders\` folder at root → it's a shader pack → route to `shaderpacks\` automatically (no `.zip` static mapping needed, use archive-content sniffing instead).

This requires peeking inside the archive before extraction. `SharpCompress` can list entries without extracting; add a `PeekArchiveType(string archivePath)` helper to `BackendCore`:
```csharp
public static ArchiveContentType PeekArchiveType(string archivePath)
{
    // Returns: Datapack, ShaderPack, ResourcePack, JarMod, Generic
}
```

---

## 6 — Mod Import from Existing Game Installations

### 6.1 Entry point

Each game dashboard gets an **"Import from Game"** toolbar button (icon `&#xE8B6;` — Download/Import arrow):
- GTA III/VC/SA: in `MainDashboardWindow` per-column header area
- GTA IV: in `Gta4DashboardWindow` per-column header
- Custom Game: in `CustomGameDashboardWindow` toolbar

### 6.2 Scan strategies per game

**GTA San Andreas:**
```
{gameDir}\modloader\           → each subfolder = one ModItem (name = folder name)
{gameDir}\CLEO\               → group .cs/.cleo by filename stem = one ModItem each
{gameDir}\scripts\            → each .asi = one ModItem
{gameDir}\moonloader\scripts\ → each .lua = one ModItem (if Moonloader present)
```

**GTA III / Vice City:**
```
{gameDir}\scripts\  → each .cs (CLEO) = one ModItem
{gameDir}\CLEO\     → same
Root *.asi files    → each = one ModItem
```

**GTA IV:**
```
{gameDir}\scripts\  → each .asi/.dll = one ModItem
{gameDir}\plugins\  → each .asi = one ModItem
{gameDir}\EFLC\scripts\ → same for TLaD/TBoGT
```

**Custom games (generic reverse-routing):**
For each configured `OutputDirectory` mapping (e.g. `.dll → Data\SKSE\Plugins`):
- Scan `{gameDir}\{outputFolder}\` for files matching the extension
- Group by logical name (filename without ext) or parent subfolder

### 6.3 Import UI flow

**Step 1 — Scan:**
New window `ModImportWindow.xaml`:
- Title: "Import from [Game Name]"
- Runs scan async with progress indicator
- Shows a `ListView` of detected items: `[✓] ModName | Type | Size | Source folder`
- "Select All" / "Select None" buttons
- Filter bar (search by name)

**Step 2 — Preview:**
At bottom: "Importing X mods will copy Y MB to TMM storage. Original files are not moved."

**Step 3 — Import:**
On confirm:
```csharp
foreach (var item in selected)
{
    string dest = Path.Combine(_core.AppDataPath, profile.RawFolderName, item.Name);
    Directory.CreateDirectory(dest);
    // Copy source files into dest
    foreach (var file in item.SourceFiles)
        File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
    // Create ModItem and add to _mods
    var mod = new ModItem { Name = item.Name, LoadOrder = _mods.Count, IsImported = true };
    _mods.Add(mod);
}
SaveModsToJson();
```

**Step 4 — Result:**
Toast: "Imported 14 mods. Review load order, then deploy to apply."
Imported mods get an `[Imported]` badge in the mod list (yellow chip).

**`ModItem` change:** Add `bool IsImported` property (serialized to JSON).

---

## 7 — Remaining Planned Features

### 7.1 Conflict resolution engine

**Trigger:** At deploy time, before any file is written.

**Logic:**
1. Build a `Dictionary<string, string>` of `{relativeFilePath → modName}` for all enabled mods in load order
2. When two mods map to the same relative path, the higher load-order mod wins but both are flagged
3. Show conflict list: `"SkyUI.esp overrides SkyUI_SE.esp → Data\SkyUI.esp (SkyUI wins)"`

**UI:** Non-blocking warning panel above the deploy button, or a modal with "Deploy anyway" option.

**New field on `DeployManifest`:** `ConflictedFiles: List<ConflictEntry>` where `ConflictEntry` holds winning mod, losing mod, relative file path.

### 7.2 Mod profiles / loadouts

- Save current enabled/disabled state + load order as a named loadout
- Stored as `{AppData}\{game}\loadouts\{name}.json`
- UI: dropdown in each dashboard header — "Active Loadout: [Default ▾]" with Save / Load / Delete
- Switching loadouts restores enable state + load order, marks changes pending

### 7.3 Community game library (future, not to implement yet)

- `https://raw.githubusercontent.com/...` or a dedicated endpoint serving `.tmmgame` files
- Launcher shows a "Browse game configs" button → fetches index → displays community cards
- One-click import populates the Add Custom Game form

### 7.4 Cover art

- Local picker: drag-and-drop image or Browse button on launcher card
- Stored as `{AppData}\GameArt\{key}.png` (resized to 460×215)
- SteamGridDB: if `SteamAppId` is set, fetch via `https://www.steamgriddb.com/api/v2` (user provides API key in Settings)
- Shown as background image on launcher card (overlay with gradient for text legibility)

### 7.5 Deploy status indicator for all dashboards

MainDashboard has a grey/accent/orange deploy button based on `_hasPendingChanges`. Replicate to GTA IV and Custom dashboards:
- Grey: `ControlBgBrush` — nothing to deploy
- Accent: pending mods  
- Orange: pending + override active

Requires subscribing to `_mods.CollectionChanged` and tracking a `_hasPendingChanges` bool in each dashboard (same pattern as `CustomGameDashboardWindow._hasPendingChanges`).

---

## 8 — File Structure Changes Summary

```
Assets/
  GameProfiles/               ← NEW
    skyrim_ae.tmmgame         ← NEW (EmbeddedResource)
    minecraft.tmmgame         ← NEW (EmbeddedResource)

Models/
  CustomGameProfile.cs        ← add InstallerHints, LauncherCardConfig, IsBuiltIn, Description, Author, Version
  ModItem.cs                  ← add IsImported bool

Services/
  GameRegistry.cs             ← add LoadBuiltInProfiles(), ExportAsync(), ImportAsync(), IsBuiltIn flag
  BackendCore.cs              ← add PeekArchiveType(), SimulateDeployAsync(), SmartDllRouter

Views/
  CustomGameConfigWindow.xaml     ← Import/Export buttons, move-up/down on routes, conflict highlight, test routing panel
  CustomGameConfigWindow.xaml.cs  ← all new handlers
  SmartDllDialog.xaml + .cs       ← NEW
  DeployPreviewWindow.xaml + .cs  ← NEW
  MinecraftWorldPickerDialog.xaml + .cs  ← NEW
  ModImportWindow.xaml + .cs      ← NEW
  GameLauncherWindow.xaml         ← drag-drop import, built-in profile section
  GameLauncherWindow.xaml.cs      ← import handler, built-in card rendering
```

---

## 9 — Implementation Order (suggested for next session)

1. **Fix known gaps first** (§0 gap list) — ~1 session
   - Steam protocol launch in Custom dashboard
   - Deploy status for IV / Custom dashboards
   - Toolbar label toggle for IV / Custom dashboards
   - Backup folder in Custom dashboard context menu

2. **`.tmmgame` format + Import/Export** (§1 + §2.1) — ~1 session
   - Model changes, serialization, SaveFileDialog, OpenFileDialog
   - Import/Export buttons in `CustomGameConfigWindow`
   - Drag-drop import on launcher

3. **CustomGameConfigWindow QoL** (§2.2 – §2.5) — ~1 session
   - Move-up/down arrows on route cards
   - Conflict highlight
   - Test routing panel
   - File type chips, description field

4. **Smart DLL wizard** (§3) — ~1 session
   - `SmartDllDialog`, engine proxy detection, variant picker for DXVK
   - Wire into install flow for all games with `.dll` in file types

5. **Skyrim AE built-in profile** (§4) — ~0.5 session
   - Embed `skyrim_ae.tmmgame`, `LoadBuiltInProfiles()`, launcher card

6. **Deploy preview panel** (§4.3) — ~0.5 session
   - `DeployPreviewWindow`, `SimulateDeployAsync()`

7. **Mod import from existing installs** (§6) — ~1 session
   - SA scan (modloader, CLEO, scripts), then GTA III/VC, then IV

8. **Minecraft built-in profile** (§5) — ~1 session
   - Embed `minecraft.tmmgame`, `PeekArchiveType()`, world picker dialog, shader detection

9. **Conflict resolution + File Arbitrator** (§10) — ~2 sessions

10. **Mod profiles / loadouts** (§7.2) — ~1 session

---

## 10 — Advanced Overwrite & Conflict Resolution

Design goal: simpler than Vortex's VFS, but gives the user full per-file control.
No virtual filesystem — TMM still deploys directly. Conflict resolution is a layer
*on top of* the existing deploy pipeline, not a replacement of it.

---

### 10.1 Core data model

#### File Version Store

For every file overwritten by any mod, TMM keeps that mod's version in a persistent
store. Storage layout:

```
AppData\TMM\FileVersions\
  {gameKey}\
    {sanitized_relative_path}\       e.g.  "Data__SKSE__Plugins__SkyUI.dll"
      _original.bak                  ← game's original file (captured once, never overwritten)
      SkyUI-5.2.bin                  ← SkyUI mod's version (updated when mod changes)
      SkyUI-5.3.bin                  ← if user updates SkyUI, previous version stays
      SkyUI_SE-4.1.bin               ← competing mod's version
```

Path sanitisation: replace `\` and `/` with `__`, strip leading dots.

#### File Ownership Registry

One JSON file per game:
```
AppData\TMM\{gameKey}_fileregistry.json
```

Structure:
```json
{
  "version": 1,
  "entries": {
    "Data\\SKSE\\Plugins\\SkyUI.dll": {
      "owner": "SkyUI",
      "pinnedTo": null,
      "claimedBy": ["SkyUI", "SkyUI_SE"],
      "originalCaptured": true
    },
    "d3d11.dll": {
      "owner": "DXVK",
      "pinnedTo": null,
      "claimedBy": ["DXVK"],
      "originalCaptured": true
    }
  }
}
```

Fields:
- `owner` — which mod currently has its version deployed (last-write wins by load order, unless overridden)
- `pinnedTo` — if non-null, this mod's version is always used regardless of load order
- `claimedBy` — all mods that have ever written this file (conflict = `claimedBy.Count > 1`)
- `originalCaptured` — whether we have `_original.bak` for this path

#### Model changes

New class `Models/FileVersionStore.cs`:
```csharp
public record FileRegistryEntry(
    string Owner,
    string? PinnedTo,
    List<string> ClaimedBy,
    bool OriginalCaptured
);

public class FileVersionStore
{
    public int Version { get; set; } = 1;
    public Dictionary<string, FileRegistryEntry> Entries { get; set; } = new();
}
```

---

### 10.2 Deploy pipeline changes (BackendCore)

Current deploy flow:
```
foreach mod → foreach file → backup original → copy to game dir
```

New deploy flow:
```
foreach mod (in load order):
  foreach file in mod:
    relativePath = file relative to game dir

    // 1. Capture original if not already done
    if registry[relativePath].originalCaptured == false:
      copy game file → FileVersions/{gameKey}/{sanitized}/original.bak
      registry[relativePath].originalCaptured = true

    // 2. Store this mod's version
    copy mod file → FileVersions/{gameKey}/{sanitized}/{modName}.bin

    // 3. Register claim
    registry[relativePath].claimedBy.Add(modName)  // if not already present

    // 4. Determine winner
    winner = registry[relativePath].pinnedTo ?? modName   // pin overrides load order

    // 5. Deploy winner's version
    source = pinnedTo != null
        ? FileVersions/{gameKey}/{sanitized}/{pinnedTo}.bin
        : mod file (in-memory, already extracted)
    copy source → gameDir/relativePath

    registry[relativePath].owner = winner

// Save registry
```

Key point: if a file is `pinnedTo` a mod that has a lower load order, the pinned
version is still deployed. Load order continues to control non-conflicted files.

---

### 10.3 New BackendCore methods

```csharp
// Returns all conflicted files (claimedBy.Count > 1) for a game
public List<(string RelativePath, FileRegistryEntry Entry)> GetConflicts(GameProfile profile);

// Returns all versions stored for a given file path
public List<(string ModName, string VersionPath, DateTime LastModified)>
    GetVersionsForFile(GameProfile profile, string relativePath);

// Pin a specific file to a specific mod's version (persists to registry)
// Pass null to unpin (revert to load-order-wins)
public void PinFileVersion(GameProfile profile, string relativePath, string? modName);

// Get the stored original (pre-all-mods) version path for a file
public string? GetOriginalVersionPath(GameProfile profile, string relativePath);

// Open a stored version in a temp location for preview
public string ExtractVersionToTemp(string versionPath);
```

---

### 10.4 Conflict Resolver — UI

New window: `Views/ConflictResolverWindow.xaml`

Opened from:
- A "Conflicts" toolbar button in each dashboard (shows the count badge: `Conflicts (3)`)
- Automatically after deploy if new conflicts are introduced ("Deploy complete — 3 new conflicts found. Review now?")

#### Layout (two-panel)

```
┌─────────────────────────────────────────────────────────────────────┐
│  TMM — Conflict Resolver — Skyrim AE                        [×]     │
├──────────────────┬──────────────────────────────────────────────────┤
│  CONFLICT LIST   │  FILE VERSIONS                                    │
│  ─────────────── │  ─────────────────────────────────────────────── │
│  ● 3 conflicts   │  Data\SKSE\Plugins\SkyUI.dll                      │
│                  │                                                    │
│  Data\SKSE\Pl... │  ┌─ Versions ─────────────────────────────────┐  │
│    SkyUI  ←win   │  │ ◉ SkyUI v5.2        127 KB   [Preview]     │  │
│    SkyUI_SE      │  │   SkyUI_SE v4.1     118 KB   [Preview]     │  │
│                  │  │   Original           96 KB   [Preview]     │  │
│  Data\meshes\... │  └────────────────────────────────────────────┘  │
│    ModA  ←win    │                                                    │
│    ModB          │  Pinned to:  SkyUI  [Change ▾]  [Clear Pin]       │
│                  │                                                    │
│  scripts\main... │  Load order winner would be: SkyUI_SE             │
│    ModC  ←win    │  Current deployed version:   SkyUI (pinned)       │
│    ModD          │                                                    │
│                  │  [Browse File Explorer →]                         │
│  ─────────────── │                                                    │
│  ◉ = pinned      │                                                    │
│  ← = current win │                                                    │
├──────────────────┴──────────────────────────────────────────────────┤
│                    [ Redeploy to apply changes ]   [ Close ]        │
└─────────────────────────────────────────────────────────────────────┘
```

Left panel: `ListView` of conflicted files grouped by directory.
Each item shows competing mods; `←win` marks current winner; `◉` marks a pinned file.

Right panel: Selected file's version list. Each row: mod name, size, last modified, Preview button.

**Preview button:** copies the `.bin` to a temp path with the correct extension and opens it with `ShellExecute` (lets user open `.esp` in xEdit, `.dll` in Dependencies, etc.).

**Change ▾ dropdown:** picks which mod to pin to (or "Load order winner").

**Clear Pin:** removes the pin; load order resumes control on next deploy.

---

### 10.5 File Explorer — UI

Opened from "Browse File Explorer →" button in the Conflict Resolver, or from a toolbar button in each dashboard.

New window: `Views/FileExplorerWindow.xaml`

#### Layout

```
┌─────────────────────────────────────────────────────────────┐
│  TMM — File Explorer — Skyrim AE                     [×]    │
├─────────────────────┬───────────────────────────────────────┤
│  DIRECTORY TREE     │  FILES IN:  Data\SKSE\Plugins\        │
│                     │  ─────────────────────────────────    │
│  ▶ (game root)      │  ● SkyUI.dll        127 KB   [2 mods] │
│  ▼ Data             │  ◆ hudframework.dll   94 KB   [1 mod]  │
│    ▼ SKSE           │  ○ PapyrusUtil.dll    61 KB   [clean] │
│      ▼ Plugins  ← │                                        │
│    ▼ Scripts        │                                        │
│    ▶ meshes         │                                        │
│    ▶ textures       │                                        │
│  ▶ scripts\         │                                        │
├─────────────────────┴───────────────────────────────────────┤
│  Legend:  ● conflict   ◆ 1 mod   ○ original/clean          │
└─────────────────────────────────────────────────────────────┘
```

**Tree:** `TreeView` built by scanning the game directory. Directories with any
conflicted descendants are highlighted.

**File list:** `ListView` for the selected directory. Each row:
- Dot colour: ● red = conflict, ◆ yellow = 1 mod owns it, ○ green = no mod touches it
- Badge: `[2 mods]` is clickable and jumps to that file in the Conflict Resolver

**Implementation note:** The tree is built lazily (expand-on-demand) to avoid
scanning the entire game dir upfront. Files are cross-referenced against the
`FileVersionStore` registry to determine their dot colour.

---

### 10.6 Conflict badge in dashboards

Each dashboard toolbar shows a conflict indicator:

```
[⚠ 3 conflicts]  ← orange button, click opens ConflictResolverWindow
```

or when clean:

```
[✓ No conflicts]  ← grey, still clickable (opens resolver showing "all clear")
```

Badge count = `GetConflicts(profile).Count`.

Updated after every deploy and when mods are added/removed.

**XAML element name:** `btnConflicts` (same pattern as `btnDeploy`).
**Handler:** `BtnConflicts_Click` → `new ConflictResolverWindow(_core, _profile) { Owner = this }.ShowDialog()`.

---

### 10.7 Storage management

The FileVersionStore can grow large over time. Add a housekeeping pass:

**Triggered:** When a mod is deleted from TMM (context menu → Delete Mod).

**Action:** Remove that mod's `.bin` files from `FileVersions\{gameKey}\{paths}\`.
If `claimedBy` becomes empty after removal, delete the entire path folder.
Update the registry entries accordingly.

**Manual cleanup:** Settings → Diagnostics → "Wipe File Version Cache" (with warning
that per-file pinning will be lost and rollback capability reduced).

---

### 10.8 Implementation order within this feature

1. `FileVersionStore` model + `fileregistry.json` persistence
2. `BackendCore` deploy pipeline changes (capture originals, store mod versions, apply pins)
3. New `BackendCore` methods (GetConflicts, GetVersionsForFile, PinFileVersion)
4. `ConflictResolverWindow` (list panel + version panel + pin controls)
5. Conflict badge in all dashboard toolbars
6. `FileExplorerWindow` (directory tree + file list with dot indicators)
7. Preview button (temp-extract + ShellExecute)
8. Housekeeping on mod delete

Estimated: ~2 sessions total.

---

*End of plans. Commit hash when created: see `git log --oneline -1`.*
