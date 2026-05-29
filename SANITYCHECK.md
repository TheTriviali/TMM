# TMM — Sanity Check Log
*Living document. Pre-release verification checklist for the current **unified-shell**
architecture. Mark dates/notes as you verify; add a `[!]` row under Regressions when
something breaks.*

**Last Updated:** 2026-05-29 (v0.1-alpha-9 — rewritten for the unified shell; the old
per-window dashboards/launcher checklist was retired)

---

## How to use

Before a release or after a big refactor, run the rows for each area you touched. These are
manual/UI checks — the automated suite (`dotnet test`, 55 tests) covers deploy/rollback,
planner, rule engine, load order, and `.tmmpack` parsing.

---

## Core: Launch & Startup

| Check | Last verified | Notes |
|-------|--------------|-------|
| App launches without crash dialog | — | `App.OnStartup` → `BackendCore` (Logger.Initialize) → `UnifiedShellWindow` |
| First launch shows InitialSetupWindow (language + game cards) | — | gated on `Settings.FirstLaunch` |
| Built-in card → SelectBuiltinGameWindow; custom card → wizard | — | S7 unified onboarding |
| Theme + accent applied on startup | — | |
| Window size/position restored | — | `Settings.Window*` |

---

## Unified Shell — navigation

| Check | Last verified | Notes |
|-------|--------------|-------|
| Left nav switches Library / Mods / Backups / Downloads / Paths / Settings | — | single window, swappable pages |
| Language dropdown shows display names, switches live | — | |
| Library: grid / list / showcase view modes | — | |
| Library: set default, reorder (persists), search/filter | — | |

---

## Mod Manager (all games — built-in + custom use one panel)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Built-in game resolves its path into the panel | — | `InitCustomGame` syncs `Settings.GamePaths` → `config.GameDirectory` |
| Drag-drop archive (.zip/.rar/.7z) installs a mod | — | nested archives unwrap |
| Install freezes a plan to `_tmm/deployplan.json` | — | rules run once (principle #1) |
| Toggle enable/disable, drag-to-reorder load order | — | bottom/highest wins |
| Favorites star column + context toggle | — | |
| Groups: "Show groups" toggle, set/clear group regenerates plan | — | |
| Integrity row shows only when configured (green ✓ / blue ℹ / amber missing) | — | see A1 brief — mismatch should read as info, not warning |
| Sidebar "find mods": NexusMods link when `nexusSlug` set, else DuckDuckGo | — | S6 |

---

## Deploy / Backup / Rollback

| Check | Last verified | Notes |
|-------|--------------|-------|
| Deploy opens DeployPreviewWindow with the frozen plan | — | |
| Cross-mod conflicts surface; ConflictResolverWindow picks a winner | — | `ConflictAnalyzer` |
| Deploy writes files; first-touch baseline captured before overwrite | — | `Baselines/{key}/baseline.json` |
| Per-deploy manifest saved; backups pruned to last 3 | — | |
| BackupsPage: select game, list snapshots, restore w/ confirm | — | |
| Rollback restores to the **baseline**, not the previous deploy | — | covered by `BackendCoreDeployTests` |
| Backup size badge appears over quota | — | `BackupSizeWarnBytes` |

---

## Custom Game wizard (CustomGameSetupWizard)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Add new game — saves, appears in Library | — | |
| Edit existing — fields pre-filled | — | |
| Step 1: name/dir/exe/SteamId/NexusSlug + advanced (overlay, companions, **search hints**, integrity) | — | |
| Step 4 review summarizes all of the above incl. search hints | — | |
| Export `.tmmgame` round-trips searchHints / nexusSlug / integrity | — | `FromExport` ↔ `ExportConfigAsync` |
| Quick Scan auto-locates a custom game via `searchHints` | — | O2 |

---

## Loadouts & .tmmpack

| Check | Last verified | Notes |
|-------|--------------|-------|
| Save / apply / rename / delete loadout | — | invalid filename chars rejected |
| Compare two loadouts (LoadoutDiffWindow) | — | |
| Export `.tmmpack` | — | `TmmPackBuilder` |
| Import `.tmmpack` into selected game (confirm prompt, collisions handled) | — | `TmmPackInstaller`; zip-slip-safe |

---

## Sync / Import (existing modded install)

| Check | Last verified | Notes |
|-------|--------------|-------|
| Scan detects loose/CLEO/modloader candidates | — | `ModImporter` |
| Review window: select / exclude / rename | — | split/merge still pending (D-B5) |
| Import seeds baseline, moves files (transactional), re-deploys to restore state | — | |

---

## Theme / Localization

| Check | Last verified | Notes |
|-------|--------------|-------|
| Theme presets + custom accent apply live | — | |
| Language switch updates bindings live | — | en-US / es-MX |

---

## Regression notes

*(date + what broke + suspected cause + fixed?)*

| Date | What broke | Suspected cause | Fixed? |
|------|-----------|----------------|--------|
| 2026-05-29 | `master` didn't compile | commit `b09c02a` shipped C4 referencing `ConflictAnalyzer`/`ConflictEntry`/`ConflictParticipant` without committing them | ✅ reconstructed `Services/ConflictAnalyzer.cs` (v0.1-alpha-9) |
