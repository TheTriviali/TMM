# TMM — Active Plans

> Living plan doc. Each brief is self-contained for a cold agent. Completed work is
> archived in [CHANGELOG.md](CHANGELOG.md).
>
> **Standing rule:** Any feature that works for built-in games must be fully configurable
> via the custom-game wizard — not complete until it appears in Step 1 (input) and Step 4
> (review). Users never edit `.tmmgame` JSON directly.
>
> **Architectural principles** ([CLAUDE.md](CLAUDE.md)): (1) deployment plans freeze at
> install; (2) rollback restores to the first-touch baseline.

---

## Model legend & delegation guidance

Each brief is tagged with the model to hand it to. **Prefer the lowest capable model.**

- 🟢 **Haiku** — trivial, fully-specified, single-file mechanical edits. No judgment.
- 🔵 **Sonnet** — moderate, well-specified, multi-file but no open design questions.
- 🟣 **Opus** — needs design judgment, cross-cutting architecture, or a mockup the user
  must approve first.

---

## Group I — Mockup backport: UI phases  (design in [HANDOFF_BACKPORT.md](HANDOFF_BACKPORT.md))

> **Context.** Groups H + M3 are complete (see CHANGELOG v0.1-alpha-11). These are the two
> remaining UI backport phases. All design decisions are frozen in HANDOFF_BACKPORT.md.
> **Deferred, do NOT build:** mod source+version, update-available badge, "Updates" filter chip.

### M3 — Library Home view (replace grid)  ✅ DONE (5b9aed4)
See CHANGELOG v0.1-alpha-11.

### M2 — Enriched mod list (inline conflicts + bulk bar + categories + filter chips)  🔵 Sonnet

Backport Mockup 2 into `ModManagerPage`. All backend APIs are ready (H1–H4). Key files:
- Mod list: `Views/Subpages/ModManagerPage.xaml` + `.xaml.cs`
- Conflict data: `Services/ConflictAnalyzer.cs` → `AnalyzeByMod()`
- Category colours: `Models/ModCategories.cs` → `BrushFor()`
- Batch ops: `Views/Subpages/ModManagerPage.Batch.cs`

**What to build:**
- **Category colour spine:** 4 px left border on each mod row using `ModCategories.BrushFor(mod.Category)`.
  Colour is purely visual — no routing change.
- **Inline conflict badge:** per-row badge showing `OverwritesCount`/`OverwrittenByCount` from H2
  `AnalyzeByMod`. Expand on hover/click to show clash detail (destination + winner name). Feed via a
  background pass when the mod list loads; cache result in a `Dictionary<string, ModConflictSummary>`.
- **Bulk-action bar:** appears above the list when `Cust_ModList.SelectedItems.Count > 1`. Buttons:
  Enable / Disable / Set Group / Remove — wire to H4 `Batch*` methods. Bar collapses on deselect-all.
- **Filter chips:** Enabled / Conflicts / Favorites row above the mod list. Client-side filter over
  the live `ObservableCollection`. Conflicts chip uses H2 result.
  **Do NOT add "Updates" chip** (deferred).
- **Per-row hover actions:** surface Open Folder + Properties as right-side hover icon buttons.
  Handlers already exist in `ModManagerPage.xaml.cs` (`MenuOpenFolder_Click`, `MenuProperties_Click`).

**Do NOT add:** source/version line, Update badge, any update-checking logic.

Localize all new strings en-US + es-MX. Build clean before committing.

---

### M1 — Game Workspace (tabbed shell + slim toolbar + nav restructure)  🟣 Opus

The riskiest change; do after M2 is merged. Restructures `UnifiedShellWindow` so a selected
game owns a focused workspace. Run the mockup first (`dotnet run --project Mockups\TMM.Mockups.csproj`,
pick "Game Workspace") to see the intended shape.

**Game header** (replaces the ModManager top toolbar):
- Cover initials tile, game title, readiness badge (reuse `GameCard` logic)
- Loadout switcher dropdown (reuse `BackendCore.ListLoadouts` / `ApplyLoadoutAsync`)
- Deploy-status pill showing `PendingChanges(gameKey).HasChanges` (H3)
- Primary buttons: Deploy / Play / `⋯` overflow

**Sub-tabs** (scoped to one game):
- **Mods** — current mod list (the meat of ModManagerPage)
- **Conflicts(N)** — full conflict table from H2 `AnalyzeByMod` + `AnalyzeProxyConflicts`
- **Backups** — re-host `BackupsPage` content here; retire standalone global page
- **Downloads** — re-host the in-manager downloads drawer here; retire standalone `DownloadsPage`
- **Config** — surface the Edit Config flow (`BtnEditConfigCustom_Click`)

**Slim toolbar:** 3 primary verbs (Install, Deploy, Play) + `⋯` overflow. The ~14-button strip is gone.
Edit Config → Config tab. Refresh / Files / Import / Rollback / Export → overflow menu.

**Shell nav restructure:**
- Global rail: Library / Notifications / Help / Settings only
- Game-scoped rail items (Downloads, Backups) removed from global nav once hosted as tabs
- Nav return: restore **same game + same sub-tab** (frozen decision, HANDOFF_BACKPORT.md)
- "Back to library" affordance lives in the game header
- `UnifiedShellWindow` tracks `_activeWorkspaceEntry` (a `LibraryEntry`) + `_activeWorkspaceTab`
  (string, e.g. "Mods") to restore after returning from a global destination

**Entry/exit:** Clicking a global-rail item while in the workspace preserves
`_activeWorkspaceEntry` + `_activeWorkspaceTab`. Returning navigates back to the stored workspace.
Clicking "Back to library" in the game header clears both and navigates to Library/Home.

Localize all new strings en-US + es-MX. Build clean before committing.

---

## Group D — Codebase health (standing)

### AUDIT1 — Periodic file-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ STANDING
Re-run when files sprawl. Last pass (2026-05-29) split `BackendCore.cs` and
`ModManagerPage.xaml.cs` into per-concern partials (see CHANGELOG). Next time, re-inventory the
top-20 largest source files and flag anything over ~800 lines for a partial-class split. **Gotcha:**
WPF code-behind splits must keep `partial class` + the XAML `x:Class` intact; never move
`InitializeComponent` wiring. Split only where it reduces real cognitive load.
