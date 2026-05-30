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

> All design decisions frozen in HANDOFF_BACKPORT.md.
> **Deferred, do NOT build:** mod source+version, update-available badge, "Updates" filter chip.

### M3 — Library Home view (replace grid)  ✅ DONE (5b9aed4)
### M2 — Enriched mod list  ✅ DONE (a197b6d)
See CHANGELOG v0.1-alpha-11 and v0.1-alpha-12 respectively.

---

### M1 — Game Workspace (tabbed shell + slim toolbar + nav restructure)  🟣 Opus

**The last and riskiest backport phase.** Run the mockup first to anchor the design:

```
dotnet run --project Mockups\TMM.Mockups.csproj   # pick "Game Workspace"
```

All frozen decisions are in [HANDOFF_BACKPORT.md](HANDOFF_BACKPORT.md). This brief adds
the codebase-specific wiring details.

---

#### What M1 produces

A selected game owns a **full workspace** inside `UnifiedShellWindow`. The user enters it
by clicking a game card (or the default-game auto-open on launch); exits via a "← Library"
button in the game header.

```
GLOBAL RAIL (4 items only)          GAME WORKSPACE
┌──────┐  ┌───────────────────────────────────────────────────────┐
│  🏠  │  │ [cover] Grand Theft Auto III    ● Ready               │
│  🔔  │  │         Rockstar · 12 mods  [Loadout ▾] [Deploy][Play]⋯│
│  ❓  │  ├───────────────────────────────────────────────────────┤
│  ⚙  │  │ ⚠ 3 changes since last deploy — Review & Deploy →     │
└──────┘  ├───────────────────────────────────────────────────────┤
          │ Mods │ Conflicts(2) │ Backups │ Downloads │ Config    │
          ├───────────────────────────────────────────────────────┤
          │  …active tab content…                                  │
          └───────────────────────────────────────────────────────┘
```

---

#### Shell nav changes  (`Views/UnifiedShellWindow.xaml(.cs)`)

**Global rail** currently has 8 nav items. Post-M1 it has **4**:
- Keep: Library (🏠), Notifications (🔔), Help (❓), Settings (⚙)
- Remove from rail: Mod Manager, Downloads, Backups, Add Game (Add Game moves to Library header)

The rail `NavigateTo` / `SetNavActive` flow is already in place. Retire the Mod Manager,
Downloads, and Backups nav buttons + their `Visibility="Visible"/"Collapsed"` switches.

**New state to track** (add to `UnifiedShellWindow.xaml.cs`):
```csharp
private LibraryEntry? _workspaceEntry;   // the game whose workspace is open
private string _workspaceTab = "Mods";   // last active sub-tab
```

When a global-rail item is clicked while `_workspaceEntry != null`, preserve both fields.
When the user returns to Library and the game is still set, navigate straight back to
`_workspaceEntry` + `_workspaceTab`. Clicking "← Library" in the game header sets
`_workspaceEntry = null` and navigates to "Library".

---

#### Game header  (new `Views/Controls/WorkspaceHeader.xaml(.cs)`)

Build as a separate `UserControl` (easier to test, keeps `UnifiedShellWindow` lean):

| Element | Source |
|---|---|
| Cover tile (initials) | Same `CoverInitials()` logic as `LibraryPage.RenderHero` |
| Title | `LibraryEntry.DisplayName` |
| Readiness badge | `GameCard` badge logic — reuse `UiColors` colours |
| Loadout switcher | `BackendCore.ListLoadouts(gameKey)` → `ComboBox`; on change call `BackendCore.ApplyLoadoutAsync` |
| Pending pill | `BackendCore.PendingChanges(gameKey).HasChanges` → amber pill "N changes" |
| Deploy button | Fires existing `BtnDeployCustom_Click` logic |
| Play button | Fires existing `BtnLaunchCustom_Click` / `LaunchGame` logic |
| `⋯` overflow | `ContextMenu` with: Refresh, Import from folder, Rollback, Export profile |
| Pending banner | Full-width amber strip "N changes since last deploy — Review & Deploy →" (same as mockup) |
| ← Library | Button in top-left of header; sets `_workspaceEntry = null`, navigates to Library |

---

#### Sub-tabs  (new `Views/Controls/WorkspaceTabBar.xaml(.cs)` or inline in shell)

