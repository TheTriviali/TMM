# TMM — UX Review Handoff (Sonnet → Opus)

> **Purpose:** Sonnet just finished a library/card design pass. This doc hands the baton back to
> Opus for the next design-level review — capturing everything that shipped, the open questions
> from the previous pass (answered or still live), and potential next areas.
>
> **Date:** 2026-05-29 · **Branch:** master · **Build:** clean (0 warnings), 60/60 tests pass.

---

## 1. What TMM is (one paragraph)

Lightweight, direct-deploy mod manager for the GTA III series + arbitrary user-added games
(Skyrim, FNV, Cyberpunk, RDR2, Witcher 3 ship as built-in profiles). WPF + C# (.NET 10).
No VFS — mods are copied into the game directory. Deployment plans freeze at install;
rollback restores to a first-touch baseline. Custom games are first-class: anything a
built-in game can do must be configurable via the custom-game wizard (no hand-edited JSON).

## 2. The primary surfaces

| Surface | File | Role |
|---|---|---|
| Library | `Views/Subpages/LibraryPage.xaml(.cs)` | Game grid / list / showcase. Entry point. |
| Game card | `Views/Controls/GameCard.xaml(.cs)` | One game; Play / Manage / Default toggle / ⋯ overflow. |
| Mod Manager | `Views/Subpages/ModManagerPage.xaml` + partials | Per-game mod list, deploy, downloads drawer. |
| Shell | `Views/UnifiedShellWindow.xaml.cs` | Nav, `BuildLibraryEntries`, routing between pages. |
| Downloads | `Views/Subpages/DownloadsPage.xaml.cs` | WebView2 browser + archive list (also embedded as a drawer in Mod Manager). |
| Add/Edit Game | `Views/Subpages/AddGamePage.xaml(.cs)` | Full-shell wizard (Step 1–4 stacked) for creating/editing games. |

Library view mode (`grid` | `list` | `showcase`) persists in `AppSettings.LibraryViewMode`.

---

## 3. What Sonnet just shipped

### 3a. Library / GameCard design pass

