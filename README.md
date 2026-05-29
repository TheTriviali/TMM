# TMM — Triviali's Mod Manager

A lightweight mod manager for **any game**. Direct-deploy architecture — no virtual filesystem, no manifests to maintain, no staging folders. Just rules, deployment, and one-click rollback.

**Ships with built-in profiles for:** GTA III, Vice City, San Andreas, IV, The Lost and Damned, The Ballad of Gay Tony.
**Add any other game** through the Custom Game wizard.

---

## ⚠️ Active Development

TMM is under active heavy development by its creator. Architecture is still in flux — expect breaking changes between builds. **External PRs are not accepted yet** (wait for v1.0 stabilization). Bug reports with logs are always welcome.

---

## Quick Start

1. Grab the latest release zip, extract anywhere.
2. Run `TMM.exe`. First launch creates `%APPDATA%\TMM\`.
3. Pick or add a game. Built-in GTA profiles appear automatically; click **Add Custom Game** for anything else.
4. Set the game directory using the 📂 button in the sidebar.
5. Drag-drop mod archives (`.zip` / `.rar` / `.7z`) into the mod list. Reorder them — bottom overrides top.
6. Hit **Deploy**. Files land in the game folder per your routing rules.
7. If something breaks, hit **Rollback**.

---

## Core Concepts

### Direct Deploy

Mods get copied directly into the game's directory — no virtual filesystem, no symlinks, no overlay redirection. What you see in the game folder is what's running. This means modded files persist outside of TMM (the game doesn't need TMM running to launch), and any external tool can inspect the result.

### Backup & Rollback

Every deploy snapshots the files it's about to overwrite. The last **3 deploys per game** are retained — older snapshots get pruned automatically. One click in the **Backups** page restores any retained state.

### Routing Rules (the heart of custom games)

When you add a game, you teach TMM where its mods belong. Rules map file patterns to destination folders:

- `.asi` files → `scripts\`
- `.dll` files → `plugins\` if `plugins\` exists, else game root
- Anything else → game root

Rules are built in plain English via a sentence builder — no JSON editing required. One-click presets exist for common engines (ASI Loader, CLEO, SKSE, etc.).

### Rules Freeze at Install

When you add a mod, TMM evaluates the routing rules **once** and saves the resulting deployment plan. Subsequent deploys execute that saved plan verbatim — they don't re-evaluate rules. This makes deploys predictable: editing routing rules later won't silently change what existing mods do. (Editing rules surfaces a "replan affected mods?" prompt instead.)

### First-Touch Baseline

The first time TMM touches a file in your game directory, it remembers the original bytes. Rollback restores to *that* state — not to the previous deploy. So stacking multiple mods doesn't lose the vanilla baseline.

---

## Features

### Mod Management

- Drag-and-drop archives — nested archives auto-unwrap
- Per-game search with live filtering
- Drag-to-reorder load order (bottom wins)
- Per-mod enable/disable
- Right-click context menu: open mod folder / game folder / backup folder

### Game Library

Three view modes:
- **Grid** — card layout with action buttons always visible
- **List** — compact rows, drag-to-reorder, mod count at a glance
- **Showcase** — hero card for your default game + horizontal carousel below

Library extras:
- Set a default game (or clear it by clicking again)
- Archive games you're not using without deleting their data
- Filter / search by name
- Reorder games via drag (persists across sessions)

### Custom Game Profiles

Each profile holds:
- Display name, executable path, Steam App ID (optional)
- Per-extension routing rules + conditional rules (folder-existence checks)
- Companion sibling folders (e.g. `CLEO_TEXT/`, `CLEO_FONTS/` for CLEO scripts)
- Optional **integrity verification** — expected exe byte size + accepted MD5 list, with an auto-detect button to capture the current binary in one click
- Cover art

Profiles export to portable `.tmmgame` files. Share them; users never need to touch JSON directly.

### Visual Customization

- 25 built-in theme presets — Dracula, Nord, Gruvbox, Catppuccin, Synthwave, Vice City Neon, GTA Online, plus light/dark variants
- HSV 2D color picker for custom accent + background colors
- Windows 11 Mica/Acrylic backdrop support
- 8 font choices (Bahnschrift, Segoe UI variants, Consolas, etc.)
- Random theme button (the dice 🎲)

### Diagnostics

- Test Routing panel — pick a file and see exactly where it would deploy before committing
- Per-game integrity status (when configured): colored dot in the sidebar shows OK / size mismatch / hash mismatch / file missing
- Crash dialog with copy-to-clipboard stack trace
- App log at `%APPDATA%\TMM\TMM.log`

---

## File Locations

```
%APPDATA%\TMM\
├── settings.json              ← themes, game paths, view modes
├── ModsRaw_{gameKey}\         ← per-game mod sources
│   └── {ModName}\
│       └── _tmm\
│           ├── deployplan.json  ← frozen plan from install time
│           └── modinfo.json     ← group membership, etc.
├── Backups\{gameKey}\         ← rollback snapshots (last 3)
├── Baselines\{gameKey}\       ← first-touch vanilla baseline
├── CustomGames\               ← user-added .tmmgame profiles
└── Themes\                    ← exported .mmtheme files
```

Built-in game keys: `III` `VC` `SA` `IV` `TLAD` `TBOGT`. Custom games get auto-generated keys like `CUSTOM_abc123`.

**Migration note:** older versions stored data at `%APPDATA%\TGTAMM\`. TMM migrates this automatically on first launch.

---

## System Requirements

- Windows 10 or 11 (Windows 7+ may work; not tested)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — bundled with official releases
- 2 GB RAM
- 1 GB free disk space, plus headroom for backups (a heavily modded GTA SA baseline can hit several GB)

---

## Recently Completed (v0.1-alpha-8)

✅ **Sync / import** — point TMM at a pre-modded game folder, auto-detect existing mods  
✅ **Mod groups** — collapsible nested deployment (`modloader\GroupName\ModName\`)  
✅ **Conflict resolution** — visual highlighting + per-conflict winner-override UI  
✅ **Mod loadouts** — save, restore, rename, delete, export as `.tmmpack`, and diff side-by-side  
✅ **Smart DLL wizard** — detects proxy DLLs (dinput8, d3d9, ScriptHook, SKSE, etc.) on install  
✅ **Mod favorites** — star/pin key mods  
✅ **Recent activity feed** — last 20 actions surfaced from the Backups page  
✅ **Backup size monitoring** — quota badge when backups exceed configured threshold  
✅ **Log rotation + crash log attach** — 5 MB cap with 3 rotations; recent log lines attached to crash reports  

## In Progress

Active work toward v1.0:

- **Folder-overlay deploy** — mods shipping with `models/`, `data/`, `audio/` folders that mirror game structure (currently routed by extension only)
- **Smart DLL wizard E2/E3** — auto-routing hints from proxy detections; multi-proxy version conflicts
- **First-launch flow polish** — collapse the four-dialog flow into one guided panel

See [PLANS.md](PLANS.md) for the full roadmap.

---

## Known Limitations

The honest list. TMM works, but it's pre-1.0 and these gaps are real:

**Architecture (in flight):**
- Routing rules re-evaluate on every deploy (the "freeze at install" guarantee in the docs above is the goal, not the current behavior — coming in B2)
- Rollback uses per-deploy snapshots, not a true vanilla baseline. Stacking mods can lose the original files (B3 will fix this)
- Mods with `models/`, `data/`, `audio/` folders that mirror game structure get routed by extension only, not merged as overlays (B4)
- No way to import a pre-existing modded install — TMM has to start clean today (B5)
- No mod groups / collapsible nesting yet (B6)

**Deploy / rollback:**
- No conflict detection — two mods writing the same destination file silently overwrites
- Backup retention is hard-coded at 3 deploys, no UI to change it
- Backups can balloon for large games (heavily modded SA: multi-GB) with no warning or quota
- Empty mod-side directories are skipped on deploy
- Symlinks in mod sources are not supported; behavior is unpredictable if encountered
- No way to manually trigger a baseline re-capture if something gets out of sync

**UI / UX:**
- First-launch flow is four dialogs deep for one decision (language → game picker → built-in vs custom → setup)
- Steam protocol launch not wired for custom game dashboards
- "Find Mods" sidebar buttons currently point at generic Nexus/ModDB homepages (no per-game deep links)
- Drag-drop into IV/TLaD/TBoGT shared folder structure has rough edges
- Theme picker refresh from non-GTA dashboards is flaky
- "Open Mods Store" context menu item is a stub (no implementation)
- Theme manager window doesn't always refresh after a theme change
- Some hard-coded English strings remain in XAML (incomplete localization coverage)
- Wizard validation events aren't wired on Steps 2–4 (3 known CS0067 warnings)
- No undo for individual mod removal — full rollback is the only escape

**Custom games:**
- Auto-scan (`QuickScan`) hardcodes GTA-specific paths; doesn't search custom games yet
- Per-game search hints in `.tmmgame` profiles not implemented
- No way to validate a `.tmmgame` profile is complete before saving it

**Other:**
- No log rotation — `TMM.log` grows unbounded
- One-click essentials downloader doesn't gracefully handle network failures mid-download
- Crash handler exists but doesn't auto-attach the log to the dialog
- No telemetry, no auto-update check, no version-pinning of mods

---

## Reporting Bugs

Open a GitHub Issue with:
- Steps to reproduce
- Which game(s) affected
- Screenshot if relevant
- Contents of `%APPDATA%\TMM\TMM.log`

---

## License

Open source. See [LICENSE](LICENSE).
