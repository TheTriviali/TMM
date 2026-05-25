# TMM — Sanity Check Log
*Living document. Add an entry whenever a feature is verified working. Reference this when something breaks to find when it last worked.*

**Last Updated:** 2026-05-25 (post-rebrand)

---

## How to use

Before starting a new feature or after a big refactor, run through the checklist for each area you touched. Mark dates and notes as you verify. If something is broken, add a `[!]` entry describing what's wrong and when it regressed.

**Checklist template for PLANS.md work:**
1. Pre-flight verification (rows below relevant to the change)
2. Implementation step
3. Post-step verification (affected UI elements, build health)

---

## Core: Launch & Startup

| Check | Last verified | Notes |
|-------|--------------|-------|
| App launches without crash dialog | 2026-05-25 | Clean build, TMM.exe starts, TGTAMM→TMM rebrand complete |
| GameLauncherWindow opens | 2026-05-25 | GTA III/VC/SA + IV cards visible |
| Custom game cards visible if configured | 2026-05-25 | Example custom game profile loads |
| Theme applied on startup | 2026-05-25 | Accent colors, dark bg applied |
| Settings window opens from launcher | 2026-05-25 | Theme/font/path settings accessible |
| Namespace: TGTAMM → TMM | 2026-05-25 | All .cs/.xaml using `namespace TMM` |
| AppData path: TGTAMM → TMM | 2026-05-25 | BackendCore uses `%APPDATA%\TMM\`, migration from TGTAMM works |
| Project files: TGTAMM.{sln,csproj} → TMM.* | 2026-05-25 | TMM.sln references TMM.csproj |
| GitHub repo renamed | 2026-05-25 | https://github.com/TheTriviali/TMM (main branch deleted, master is default) |

---

## GTA III Dashboard (MainDashboardWindow)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Opens from launcher | 2026-05-23 | |
| All 3 mod lists (III/VC/SA) load | — | |
| Drag mod file onto list → installs | — | |
| Toggle mod enabled/disabled | — | |
| Context menu: Rename, Delete, Move Up/Down | — | |
| Deploy button → deploys to game dir | — | |
| Sidebar links readable (not icon-font boxes) | 2026-05-25 | Sidebar UI consistent |
| Window border is 1px (no thick gradient border) | 2026-05-25 | Streamlined theming post-simplification |
| CornerRadius = 10 on outer border | 2026-05-25 | UI rounding consistent |

---

## GTA IV Dashboard (Gta4DashboardWindow)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Opens from launcher | 2026-05-25 | |
| IV/TLaD/TBoGT tab switching | 2026-05-25 | Multi-episode layout working |
| Mod list loads per-game | 2026-05-25 | |
| Deploy flow functional | 2026-05-25 | |

---

## Custom Game Dashboard (CustomGameDashboardWindow)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Opens from launcher for a configured custom game | 2026-05-25 | Custom profiles load correctly |
| Mod list loads | 2026-05-25 | |
| Install mod from archive | 2026-05-25 | |
| Deploy button works | 2026-05-25 | Custom game deploy flow |
| Edit config button opens CustomGameConfigWindow | 2026-05-25 | |

---

## CustomGameConfigWindow

| Check | Last verified | Notes |
|-------|--------------|-------|
| Add new game — saves and appears on launcher | 2026-05-23 | |
| Edit existing game — fields pre-filled | 2026-05-23 | |
| File type chips / output directory mappings | — | |
| Conditional routes — add, remove | — | |
| Export .tmmgame → file written with forward slashes | 2026-05-23 | Verified no double-backslash |
| Import .tmmgame → fields populated | 2026-05-23 | |
| Import .tmmgame in config window → form overwritten | 2026-05-23 | |

---

## GameLauncherWindow

| Check | Last verified | Notes |
|-------|--------------|-------|
| GTA III, IV, custom game cards render | 2026-05-25 | Card layout consistent |
| Drag .tmmgame onto launcher → opens config dialog | 2026-05-25 | Drag-drop import works |
| Import button → opens file dialog → config dialog | 2026-05-25 | File browser functional |
| Click game card → opens correct dashboard | 2026-05-25 | Navigation working |
| Settings button → opens SettingsWindow | 2026-05-25 | Settings accessible |

---

## Settings

| Check | Last verified | Notes |
|-------|--------------|-------|
| SettingsWindow opens | — | |
| GTA III mode — shows GTA III settings only | — | |
| GTA IV only mode — shows IV-specific settings | — | |
| Custom game mode — shows custom game settings | — | |
| Theme change → applied live across open windows | — | |
| Accent color change → applied live | — | |

---

## Theme System

| Check | Last verified | Notes |
|-------|--------------|-------|
| Win31 titlebar | — | |
| Compact titlebar | — | |
| Dark base theme | 2026-05-23 | |
| Light base theme | — | |
| Accent color changes ComboBox border + AccentBrush | — | |
| AccentBorderEnabled toggle | — | |

---

## Regression notes

*(Add entries here when something breaks — date + what + suspected cause)*

| Date | What broke | Suspected cause | Fixed? |
|------|-----------|----------------|--------|
| — | — | — | — |