- **B1 — Labeled GameCard actions + overflow menu (commit bb82dbf).**  
  Promoted Play + Manage to large labeled primary buttons (~28 px tall). Default is now a labeled
  ★ pill toggle (it's a state, not an action). Edit / Export / Archive/Unarchive moved into a `⋯`
  overflow (also right-click). Export is wired: saves `.tmmgame` via `GameRegistry.ExportConfigAsync`.
  Drag grip made visible (faint → solid on hover). Layout:
  ```
  ┌──────────────────────────────────┐
  │ ⠿                          ◖BETA◗ │
  │           GRAND THEFT             │
  │            AUTO: III              │
  │  ★ Default                    ⋯  │
  │  ┌──────────────┐ ┌─────────────┐│
  │  │   ▶  Play    │ │  ☰  Manage  ││
  │  └──────────────┘ └─────────────┘│
  ├──────────────────────────────────┤
  │ Rockstar · 2001           12 mods │
  └──────────────────────────────────┘
  ```

- **B2 — Showcase view horizontal symmetry (commit be59e55).**  
  Hero + carousel now share a centered max-width content column with equal gutters.

- **Card button hit-test fix (commit 81b318f).**  
  A transparent hover overlay was swallowing all clicks on the card face;
  `IsHitTestVisible="False"` on the overlay layer resolved it.

- **User-settable card colors + varied default gradients (commit d03a202).**  
  Built-in games now have distinct gradients (VC magenta, SA orange, TLAD crimson, TBoGT gold;
  III green & IV blue unchanged). Users can override any game's gradient via right-click →
  *Set Card Color…* (preset swatches + custom hex). Stored in `AppSettings.CardColorOverrides`.

- **Elegant unarchive (commit c29244b).**  
  A header toggle pill ("🗄 Show archived (N)" / "Hide archived") reveals archived games inline.
  Per-card unarchive is ⋯ → *Unarchive*. Previously `ShowArchived` had no UI surface.

### 3b. Welcome + navigation flow (Groups A–E, earlier this session)

| Item | Summary |
|---|---|
| A1 | "Go to library" card on welcome screen → skips picker, removes Skip button. |
| A2 | First-time manage → offers to set that game as default. |
| A3 | Startup routes to default game's Mod Manager (not Library) when a default is set. |
| E1 | Downloads page follows active/default game (not always GTA III). |
| E2 | Unified folder-open handlers; `FactoryReset` re-creates base dirs; `OpenOwnedFolder` helper. |
| E3 | Inline "Set game folder" banner + clickable sidebar path (works for built-ins + custom). |
| E4 | `BackendCore.InstallArchiveForGameAsync` extracted; Install button on Downloads page. |
| E5 | Downloads drawer auto-opens when archives exist for the current game. |

---

## 4. Open UX questions from the previous pass — current status

> **Opus design pass 2026-05-29 resolved Q1, Q3, Q4 — now frozen briefs in
> [PLANS.md](PLANS.md) Group F (F1/F2/F3). Q2, Q5–Q7 still open.**

1. ~~**Card information hierarchy / ready-dot discoverability.**~~ **→ RESOLVED (F1).** The 7px ready
   dot and the BETA/ALPHA chip were two state signals competing badly. The dot becomes a **labeled
   readiness badge** (Ready / Needs folder / No mods), "Needs folder" clickable into the browse flow;
   the maturity chip is **removed entirely**.

2. **Three view modes — do all earn their place?** **→ partially: showcase is being cut (F3).**
   Grid + list remain. *Still open:* is the grid/list split itself worth a switcher, or should one be
   the default with the other a density toggle?

3. ~~**Color vs. artwork — two overlapping paths.**~~ **→ RESOLVED (F2).** Unified into a single
   **Appearance…** dialog (gradient + artwork + live preview).

4. ~~**Status taxonomy (BETA / ALPHA / PRE-ALPHA / TESTING).**~~ **→ RESOLVED (F1).** Removed from the
   card; readiness badge replaces it. (`LibraryEntry.Status` model field kept, just unrendered.)

5. **First-run & empty states.**  
   Welcome flow (A1/A2/A3) is now cleaner. Worth a cold-start walk: factory reset → library →
   set folder → download → install → deploy. *Recommend a live test before the next feature pass.*

6. **Mod Manager density.**  
   Left sidebar + right Downloads drawer + set-folder banner + search + mod list.
   *Still open:* is the chrome coherent at small window sizes (~900 px wide)?

7. **Discoverability of right-click / ⋯ overflow.**  
   Set Group, Set Color, Set Artwork, Export, Archive all live there.
   Power-user-friendly, but a new user may never find them. *Still open.*

---

## 5. Potential next areas (no decisions made)

These are observations from the Sonnet pass — **not a backlog, just input for your design judgment:**

- **Library → Mod Manager back-navigation.** `pageModManager.BackRequested` returns to the Library,
  but there's no breadcrumb or title showing *which* game you're in. A small "GTA III ›" header
  would help orientation.

- **Empty mod list state.** When a game has no mods, the Mod Manager workspace is blank. A gentle
  empty-state illustration or "drag a mod here / browse Downloads" prompt could reduce friction.

- **Loadouts.** Loadouts exist in the backend (`%APPDATA%\TMM\Loadouts_{key}\`) and there's a
  `Loadouts.cs` partial in Mod Manager, but the loadout UI has not been audited recently.
  Worth a pass to confirm the UX matches the backend capability.

- **Card size + density.** The current 240×160 card (grid mode) is generous — on a large monitor
  with 10+ games the grid has a lot of whitespace. A compact/dense card option might help power users.

- **Notifications page.** Added in v0.1-alpha-10 (bell icon in nav). No design review yet.

---

## 6. How to run it

- `/run` (or `/run --fresh` for a clean first-launch state) launches the app.
- `/test` runs the 60-test suite. `/fmt` checks style.
- Build note: a running TMM.exe locks `bin\TMM.exe`; close the app before a full `dotnet build`,
  or the compile succeeds but the final copy-to-bin step errors (not a real failure).

## 7. Guardrails (don't break these)

- **Custom-game parity:** any new library/card feature must also work for wizard-added games.
- **Deploy plans freeze at install; rollback → first-touch baseline.** No per-deploy re-evaluation.
- **No hand-edited JSON for users.** `.tmmgame` is a bundling shortcut only.
- Localize new strings in both `Assets/Localization/en-US.json` and `es-MX.json`.
- Nullable enabled; specific catches (not bare `Exception`); minimal WPF code-behind.

---

*Drafted by Sonnet 4.6. Edit freely — this is scaffolding, not scripture.*
