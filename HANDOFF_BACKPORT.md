# Handoff — Backport the design mockups into the main app

**For:** a fresh chat (start on 🟣 Opus — this is cross-cutting design + orchestration).
**Created:** 2026-05-30. **Branch:** master.
**Companion docs:** [UX_REVIEW.md](UX_REVIEW.md) (the why), [PLANS.md](PLANS.md) Group H
(the delegable backend pieces), and the runnable mockups in [`Mockups/`](Mockups/).

---

## What this is

We built three UI-only mockups in a standalone throwaway WPF project
(`Mockups/TMM.Mockups.csproj` — no backend, hardcoded XAML, real TMM palette). Run them:

```
dotnet run --project Mockups\TMM.Mockups.csproj
```

The user reviewed them and **approved backporting all three** into the real app. This
doc is the orchestration brief: what each mockup proposes, what already exists in the
codebase, what's missing, and the **frozen scope decisions** the user made on
2026-05-30. Build to those decisions; don't re-open them.

> ⚠️ The mockups are *visual intent*, not pixel law. The palette is lifted from
> `ThemeEngine.cs`; the spacing scale is 4/8/12/16/24/28. Where a mockup shows a
> feature we explicitly deferred (see below), do **not** build it — stub or omit.

---

## Frozen scope decisions (user, 2026-05-30)

1. **Game Workspace (mockup 1) = FULL tabbed workspace.** Restructure the shell so a
   selected game owns a focused workspace: a **game header** (cover, title, readiness
   badge, **loadout switcher**, **deploy-status pill**, Deploy / Play / overflow) and
   **sub-tabs: Mods · Conflicts · Backups · Downloads · Config**. Downloads and Backups
   **move out of the global left rail** and become in-game tabs. The global rail drops
   to truly-global destinations only (Library/home, Notifications, Help, Settings). The
   ~14-button ModManager toolbar collapses into 3 primary verbs + a `⋯` overflow
   (Edit Config → the Config tab; Refresh / Files / Import / Rollback / Export → overflow).

2. **Mod list (mockup 2) — build NOW: inline conflicts + pending-changes banner.**
   **Defer (omit/stub for this pass): mod source+version line, and update-available
   checking.** The "Update" badge and "v1.3 · nexusmods.com" source line in the mockup
   are future work — leave them out of the backport.

3. **Library Home (mockup 3) = REPLACE the grid view entirely.** Home (Continue hero +
   stats strip + your-games cards + recent-activity feed) becomes the library; only
   **Home + the existing list view** remain. The grid/showcase view is gone (showcase
   was already removed in Group F3). Migrate any persisted `LibraryViewMode == "grid"`
   to the new home. **Note:** the user removed the big "Add a game" *card* from the grid —
   keep only the "Add game" *button* in the section header.

