# TMM Codebase Guide

Reference for AI sessions and developers. Two sections:
- **Table of Contents** — human-readable pseudocode overview of what each file does
- **Search Index** — keyword tags for fast AI lookup

---

## Table of Contents

### Entry Point
```
App.xaml.cs
  on startup →
    register global crash handler (ShowCrashDialog → MessageBox + clipboard)
    create BackendCore
    show GameLauncherWindow
```

---

### Windows / Views

#### `GameLauncherWindow` — Main Hub
```
shows cards for: GTA III Series, GTA IV Series, each custom game, + Add button
each card has: title, subtitle, status dot, Manage button
clicking Manage →
  GTA III  → if FirstLaunch: show InitialSetupWindow → open MainDashboardWindow
  GTA IV   → if no IV paths set: show InitialSetupWindow → open Gta4DashboardWindow
  Custom   → open CustomGameDashboardWindow
  Add      → open CustomGameConfigWindow → register via GameRegistry
cards also have Edit / Delete buttons for custom games
```

#### `MainDashboardWindow` — GTA III Series (III / VC / SA)
```
single mod list for whichever game is active
toolbar: install mod, refresh, rescan, deploy, rollback, launch, open appdata, settings
per-game: path label + browse button, search filter, status dot
deploy → BackendCore.DeployModsAsync
rollback → BackendCore.RollbackDeployAsync (picks latest snapshot)
context menu on mod: rename, set load order, toggle, open folder, delete, properties
drag-drop reorder within list
keyboard: F2=rename, Space=toggle, Del=delete, F5=deploy, Ctrl+↑/↓=move
```

#### `Gta4DashboardWindow` — GTA IV Series (IV / TLaD / TBoGT)
```
three-column layout: one column per episode
each column: status dot, path label + browse, search filter, mod list, deploy + rollback + launch buttons
toolbar: install mod (asks which episode), refresh, rescan, deploy all, open appdata, settings, back
mod install → shows EpisodePicker to choose which episode → extracts archive → SmartArchivePostProcess
SmartArchivePostProcess:
  single-root unwrap (strip outer folder)
  known-folder detection (plugins/, scripts/, modloader/, bin/)
  if no known structure + readme found → offer to open readme
```

#### `CustomGameDashboardWindow` — User-Added Games
```
single mod list for a custom game profile
toolbar: install mod, refresh, launch (if ExePath set), settings, back
archive install → ExtractArchiveSafeAsync → stage in ModsRaw{key}/
deploy → BackendCore.DeployModsAsync
```

#### `InitialSetupWindow` — First-Run Path Wizard
```
shows GameSetupRow for each of: III, VC, SA, IV, TLaD, TBoGT
each row: browse button, detected path, status indicator
IV row change → auto-derives TLaD + TBoGT paths via SetVanillaPath
runs QuickScan on load to pre-populate known paths
Finish button requires at least one game ready → sets FirstLaunch=false
```

#### `SettingsWindow`
```
tabs: Appearance, Paths, Advanced
Appearance: theme picker → ThemeManagerWindow, font, Mica toggle
Paths: shows GameSetupRow for each game (same as InitialSetupWindow)
Advanced: factory reset, diagnostics (MD5 check, drive space)
```

#### `ThemeManagerWindow`
```
lists all built-in theme presets grouped by category
live preview on hover/select
apply → ThemeEngine.ApplyTheme
export preset → .mmtheme JSON file
```

#### `CustomGameConfigWindow`
```
form: game name, game directory (browse), exe path (browse), steam app id,
      routing rules (sentence builder), file extensions
validation: steamAppId must be numeric, extensions must start with ".", routes need both fields
test routing panel: browse a test file → shows where it would land (conditional dir check included)
export .tmmgame / import .tmmgame
returns CustomGameProfile on confirm
```

#### Supporting Windows
```
ModPropertiesWindow      — read-only view of mod metadata (name, order, path, enabled)
RenameWindow             — single text input dialog (rename + set load order)
AboutWindow              — version, credits
ArchiveExtractionWindow  — progress display during archive extraction
GameSetupRow             — reusable path browse row (used by InitialSetupWindow + SettingsWindow)
```

---

### Services

