# TMM - Triviali's Mod Manager

A general-purpose mod manager for classic games. Built for simplicity — no scripts, no manifests, just a GUI. Easier to get started with than MO2, with direct-deploy mod installation and automatic rollback support.

Currently ships with built-in support for the **GTA III Series** (III, Vice City, San Andreas). Custom game profiles let you add any game with configurable per-filetype output directory routing.

## Alpha Notice
TMM is currently in **Alpha**. Expect bugs, unpolished edge cases, and missing features. Feedback, issue reports, and pull requests are welcome!

## Features

### Modding
* **Direct Deploy:** Mods are copied straight into your game's installation directory. No virtual filesystem staging, no intermediate copies. Fast and straightforward.
* **Automatic Backup & Rollback:** Before any file is overwritten, the original is backed up to `AppData\TMM\Backups\`. The last 5 deploys per game are retained. Click the **Rollback** button to restore a game to its pre-deploy state.
* **Custom Game Support:** Add any game via "Add Custom Game". Configure the game directory, executable, and per-extension output subdirectory routing. Conditional routing rules use a plain-English sentence builder (*"Put .asi files into plugins if plugins\ exists, otherwise game root"*) with one-click presets for ASI Loader, Source Engine, SKSE, and CLEO.
* **Exe-as-Mod Downgrading (GTA):** Install a 1.0 `gta3.exe` / `gta-vc.exe` / `gta-sa.exe` directly as a mod. The manager auto-detects the game, assigns load order 0, and unlocks deployment even on Steam installs.
* **Force Deploy Override (GTA):** Right-click any play button or the mod list to toggle Force Deploy Override for games where the exe check would block deployment.
* **Smart Nested Archive Extraction:** Archives that wrap content in a single subdirectory are automatically unwrapped.
* **Multi-hash MD5 Verification (GTA):** Accepts all known 1.0 build variants (US/EU pressings, different downgrader tools).
* **Drag-and-Drop Load Orders:** Visual priority list with drop-line indicator. Bottom overrides Top (0 loads first, higher numbers win).
* **Per-Game Search:** Each game's mod list has its own search box.
* **One-Click Essentials (GTA):** Auto-download DXVK, SilentPatch, Ultimate ASI Loader, Widescreen Fixes, Modloader, Project 2DFX, CLEO.

### Toolbar
* **Deploy button** — deploys all configured games. Turns grey when nothing needs deploying, accent-colored when changes are pending.
* **Rollback button** — restores the active game to its pre-deploy state using the most recent backup.
* **Play buttons (III / VC / SA)** — launch the game directly. **Green** = ready to play with 1.0 exe. **Red** = Vanilla exe, no override. **Orange** = override active (mods deploy, but game needs 1.0 exe to run).
* **Random Theme button** — instantly applies a random theme preset.
* **Toolbar Labels** — toggle text labels beneath all icons. State persists across sessions.

### Look & Feel
* **Modular UI Themes:** Windows 7 Aero, Windows 8/10, Windows 9x Classic (dark/light), macOS Dark, macOS Light, Vanilla, Compact.
* **34+ Built-in Theme Presets:** GTA-era classics, popular editor themes (Dracula, Nord, Gruvbox), cyberpunk, vaporwave, neon, retro amber, and more.
* **HSV Color Pickers:** Two-pane 2D spectrum pickers (accent + background) with hex input, live preview.
* **Enhanced Mica/Acrylic Backdrop:** DWM API for Win11 native Mica with adjustable intensity.
* **Font Choices:** 8 system fonts — Bahnschrift, Segoe UI variants, Calibri, Consolas, and more.
* **Intelligent Text Contrast:** Three algorithms (WCAG, YIQ, Invert Snap) for automatic foreground color selection.

### Diagnostics & Status
* **Diagnostics Console:** MD5 check (with mod-override detection), Steam protocol controls, error log, download cache wipe.
* **Right-click Folder Access:** Open mod folder, game folder, or backup folder from the mod list context menu.

---

## GTA Downgrade Requirement

For the GTA III Series, Steam installs ship with DRM that blocks mods from running. You need a 1.0 downgraded executable:
1. Install a 1.0 downgraded `.exe` as a mod (+ button → select the exe file), OR
2. Manually replace your vanilla exe with a 1.0 version

When deploying with the override enabled but a Steam exe still in place, TMM will warn you which games can't yet be played.

---

## MD5 Reference (GTA)

| Game            | Accepted 1.0 Hash(es)                          |
|-----------------|------------------------------------------------|
| GTA III         | `85414bf9eb414d00ad81062360f0db1f`             |
| GTA Vice City   | `8f3707edaa361957c70f8b13998816f1` (primary)  |
|                 | `167a5c8b31b3e0dbefa033ca24453d4e` (ModDB DG) |
| GTA San Andreas | `00eb2056583dfa6a4ca79dedf70df5e9`             |

Use **Settings -> MD5** to check which hash your exe produces.

---

## Planned Features
* **Conflict Resolution Engine:** Warn when two mods overwrite the same critical file.
* **Mod Profiles & Loadouts:** Save/swap between preset mod configurations.
* **Expanded Game Support:** More built-in game profiles beyond the GTA III Series.
* **SAMP / MTA Support.**

---

## Contributing
Fork, submit PRs, or open an Issue. If you're tackling a known bug, comment in the Issues tab to avoid duplicating work.

### Codebase Navigation
All source files include a **Table of Contents** block at the top with approximate line numbers. Key files:

| File | What's in it |
|------|-------------|
| `Services/BackendCore.cs` | Settings, game detection, MD5 verification, deploy pipeline, backup/rollback, archive extraction, downloads |
| `Views/MainDashboardWindow.xaml.cs` | All UI event handlers, drag-drop, toolbar logic, deploy/rollback flow |
| `Views/CustomGameDashboardWindow.xaml.cs` | Custom game dashboard — deploy, rollback, mod list management |
| `Theming/ThemeEngine.cs` | Dynamic brush application, DWM Mica, HSV helpers, contrast algorithms |
| `Models/AppSettings.cs` | All persisted settings |
| `Models/GameProfile.cs` | Per-game constants (exe names, Steam IDs, accepted MD5s) |
| `Models/CustomGameProfile.cs` | Custom game profile with per-extension output directory routing |
| `Models/DeployManifest.cs` | `DeployManifest` and `BackupEntry` records for rollback |
| `Models/GameState.cs` | `ExeStatus` enum, `GameDetectionState` record, `GameStateManager` singleton |
| `Models/ModItem.cs` | Single mod entry with `INotifyPropertyChanged` |
</content>
</invoke>