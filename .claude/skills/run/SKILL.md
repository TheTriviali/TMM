---
description: Launch and test the TMM application
---

# /run — Launch TMM

Builds TMM in Release mode and launches the exe from the Release output folder.

## Steps

1. If `--nuke` or `--full-clear` was passed, use PowerShell to delete the entire `C:\Users\noahd\AppData\Roaming\TMM` directory before building:
   ```powershell
   Remove-Item -Path "C:\Users\noahd\AppData\Roaming\TMM" -Recurse -Force -ErrorAction SilentlyContinue
   ```
   This removes all app data: settings, mods, backups, baselines, loadouts, and custom games.
2. If `--fresh` or `--clean` was passed, delete only `C:\Users\noahd\AppData\Roaming\TMM\settings.json` before launching (do not delete the whole TMM folder).
3. Run `dotnet build -c Release --no-logo` in the project root (`C:\Users\noahd\source\repos\tmm\tmm`).
4. Launch `bin\Release\net10.0-windows\TMM.exe` via `Start-Process` (PowerShell).
5. Report build result and what was cleared (if anything).

## Flags

- `--nuke` or `--full-clear` — delete the entire `C:\Users\noahd\AppData\Roaming\TMM` directory before build/launch (wipes all app data: settings, mods, backups, baselines, loadouts, custom games)
- `--fresh` or `--clean` — delete only `C:\Users\noahd\AppData\Roaming\TMM\settings.json` before launch to trigger first-run flow (keeps mods and other data)
- (no flag) — launch with existing settings and data intact

## Build output path

Always build and run **Release**, not Debug:
- Build: `dotnet build -c Release`
- Exe: `bin\Release\net10.0-windows\TMM.exe`

## After launching

Tell the user what to look for based on flags used:
- **--nuke** or **--full-clear**: welcome screen appears; entire app data was wiped
- **--fresh**: welcome screen appears with profile dropdown and language picker in the corner (settings reset, other data preserved)
- **normal**: app opens to library (or last workspace if StartupPage = ModManager)
