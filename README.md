# TMM - Triviali's Mod Manager

A lightweight, cross-game mod manager for classic and modern games. Built for simplicity — no scripts, no manifests, no intermediate virtual filesystems. Just a clean GUI with direct-deploy mod installation, automatic backups, and instant rollback.

**Currently ships with:** GTA III Series (III, Vice City, San Andreas, IV, TLaD, TBoGT) + Skyrim Anniversary Edition  
**Extensible to:** Any game via custom game profiles with per-filetype output directory routing

## Alpha Notice

TMM is currently in **Alpha**. Expect bugs, unpolished edge cases, and missing features. Feedback, issue reports, and pull requests are welcome!

---

## Quick Start

1. **Download & Extract:** Unzip to any folder
2. **First Launch:** App creates `%APPDATA%\TMM\` folder structure automatically
3. **Add Games:**
   - Built-in GTA III Series games appear in launcher on first run
   - Add custom games via "Add Custom Game" button
4. **Install Mods:** Drag-drop `.zip`/`.rar`/`.7z` files into mod list, arrange load order, click Deploy
5. **Play:** Use in-app Play buttons or launch manually from game directory

---

## How It Works

### Direct Deploy Architecture

Unlike traditional mod managers that layer mods into a virtual staging folder, TMM deploys mods **directly to your game directory**:

```
Game Directory
├── gta3.exe
├── gta3.set
├── models/          ← mods deploy here
├── scripts/         ← mods deploy here
└── ...
```

**Benefits:**
- No intermediate copies or staging overhead
- Faster deployment for large mod counts
- Clear file structure — see exactly what's where
- Works offline after deploy (no VFS redirection needed)

### Backup & Rollback System

Before overwriting any file, TMM creates a timestamped backup:

```
%APPDATA%\TMM\Backups\{gameKey}\{timestamp}\
├── (backup copy of all files that would be overwritten)
└── .manifest.json (tracks what was changed)
```

The last **3 deploys per game** are retained. Click the **Rollback** button to restore any previous state. Backups are removed when you exceed the limit.

### Custom Game Profiles

Add any game with:
- **Game Name** — user-friendly label
- **Game Directory** — where the executable lives
- **Executable Path** — relative path to `.exe`
- **File Type Routing** — map extensions to output subdirectories:
  - `.asi` → `plugins\`
  - `.dll` → `scripts\`
  - `*` → root (default)
- **Conditional Routing** — *"Put .dll files into plugins\ if plugins\ exists, otherwise root"* (one-click presets for ASI Loader, SKSE, etc.)

Custom game configs are saved as `.tmmgame` files and can be exported/imported to share with friends.

---

## Features

### Core Modding

* **Direct Deploy:** Copy mods straight into game directory. No VFS, no intermediate staging.
* **Automatic Backup & Rollback:** Every deploy creates timestamped backups. Rollback to any previous state.
* **Custom Game Support:** Add any game. Per-extension output directory routing with plain-English sentence builder routing rules + one-click presets (ASI Loader, Source Engine, SKSE, CLEO, etc.).
* **Multi-game Launcher:** Home screen showing all games (built-in + custom) as cards. Quick access to Edit/Delete custom games.
* **Smart Nested Archive Extraction:** Archives wrapping content in a single subdirectory are automatically unwrapped.
* **Drag-and-Drop Load Orders:** Visual priority list with drop-line indicator. Bottom overrides top (0 loads first).
* **Per-Game Search:** Each game's mod list has independent search box with live filtering.
* **Test Routing Panel:** Before deploying, simulate file routing within CustomGameConfigWindow. Browse a test file and see exactly where it would be installed based on your routing rules.

### GTA-Specific Features

* **Exe-as-Mod Downgrading:** Install a 1.0 `gta3.exe` / `gta-vc.exe` / `gta-sa.exe` directly as a mod. Auto-detects game, assigns load order 0, and unlocks deployment even on Steam installs (which ship with DRM).
* **Force Deploy Override:** Right-click any play button or mod list to toggle override for games where exe check would block deployment.
* **Multi-hash MD5 Verification:** Accepts all known 1.0 build variants (US/EU pressings, different downgrader tools).
* **One-Click Essentials:** Auto-download SilentPatch, Ultimate ASI Loader, Widescreen Fixes, Project 2DFX, CLEO.
* **GTA IV Wizard:** Opening IV/TLaD/TBoGT with no paths configured shows setup wizard to auto-detect Steam paths.

### Toolbar

* **Deploy Button** — deploys all pending changes across all games. Greyed when nothing pending, accent-colored when changes exist.
* **Rollback Button** — restores active game to previous backup state.
* **Play Buttons (GTA only)** — Launch directly. **Green** = 1.0 exe detected, ready to play. **Red** = vanilla exe, would block mods. **Orange** = override active (mods deploy but game needs 1.0 exe to run).
* **Dice Theme Button** — Instantly apply random theme preset.

### Visual Customization

* **Themed Window Styles:** Dark, Light, Compact, and retro-inspired chrome variants.
* **25 Curated Built-in Theme Presets:**
  - **GTA-inspired:** Vice City Neon, GTA III Era, San Andreas Grove, GTA Online
  - **Popular Editors:** Dracula, Nord, Gruvbox, Catppuccin, One Dark, Monokai, Solarized Dark, GitHub Dark
  - **Synthwave/Retro:** Synthwave Sunset, Outrun, Retrowave, 80s Neon
  - **Dark Quality:** Matrix (neon green), Deep Ocean, Obsidian, Slate
  - **Light Variants:** Light Sky, Solarized Light, Nord Light, Light Teal
* **HSV Color Pickers:** Two-pane 2D spectrum pickers (accent + background) with hex input and live preview.
* **Mica/Acrylic Backdrop:** DWM API for Windows 11 native Mica backdrop.
* **Font Choices:** 8 system fonts — Bahnschrift, Segoe UI (Light/Regular/Bold), Calibri, Consolas.

### Diagnostics & Status

* **MD5 Diagnostics Console:** Check game exe hash against known 1.0 variants, detect mod overrides.
* **Error Reporting:** Crash dialog with one-line friendly message + copy-to-clipboard full stack trace.
* **Right-click Folder Access:** Open mod folder, game folder, or backup folder from mod list context menu.
* **Download Cache Wipe:** Clear temporary archive cache with confirmation.
* **AppData Location:** All settings, mods, and backups stored in `%APPDATA%\TMM\` (migrated automatically from old `%APPDATA%\TGTAMM\` on first run).

---

## GTA Downgrade Requirement

For the GTA III Series, Steam installs ship with DRM that blocks mods from running. You need a 1.0 downgraded executable:

1. **Option A (Recommended):** Download a 1.0 `.exe` and install it as a mod in TMM:
   - Click `+` button in mod list
   - Select the `.exe` file
   - TMM auto-detects game and assigns load order 0
   - Click Deploy

2. **Option B:** Manually replace your vanilla exe with a 1.0 version in your game directory

**Exe Auto-Detection:**
- `gta3.exe` 85MB → GTA III
- `gta-vc.exe` 116MB → Vice City
- `gta-sa.exe` 132MB → San Andreas

When deploying with override enabled but a Steam exe still in place, TMM displays a warning showing which games can't yet be played.

---

## MD5 Reference (GTA 1.0 Builds)

| Game            | Accepted Hash(es)                                         |
|-----------------|-----------------------------------------------------------|
| GTA III 1.0     | `85414bf9eb414d00ad81062360f0db1f`                        |
| GTA Vice City   | `8f3707edaa361957c70f8b13998816f1` (primary)             |
|                 | `167a5c8b31b3e0dbefa033ca24453d4e` (ModDB downgrader)   |
| GTA San Andreas | `00eb2056583dfa6a4ca79dedf70df5e9`                        |

Check your exe hash via **Settings → Diagnostics → MD5 Check**.

---

## AppData Structure

```
%APPDATA%\TMM\
├── settings.json               (settings: theme, font, game paths, deploy overrides)
├── ModsRaw/
│   ├── III/
│   ├── VC/
│   ├── SA/
│   ├── IV/
│   ├── TLaD/
│   ├── TBoGT/
│   └── {customGameKey}/        (custom game mods)
├── Backups/
│   ├── III/{timestamp}/        (rollback snapshots)
│   ├── VC/{timestamp}/
│   └── ...
├── CustomGames/
│   ├── skyrim.json             (.tmmgame profile for custom game)
│   └── ...
└── Themes/
    ├── Dark Teal.mmtheme       (exported theme presets)
    └── ...