4. **Mod list extras — build NOW: bulk-action bar + categories/tags.** Multi-select rows
   → enable / disable / set-group / remove, plus filter chips (Enabled / Conflicts /
   Favorites — **not** "Updates", that's deferred with update-checking). Categories/tags
   are a real model addition driving the row colour spine. **Standing rule applies:**
   categories must be configurable for custom games via the wizard (Step 1 input +
   Step 4 review), not just built-ins.

---

## Mockup-by-mockup gap analysis (what exists vs. what's missing)

### Mockup 1 — Game Workspace  *(biggest lift; stays Opus-led)*
| Piece | Exists? | Notes |
|---|---|---|
| Loadout switcher | ✅ backend | `BackendCore.Loadouts.cs`, `ModLoadout`, `ModManagerPage.Loadouts.cs`. Today it's a toolbar icon → make it the header dropdown. |
| Deploy-status pill / pending banner | ❌ | Needs pending-changes tracking — **PLANS.md H3**. |
| Readiness badge | ✅ | `LibraryEntry.IsReady` + Group F1 badge logic — reuse. |
| Backups as a tab | ✅ page exists | `BackupsPage` + `BackendCore.Backups.cs`. Re-host inside the workspace instead of the global rail. |
| Downloads as a tab | ✅ | The in-manager drawer (Group C1) already scopes downloads to the game — fold it into a Downloads tab; retire/duplicate-check the standalone `DownloadsPage`. |
| Config tab | ✅ | `BtnEditConfigCustom_Click` / Edit Config flow → becomes the tab body. |
| Slim toolbar / overflow | ⚠️ rework | Pure UI restructure of `ModManagerPage.xaml` + `.Toolbar.cs`. |
| Shell nav restructure | ⚠️ rework | `UnifiedShellWindow.xaml(.cs)` `NavigateTo` / `SetNavActive` — the core change. Mind the active-game context already tracked (`_activeModManagerEntry`). |

### Mockup 2 — Enriched Mod List
| Piece | Exists? | Notes |
|---|---|---|
| Inline conflict badge + Conflicts tab | ⚠️ data only | `ConflictAnalyzer.Analyze(List<(ModItem,DeploymentPlan)>)` returns per-destination conflicts. Needs a **per-mod aggregation** ("overwrites N", winners) — **PLANS.md H2**. Frozen plans live at `ModsRaw_{key}/{Mod}/_tmm/deployplan.json`. |
| Category colour spine | ❌ model | New field on `ModItem` + persistence + wizard — **PLANS.md H1**. |
| Source + version line | ❌ | **DEFERRED** — omit. |
| Update badge | ❌ | **DEFERRED** — omit (and drop the "Updates" filter chip). |
| Per-row hover actions | ⚠️ | Open-folder / Properties already exist as context-menu handlers in `ModManagerPage.xaml.cs` — surface as hover buttons. |
| Multi-select bulk bar | ⚠️ | `Cust_ModList` is already `SelectionMode="Extended"`. Per-mod enable/disable/group/remove exist; need batch wrappers — **PLANS.md H4**. |
| Filter chips | ❌ UI | Client-side filter over the existing list; favorites/enabled are on `ModItem`, conflicts from H2. |

### Mockup 3 — Library Home
| Piece | Exists? | Notes |
|---|---|---|
| Continue hero | ⚠️ | Default game = `Settings.DefaultGameKey`; entries via `BuildLibraryEntries()`. Play/Manage handlers exist on the card path. |
| Quick-stats strip | ⚠️ derive | Games count, mods installed, backup usage (`BackendCore.Backups`), update count (deferred → drop or 0). |
| Your-games cards | ✅ | `GameCard` already renders readiness/needs-folder (Group F1). Reuse; **no big add-game card** (button only). |
| Recent-activity feed | ✅ backend | `ActivityLogger` + `ActivityFeedWindow` already exist — inline the feed into Home. |
| Replace grid view | ⚠️ | `LibraryPage.xaml(.cs)` view-mode switching + `UnifiedShellWindow` `BtnViewMode_Click` / `UpdateViewModeButtonStyles`. Migrate `"grid"` → home; keep list. |

---

## Open questions — RESOLVED (user, 2026-05-30)

All six answered in the backport chat. Frozen — do not re-open.

1. **Workspace entry/exit:** Returning from a global-rail item restores the **same game +
   same sub-tab**. An explicit **"Back to library"** affordance lives in the game header.
2. **Backups/Downloads global access:** **Strictly per-game.** No cross-game rollup; retire
   the standalone global Backups/Downloads pages once re-hosted as tabs.
3. **Categories taxonomy:** **Fixed preset, single category per mod.** Preset list
   (Gameplay / Visual / Audio / Map / Other). Custom games pick from the same preset via
   the wizard. Drives the colour spine + filter chips.
4. **Pending-changes definition:** **enable/disable + reorder + install/remove** all count;
   keep the per-bucket `{Enabled,Disabled,Reordered,AddedRemoved}` summary. Switching the
   active loadout **does NOT reset** the baseline — pending is always *current state vs what
   is physically deployed* (last `DeployManifest`).
5. **Conflicts tab scope:** **File conflicts + proxy-DLL conflicts** (`AnalyzeProxyConflicts`)
   — the tab is the one place to see every clash. Inline resolved-winner editing stays in the
   existing DeployPreview for this pass.
6. **Home stats:** **Count + cached size.** Show "N mods installed" plus a total size read
   from a **cached/persisted** value (updated on install/deploy) — no live disk walk on
   Home render.

---

## Suggested sequencing

1. Land the **delegable backend pieces** (PLANS.md **H1–H4**) first — they're
   well-specified, independent, and unblock the UI. Hand to Sonnet/Haiku.
2. **Mockup 3 (Home)** next — most self-contained, lowest structural risk, immediate
   visible win. (Needs H4-free; uses ActivityLogger + existing cards.)
3. **Mockup 2 (list)** — once H1/H2 land, the row enrichment + bulk bar + filters are
   mostly UI.
4. **Mockup 1 (workspace)** last — the nav restructure is the riskiest; do it when the
   per-game features (Conflicts, pending pill) it hosts already exist.

Every step: build clean, verify pass+fail paths, localize new strings (en-US + es-MX),
honour the two architectural principles (frozen plans; first-touch baseline) and the
standing custom-game-wizard rule. Commit per logical unit.
