# TMM — FAQ

This FAQ answers common end-user questions about TMM features, workflows, and where things live. It's written in plain English and aims to be a helpful companion to the in-app help prompts.

---

## Integrity checks {#integrity}

When TMM shows the blue "ℹ Executable differs" cue, it means the executable you have differs from the one the profile or pack author used when they created the integrity fingerprint. This is informational only — TMM does not block deploys because of a mismatch; it simply flags that the executable differs.

### Why it happens

- Profiles and packs can include an expected file size and/or one or more accepted MD5 hashes (fingerprints of the game executable).
- If your exe's size or MD5 doesn't match what the profile expects, TMM reports that difference.
- This often occurs when you have a different version of the game (e.g., downgrader patch, GOG vs Steam, or a later update).

### What TMM checks

- **Size check (fast):** Compares file size in bytes.
- **MD5 check (slower but stricter):** Computes the hash of the entire executable and compares it against one or more accepted hashes.
- If the profile is configured with a file size, size is always checked first. MD5 runs only if size passes or was not configured.

### Status indicators

- **✓ Integrity verified** (green) — The executable matches the profile's expected fingerprint(s). No action needed.
- **ℹ Executable differs** (blue) — The executable size or MD5 differs from what the profile expects, but mods may still work. This is informational.
- **⚠ Exe missing** (amber) — The executable file was not found at the game path. This is actionable: fix the game directory path.

### What to do

- If you trust the source of the profile/pack, you can proceed — many mods still work across slightly different exe builds or patches.
- If the exe is actually missing, use the game directory setter to point to the correct location.
- If you want to update the profile's expected fingerprint(s), re-import the game with the current executable to capture the new hashes.

---

## .tmmpack files {#tmmpack-files}

.tmmpack is a TMM export format that bundles a loadout (enabled mods and their order) together with the mod source files for easy sharing and redistribution.

### What's inside a pack

A .tmmpack is a ZIP file containing:
- **manifest.json** — Metadata: pack format version, target game, loadout name, TMM version used to create it, creation timestamp, and a list of included mod names.
- **loadout.json** — The loadout record (which mods are enabled and their load order).
- **mods/{ModName}/...** — Extracted mod files organized in folders matching their original structure. TMM metadata (`_tmm` folders) is excluded.

### How import works

- When you import a .tmmpack, its contents are applied to the **currently selected game** in TMM, not necessarily the pack's original target.
- This is intentional: it lets you share a loadout setup across similar games (e.g., export a GTA III setup, import it into VC, and adjust as needed).
- If a name collision occurs (a mod with the same name already exists), TMM will rename the imported mod to avoid overwriting it (e.g., `MyScript` → `MyScript_imported`).
- Each imported mod is frozen with a deployment plan — when you deploy, it uses the plan from import time, not the current routing rules.

### Exporting a pack

- Go to the Loadouts menu, select a loadout, and choose "Export as .tmmpack".
- TMM bundles the selected loadout's enabled mods into a ZIP file that you can share with friends or keep as a backup.

### Common use cases

- **Share a mod setup:** Export a loadout so a friend can import your exact mod configuration and load order.
- **Backup:** Export a working loadout before experimenting with new mods.
- **Quick reset:** Import a saved loadout to restore a known-good state.

---

## Deploy, backup & rollback {#deploy-backup-rollback}

### How deploys work

A deploy applies the mods in your current loadout to the target game directory, copying or overwriting files according to the routing rules defined in the game profile.

**The deploy process:**
1. **Plan preview:** Before deploying, TMM shows a preview of what will be written, which files are new, which would be overwritten, and any conflicts (mods writing to the same destination).
2. **Conflict resolution (if needed):** If multiple mods target the same file, you can choose which one "wins" or exclude individual mods from the deploy.
3. **Baseline capture:** The first time TMM touches a game file, it creates a snapshot of the original (first-touch baseline). This happens automatically before the first deploy.
4. **Backup:** TMM creates a timestamped backup of any game files it's about to overwrite.
5. **Deploy:** Files are copied or overwritten according to the plan.
6. **Manifest:** A record of what was deployed is saved so you can identify and roll back the changes later.

