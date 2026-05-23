# TMM â€” Implementation Plans
*Last updated 2026-05-23. For future features and deferred concepts, see [`FUTURE_ADDITIONS.md`](FUTURE_ADDITIONS.md).*

> **[2026-05-23] Haiku pass (2nd session) â€” Â§2.4, Â§4.1, Â§4.3, Â§7.2:**
> - âœ… Â§2.4 Test routing dry-run panel: Collapsible panel in CustomGameConfigWindow, BtnTestRouting_Click, RunTestRoute() simulation
> - âœ… Â§4.1 Skyrim AE embedded profile: skyrim_ae.tmmgame asset, LoadBuiltInProfilesAsync(), GameLauncherWindow "Supported Games" section
> - âœ… Â§4.3 Deploy preview panel: DeployPreviewWindow.xaml/cs, DeploymentGroup model, file grouping by destination
> - âœ… Â§7.2 Mod loadouts foundation: ModLoadout.cs, BackendCore.SaveLoadoutAsync/LoadLoadoutAsync, storage model
> 
> **Advanced features (Smart DLL wizard, Minecraft, Conflict Resolution, etc.) moved to [`FUTURE_ADDITIONS.md`](FUTURE_ADDITIONS.md) â€” not actively developed.**

> **Companion file:** [`SCOPE.md`](SCOPE.md) is a shorter, human-readable overview of planned features. Whenever this file is updated, also update SCOPE.md to keep both in sync.

> **Session changelog:**
> - Â§1 + Â§2.1 complete (`.tmmgame` import/export, drag-drop launcher, CustomGameConfigWindow buttons)
> - Optimization pass complete: `ShellHelper` extracted, `GridViewColumnHeader` style centralized in App.xaml, `DashboardListItemStyle` centralized, `WindowBorderGradientBrush` dead code removed, `BtnDonate_Click` dead code removed, `ModListStyle` ItemContainerStyle de-duplicated across all three dashboards
> - Deferred UI refactor added as Â§UI-R (see below); do NOT execute until explicitly resumed
> - **VFS removal dead code cleanup complete (2026-05-23):**
>   - Removed `DeepScanDrives()` + `RecursiveSearch()` (never called; QuickScan handles all cases)
>   - Removed `SmartSteamLaunch()` stub (deferred to Smart DLL wizard feature)
>   - Removed `CopyDirectoryParallelAsync()` (sync `CopyDirectory()` sufficient)
>   - Removed `DebugStaging` property (legacy VFS flag)
>   - Simplified `GameState.cs`: deleted `GameStateManager` + `GameDetectionState` + shim, kept `ExeStatus` enum
>   - Removed empty `ModList_SelectionChanged()` event handler
>   - **Total: ~240 lines eliminated, 0 errors/warnings**

---

## 0 â€” Current Shipped State (as of this document)

Everything below is **live on `master`** and working:

| Area | Status |
|------|--------|
| Direct deploy to game dir | âœ… |
| Backup & rollback (5-deep) | âœ… |
| Smart archive extraction | âœ… |
| GTA III/VC/SA full dashboard | âœ… |
| GTA IV/TLaD/TBoGT dashboard + wizard | âœ… |
| Multi-game launcher (GameLauncherWindow) | âœ… |
| Custom game support (Add/Edit/Delete) | âœ… |
| Sentence-builder routing rules + presets | âœ… |
| Context-aware SettingsWindow (Full / GtaIvOnly / CustomGame) | âœ… |
| Themes + dice button in all three dashboards | âœ… |
| Edit config (pencil) button in Custom Game dashboard | âœ… |
| CrashReportWindow with copy-to-clipboard | âœ… |
| App icon (T on dark, multi-size .ico) | âœ… |
| CODEBASE_GUIDE.md | âœ… |
| WindowBorderBrush on all windows (no forced accent border) | âœ… |
| macOS traffic-light buttons as default titlebar | âœ… |
| OpenFolder via explorer.exe (fixes newly-created dir error) | âœ… |
| `.tmmgame` export/import (CustomGameConfigWindow + GameLauncherWindow drag-drop) | âœ… |
| `ShellHelper` â€” shared OpenFolder/OpenUrl (removed 3 local duplicate methods) | âœ… |
| `GridViewColumnHeader` style centralized in App.xaml | âœ… |
| `DashboardListItemStyle` centralized in App.xaml, ModListStyle BasedOn in all dashboards | âœ… |
| `WindowBorderGradientBrush` dead resource + ThemeEngine writes removed | âœ… |
| VFS removal dead code cleanup (DeepScan, SmartSteam, ParallelCopy, DebugStaging, GameState manager) | âœ… |

