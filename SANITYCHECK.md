# TMM — Sanity Check Log
*Living document. Add an entry whenever a feature is verified working. Reference this when something breaks to find when it last worked.*

---

## How to use

Before starting a new feature or after a big refactor, run through the checklist for each area you touched. Mark `[x]` with today's date in the notes. If something is broken, add a `[!]` entry describing what's wrong and when it regressed.

---

## Core: Launch & Startup

| Check | Last verified | Notes |
|-------|--------------|-------|
| App launches without crash dialog | 2026-05-23 | Clean build, TMM.exe starts |
| GameLauncherWindow opens | 2026-05-23 | GTA III/VC/SA + IV cards visible |
| Custom game cards visible if configured | 2026-05-23 | BO3 card showed correctly |
| Theme applied on startup (macOS default) | 2026-05-23 | Traffic-light buttons, dark bg |
| Settings window opens from launcher | 2026-05-23 | After VFS dead code cleanup |
| VFS dead code removal: build clean | 2026-05-23 | 0 errors/warnings; removed DeepScan, SmartSteam, ParallelCopy, DebugStaging, GameState manager |

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
| Sidebar links readable (not icon-font boxes) | 2026-05-23 | Fixed with SidebarLinkBtnStyle |
| Theme switcher (dice) opens ThemeManagerWindow | — | |
| All 6 themes render correctly in this window | — | |
| Window border is 1px (no thick gradient border) | 2026-05-23 | Fixed from 4px gradient |
| CornerRadius = 10 on outer border | 2026-05-23 | |

---

## GTA IV Dashboard (Gta4DashboardWindow)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Opens from launcher | 2026-05-23 | |
| IV/TLaD/TBoGT tab switching | — | |
| Mod list loads per-game | — | |
| Install, toggle, deploy flow | — | |
| Context menu functional | — | |
| Theme/dice button works | — | |

---

## Custom Game Dashboard (CustomGameDashboardWindow)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Opens from launcher for a configured custom game | 2026-05-23 | BO3 tested |
| Mod list loads | — | |
| Install mod from archive | — | |
| Deploy button works | — | |
| Edit config (pencil) button opens CustomGameConfigWindow | 2026-05-23 | |
| Theme/dice button works | — | |
| Open Mod Folder / Open Game Folder context items | — | |

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
| GTA III, IV, custom game cards render | 2026-05-23 | |
| Drag .tmmgame onto launcher → opens config dialog | 2026-05-23 | |
| Import button → opens file dialog → config dialog | 2026-05-23 | |
| Click game card → opens correct dashboard | — | |
| Reset button → prompts before clearing | — | |
| Settings button → opens SettingsWindow | — | |
| About button | — | |

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
