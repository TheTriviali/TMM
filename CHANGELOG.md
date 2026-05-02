# Changelog

All notable changes to TGTAMM are listed here, newest first.

---

## [Unreleased] — 2026-05-02

### Added
- **Per-game search boxes** — each game column (III / VC / SA) now has its own search box between the title and the mod list, replacing the single shared search bar.
- **Toolbar label toggle** — italic *T* button in the toolbar left section shows/hides text labels beneath each centered tool icon. State persists across sessions via `AppSettings.ToolbarShowLabels`.
- **Deploy button downgrade routing** — when deploy is greyed out and a vanilla (non-downgraded) executable is detected, hovering shows "Can't deploy — one or more games need a 1.0 downgrade. Click for help." Clicking opens the Help & Troubleshooting window directly.
- `ToolbarShowLabels` property added to `AppSettings`.

### Changed
- **Toolbar is now full-window width** — moved above the sidebar/modlist split so it always spans the entire window regardless of sidebar state.
- **Centered toolbar icon group** — the Install Mod → DXVK icon group is now centered between the left-pinned sidebar toggle and the right-pinned deploy/play/help buttons.
- **Sidebar opens next to modlists only** — the toolbar remains fixed at the top; toggling the sidebar only affects the content area below it.
- **Deploy button stays enabled** — the button is always clickable; when blocked by a downgrade requirement it routes to Help instead of being unresponsive.
- **Win7 / Win8 / Win10 close button** — removed the styled pill/border at rest state. The × glyph now appears borderless and only gains a red background on hover, consistent with the other title bar styles.

### Removed
- **Clone Game button from Initial Setup** — cloning to the virtual folder happens automatically on first deployment, so the manual Clone button in the setup window was removed.
- Single shared search bar (replaced by per-game search boxes).

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
