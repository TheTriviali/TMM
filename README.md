# Triviali's GTA Mod Manager (TGTAMM) - Alpha

A specialized, high-performance mod manager for the classic 3D GTA Trilogy (III, Vice City, San Andreas). Built with a focus on UI personalization, modern aesthetics, and a safe "Zero-Footprint" modding approach.

## ⚠️ Alpha Notice
TGTAMM is currently in **Alpha**. Expect bugs, unpolished edge cases, and missing features. Feedback, issue reports, and pull requests are welcome!

## 🚀 Core Features
* **Virtual File System (VFS) Modding:** Mods are deployed to an AppData virtual folder — your base game installation is never touched. The virtual folder is created automatically on first deployment.
* **Exe-as-Mod Downgrading:** Install a 1.0 gta3.exe/gta-vc.exe directly as a mod. The manager auto-detects the game, assigns load order 0, and unlocks VFS deployment even on Steam installs.
* **Smart Nested Archive Extraction:** Archives that wrap content in a single subdirectory are automatically unwrapped so the exe and files end up at the right depth.
* **Multi-hash MD5 Verification:** Accepts all known 1.0 build variants (US/EU pressings, different downgrader tools). VC currently accepts two known-good hashes.
* **Per-Game Search:** Each game column has its own search box for filtering the mod list independently.
* **Modular UI Themes:** Windows 7 Aero, Windows 8/10, Windows 9x Classic, macOS Dark, macOS Light, Vanilla, Compact.
* **HSV Color Pickers:** Two-pane 2D spectrum pickers (accent + background) with hex input, live preview, and drag support.
* **Theme Presets (.mmtheme):** 11 built-in presets. Import/export `.mmtheme` JSON files for sharing.
* **Live Backdrop Effects:** Mica/Acrylic via DWM API (Win11 native; panel transparency approximation on Win10).
* **Font Choices:** 8 system fonts available including Bahnschrift (default), Segoe UI variants, Calibri, Consolas.
* **Intelligent Text Contrast:** Three algorithms (WCAG, YIQ, Invert Snap) for automatic foreground color selection.
* **Drag-and-Drop Load Orders:** Visual priority list. Bottom overrides Top (0 loads first).
* **One-Click Essentials:** DXVK, SilentPatch, Ultimate ASI Loader, Widescreen Fixes, Modloader, Project 2DFX, CLEO.
* **Right-click Folder Access:** Open mod folder, base game folder, or virtual folder directly from the mod list.
* **Diagnostics Console:** MD5 check (with mod override detection), Steam protocol controls, error log, cache wipe — accessible from Settings.
* **Toolbar Labels:** Toggle text labels beneath toolbar icons with the italic *T* button.

## 🛑 Current Known Issues
* **Application Load Error 5 (Steam DRM):** Virtual Mode requires a 1.0 downgraded executable. Install the downgraded exe as a mod (➕ button, select .exe) and enable it — this bypasses the DRM check automatically. The deploy button will display a tooltip and open Help when this condition is detected.
* **Steam File Verification:** Internal Steam validation protocol may fail to invoke reliably on all systems.
* **Staging Directory Cleanup:** Locked files from extractions may survive until the next launch.

## 🗺️ Planned Features
* **Conflict Resolution Engine:** Warn when two mods overwrite the same critical file (gta3.img, main.scm, etc.).
* **Automated Downgrader Integration:** Fetch and apply 1.0 patches within the app.
* **Mod Profiles & Loadouts:** Save/swap between preset mod configurations (Vanilla+, Total Conversion, etc.).
* **SAMP / MTA Support.**
* **Complementary Color Picker:** Algorithmically suggest accent colors that pair with the chosen background.

## 🔬 MD5 Reference

| Game            | Accepted 1.0 Hash(es)                          |
|-----------------|------------------------------------------------|
| GTA III         | `85414bf9eb414d00ad81062360f0db1f`             |
| GTA Vice City   | `8f3707edaa361957c70f8b13998816f1` (primary)  |
|                 | `167a5c8b31b3e0dbefa033ca24453d4e` (ModDB DG) |
| GTA San Andreas | `170b3a9108687b26da2d8901c6948a18`             |

Use **Settings → 🔬 MD5** to check which hash your exe produces.

## 🤝 Contributing
Fork, submit PRs, or open an Issue. If you're tackling a known bug, comment in the Issues tab to avoid duplicating work.