```

**Migration:** AppData is automatically migrated from `%APPDATA%\TGTAMM` to `%APPDATA%\TMM` on first launch.

---

## Planned Features

* **Conflict Resolution:** Warn when two mods overwrite the same file.
* **Smart DLL Wizard:** Auto-detect proxy DLLs (d3d11, d3d9, etc.) and suggest output directories.
* **Mod Profiles & Loadouts:** UI for saving/loading named mod configurations (enable state + load order per game).
* **Expanded Game Support:** Additional built-in profiles (Skyrim, Fallout, Baldur's Gate 3, etc.).
* **Mod Store Integration:** One-click install from ModDB/Nexus (future).

---

## Known Limitations

* Custom game toolbars missing some features (deploy color state, backup folder access) that are in GTA dashboards
* Steam protocol launch not wired in custom game dashboards
* ThemeManagerWindow refresh partially broken after theme change from non-GTA dashboards
* "Open Mods Store" context menu item is a stub

---

## Contributing

Fork, submit PRs, or open an Issue. If you're tackling a known bug, comment in the Issues tab to avoid duplicate work.

### Codebase Navigation

All source files include a **Table of Contents** block at the top. Key files:

| File | Purpose |
|------|---------|
| `Services/BackendCore.cs` | Core logic: settings, game detection, deploy pipeline, backup/rollback, archive extraction |
| `Services/GameRegistry.cs` | Singleton loader for built-in + custom game profiles |
| `Views/GameLauncherWindow.xaml.cs` | Entry point: game card launcher |
| `Views/MainDashboardWindow.xaml.cs` | GTA III/VC/SA dashboard UI + event handlers |
| `Views/Gta4DashboardWindow.xaml.cs` | GTA IV/TLaD/TBoGT dashboard |
| `Views/CustomGameDashboardWindow.xaml.cs` | Generic single-game dashboard for custom games |
| `Views/CustomGameConfigWindow.xaml.cs` | Add/Edit custom game dialog with routing sentence builder |
| `Theming/ThemeEngine.cs` | Dynamic brush application, DWM Mica, HSV helpers, contrast algorithms |
| `Models/AppSettings.cs` | All persisted settings (themes, window size, toolbar state, etc.) |
| `Models/GameProfile.cs` | Built-in game constants (exe names, Steam IDs, MD5s) |
| `Models/CustomGameProfile.cs` | Custom game config with per-extension output routing |
| `Models/DeployManifest.cs` | Backup tracking: what files changed, when, and where |
| `Models/ModItem.cs` | Single mod entry with property change notifications |

Full architecture documented in `CODEBASE_GUIDE.md`.

### Documentation

* `CODEBASE_GUIDE.md` — Pseudocode table of contents and AI search index for all windows, services, models, and conventions
* `CHANGELOG.md` — Full version history with added/changed/removed per release

---

## Technical Details

### Deploy Pipeline

1. **Pre-deploy:** Scan for pending mods (enabled but not yet deployed)
2. **Backup Phase:** Copy all files that will be overwritten to `Backups/{gameKey}/{timestamp}/`
3. **Deploy Phase:** Copy mods to game directory, respecting per-extension output routing
4. **Manifest:** Save `DeployManifest.json` recording what changed, where, and when
5. **Prune:** If 4+ backups exist, delete oldest ones to stay within 3-backup limit

### File Routing

Given a mod file `MyMod/MyPlugin.asi`:

1. Check conditional routes: *"if plugins\ exists, put in plugins\, else root"*
2. If match found, deploy to conditional target
3. Else check extension map: `.asi` → `plugins\`
4. Else use wildcard: `*` → root

Custom games define these routes via the sentence builder or `.tmmgame` JSON export.

---

## System Requirements

* **Windows 7 or later** (best on Windows 10/11)
* **.NET 10 Runtime** — Download from [dotnet.microsoft.com/download/dotnet/10.0](https://dotnet.microsoft.com/download/dotnet/10.0)
  - Choose "Run desktop apps" installer (not SDK)
  - Pre-bundled in official releases (no separate install needed)
* **2 GB RAM** minimum
* **1 GB free disk space** (plus backup storage)

---

## License

TMM is open source. See LICENSE file for details.

---

## Support

**Issues?** Open a GitHub Issue with:
- Steps to reproduce
- Which game(s) affected
- Screenshot if applicable
- Contents of `%APPDATA%\TMM\TMM.log`

**Questions?** Check CODEBASE_GUIDE.md and the issue tracker before opening a new issue.