Five tabs, each a `Border` toggle. Active tab body shown via `Visibility`:

| Tab | Content | Notes |
|---|---|---|
| **Mods** | Current `ModManagerPage` content (already enriched by M2) | Default tab |
| **Conflicts(N)** | Table of all clashes from `ConflictAnalyzer.AnalyzeByMod` + `AnalyzeProxyConflicts` | N = total clash count across both; 0 hides the badge |
| **Backups** | `BackupsPage` content re-hosted inline | Retire the standalone global `BackupsPage` nav |
| **Downloads** | The in-manager downloads drawer content (`Cust_DownloadsBorder` area) re-hosted | Retire standalone `DownloadsPage` nav |
| **Config** | Edit Config flow (`BtnEditConfigCustom_Click` logic) rendered inline | Replaces "Edit Config" toolbar button |

---

#### Conflicts tab detail

Use `ConflictAnalyzer.AnalyzeByMod(plans)` + `AnalyzeProxyConflicts(plans)` (both already
exist; H2 wired `AnalyzeByMod`). Render a simple `ListView` / `ItemsControl`:

```
[mod name]  overwrites N · overwritten by M
  └─ data\handling.cfg → winner: SkyGFX
  └─ dinput8.dll (proxy) → winner: SilentPatch
```

Winner is the highest-`FinalLoadOrder` participant. Read-only display; re-run on tab
activation. No inline editing (stays in `DeployPreviewWindow` per frozen decision #5).

---

#### Slim toolbar / overflow

The current `ModManagerPage` toolbar has ~14 buttons. Post-M1:

**Keep visible (3 primary verbs):** Install · Deploy · Play
- Deploy + Play already have large icon+label buttons; keep them.
- Install is `BtnInstallModCustom_Click`.

**Move to `⋯` overflow `ContextMenu`:**
Refresh (`BtnRefreshCustom_Click`), Import (`BtnImportFromGame_Click`),
Open Mods Folder (`MenuOpenModsFolder_Click`), Rollback (`BtnRollbackCustom_Click`),
Export Profile, Help, About.

**Remove entirely (moved to workspace tabs):**
- Edit Config → Config tab
- Downloads toggle → Downloads tab
- Backups shortcut → Backups tab
- Back button → "← Library" in the game header

Loadout switcher moves from toolbar icon to the **game header** dropdown.

---

#### Files to touch

| File | Change |
|---|---|
| `Views/UnifiedShellWindow.xaml` | Remove Mod Manager / Downloads / Backups / Add-Game rail buttons; add workspace host area |
| `Views/UnifiedShellWindow.xaml.cs` | Add `_workspaceEntry` / `_workspaceTab`; `NavigateToWorkspace(entry, tab)` method; wire LibraryPage `ManageRequested` → `NavigateToWorkspace` |
| `Views/Controls/WorkspaceHeader.xaml(.cs)` | **New** — game header UserControl (see above) |
| `Views/Subpages/ModManagerPage.xaml(.cs)` | Strip the toolbar down to 3 verbs + `⋯`; remove Back button; remove Downloads + Loadout toolbar items (they move to the header) |
| `Views/Subpages/ModManagerPage.Toolbar.cs` | Thin it out per the overflow move |
| `Views/Subpages/BackupsPage.xaml(.cs)` | No content change; just retire its global nav entry |
| `Views/Subpages/DownloadsPage.xaml(.cs)` | No content change; retire its global nav entry |

**Do NOT delete** the page files themselves — just remove their global-rail nav entry. The
pages remain functional as UserControls hosted inside the workspace.

---

#### Localize all new strings en-US + es-MX. Build clean before committing.

---

## Group D — Codebase health (standing)

### AUDIT1 — Periodic file-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ STANDING
Re-run when files sprawl. Last pass (2026-05-29) split `BackendCore.cs` and
`ModManagerPage.xaml.cs` into per-concern partials (see CHANGELOG). Next time, re-inventory the
top-20 largest source files and flag anything over ~800 lines for a partial-class split. **Gotcha:**
WPF code-behind splits must keep `partial class` + the XAML `x:Class` intact; never move
`InitializeComponent` wiring. Split only where it reduces real cognitive load.
