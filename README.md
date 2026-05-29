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

## Recently Completed (v0.1-alpha-9)

✅ **`.tmmpack` import** — load a shared pack into any game (zip-slip-safe, collision-handled, loadout rebuilt)  
✅ **Profile search hints** — a shared `.tmmgame` carries default install locations and auto-locates the game on another PC  
✅ **Restored GTA III/VC/SA integrity hashes** — vanilla/downgrader MD5s back in the bundled profiles  
✅ **Unified onboarding** — language + game choice on one screen (was four dialogs)  
✅ **Build restored + audit cleanup** — fixed a broken `master`, removed dead code, centralized the version string  

### Earlier (v0.1-alpha-8 and before)

✅ **Rules freeze at install** + **first-touch baseline** rollback (the two architectural guarantees, now real)  
✅ **Sync / import** from a pre-modded game folder · **Mod groups** (`modloader\Group\Mod\`)  
✅ **Folder-overlay deploy** · **Conflict resolution** (highlight + per-conflict winner override)  
✅ **Mod loadouts** — save/restore/rename/delete, export `.tmmpack`, diff side-by-side  
✅ **Smart DLL wizard** (proxy-DLL detection) · **Favorites** · **Recent activity feed**  
✅ **Backup size monitoring** · **Log rotation + crash-log attach**  

## In Progress

Active work toward v1.0 (see [PLANS.md](PLANS.md) for the model-tagged briefs):

- **Verbose notifications + Notifications tab** — opt-in diagnostic feed, browsable in a dedicated page
- **Whole-program add/edit-game experience** — replace the modal wizard with a full shell tab (✎)
- **In-app FAQ** + softer, informational integrity messaging
- **Smart DLL wizard E2/E3** — proxy-DLL auto-routing hints; multi-proxy version conflicts
- **Import split/merge** — refine detected mods during sync/import

---

## Known Limitations

The honest list. TMM works, but it's pre-1.0 and these gaps are real:

**Deploy / rollback:**
- Backup retention is hard-coded at 3 deploys per game — no UI to change it
- No way to manually trigger a baseline re-capture if something gets out of sync

**Custom games / import:**
- Sync/import can detect, select, exclude, and rename mods, but can't yet **split** one detected candidate into several or **merge** several into one
- Built-in GTA auto-scan still uses hardcoded Steam roots (custom games use portable `searchHints`); folding the built-in path onto search hints is pending
- No "is this profile complete?" validator before saving

**Smart DLL wizard:**
- Proxy DLLs are detected + flagged on install, but not yet auto-routed to game root (E2), and two mods shipping the same proxy DLL isn't called out as a distinct conflict (E3)

**UI / UX:**
- Notifications are transient toasts only — no browsable history or verbose/diagnostic mode yet (both planned)
- Add/edit-game still uses a four-step modal wizard (a full-window experience is planned)
- Some hard-coded English strings remain in advanced wizard fields (incomplete localization coverage)
- Wizard validation events aren't wired on Steps 2–4 (3 intentional CS0067 warnings)
- No undo for individual mod removal — rollback is the only escape

**Other:**
- One-click essentials downloader doesn't gracefully handle network failures mid-download
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
