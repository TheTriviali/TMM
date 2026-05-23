# TMM — Future Additions & Advanced Features

**Status:** Conceptual planning. These features are deferred pending core feature stabilization and user feedback.

---

## Overview

This file contains feature concepts and architectural ideas that may be implemented in future releases. These are:
- **Built-in game profiles** (Skyrim AE, Minecraft) with installers and specialized routing
- **Smart DLL wizard** for automatic engine proxy detection  
- **Advanced mod import** from existing game installations
- **Conflict resolution engine** with per-file pinning
- **UI modernization** (menu bar, responsive layout)

None of these are currently active development. They remain fully specified for future implementation.

---

## 3 — Smart DLL Installer Wizard 🔵 FUTURE

Fires automatically when a mod with `.dll` files is installed and the game profile has DLL support enabled.

### 3.1 Trigger conditions

- Archive contains `.dll` files at root or in single subdirectory
- Game profile has `installerHints.smartDllWizard = true`

### 3.2 Wizard decision tree

```
Archive arrives containing .dll(s)
│
├─ Any .dll filename matches engineProxyNames?
│   │
│   ├─ YES → Engine proxy route
│   │   ├─ Multiple variants present (d3d9 + d3d11 + d3d12)?
│   │   │   └─ Auto-select based on dxVersionTarget
│   │   └─ Route to game root (.)
│   │
│   └─ NO → Check directory structure
│       ├─ In SKSE\Plugins\ subfolder? → Route to Data\SKSE\Plugins\
│       ├─ In plugins\ subfolder? → Route accordingly
│       └─ Loose at root? → Show SmartDllDialog
```

### 3.3 SmartDllDialog UI

Simple modal asking user to classify the DLL:
- Script Extender Plugin (SKSE)
- Engine Proxy / Injector (DXVK, ENB, ReShade)
- Manual selection

### 3.4 Known engine proxies

```csharp
d3d8.dll, d3d9.dll, d3d10.dll, d3d11.dll, d3d12.dll,
dxgi.dll, dinput8.dll, dsound.dll, winmm.dll,
version.dll, binkw32.dll
```

---

## 4 — Built-in Game Profiles

### 4.1 Skyrim Anniversary Edition

**Status:** Profile file created (`skyrim_ae.tmmgame`), embedded in build, registry loading implemented, launcher section added.

**Implementation:** First-use expansion via inline card UI; game directory set before dashboard opens.

```json
{
  "gameName": "Skyrim Anniversary Edition",
  "exePath": "SkyrimSE.exe",
  "steamAppId": "489830",
  "modFileTypes": ".zip, .rar, .7z, .esp, .esm, .esl, .dll, .bsa, .ba2, .ini, .json, .pex, .psc",
  "outputDirectories": {
    ".esp": "Data", ".esm": "Data", ".esl": "Data",
    ".bsa": "Data", ".ba2": "Data",
    ".pex": "Data\\Scripts", ".psc": "Data\\Scripts\\Source"
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
  }
}
```

### 4.2 Minecraft Java Edition

**Status:** Conceptual. Profile structure sketched, world picker dialog planned.

**Key features:**
- Version selector (MC 1.16 → 1.21+) with Java version mapping
- Datapack installer with world picker
- Shader pack auto-detection (archive sniffing)
- Mod loader detection (Fabric/Forge warning)

```json
{
  "gameName": "Minecraft Java Edition",
  "exePath": null,
  "modFileTypes": ".zip, .jar, .json",
  "outputDirectories": {
    ".jar": "mods",
    ".zip": "resourcepacks"
  }
}
```

### 4.3 Other Potential Games

- **Baldur's Gate 3** — simple .pak routing, no conditional logic
- **Starfield** — .ba2 packaging, SFSE plugin routing
- **Oblivion** — similar to Skyrim, older plugin format
- **Morrowind** — ancient plugin system, archaic file handling

---

## 5 — Mod Import from Existing Installations

**Status:** Conceptual. Scan strategies defined per game.

### 5.1 Scan strategies

**GTA San Andreas:**
```
{gameDir}\modloader\           → each subfolder = one mod
{gameDir}\CLEO\                → each .cs/.cleo = one mod
{gameDir}\scripts\             → each .asi = one mod
{gameDir}\moonloader\scripts\  → each .lua = one mod
```

**GTA III / Vice City:**
```
{gameDir}\scripts\  → each .cs (CLEO)
{gameDir}\CLEO\     → same
Root *.asi files    → each
```

