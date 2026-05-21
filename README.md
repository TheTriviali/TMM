# Triviali's GTA Mod Manager (TGTAMM) - Alpha

A specialized, high-performance mod manager for the classic 3D GTA Trilogy (III, Vice City, San Andreas). Built with a focus on UI personalization, modern aesthetics, and a safe "Zero-Footprint" modding approach.

## ⚠️ Alpha Notice
TGTAMM is currently in **Alpha**. Expect bugs, unpolished edge cases, and missing features. Feedback, issue reports, and pull requests are welcome!

## 🚀 Features

### Modding
* **Virtual File System (VFS) Modding:** Mods are deployed to an AppData virtual folder — your base game installation is never touched. The virtual folder is created automatically on first deployment.
* **Exe-as-Mod Downgrading:** Install a 1.0 `gta3.exe` / `gta-vc.exe` / `gta-sa.exe` directly as a mod. The manager auto-detects the game, assigns load order 0, and unlocks VFS deployment even on Steam installs.
* **Force Deploy Override:** For non-downgraded (Vanilla/Steam) games, right-click any play button (or right-click the mod list) to toggle **⚡ Force Deploy Override**. When enabled:
  - Mods deploy normally (virtual folder + overrides bypass the exe check)
  - Play button turns **orange** (vs red when disabled) to signal the override is active
  - **Important:** The game exe is still Steam/Vanilla, so launching will still fail with Error 5
  - Install a 1.0 downgraded exe as a mod to actually run the game
  - Useful for testing/verifying mods before committing to a downgrade. Persists across sessions.
* **Smart Nested Archive Extraction:** Archives that wrap content in a single subdirectory are automatically unwrapped so files end up at the correct depth.
* **Multi-hash MD5 Verification:** Accepts all known 1.0 build variants (US/EU pressings, different downgrader tools). Vice City accepts two known-good hashes.
* **Drag-and-Drop Load Orders:** Visual priority list with drop-line indicator. Bottom overrides Top (0 loads first).
* **Per-Game Search:** Each game column has its own search box for filtering the mod list independently.
* **One-Click Essentials:** Auto-download and install DXVK, SilentPatch, Ultimate ASI Loader, Widescreen Fixes, Modloader, Project 2DFX, CLEO.