#### `BackendCore` — Core Orchestrator
```
AppDataPath       → %APPDATA%\TMM\
Settings          → AppSettings (loaded from settings.json)
Mods[key]         → ObservableCollection<ModItem> per game

InitializeAsync() → load settings, create mod dirs, register all game profiles with GameRegistry
QuickScan()       → check fixed drives at known Steam/ProgramFiles paths for each game exe
SetVanillaPath(profile, path)
  → saves path; if IV, auto-derives TLaD (TLAD\ or TLaD\) and TBoGT (EFLC\ or TBoGT\)
IsGameReady(profile) → true if path is set and non-empty

DeployModsAsync(profile, mods, progress, ct)
  → creates backup manifest → copies enabled mods in load order to game dir
  → uses RoutingRules / ConditionalRoutes to route files to subdirs
RollbackDeployAsync(manifest, progress)
  → restores game dir from backup snapshot
GetRollbackManifests(key) → list of DeployManifest sorted newest first

RefreshAllModListsAsync() → reloads Mods[key] from disk for all games
ExtractArchiveSafeAsync(path, dest, ct) → uses SharpCompress, handles zip/rar/7z
ForceDeleteDirectory(path) → recursive delete ignoring readonly flags
GetDriveSpaceInfo() → "X.X GB free on C:"
OpenAppData() → shell-opens AppDataPath
```

`DeploymentProgress` record struct lives at the top of `BackendCore.cs`:
```
public readonly record struct DeploymentProgress(string Stage, int Current, int Total)
```

#### `GameRegistry` — Game Roster (Singleton)
```
Instance → thread-safe singleton

GetAllGames()        → all built-in + custom GameProfiles
GetCustomGames()     → Dictionary<string, CustomGameProfile> of user-added games
GetGameProfile(key)  → GameProfile? by key
GetCustomGameConfig(key) → CustomGameProfile? by key

AddCustomGameAsync(config)         → assigns key, saves to disk, adds to registry
UpdateCustomGameAsync(key, config) → edits existing entry
DeleteCustomGameAsync(key)         → removes from registry + disk
```

#### `NotificationService`
```
Show(message, type, durationMs) → adds NotificationItem to Queue; DispatcherTimer removes it after durationMs
Queue → ObservableCollection<NotificationItem> (UI binds to this for toast display)
Helpers: ShowSuccess / ShowWarning / ShowError / ShowInfo
```

`NotificationItem` + `NotificationType` enum are defined inline at the top of `NotificationService.cs`.

#### `SteamLauncher`
```
Invoke(action, appId) → runs Steam protocol commands (install/validate/uninstall/rungameid)
```

---

### Models

| Model | Purpose |
|---|---|
| `GameProfile` | Immutable record: Key, DisplayName, ExeName, SteamAppId, Vanilla10Md5, ConditionalRoutes. Static instances: III, VC, SA, IV, TLaD, TBoGT. Also defines `ExeStatus` enum (Unknown/Vanilla/Downgraded). |
| `CustomGameProfile` | User-defined game: GameName, GameDirectory, ExePath, SteamAppId, RoutingRules, InstallerHints, LauncherCard |
| `ModItem` | Single mod: Name, IsEnabled, LoadOrder, RawFolderPath. Persisted as modinfo.txt in mod folder |
| `AppSettings` | All persisted settings: GamePaths, FirstLaunch, theme/font fields, DeployOverrides, CustomGameKeys |
| `DeployManifest` | Backup snapshot: Timestamp, ModNames, per-file backup paths. Used for rollback |
| `RoutingRule` | Extension pattern + Destination + optional FallbackDestination + CheckSubdir. Used in CustomGameProfile |
| `ConditionalRoute` | Legacy backward-compat route (`.tmmgame` v1.0 import only). Defined in `TmmGameConfig.cs` |

---

### Helpers

**`Helpers/Helpers.cs`** — three static helpers in one file:

| Class | Methods |
|---|---|
| `ShellHelper` | `OpenFolder(path)`, `OpenUrl(url)` — shell-execute wrappers |
| `UiColors` | Static Color + SolidColorBrush constants: DisabledGray, ReadyGreen, NotReadyRed, PendingOrange |
| `JsonHelper` | `PrettyOptions` + `TmmGameOptions` — shared `JsonSerializerOptions` instances |

---

### Theming

#### `ThemeEngine`
```
ApplyTheme(settings)        → sets all DynamicResource brushes in App.Resources
ApplyFont(window, settings) → sets FontFamily on window
TryApplyMica(window, enabled) → enables Windows Mica backdrop via WindowChrome
Text contrast: hardcoded WCAG algorithm
Mica intensity: hardcoded 0.75
```

---

### Key Conventions

**Game Keys:** `"III"` `"VC"` `"SA"` `"IV"` `"TLaD"` `"TBoGT"` + custom keys (e.g. `"CUSTOM_abc123"`)