**Known gaps â€” status:**
- âœ… `ToolbarShowLabels` â€” GTA IV and Custom both call `ApplyToolbarLabels()` on load; toggle button is in MainDashboard only (acceptable)
- âœ… Steam protocol launch â€” `BtnLaunch_Click` in CustomGameDashboard already checks `SteamAppId` â†’ `SteamLauncher.Invoke`
- âš ï¸ ThemeManagerWindow has a MainDashboard-specific callback; after theme change from IV/Custom dashboards the internal refresh is partially broken *(still open)*
- âœ… "Open Mods Store" in GTA IV â€” now opens `https://www.nexusmods.com/gta4/`
- âœ… Deploy button color state â€” all 3 dashboards have `UpdateDeployButton()` / `UpdateEpisodeDeployButton()`
- âœ… Backup Folder context menu â€” present in CustomGameDashboard (verified)

---

## 1 â€” .tmmgame Export/Import Format

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
- `gameDirectory` â€” intentionally blank in exported files so each user fills in their own path
- `installerHints.engineProxyNames` â€” list of DLL filenames that trigger engine-proxy routing (not SKSE)
- `installerHints.dxVersionTarget` â€” when a DXVK archive contains multiple variants (d3d9/d3d11/d3d12), pick only the matching one
- `installerHints.smartDllWizard` â€” if true, the DLL installer wizard fires for this game
- `launcherCard` â€” optional; if present, a card is shown in GameLauncherWindow for this game
- `$schema` version allows forward-compatible parsing

### 1.2 Model changes

**`CustomGameProfile.cs`** â€” add new fields:
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
3. Serializes `CustomGameProfile` â†’ JSON, blanks out `GameDirectory`, writes file
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

## 2 â€” CustomGameConfigWindow Refinements

### 2.1 Import/Export buttons in the window

**XAML:** Add a `StackPanel` with `Import` and `Export` buttons on the LEFT side of the footer:
```xml
<Button Content="Import Config..." Click="BtnImportConfig_Click" .../>
<Button Content="Export Config..." Click="BtnExportConfig_Click" .../>
```
The existing `Cancel` and `Save` buttons stay on the right.

**Code-behind:**
- `BtnImportConfig_Click` â€” OpenFileDialog â†’ parse .tmmgame â†’ fill all fields (with dirty-check prompt)
- `BtnExportConfig_Click` â€” same as Â§1.3 export flow but can also export a *partially configured* config (useful for creating community templates before knowing the actual game path)

### 2.2 Rule drag-to-reorder

The `ItemsControl icCondRoutes` needs to become a drag-reorderable list.

**Approach:** Replace with a `ListBox` that has a transparent item container style matching the current card look, plus `PreviewMouseLeftButtonDown` / `MouseMove` / `Drop` handlers (same pattern as the mod list in the dashboards).

OR: Simpler â€” add Up/Down arrow buttons on each card next to the Remove (Ã—) button. Arrows swap the item with the one above/below in `_condRoutes`. Less drag-drop complexity, works fine for small lists.

**Recommended:** Up/Down arrow buttons for now. Drag handles can be added later.

