# TMM — Triviali's Mod Manager

A lightweight mod manager for **any game**. Direct-deploy architecture — no virtual filesystem, no staging folders, no manifests to maintain. Routing rules freeze at install; deploys just execute.

**Built-in profiles for:** GTA III, Vice City, San Andreas, IV, The Lost and Damned, The Ballad of Gay Tony.  
**Add any other game** through the custom game wizard.

---

## ⚠️ Active Development

TMM is under active heavy development. Architecture is still in flux — expect breaking changes between builds. External PRs are not accepted yet (wait for v1.0 stabilization). Bug reports with logs are always welcome.

---

## Quick Start

1. Grab the latest release zip, extract anywhere.
2. Run `TMM.exe`. First launch creates `%APPDATA%\TMM\`.
3. Pick or add a game. Built-in GTA profiles appear automatically; click **Add Game** for anything else.
4. Set the game directory.
5. Drag-drop mod archives (`.zip` / `.rar` / `.7z`) onto the mod list. Reorder them — bottom overrides top.
6. Hit **Deploy**. Files land in the game folder per your routing rules.
7. If something breaks, hit **Rollback**.

---

## Core Concepts

### Direct Deploy

Mods get copied directly into the game directory — no virtual filesystem, no symlinks. What you see in the game folder is exactly what's running. Modded files persist outside TMM; the game doesn't need TMM running to launch.

### Backup & Rollback

Every deploy snapshots files it's about to overwrite. The last **3 deploys per game** are retained; older snapshots are pruned automatically. The **Backups** page lets you restore any retained state.

### Routing Rules

When you add a game, you define where its mods land. Rules map file patterns to destination folders:

- `.asi` files → `scripts\`
- `.dll` files → `plugins\` if that folder exists, else game root
- Everything else → game root

Rules are written with a sentence builder — no JSON editing. One-click presets exist for common mod frameworks (ASI Loader, CLEO, SKSE, etc.).

### Rules Freeze at Install

When you add a mod, TMM evaluates your routing rules **once** and saves the result as a deployment plan. Subsequent deploys run that saved plan verbatim — they never re-evaluate rules. Changing rules later surfaces a "replan affected mods?" prompt; existing mods are never silently re-routed.

### First-Touch Baseline

The first time TMM touches a game file, it records the original bytes. Rollback restores to *that* state — not the previous deploy. Stacking multiple deploys never loses the vanilla baseline.

---

## Features

### Mod Management

- Drag-and-drop archives — nested archives auto-unwrap
- Per-game search with live filtering and chip tabs (All / Enabled / Conflicts / Favorites)
- Drag-to-reorder load order (bottom wins)
- Per-mod color spine and conflict badge — click a badge to expand clash detail inline
- Bulk enable / disable / move / remove on multi-select
- Favorites (star/pin per mod, persisted)
- Right-click context menu: open mod folder, game folder, backup folder

### Game Library

Two views:

- **Home** — continue card for the active game (Play / Manage, pending-redeploy badge), quick-stats strip (games configured / mods installed / backup usage), full game list below, and recent activity feed
- **List** — compact rows with mod counts, drag-to-reorder games

### Add / Edit Game

A dedicated full-page experience — not a modal. Sections (Essentials, Mod Types, Routing, Review) stack in a scrollable view with a left jump-rail and completion dots. A live summary bar shows routing coverage as you configure. Edit mode pre-fills all fields from the saved profile.

### Custom Game Profiles

Each profile holds:

- Display name, executable path, Steam App ID (optional)
- Per-extension routing rules with conditional checks (e.g. route `.dll` to `plugins\` only if that folder exists)
- Companion sibling folders (e.g. `CLEO_TEXT\`, `CLEO_FONTS\`)
- Optional integrity verification — expected exe byte size + accepted MD5 list, with auto-detect
- Cover art
- Search hints — default install locations that travel inside a `.tmmgame` file, so a shared profile self-locates on another machine

Profiles export to portable `.tmmgame` files. Users never need to touch JSON directly.

### Loadouts

Save named snapshots of your mod enable-state and load order. Switch between loadouts with one click. Compare two loadouts side-by-side in the diff viewer (additions, removals, enable changes, reorders).

Export a loadout as a `.tmmpack` — mod sources and loadout definition bundled into one archive for sharing. Import a `.tmmpack` into any compatible game.

### Conflict Manager

The Conflicts tab surfaces every file that two or more mods want to deploy to the same path. Per-conflict winner override lets you pick which mod wins without changing global load order. Proxy DLL conflicts (two mods shipping the same proxy filename) are caught separately.

### Notifications

Persistent notifications page (bell icon, nav rail). All operations record to a browsable history — newest-first, capped at 500 entries with the last 200 surviving restarts. Level filter: All / Info / Success / Warning / Error.

**Verbose mode** (Settings toggle) emits diagnostic toasts at backend operations in real time; off by default.

### Visual Customization

- Fixed dark theme (Windows 11 color palette)
- 8 two-tone accent presets — or enter any two hex values directly
- Gradient accent applied to window border, library cards, and UI highlights

### Diagnostics

- **Test Routing panel** — pick a file, see exactly where it would deploy before committing anything
- **Integrity status** — colored dot in the sidebar (OK / size mismatch / hash mismatch / missing) when configured
- Crash dialog with copy-to-clipboard stack trace
- App log at `%APPDATA%\TMM\TMM.log`

---

## File Locations

```
%APPDATA%\TMM\
├── settings.json              ← themes, game paths, view prefs
├── notifications.json         ← persistent notification history
├── ModsRaw_{gameKey}\         ← per-game mod sources
│   └── {ModName}\
│       └── _tmm\
│           ├── deployplan.json  ← frozen plan from install time
│           └── modinfo.json     ← category, group, etc.
├── Backups\{gameKey}\         ← rollback snapshots (last 3)
├── Baselines\{gameKey}\       ← first-touch vanilla baseline
├── Loadouts_{gameKey}\        ← saved loadout files
├── CustomGames\               ← user-added .tmmgame profiles
└── Themes\                    ← exported .mmtheme files
```

Built-in game keys: `III` `VC` `SA` `IV` `TLAD` `TBOGT`. Custom games get keys like `CUSTOM_abc123`.

**Migration note:** older versions stored data at `%APPDATA%\TGTAMM\`. TMM migrates this automatically on first launch.

---

## System Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) — bundled with official releases
- 2 GB RAM
- 1 GB free disk space, plus headroom for backups (a heavily modded GTA SA baseline can hit several GB)

---

## Known Limitations

**Deploy / rollback:**
- Backup retention is hard-coded at 3 deploys per game — no UI to change it
- No way to manually trigger a baseline re-capture if something gets out of sync

**UI / UX:**
- Some hard-coded English strings remain in advanced wizard fields (incomplete localization)
- No undo for individual mod removal — rollback is the only escape

**Other:**
- One-click essentials downloader doesn't gracefully handle network failures mid-download
- No auto-update check or mod version-pinning

---

## What's New

See [CHANGELOG.md](CHANGELOG.md) for full version history.

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