**Mod storage path:** `%APPDATA%\TMM\ModsRaw{key}\{ModName}\`  
**Mod metadata:** `modinfo.txt` (JSON-serialized ModItem) inside each mod folder  
**Settings file:** `%APPDATA%\TMM\settings.json`  
**Backup snapshots:** `%APPDATA%\TMM\Backups\{key}\{timestamp}.json`  
**Custom game registry:** `%APPDATA%\TMM\CustomGames\{key}.json`

**IV path auto-derive:** Setting IV path checks for `TLAD\` or `TLaD\` → sets TLaD; checks `EFLC\` or `TBoGT\` → sets TBoGT

**Deploy flow:**
1. `DeployModsAsync` iterates enabled mods in LoadOrder
2. Backs up any existing files at destination
3. Copies mod files respecting RoutingRules / ConditionalRoutes (e.g. `.asi` → `plugins\` if that folder exists)
4. Saves DeployManifest for rollback

**Resource keys (App.xaml):**  
`AccentBrush` `AccentTextBrush` `AccentLabelBrush` `BgBrush` `PanelBrush` `HeaderBrush`  
`TextBrush` `SubTextBrush` `ControlBgBrush` `CheckeredRowBrush`  
Styles: `IconButtonStyle` `CardButtonStyle` (GameLauncherWindow-local)  
Window-local styles: `ColActionBtn` `ToolIconBtn` `ModListStyle` `ModListTemplate`

---

## Search Index

**crash handler / error popup** → `App.xaml.cs` `ShowCrashDialog`  
**game path storage / where paths are saved** → `AppSettings.GamePaths` → `settings.json`  
**IV auto-derive TLaD TBoGT** → `BackendCore.SetVanillaPath`  
**deploy mods / copy mods to game folder** → `BackendCore.DeployModsAsync`  
**rollback / undo deploy** → `BackendCore.RollbackDeployAsync` + `DeployManifest`  
**mod list on disk / how mods are stored** → `%APPDATA%\TMM\ModsRaw{key}\`  
**mod metadata persistence** → `modinfo.txt` in mod folder, JSON of `ModItem`  
**custom game add/edit/delete** → `GameRegistry` + `CustomGameConfigWindow`  
**theme application** → `ThemeEngine.ApplyTheme` → `App.Resources` DynamicResource brushes  
**all themes list / theme presets** → `ThemeManagerWindow`  
**first run / onboarding flow** → `AppSettings.FirstLaunch` → `InitialSetupWindow`  
**archive extraction** → `BackendCore.ExtractArchiveSafeAsync` (SharpCompress)  
**smart archive unwrap** → `Gta4DashboardWindow.SmartArchivePostProcess`  
**ASI routing to plugins folder** → `ConditionalRoute` on IV/TLaD/TBoGT profiles  
**Steam launch** → `SteamLauncher.Invoke` (install/validate/uninstall/rungameid commands)  
**drag-drop reorder** → `MainDashboardWindow` + `Gta4DashboardWindow` List_Drop handlers  
**context menu on mod** → `ModContextMenu` resource in each dashboard XAML  
**backup snapshots location** → `%APPDATA%\TMM\Backups\`  
**custom game registry location** → `%APPDATA%\TMM\CustomGames\`  
**game exe names** → `GameProfile.ExeName` (gta3.exe, gta-vc.exe, gta-sa.exe, GTAIV.exe, TLAD.exe, EFLC.exe)  
**status dot color logic** → `SetDotColor` in dashboard windows  
**Mica backdrop** → `ThemeEngine.TryApplyMica`  
**notification toasts** → `NotificationService.Show` → `NotificationService.Queue`  
**factory reset** → `BackendCore.FactoryReset` called from `SettingsWindow`  
**drive space** → `BackendCore.GetDriveSpaceInfo`  
**shell open folder/url** → `ShellHelper.OpenFolder` / `ShellHelper.OpenUrl` in `Helpers/Helpers.cs`  
**UI color constants** → `UiColors` in `Helpers/Helpers.cs`  
**JSON options** → `JsonHelper.PrettyOptions` / `JsonHelper.TmmGameOptions` in `Helpers/Helpers.cs`  
**exe status / downgrade detection** → `ExeStatus` enum in `GameProfile.cs`  
**routing rules (custom games)** → `RoutingRule` in `CustomGameProfile.cs`  
**legacy routing import** → `ConditionalRoute` in `TmmGameConfig.cs`  
**resource brushes missing / XAML static resource error** → check `App.xaml` resources section; window-local styles (e.g. `CardButtonStyle`) are not available in other windows  
