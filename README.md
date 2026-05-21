# Triviali's GTA Mod Manager (TGTAMM) - Alpha

A specialized, high-performance mod manager for the classic 3D GTA Trilogy (III, Vice City, San Andreas). Built with a focus on UI personalization, modern aesthetics, and a safe "Zero-Footprint" modding approach.

## ⚠️ Alpha Notice
TGTAMM is currently in **Alpha**. Expect bugs, unpolished edge cases, and missing features. Feedback, issue reports, and pull requests are welcome!

## 🚀 Features

### Modding
* **Virtual File System (VFS) Modding:** Mods are deployed to an AppData virtual folder — your base game installation is never touched. The virtual folder is created automatically on first deployment.
* **Exe-as-Mod Downgrading:** Install a 1.0 `gta3.exe` / `gta-vc.exe` / `gta-sa.exe` directly as a mod. The manager auto-detects the game, assigns load order 0, and unlocks VFS deployment even on Steam installs.
* **Force Deploy Override:** For non-downgraded (Vanilla/Steam) games, right-click any play button in the toolbar to toggle **⚡ Force Deploy Override**. When enabled, deployment proceeds without requiring a 1.0 exe — useful for testing mods before committing to a downgrade. The override persists across sessions and can be toggled off at any time.
* **Smart Nested Archive Extraction:** Archives that wrap content in a single subdirectory are automatically unwrapped so files end up at the correct depth.
* **Multi-hash MD5 Verification:** Accepts all known 1.0 build variants (US/EU pressings, different downgrader tools). Vice City accepts two known-good hashes.
* **Drag-and-Drop Load Orders:** Visual priority list with drop-line indicator. Bottom overrides Top (0 loads first).
* **Per-Game Search:** Each game column has its own search box for filtering the mod list independently.
* **One-Click Essentials:** Auto-download and install DXVK, SilentPatch, Ultimate ASI Loader, Widescreen Fixes, Modloader, Project 2DFX, CLEO.

### Toolbar
* **Deploy button** — always visible on the right; turns grey when nothing needs deploying, accent-coloured when changes are pending. When a Vanilla exe blocks deployment, clicking opens the Help window.
* **Play buttons (III / VC / SA)** — launch the modded virtual folder directly. Green = ready, red = Vanilla exe detected. Right-click for the Force Deploy Override option.
* **Toolbar Labels** — toggle text labels beneath all icons with the italic *T* button. Labels include Play III / Play VC / Play SA. State persists across sessions.
* **Consistent icon theming** — all filled-background buttons (Deploy + Play) use the same `AccentTextBrush` foreground so icon contrast is always correct regardless of the active accent color.

### Look & Feel
* **Modular UI Themes:** Windows 7 Aero (with refined button styling), Windows 8/10, Windows 9x Classic (dark/light modes), macOS Dark, macOS Light, Vanilla, Compact.
* **HSV Color Pickers:** Two-pane 2D spectrum pickers (accent + background) with hex input, live preview, and drag support.
* **Theme Presets (.mmtheme):** 11 built-in presets. Import/export `.mmtheme` JSON files for sharing.
* **Enhanced Mica/Acrylic Backdrop:** DWM API for Win11 native Mica with user-adjustable intensity. More opaque panels ensure the backdrop effect remains visible across all color schemes.
* **Refined UI Polish:** Consistent corner rounding (10px main window, 5px buttons), improved theme compatibility, and adaptive Mica calculations.
* **Font Choices:** 8 system fonts — Bahnschrift, Segoe UI variants, Calibri, Consolas, and more.
* **Intelligent Text Contrast:** Three algorithms (WCAG, YIQ, Invert Snap) for automatic foreground color selection.

### Diagnostics & Status
* **Diagnostics Console:** MD5 check (with mod-override detection), Steam protocol controls, error log, cache wipe — accessible from Settings.
* **Status Indicators:** Info icon (ℹ) at the bottom of the sidebar displays when one or more games are unmapped; accent-colored for consistency with overall theme.
* **Right-click Folder Access:** Open mod folder, base game folder, or virtual folder directly from the mod list context menu.

---

## 🛑 Current Known Issues
* **Application Load Error 5 (Steam DRM):** Virtual Mode requires a 1.0 downgraded executable. Install the downgraded exe as a mod (➕ button, select `.exe`) or enable the **Force Deploy Override** (right-click any play button) to bypass the check.
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