### Backups

- **Automatic:** Before any file is overwritten by a deploy, TMM saves a copy to a timestamped backup folder.
- **Location:** `%APPDATA%\TMM\Backups\{GameKey}\{timestamp}\` — each backup is timestamped so you can pick the correct one.
- **Contents:** Backup manifests list which files were backed up and when, so you can identify which deploy made which changes.
- **Cleanup:** You can manually delete old backups from the Backups page. Backups count toward your total mod storage; if you exceed the configured quota (default 5 GB), the Backups page shows an "Over quota" badge.

### Rollback

- **What it does:** Restores game files to the state TMM first observed them (the first-touch baseline). It does NOT restore to the state before the most recent deploy — it always rolls back to the original/baseline.
- **Before rollback:** TMM prompts you to confirm and shows what files will be restored.
- **Limits:** Rollback only restores files that TMM backed up. Manual edits you made directly to the game folder (outside of TMM) are NOT restored unless TMM had already backed them up.
- **Location:** Baseline snapshots are stored at `%APPDATA%\TMM\Baselines\{GameKey}\`.

### Safe deploys

- TMM emphasizes safety: it never silently deletes files and always shows you a preview before making changes.
- The app won't deploy if the game directory doesn't exist or hasn't been set.

---

## Mod types, routing, and conflicts {#mod-types-routing-conflicts}

### Mod types

A mod type defines a category of mod file (e.g., "ASI plugins", "CLEO scripts") and is used to auto-route those files to the correct game folder.

**Built-in mod types include:**
- ASI plugin (.asi)
- CLEO script (.cs, .cs4, .cs5, .fxt)
- Script (game-engine-specific)
- Configuration (.ini, .cfg)

You can add custom types when creating a game profile, and define where each type should be installed.

### Routing rules

A routing rule is a pattern that decides where a mod file ends up during deploy.

**Example:** "All .asi files → `scripts\` folder" or "All CLEO scripts matching `.cs*` → `cleo\` folder if it exists, otherwise `scripts\`."

Each rule has:
- **Pattern:** File extension, wildcard, or condition (e.g., `.asi`, `*.fxt`, or "if containing directory name is `bin`").
- **Destination:** Where the file is copied to (e.g., `scripts\`, `game root\`, or a relative path).
- **Conditions (optional):** "Only if this folder exists", "skip this file", etc.
- **Priority:** Higher-priority rules are evaluated first, so you can chain them (e.g., "Is it a proxy DLL? No? Check the next rule.").

### Conflicts

A conflict occurs when two or more enabled mods try to write to the same destination file.

**How conflicts are detected:**
- During the deploy preview, TMM scans all enabled mods' planned file writes and flags any destination that would be written by multiple mods.

**Resolving conflicts:**
- The Conflict Resolver window lets you pick a winner for each conflict (the mod whose version gets deployed).
- Non-winners are automatically skipped in the deploy plan.
- Load order matters: higher-load-order mods are suggested as the default winner, but you can override.

### Proxy DLLs

TMM detects known proxy DLLs — system DLL names that mod loaders (like Script Hook, SKSE, F4SE) masquerade as to inject code at startup.

**Known proxy DLLs include:** dinput8.dll, d3d9.dll, d3d11.dll, dsound.dll, scripthookv.dll, skse64_loader.exe, and ~15 others.

**Why it matters:** Proxy DLLs must sit beside the game executable, not in `plugins/` or `scripts/` folders. TMM flags them during install so you can confirm they're routed to the game root.

---

## Custom games & search hints {#custom-games-search-hints}

### Custom-game wizard

The "Add / Edit Game" wizard (or ✎ pencil button on a game card to edit) guides you through four steps:

1. **Essentials:** Game name, installation folder, executable name/path, Steam/Nexus IDs (optional), integrity settings.
2. **Mod Types:** Define the mod categories your game uses (ASI plugins, CLEO scripts, etc.).
3. **Routing Rules:** Set up file-routing patterns so mods install to the right folders.
4. **Advanced:** Optional settings like overlays, companion features, and hints for finding the game on other PCs.
5. **Review:** A summary of your profile before you create it.

**Key principle:** Anything configurable in a built-in game must be configurable here. You do not need to edit .tmmgame JSON files by hand.

### Search hints

Search hints are optional folder patterns that help TMM auto-locate games on your disk during a scan.

**Use case:** If you installed a game to `D:\Games\GTA III` instead of the default location, a search hint `games\gta-iii` (relative to any drive root) helps TMM find it automatically.

**How they work:**
- When you run a game scan (or QuickScan), TMM probes the hints across every fixed drive.
- If a hint matches and the configured executable is found there, TMM suggests it as a candidate.
- You can accept the suggestion, edit the path, or ignore it.

**Editing hints:**
- In the wizard, go to Step 1 (Essentials) and expand the search hints field.
- Add folder patterns relative to a drive root (e.g., `Games\GTA III` or `Program Files\Bethesda Softworks\Skyrim`).

---

## Loadouts {#loadouts}

A loadout is a named snapshot of your mod configuration: which mods are enabled, which are disabled, and their load order.

### Creating a loadout

- In the Mod Manager, arrange your mods and enable the ones you want.
- Click the Loadouts button and select "Save loadout".
- Name it (e.g., "Vanilla", "High Graphics", "Chaos Mode").
- The loadout is saved and can be applied anytime.

### Applying a loadout

- Click Loadouts and select a saved loadout.
- All mods are immediately updated to match the loadout state (enabled/disabled, reordered).
- Deploy to apply the changes to your game.

### Comparing loadouts

- Right-click a loadout and select "Compare", or go to the loadout panel and use the "Compare" button.
- The Loadout Diff window shows side-by-side what's different: mods added, removed, enabled/disabled, or reordered.

### Exporting and importing

- **Export:** Select a loadout and choose "Export as .tmmpack". Mods and loadout are bundled into a shareable ZIP.
- **Import:** Go to Loadouts menu and select "Import .tmmpack". The pack's mods are added to the game and the loadout is reconstructed.

### Managing loadouts

- **Rename:** Right-click a loadout and select "Rename".
- **Delete:** Right-click a loadout and select "Delete" (with confirmation).
- **Overwrite:** Save a new loadout with an existing name and confirm the overwrite.

---

## Mod favorites and organization {#mod-favorites}

### Starring (favoriting) mods

- Each mod row has a star icon (☆) next to its name.
- Click to star a mod (★). Starred mods appear at the top of the list, regardless of sort order.
- This is useful for quickly accessing frequently-toggled mods or keeping important mods visible during reorganization.

### Finding mods

- Use the search bar at the top of the mod list to filter mods by name.
- Sort by name, load order, or favorites.

### Mod properties

- Right-click a mod and select "Properties" to see its deployment plan, detected type, and folder location.
- This helps troubleshoot routing issues or understand why a mod ended up in a particular folder.

---

## Importing mods from an existing game folder {#mod-import}

If you have a game folder that already contains mods (loose files or mixed with the game), TMM can scan and import them.

### How import works

- Go to the Library, select a game, and look for the "Import mods" button (or similar in the context menu).
- TMM scans the game folder for recognizable mod files (ASI plugins, CLEO scripts, INIs, etc.).
- It presents candidates grouped by likely mod name.
- You can select which candidates to import; TMM moves them into its managed folder structure and freezes a deployment plan for each.
- When you deploy, the mods are placed back into the game folder according to their plans.

### What import detects

- Loose .asi, .dll, .cs, .cs4, .cs5, .fxt, and .ini files.
- Subfolders that contain recognizable mod files.
- Files are grouped heuristically (e.g., a `scripts` folder containing multiple .cs files → one "scripts" mod candidate).

### Collision handling

- If an imported mod's name collides with an existing one, TMM renames it (e.g., `MyScript` → `MyScript_imported`).
- You can rename it further in the UI if desired.

---

## Activity feed and recent actions {#activity-feed}

The Activity Feed records your recent actions (deploys, rollbacks, imports, loadout operations) for reference and troubleshooting.

- Access it via the Backups page or the Activity button in the toolbar.
- The feed shows the 20 most recent actions, newest first, with timestamps and operation details.
- Use it to remember when you made a change or to verify that a deploy actually completed.

---

## Where TMM keeps files {#file-locations}

All TMM data is stored under `%APPDATA%\TMM\` on Windows. Here's the breakdown:

| Location | Contents |
|----------|----------|
| `settings.json` | App settings (language, paths, window size, recent activity, etc.) |
| `ModsRaw_{GameKey}/` | Installed mods for a game (one subfolder per mod). Each mod has a `_tmm/deployplan.json` frozen at install time. |
| `Backups/{GameKey}/{timestamp}/` | Timestamped backups of files overwritten during deploys. Each includes `manifest.json` listing what was backed up. |
| `Baselines/{GameKey}/baseline.json` | First-touch baseline manifest for a game (captures original file state). |
| `Baselines/{GameKey}/snapshots/` | Per-mod baseline snapshots (used during rollback to determine what to restore). |
| `Loadouts_{GameKey}/` | Saved loadouts (one .json file per loadout). |
| `CustomGames/{GameKey}.json` | Profile definition for a custom game (if you added it). |
| `TMM.log` | Rolling application log (rotates at 5 MB; keeps up to 3 backups). |

### Tip

- Use the Settings page or the app UI to change paths and manage files rather than editing files directly.
- Manually deleting or moving files in `%APPDATA%\TMM\` can break mod installations or backups. Always use the UI.

---

## Supported games

**Built-in:** GTA III, GTA: Vice City, GTA: San Andreas, GTA IV, GTA: Chinatown Wars (IV Episodes: TLAD, TBOGT), Skyrim Anniversary Edition, Fallout: New Vegas, Cyberpunk 2077, Red Dead Redemption 2, The Witcher 3.

**Custom:** You can create and manage custom game profiles using the Add Game wizard. TMM will manage mods for any game you set up.

---

## Performance and storage

### Mod storage

- Mods are stored under `%APPDATA%\TMM\ModsRaw_{GameKey}/` in their extracted form.
- Each mod's folder contains the original files plus TMM metadata.

### Backups and quota

- Backups are timestamped and stored under `%APPDATA%\TMM\Backups/`.
- If total backup size exceeds the configured quota (default 5 GB), the Backups page displays an "Over quota" badge.
- You can delete old backups manually to free space; TMM does not auto-prune.

### Log file management

- TMM maintains a rotating log file (`TMM.log`) that caps at 5 MB.
- When the limit is reached, the current log is renamed and a new one starts.
- TMM keeps the 3 most recent log files for troubleshooting.

---

## Troubleshooting

### Game directory not found

- Ensure the path is correct and points to the folder containing the game's main executable.
- Use the folder browser button (if available) to navigate to the game folder.

### Mods not deploying

- Check that at least one mod is enabled.
- Review the deploy preview to see if any routing rules are missing or incorrect.
- Verify that the game folder exists and is writable by your user account.

### Integrity check shows "Exe differs"

- This is usually safe to ignore if you trust the mod setup. Many mods work across different exe versions.
- If you want to update the profile's expected fingerprint, re-import the game with the current executable.

### Backup or rollback failed

- Ensure your game folder is writable and has sufficient free space.
- Check the TMM.log file (under `%APPDATA%\TMM\`) for detailed error messages.

---

If you want to update or expand this FAQ, please let me know which sections need clarification or additional detail.