**XAML change on each card:**
```xml
<StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
    <Button Content="â–²" Click="BtnMoveRuleUp_Click" .../>
    <Button Content="â–¼" Click="BtnMoveRuleDown_Click" .../>
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

### 2.4 Test routing dry-run  â¬› HAIKU-ELIGIBLE

**Decided:** Inline collapsible panel below the routing rules; "Test Routingâ€¦" button sits next to "+ Add Rule".

**Button row:**
```xml
<StackPanel Orientation="Horizontal" Margin="0,0,0,14">
    <Button Content="+ Add Rule"       Click="BtnAddCondRoute_Click" .../>
    <Button Content="Test Routing..."  Click="BtnTestRouting_Click"  .../>
</StackPanel>
```

**Panel (initially Collapsed, shown after first click):**
```xml
<Border x:Name="pnlTestRouting" Visibility="Collapsed" ...>
    <StackPanel>
        <Grid>  <!-- file path row -->
            <TextBox x:Name="txtTestFile" Placeholder="Drop or browse a file..." />
            <Button Content="Browse..." Click="BtnTestRoutingBrowse_Click"/>
        </Grid>
        <TextBlock x:Name="txtTestResult" Text="â†’ (nothing yet)" />
    </StackPanel>
</Border>
```

**BtnTestRouting_Click:** Toggle `pnlTestRouting.Visibility`. If showing and `txtTestFile` already has a value, call `RunTestRoute()`.

**BtnTestRoutingBrowse_Click:** `OpenFileDialog` (no filter) â†’ set `txtTestFile.Text` â†’ call `RunTestRoute()`.

**RunTestRoute():** Call `SimulateRoute(txtTestFile.Text)` â†’ set `txtTestResult.Text`.

**Flow:**
1. Opens a small modal (`TestRoutingWindow`) or inline panel
2. User picks a file (OpenFileDialog, no filter)
3. System evaluates:
   - Is the extension in `outputDirectories`? â†’ show static route
   - Does any `conditionalRoute` match the extension?
     - If yes: check whether `CheckSubdir` exists in `GameDirectory` (if directory is set)
     - Show result: `"â†’ Data\SKSE\Plugins\  (because Data\SKSE\ exists)"` or `"â†’ .  (Data\SKSE\ not found)"`
4. Display result as a simple info panel within the config window (no separate dialog needed â€” just a collapsible result area below the routing section)

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
        return $"â†’  {(dest == "." ? "(game root)" : dest)}  {reason}";
    }

    // Static mapping
    if (_mappings.FirstOrDefault(m => m.Extension.Equals(ext, StringComparison.OrdinalIgnoreCase))
            is MappingRow match)
        return $"â†’  {(match.OutputFolder == "." ? "(game root)" : match.OutputFolder)}  (static rule)";

    return $"â†’  (game root)  (no rule â€” default)";
}
```

### 2.5 Additional QoL (smaller items)

- **Description field** â€” optional free-text below game name; shown as tooltip on launcher card
- **Author/Version fields** â€” only shown when exporting (collapsible "Metadata" section)
- **Recent directories** â€” game directory field remembers last 5 used paths (dropdown arrow)
- **File type quick-add chips** â€” below the file types text box, show clickable chips for common extensions: [.zip] [.rar] [.7z] [.esp] [.dll] [.jar] â€” clicking appends to the field
- **Empty-state routing hint** â€” already implemented; ensure the hint text updates when game name is filled (personalize it: "Mods for *{gameName}* go to the game root by default")
- **"Open game dir" button** â€” small folder icon next to the Game Directory field to open it in Explorer (useful for verifying the path is right)

---

## Deferred Sections

Sections 3â€“10 (Smart DLL wizard, built-in game profiles, mod import, conflict resolution, loadouts UI, etc.) have been moved to [`FUTURE_ADDITIONS.md`](FUTURE_ADDITIONS.md) to keep this document focused on current shipped state.

