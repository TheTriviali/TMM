---
description: Launch and test the TMM application
---

# /run — Launch TMM

Builds the project and runs the WPF application for manual testing.

## What it does

1. **Build** — `dotnet build` in release mode (faster, closer to real usage)
2. **Clean old settings** — Deletes `%APPDATA%/TMM/settings.json` to trigger first-launch flow (optional; default: keep settings)
3. **Launch** — Starts `bin/Release/net10.0-windows/TMM.exe`
4. **Reports** — Shows build output and process status

## Invocation

```
/run                    # Launch with existing settings
/run --fresh            # Clear settings first (test first-launch flow, language picker)
/run --debug            # Launch debug build (slower, more verbose)
/run --build-only       # Just build, don't launch
```

## What to test

After launching:

- **First launch** (`--fresh`): Language picker appears, "Set Up Your First Game" button is visible
- **Status bar**: Bottom bar shows 🌐 and language dropdown (en-US)
- **Language switching**: Click language dropdown, verify UI responds (live update, no restart needed)
- **Window chrome**: Title bar, minimize/maximize/close buttons work
- **Navigation**: Click Library, Mod Manager, Downloads, Backups, Settings, Paths
- **Existing settings**: Close and reopen—language preference persists

## Typical workflow

```bash
/run --fresh            # Start with clean state
# ... interact with app, test language switching ...
# Close app manually when done

/run                    # Run again with saved preferences
# ... verify persistence ...
```

## Troubleshooting

- **"Build failed"** — Check `dotnet build` output for compilation errors
- **"Process not found"** — App crashed on startup; check `%APPDATA%/TMM/TMM.log`
- **"Can't connect to display"** — Requires Windows desktop; not available in headless environments
