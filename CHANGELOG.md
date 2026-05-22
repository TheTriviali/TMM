# Changelog

All notable changes to TMM are listed here, newest first.

---

## [2.0] — 2026-05-22

### Added
- **Direct Deploy:** Mods are now copied straight into the game's actual installation directory. No virtual filesystem, no staging folder, no intermediate copies.
- **Automatic Backup & Rollback:** Before overwriting any file, the original is backed up to `AppData\TMM\Backups\{gameKey}\{timestamp}\`. Last 5 deploys per game are retained. `DeployManifest` JSON tracks every file changed.
- **Rollback Button:** New toolbar button (undo icon) lets you restore the last deploy for the active game. Works in both GTA dashboard and Custom Game dashboard.
- **Custom Game Support:** Add any game with a configurable name, directory, executable, and per-extension output subdirectory routing (e.g. `.asi` -> `scripts\`, `*` -> root).
- **Multi-game Launcher:** New `GameLauncherWindow` home screen showing all configured games as cards. Built-in GTA III Series card + custom game cards with Edit/Delete actions.
- **Back to Launcher Button:** Toolbar button in all dashboards to return to the launcher.
- **Reset Button (Launcher):** Clears the download cache with a confirmation dialog.

### Changed
- **Project renamed:** TGTAMM -> TMM (Triviali's Mod Manager). AppData migrated automatically from `%APPDATA%\TGTAMM` to `%APPDATA%\TMM` on first launch.
- **`TempStagingPath` -> `DownloadCachePath`:** Staging folder concept removed. Download cache is now only used for temporary archive downloads before extraction, not for deployment.
- **DXVK config location:** `dxvk.conf` is now written to the actual game installation directory instead of the old virtual folder.
- **Context menu "Open Virtual Folder"** renamed to **"Open Backup Folder"** — opens the rollback backup directory for that game.
- **`GetDriveSpaceInfo()`** no longer references VFS; shows total AppData size.
- **`BtnLaunchModded_Click`** now launches from the game's actual installation directory.
- **Output mapping UI** in Add Custom Game dialog replaced with a `DataGrid` (Extension / Output Folder columns) replacing the raw textarea.
- **Status bar in launcher** uses `AccentBrush` on a dark background panel for readability across all themes.

### Removed
- **Virtual File System (VFS):** `CloneToVirtualAsync()`, `ModdedFolderName`, `Modded{Key}` AppData folders — all removed.
- **TempStaging folder:** No longer created. `TempStagingPath` property removed from `BackendCore`.
- **`WipeTempStaging()`** replaced with `WipeDownloadCache()`.
- **mojibake characters** across all `.cs` and `.xaml` source files cleaned up (double-encoded UTF-8/Windows-1252 sequences replaced with ASCII equivalents).

---

## [Unreleased] — 2026-05-21

### Added
- **Toast notification system** — in-window toasts in bottom-right corner (1280x672 window). Replaces desktop overlay with in-app notifications that stack and auto-dismiss.
- **Dice-roll theme button** — 🎲 button in toolbar applies random preset theme with success toast feedback.
- **Accent-colored window borders** — toggleable option in Settings → Themes to apply accent color to window border (compatible with all themes including Compact).
- **34+ theme presets** — comprehensive collection including Cyberpunk, Vaporwave, Terminal Green, Vice City Pink, San Andreas Dusk, and many more.
- **Deploy-time override warnings** — when deploying with some games overridden, displays warning toast showing which games still need 1.0 executable.
- **Override context menu access** — "⚡ Toggle Force Deploy Override" available in mod list right-click menus and empty-list context menus.
- **Orange deploy button state** — when override is enabled, deploy button shows orange to signal "ready to deploy, but can't play yet."
- **Error code documentation** — updated all references to "Application Load Error 5:0000065434" for clarity.

### Changed
- **Main window dimensions** — locked to 1280x672 (optimized for 1280x720 displays with 48px taskbar at 100% scaling).
- **Notification architecture** — moved from separate desktop overlay window to integrated bottom-right corner panel within MainDashboardWindow.
- **Toolbar button sizing** — standardized to 38×38px for secondary buttons, 42×42px for primary deploy/play buttons.
- **Mica backdrop intensity** — made configurable per-theme (improved visibility on dark themes).
- **Panel color calculations** — simplified color theory with consistent lift values for better theme consistency.
- **Help window** — clarified distinction between deploy override (enables VFS deployment) vs. 1.0 executable requirement (needed for gameplay).

### Fixed
- **Toolbar icon visibility** — fixed color inconsistencies across themes (AccentBrush/AccentTextBrush theming).
- **Dice button visibility** — now uses AccentLabelBrush for consistent visibility on all themes.
- **Notification stacking** — toasts now properly stack and move within window bounds, disappearing cleanly as timers expire.
- **Corner rounding** — improved window border-radius consistency (10px outer, 9px inner clipping).
- **Win 7/8/9x button styling** — refined appearance and hover states to match theme intent.

---

## [Alpha] — 2026-04-30  *(session prior to 2026-05-02)*

### Added
- Modular title bar with six styles: macOS Dark, macOS Light, Windows Vanilla, Windows 8/10, Windows 7 Aero, Windows 9x Classic, Compact.
- HSV two-pane color pickers for accent and background with hex input and live preview.
- 11 built-in theme presets plus import/export of `.mmtheme` JSON files.
- Mica / Acrylic backdrop via DWM API (Windows 11 native; panel-transparency approximation on Windows 10).
- One-click essential installers: DXVK, SilentPatch, ASI Loader, Widescreen Fixes, Modloader, Project 2DFX, CLEO.
- Drag-and-drop load order reordering with visual drop-line indicator.
- `ExeStatus.Vanilla` detection with red play button + help notification dot when a game is not downgraded.
- Multi-hash MD5 verification (accepts all known 1.0 build variants for III, VC, SA).
- Smart nested archive extraction — single-folder wrappers are automatically unwrapped.
- Diagnostics console with MD5 check, Steam protocol controls, error log, cache wipe.
- Right-click context menu: open mod folder, base game folder, virtual folder.
- Font selector with 8 system fonts (default: Segoe UI Light).
- F5 / keyboard shortcut support for deploy.
- Virtual File System (VFS) modding — mods deployed to AppData virtual folder, base install untouched.