**Custom games (generic reverse-routing):**
For each configured `OutputDirectory` (e.g. `.dll → Data\SKSE\Plugins`):
- Scan `{gameDir}\{outputFolder}\` for matching extensions
- Group by logical name or parent subfolder

### 5.2 Import flow

1. User clicks "Import from Game" toolbar button
2. `ModImportWindow` shows scan progress + detected mods
3. User selects which mods to import (all by default)
4. Files copied to TMM storage as new mods
5. Imported mods get `[Imported]` badge in UI
6. Toast: "Imported 14 mods. Review load order, then deploy."

---

## 6 — Conflict Resolution Engine

**Status:** Conceptual. Data model, deploy pipeline changes, and UI framework all sketched.

### 6.1 Core concept

- **Per-file versioning:** Each file modified by any mod is backed up
- **Ownership registry:** Tracks which mods claim which files
- **Pinning:** Users can force a specific mod's version to always win, overriding load order
- **No virtual filesystem:** Still direct deploy; conflict resolution is metadata on top

### 6.2 Storage

```
AppData\TMM\FileVersions\
  {gameKey}\
    {sanitized_path}\
      _original.bak              ← game's original file
      ModA-v1.2.bin
      ModB-v3.1.bin
      ...

AppData\TMM\{gameKey}_fileregistry.json
{
  "Data\\SKSE\\Plugins\\SkyUI.dll": {
    "owner": "SkyUI",
    "pinnedTo": null,
    "claimedBy": ["SkyUI", "SkyUI_SE"],
    "originalCaptured": true
  }
}
```

### 6.3 UI: Conflict Resolver Window

Two-panel interface:
- **Left:** List of conflicted files grouped by directory
- **Right:** Version history for selected file with Preview + Pin controls
- Load order winner shown; can be overridden via dropdown

### 6.4 UI: File Explorer

Directory tree + file list with dot indicators:
- 🔴 Conflict (multiple mods)
- 🟡 One mod owns it
- 🟢 Original/clean (no mod touches it)

---

## 7 — Mod Profiles / Loadouts

**Status:** Data model and BackendCore methods implemented. Dashboard UI integration pending.

**Spec:** Named configurations saving enable state + load order per game.

### 7.1 Storage

```
AppData\TMM\{gameKey}\loadouts\{name}.json
{
  "name": "Full Build",
  "mods": [
    { "modName": "SkyUI", "isEnabled": true, "loadOrder": 0 },
    { "modName": "SKSE", "isEnabled": true, "loadOrder": 1 },
    ...
  ]
}
```

### 7.2 UI: Loadout Dropdown

Toolbar ComboBox (right side, before disk space):
- List of saved loadout names (Default first)
- Separator
- "Save Current..." + "Delete..." options
- Clicking item swaps to that loadout, marks changes pending

---

## 8 — UI Modernization

**Status:** Deferred pending core feature completion.

### 8.1 Traditional Menu Bar

Opt-in toggle in Settings:
- File: Install Mod, Export Config, Exit
- Edit: Settings, Preferences
- View: Toggle Toolbar/Menu, Labels, Theme, Help
- Logo + version in title area

### 8.2 Responsive Layout

- Min window: 900×500 (currently 1280×672)
- Button sizes: 38×38 → 44×44
- Font sizes: 12px → 13–14px
- Grid gaps: 12px → 8px

### 8.3 Compatibility

- All themes updated to support both layouts
- Menu bar inherits titlebar theme colors
- DPI scaling preserved

---

## 9 — Steam Integration (GTA III)

**Status:** Field exists (`SteamAppId`), not yet wired to launcher.

**Feature:** Play button launches via `steam://run/{id}` if game is on Steam and unmodified.

---

## 10 — Advanced File Detection

**Status:** Conceptual only.

### 10.1 Phase 1: Smart type detection

- `.dll` import tables → proxy vs plugin
- `.esp`/`.esm` headers → Skyrim vs Oblivion
- `.jar` manifests → Fabric vs Forge

### 10.2 Phase 2: Version tracking

Store file hash + metadata per mod folder.  
On update, detect "updated" vs "new" mods.

### 10.3 Phase 3: Auto-routing suggestions

After import, analyze contents and suggest routing rules.

---

## 11 — Backlog: Post-Core Features

### B1 — Theme System Refinement
Curated preset list (25 instead of 69), color picker, theme selector in launcher.

### B2 — Settings: Remove Per-Game Context
Consolidate Settings → only global options.

### B3 — Backup: 3-Deep Rollback
Keep last 3 deploys instead of 5 (storage optimization).

### B4 — Smart Archive Extraction
Document algorithm; defer full implementation to CustomGameEditor.

### B5 — Properties Window Expansion
Show file count, size, creation date, last modified date.

### B6 — File Path Settings Tab
Unified "Game Paths" interface in SettingsWindow; auto-detect + recent paths.

---

## Reference: Previously Implemented ✅

These were completed in prior sessions and are now live:

- ✅ `.tmmgame` import/export format + serialization
- ✅ CustomGameConfigWindow refinements (drag-to-reorder, conflict highlight, metadata fields)
- ✅ Test routing dry-run panel (inline, simulates conditional routes)
- ✅ Deploy preview window (shows file summary before deploy)
- ✅ Mod loadouts data model + BackendCore persistence
- ✅ Skyrim AE profile embedding + launcher section
- ✅ GameRegistry built-in profile loading
- ✅ Centralized UI colors, styles, JSON helpers

---

*For current work, see PLANS.md. For shipped features, see SCOPE.md.*