### Toolbar
* **Deploy button** — always visible on the right; turns grey when nothing needs deploying, accent-coloured when changes are pending. When a Vanilla exe blocks deployment, clicking opens the Help window.
* **Play buttons (III / VC / SA)** — launch the modded virtual folder directly. **Green** = ready to play with 1.0 exe. **Red** = Vanilla exe, no override (can't deploy). **Orange** = Vanilla exe with override active (can deploy, but game launch needs 1.0 exe). Right-click for the Force Deploy Override option.
* **Random Theme button (🎲)** — instantly applies a random theme preset. Great for discovering new theme combinations. Disabled themes from context menus still accessible.
* **Toolbar Labels** — toggle text labels beneath all icons with the italic *T* button. State persists across sessions.
* **Refined sizing & spacing** — secondary buttons (Install, Settings, Themes, etc.) are 38×38 for a cleaner look; primary Deploy/Play buttons stay 42×42 for visual hierarchy.

### Look & Feel
* **Modular UI Themes:** Windows 7 Aero (with refined button styling), Windows 8/10, Windows 9x Classic (dark/light modes), macOS Dark, macOS Light, Vanilla, Compact.
* **34+ Built-in Theme Presets:** GTA-era classics (Vice City, San Andreas, Liberty City), popular editor themes (Dracula, Nord, Gruvbox), plus cyberpunk, vaporwave, neon, retro amber, and more. 🎲 **Random Theme** button for instant discovery. Import/export `.mmtheme` JSON files for sharing.
* **HSV Color Pickers:** Two-pane 2D spectrum pickers (accent + background) with hex input, live preview, and drag support.
* **Enhanced Mica/Acrylic Backdrop:** DWM API for Win11 native Mica with user-adjustable intensity. More opaque panels ensure the backdrop effect remains visible across all color schemes.
* **Accent-Colored Window Border:** Optional toggle (Themes → "Accent-colored window border") to match the outer frame to your accent color. Works across all titlebar modes including Compact.
* **Refined UI Polish:** Consistent corner rounding (10px main window, 5px buttons), improved theme compatibility, and adaptive Mica calculations.
* **Font Choices:** 8 system fonts — Bahnschrift, Segoe UI variants, Calibri, Consolas, and more.
* **Intelligent Text Contrast:** Three algorithms (WCAG, YIQ, Invert Snap) for automatic foreground color selection.

### Diagnostics & Status
* **Diagnostics Console:** MD5 check (with mod-override detection), Steam protocol controls, error log, cache wipe — accessible from Settings.
* **Status Indicators:** Info icon (ℹ) at the bottom of the sidebar displays when one or more games are unmapped; accent-colored for consistency with overall theme.
* **Right-click Folder Access:** Open mod folder, base game folder, or virtual folder directly from the mod list context menu.
* **Override Toggle from Mod List:** Right-click any mod list (full or empty) to quickly toggle **⚡ Force Deploy Override** for that game without using the play button.

---

## 🛑 Current Known Issues
* **Application Load Error 5:0000065434 (Steam DRM):** The virtual folder requires a 1.0 downgraded executable to actually run the game. The **Force Deploy Override** allows mods to be installed without a downgrade, but the game will still fail to launch unless you:
  1. Install a 1.0 downgraded exe as a mod (➕ button → select `.exe`), OR
  2. Manually replace your vanilla exe with a 1.0 version
  * When deploying with the override enabled but a Steam exe still in place, TGTAMM will warn you which games can't yet be played and direct you to the fix.
* **Steam File Verification:** Internal Steam validation protocol may fail to invoke reliably on all systems.
* **Staging Directory Cleanup:** Locked files from extractions may survive until the next launch.

---

## 🗺️ Planned Features
* **Conflict Resolution Engine:** Warn when two mods overwrite the same critical file (`gta3.img`, `main.scm`, etc.).
* **Automated Downgrader Integration:** Fetch and apply 1.0 patches within the app.
* **Mod Profiles & Loadouts:** Save/swap between preset mod configurations (Vanilla+, Total Conversion, etc.).
* **SAMP / MTA Support.**
* **Complementary Color Picker:** Algorithmically suggest accent colors that pair with the chosen background.

---

## 🔬 MD5 Reference

| Game            | Accepted 1.0 Hash(es)                          |
|-----------------|------------------------------------------------|
| GTA III         | `85414bf9eb414d00ad81062360f0db1f`             |
| GTA Vice City   | `8f3707edaa361957c70f8b13998816f1` (primary)  |
|                 | `167a5c8b31b3e0dbefa033ca24453d4e` (ModDB DG) |
| GTA San Andreas | `00eb2056583dfa6a4ca79dedf70df5e9`             |

Use **Settings → 🔬 MD5** to check which hash your exe produces.

---

## 🤝 Contributing
Fork, submit PRs, or open an Issue. If you're tackling a known bug, comment in the Issues tab to avoid duplicating work.

### Codebase Navigation
All source files include a **Table of Contents** block at the top listing every section with approximate line numbers — useful when jumping into a file cold. Key files:

| File | What's in it |
|------|-------------|
| `Services/BackendCore.cs` | Settings, game detection, MD5 verification, mod deploy pipeline, archive extraction, downloads |
| `Views/MainDashboardWindow.xaml.cs` | All UI event handlers, drag-drop, toolbar logic, deploy flow |
| `Theming/ThemeEngine.cs` | Dynamic brush application, DWM Mica, HSV helpers, contrast algorithms |
| `Models/AppSettings.cs` | All persisted settings including `DeployOverrides` |
| `Models/GameProfile.cs` | Per-game constants (exe names, Steam IDs, accepted MD5s) |
| `Models/GameState.cs` | `ExeStatus` enum, `GameDetectionState` record, `GameStateManager` singleton |
| `Models/ModItem.cs` | Single mod entry with `INotifyPropertyChanged` |
