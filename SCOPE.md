# TMM — Planned Features & Scope

*Last updated 2026-05-23. This is a human-readable overview of planned work. See PLANS.md for detailed technical specs.*

> **Why this file?** PLANS.md is comprehensive but technical. SCOPE.md is for quick reference — what's coming, why it matters, and roughly when.

---

## Currently Shipped

✅ Direct deploy to game directories (no virtual filesystem)  
✅ Automatic backup & rollback (last 3 deploys per game)  
✅ Built-in support for GTA III Series (III, VC, SA, IV, TLaD, TBoGT)  
✅ Custom game support with per-extension file routing  
✅ Multi-game launcher with game cards  
✅ 25 curated theme presets with full customization  
✅ Smart archive extraction (auto-unwrap nested folders)  
✅ `.tmmgame` config export/import (share game profiles)  
✅ Drag-and-drop load order management  
✅ One-click essential downloads (DXVK, SilentPatch, ASI Loader, etc.)  

---

## Next: Core Features (§1–10)

### 1. **Custom Game Profile QoL** (~1–2 weeks)
- Move-up/down arrow buttons on routing rules (no drag yet)
- Conflict highlighting when two rules use same extension
- "Test Routing..." button to preview where files will go
- Quick-add chips for common extensions (`.zip`, `.rar`, `.7z`, `.dll`, etc.)
- Optional description field for game profiles
- "Open Game Directory" button for quick verification

### 2. **Smart DLL Installer** (~1 week)
Automatically detect and sort `.dll` files during install:
- Engine proxies (DXVK, ENB, ReShade) → game root
- Script extender plugins (SKSE, etc.) → configured plugin folder
- DXVK variants (d3d9/d3d11/d3d12) → auto-pick based on game config
- User confirmation dialog for ambiguous DLLs

### 3. **Built-in Game Profiles: Skyrim** (~1 week)
- Pre-configured routing for Skyrim Anniversary Edition
- Handles ESP/ESM data files, SKSE plugins, engine proxies
- Shows in launcher alongside GTA games
- One-click setup (just pick game directory)

### 4. **Deploy Preview Panel** (~3 days)
Before deploying, show a summary:
```
4 files → Data\          (ESP/ESM)
6 files → Data\SKSE\Plugins\ (DLL)
1 file  → game root      (DXVK)
```
Lets user verify routing before committing.

### 5. **Mod Import from Existing Installs** (~1 week)
- Scan game directory for existing mods
- Detect modloader folders, CLEO scripts, plugin directories
- Import them as TMM mods (preserves structure)
- Works per-game (different scan logic for GTA, Skyrim, etc.)
- Shows `[Imported]` badge in mod list

### 6. **Built-in Game Profiles: Minecraft** (~1 week)
- Pre-configured for Minecraft Java Edition
- Handles mods (`.jar`), resource packs, shader packs, datapacks
- World picker for datapack installation
- Shows required Java version per MC version
- Link to Prism Launcher

### 7. **Conflict Resolution Engine** (~2 weeks)
When two mods write the same file:
- Show warnings before deploy
- Store all versions of conflicted files
- Pin specific files to specific mods (override load order)
- Visual conflict resolver window with file comparison
- Browse game files to see what's in play

### 8. **Mod Profiles & Loadouts** (~1 week)
- Save current mod enable/disable state + load order
- Load preset configurations with one click
- Example: "Vortex-like setup" vs "Pure vanilla" vs "Stripped-down performance"

---

## Then: UI & UX (§B-series)

### B1. **Theme System Refinement** (deferred, ~1 week)
- Move theme picker to launcher (always accessible)
- Remove separate ThemeManagerWindow dialog
- Keep 25 curated presets, color picker for overrides
- ~500 lines saved

### B2. **Settings Cleanup** (~3 days)
- Consolidate global settings only
- Remove per-game path setup (belongs in launcher)
- ~60 lines saved

### B3. **Backup System Trim** (~1 day)
- Already done: 5-deep → 3-deep rollback

### B4. **Smart Archive Extraction** (defer to later, ~2 weeks)
- Currently partial in GTA IV dashboard
- Planned for full CustomGameEditor implementation
- README auto-open, known folder detection, user preview

### B5. **Properties Window Expansion** (~1 week)
- Show file count, total size, dates
- Extract mod description from `modinfo.txt`
- "Open in Explorer" context menu

### B6. **UI Modernization: Menu Bar + Responsive** (~2 weeks)
- Optional traditional File/Edit/View menu bar (user choice in Settings)
- Center toolbar with larger buttons (38×38 → 44×44)
- Bigger, clearer fonts (12px → 13–14px)
- Smaller window minimum (1280x672 → 900×500)
- Full theme compatibility testing

### B7. **File Path Settings Consolidation** (~1 week)
- New "Game Paths" tab in Settings window
- Manage all game paths in one place (built-in + custom)
- Path validation with visual indicators
- Auto-complete for common locations (Steam, ModOrganizer2, etc.)
- Inline edit with folder browser

### B8. **Steam Integration for GTA III** (~1 week)
- Re-implement `steam://run/` protocol launching
- Optional SteamAppId configuration per game
- Launch via Steam when available, fallback to direct exe
- Preserves Steam features (achievements, validation) for modded GTA

### B9. **Advanced Custom Game File Detection** (~2 weeks)
Phase 1: Smart file type detection
- Peek into `.dll` files → distinguish proxy DLLs from plugins
- Read TES4/ESM headers → auto-detect game version
- Inspect `.jar` manifests → identify mod loader type

Phase 2: Mod identification across updates
- Store file hashes + metadata for each mod
- Detect when mods update (hash changes, same directory structure)
- Suggest routing rule improvements based on mod contents

Phase 3: Automatic routing suggestions
- After import, analyze mod structure
- Suggest applicable routing rules
- Preview routing before confirming install

---

## Future Roadmap (Post-Core, Unscheduled)

- **Mod profiles refinement:** Save/load load-order presets
- **Steam integration:** Re-implement `steam://run/` protocol launching for custom games
- **Advanced mod detection:** Identify mods even after updates (if directory structure unchanged)
- **Expanded game support:** More built-in profiles (Fallout, Baldur's Gate 3, etc.)
- **Mod store integration:** One-click install from ModDB/Nexus (theoretical)
- **Modularization:** Split GTA III/VC/SA into separate windows, enable plugin architecture

---

## Known Gaps (To Fix)

- Toolbar label toggle only in GTA III dashboard (need in IV/Custom)
- Steam protocol launch not wired in custom game dashboards
- Theme refresh partially broken when changing from non-GTA dashboards
- Deploy button color state (grey/accent/orange) only in GTA III (need in IV/Custom)
- Backup folder context menu missing from custom games

---

## How to Update This File

Whenever PLANS.md is updated, sync this file:
1. Add new sections to match §1–10 structure in PLANS.md
2. Keep descriptions 1–2 sentences per item
3. No implementation details — just "what" and "why"
4. Link back to PLANS.md for technical specs: *"See §3 for details"*
5. Update "last updated" date at top
