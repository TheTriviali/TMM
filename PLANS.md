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

Each brief is tagged with the model I'd hand it to:

- 🟢 **Haiku** — trivial, fully-specified, single-file mechanical edits. No judgment.
- 🔵 **Sonnet** — moderate, well-specified, multi-file but no open design questions.
- 🟣 **Opus** — needs design judgment, cross-cutting architecture, or UX decisions /
  a mockup the user must approve first.

**My opinion (Opus 4.8):** delegation is worth it here. The bulk is mechanical or
well-specified (🟢/🔵). Only three pieces genuinely need Opus: the notification-store
design (NOTIF1), the whole-program add/edit-game layout (WIZ1), and the B5 import-refinement
UX. Do those 🟣 briefs first (they unblock the 🔵 work that depends on them), then fan the
rest out to Sonnet/Haiku.

---

## Group A — Integrity info + FAQ  (user request 1 & 2)  ✅ COMPLETE

### A1 — Soften integrity mismatch to a blue "info" cue  🟢 Haiku  ✅ COMPLETE
**Goal:** A hash/size mismatch must never look alarming. Show a calm blue ℹ instead of an
amber ⚠, with messaging that the exe simply differs from what the profile/pack author
built for — mods may still work.

**File:** [Views/Subpages/ModManagerPage.xaml.cs](Views/Subpages/ModManagerPage.xaml.cs) — `RefreshIntegrityAsync` (~line 120), the `result.State switch`.

**Completed Changes:**
1. ✅ Changed `SizeMismatch` and `Md5Mismatch` arms to blue brush `Color.FromRgb(64, 156, 255)` with ℹ glyph and label `"ℹ Executable differs from this profile's expected version"`.
2. ✅ Set `Cust_txtIntegrityDetail` to reassuring line: `"Your game .exe doesn't match what this profile was built for. Mods may still work — this is just informational."`
3. ✅ Kept `Ok` → green ✓. Left `FileMissing` amber (it's actionable: the path isn't set).
4. ⏭️ "Learn more" link deferred until A2 lands.

**Gotcha:** the panel shows only when integrity is configured (`ExpectedExeBytes` or `AcceptedExeMd5s`). Don't change that gate.

### A2 — Write the FAQ guide  🔵 Sonnet  ✅ COMPLETE
**Goal:** A user-facing FAQ we can link to from inside the app.

**File:** `docs/FAQ.md` (created).

**Completed Content:**
- ✅ **Integrity checks** — what the ℹ "executable differs" cue means; why TMM never blocks deploys; size vs MD5 checks; when to act.
- ✅ **.tmmpack files** — what they bundle; how import targets the currently-selected game; collision renaming; export/import workflows.
- ✅ **Deploy, backup & rollback** — the deploy preview & conflict resolution flow; baseline capture; per-deploy backups; rollback limits; safe-deploy philosophy.
- ✅ **Mod types, routing, and conflicts** — routing rules and patterns; conflict detection and resolution; proxy DLL detection and routing.
- ✅ **Custom games & search hints** — the wizard 4-step flow (Essentials, Mod Types, Routing, Advanced, Review); how search hints auto-locate games.
- ✅ **Loadouts** — save/apply/compare/export/import; naming; favorites; organization.
- ✅ **Mod favorites and organization** — starring mods, search, properties; finding & sorting.
- ✅ **Mod import from existing folders** — scanning, candidate grouping, collision handling.
- ✅ **Activity feed** — recent action tracking (20-entry feed).
- ✅ **Where TMM keeps files** — `%APPDATA%\TMM\` breakdown with table: settings.json, ModsRaw, Backups, Baselines, Loadouts, CustomGames, TMM.log.
- ✅ **Supported games** — built-in list + custom games note.
- ✅ **Performance and storage** — mod storage, backup quota, log rotation.
- ✅ **Troubleshooting** — game path, deploy issues, integrity, backup/rollback failures.

**Source:** Audited from CHANGELOG.md, CLAUDE.md, codebase exploration (TmmPackBuilder, TmmPackInstaller, ProxyDllDetector, ActivityLogger, IntegrityChecker, ModImporter, etc.). Tone is plain-English, end-user-focused.

### A3 — Wire FAQ links in-app  🟢 Haiku  ✅ COMPLETE  *(depends on A2)*
1. ✅ Added "Help / Resources" section to AboutWindow.xaml with two link buttons:
   - "View FAQ" → `https://github.com/TheTriviali/TMM/blob/master/docs/FAQ.md`
   - "GitHub Repository" → `https://github.com/TheTriviali/TMM`
2. ✅ Added BtnFaq_Click and BtnGitHub_Click handlers in AboutWindow.xaml.cs using ShellHelper.OpenUrl().
3. ✅ Added "Learn more →" link to integrity panel in ModManagerPage.xaml (lines 300-315).
4. ✅ Added BtnIntegrityLearnMore_Click handler pointing to FAQ `#integrity-checks` anchor.
5. ✅ Build verified: no errors.

---

## Group B — Verbose notifications + Notifications tab  (user request 4)

> Do **NOTIF1 (Opus) first** — it sets the data model the other three build on.

### NOTIF1 — Notification history + verbose model  🟣 Opus  ✅ COMPLETE (2026-05-29)
**Goal:** Decide and build the storage/eventing model so notifications are (a) optionally
verbose, (b) browsable in bulk.

**Decisions locked + shipped:**
- Transient **toast queue** (`NotificationService.Queue`) kept as-is; auto-expiring behavior unchanged.
- New **persistent history** (`NotificationService.History`): in-memory `ObservableCollection`,
  newest-first, cap `HistoryCapacity = 500`. A tail of `PersistTail = 200` mirrors to
  `%APPDATA%\TMM\notifications.json` (rewritten on each record; loaded on `Initialize`).
- `NotificationItem` gained a `Source` string (free-form subsystem label). `NotificationType` reused as level.
- `Settings.VerboseNotifications` added (default `false`).
- `Initialize(appDataPath, Func<bool> verboseEnabled)` wired in `BackendCore` ctor after `LoadSettings`;
  the `Func<bool>` reads settings **live** so a runtime toggle (NOTIF2) takes effect with no restart.
- All UI-collection mutations marshal to `Application.Current.Dispatcher` → safe to call from background threads.

**Finished API for NOTIF2/3/4:**
```csharp
ObservableCollection<NotificationItem> NotificationService.Queue    // transient toasts
ObservableCollection<NotificationItem> NotificationService.History  // bind the tab here (newest-first)
bool NotificationService.IsVerbose                                  // live verbose state
void Show(message, type=Info, durationMs=3500, source="")           // toast + history
void ShowSuccess/ShowWarning/ShowError/ShowInfo(message, source="") // toast + history
void ShowVerbose(message, source, type=Info)                        // history always; toast only if IsVerbose
void ClearHistory()                                                 // empties history + persists
// NotificationItem { Message, Type, DurationMs, CreatedAt(UTC), Source }
```
**Verified:** build clean, 60/60 tests pass. NOTIF2/3/4 are now unblocked.

### NOTIF2 — Settings toggle  🔵 Sonnet  ✅ COMPLETE (2026-05-29)
Add a "Verbose notifications" switch to [Views/Subpages/SettingsPage.xaml(.cs)](Views/Subpages/SettingsPage.xaml) bound to `Settings.VerboseNotifications`, saving via `core.SaveSettings()`. Mirror an existing toggle. Add locale keys to `en-US.json` + `es-MX.json`.

**Completed:** Added "Notifications" section to SettingsPage between Appearance and Diagnostics. CheckBox (`chkVerboseNotifications`) loads from `_core.Settings.VerboseNotifications` in ctor; `ChkVerboseNotifications_Click` writes back and calls `_core.SaveSettings()`. Locale keys `Settings_Notifications`, `Settings_VerboseNotifications`, `Settings_VerboseNotifications_Desc` added to both en-US.json and es-MX.json. Build clean; 59/60 tests pass (1 pre-existing flaky concurrency failure in TmmPackInstallerTests, unrelated).

### NOTIF3 — Notifications tab/page  🔵 Sonnet  ✅ COMPLETE (2026-05-29)
New `Views/Subpages/NotificationsPage.xaml(.cs)`; wire into [Views/UnifiedShellWindow.xaml(.cs)](Views/UnifiedShellWindow.xaml.cs) with a left-nav button + a `ContentPresenter` placeholder, instantiated in `Window_Loaded` exactly like `pageBackupsPlaceholder`/`_pageBackups` (~lines 84-89). Page = scrollable, newest-first list (level icon + color, message, source, timestamp), level filter (All/Info/Success/Warning/Error), and a "Clear history" button bound to the NOTIF1 history.
**Gotcha:** don't duplicate the existing `ActivityFeedWindow` — leave it alone; note a future merge as follow-up.

**Completed:** `NotificationsPage.xaml(.cs)` — header (title + filter ComboBox + Clear button), scrollable `ItemsControl` bound to a `ListCollectionView` over `NotificationService.History`, DataTemplate with DataTrigger-driven level icon (Segoe MDL2 glyphs) + color + source subline + `LocalTimeDisplay` timestamp, empty-state panel. `navBtnNotifications` (&#xEA8F; ActionCenter icon) added to shell nav between Backups and the separator; `pageNotificationsPlaceholder` ContentPresenter added. `NavigateTo`/`SetNavActive` updated. `NotificationItem.LocalTimeDisplay` computed property added (HH:mm:ss today, MM/dd HH:mm older). Locale keys `Page_Notifications`, `Notifications_*` added to both locale files. Build clean; 60/60 tests pass. **Follow-up:** merge with ActivityFeedWindow (low priority).

### NOTIF4 — Instrument low-level operations  🔵 Sonnet  ✅ COMPLETE (2026-05-29)
Sprinkle `NotificationService.ShowVerbose(...)` at representative sites so verbose mode is genuinely informative: AppData subfolder `CreateDirectory` (BackendCore ctor, GetLoadoutsPath, Baselines, Backups), `SaveSettings`, plan freeze (`OnModAddedAsync`), baseline capture, backup prune, deploy/rollback start/finish, import steps. Terse messages (`"Created Backups/III/20260529_…"`). Toasts only when verbose is on (NOTIF1 handles that).

**Completed:** Added `ShowVerbose` at — BackendCore ctor: game ModsRaw subdirs (only when first created), DownloadCache, Backups; `SaveSettings`; `GetLoadoutsPath` (first create only); `OnModAddedAsync` plan freeze; `DeployFilesToGameDirAsync` start + finish (with backup count + timestamp); `RollbackDeployAsync` start + finish; `PruneOldBackups` per deleted dir; `ModImporter.ImportAsync` baseline seed, per-mod staging, completion. All use terse source-tagged messages. Build clean; 60/60 tests pass.

---

## Group C — Whole-program add/edit-game experience  (user request 3)

### WIZ1 — Whole-program add/edit-game: design + mockup approval  🟣 Opus  ✅ APPROVED (2026-05-29)
**Goal (revised per user, 2026-05-29):** Make adding/editing a game feel like a *full part of
the program*, not a cramped modal. Approved direction: a **dedicated shell tab** ("Add / Edit
Game", ✎ pencil icon in the left nav) at full shell scale, reusing the existing four
`IWizardStep` UserControls as stacked sections. Edit reuses the same tab, pre-filled, opened by
a ✎ pencil on each Library game card.

**Section default-state decision (user-confirmed 2026-05-29):** **Essentials expanded; the rest
are scroll-to anchors via the jump-rail — NOT collapsed accordions.** All four sections render
open in the scroller; the jump-rail `BringIntoView`s each one. WIZ2 is now unblocked.

**Mockup — full shell scale (~1100×720), hosted as a shell page:**

```
+-----------+-----------------------------------------------------------------+
|  TMM      |   Add a Game                                  Editing: (new)     |
| Library   |  +------------+                                                  |
| Mods      |  |* Essentials|  ESSENTIALS                                      |
| Backups   |  |o Mod Types |  Game name   [____________________________]      |
| Notifs    |  |o Routing   |  Install dir [_______________________] [Browse]  |
| Settings  |  |o Advanced  |  Executable  [_______________________] [Browse]  |
| > Add /   |  |o Review    |  Steam AppId [______]   Nexus [_____________]    |
|   Edit  <-|  |            |  [ check: directory found | integrity optional ] |
| (active)  |  | jump-rail; |                                                  |
|           |  | dots=done  |  MOD TYPES                            [+ add]     |
|           |  |            |  - ASI Plugin (.asi)                 [edit] [x]   |
|           |  |            |  - CLEO Script (.cs .cs4 .fxt)       [edit] [x]   |
|           |  |            |                                                  |
|           |  |            |  ROUTING RULES                        [+ rule]   |
|           |  |            |  > ModLoader Tree -> modloader\        (p95)      |
|           |  |            |  > .asi -> scripts\ if exists          (p80)      |
|           |  |            |  > Default -> game root                (p10)      |
|           |  |            |                                                  |
|           |  |            |  ADVANCED  overlay - companions - hints - integ  |
|           |  |            |  REVIEW    robustness - tag - native flag        |
|           |  +------------+                                                  |
|           |   live summary: "Ready - 2 mod types, 6 rules, integrity set"    |
|           |                                          [Cancel]  [Create]      |
+-----------+-----------------------------------------------------------------+
```

**Mechanics:**
- New shell page `Views/Subpages/AddGamePage.xaml(.cs)`, injected like `BackupsPage`
  (UnifiedShellWindow.xaml.cs ~84-89) with a ✎-pencil nav button.
- Hosts a `ScrollViewer` stacking the four existing step controls as sections — each already
  does `LoadProfile`/`SaveProfile`/`IsValid`/`ValidationChanged` (near-zero rework).
- Inner **jump-rail** (left of the form): anchors that `BringIntoView` each section, with a
  filled/empty dot for completion. **Not** gated steps — free navigation.
- No Next/Back. Single **Create** (or **Save** in edit mode), live-enabled when `Step1.IsValid`.
- Entry points: Library "➕ Add Game" → blank page; per-card ✎ pencil → page pre-filled (the
  wizard ctor already accepts an existing profile); `InitialSetupWindow.Option2_Click` routes here.
- **Default section state:** Essentials expanded; the rest as scroll-to sections.

**Alternative considered (not chosen):** a standalone shell-sized `Window`. Rejected — a real
nav tab feels more "part of the program" per the user's steer and avoids a second top-level window.

### WIZ2 — Implement the whole-program add/edit page  🔵 Sonnet  *(depends on WIZ1 approval)*  ✅ COMPLETE
Build `AddGamePage` per the mockup: stack the four step controls in a scroller; add the jump-rail
+ completion dots; single Create/Save with live validation (subscribe to each step's
`ValidationChanged`). Wire the ✎ nav tab + Library entry points (➕ button, per-card pencil),
route `InitialSetupWindow.Option2_Click` here, keep Edit-mode parity. Retire (or thin to a
launcher) the old `CustomGameSetupWizard` modal once the page covers add + edit.

**Completed (2026-05-29):**
- `Views/Subpages/AddGamePage.xaml(.cs)` — full-shell page with jump-rail (Essentials/Mod Types/Routing/Review), scrolling section stack, live summary bar, Create/Save + Cancel.
- `UnifiedShellWindow` — ✎ pencil nav button added, "AddGame" page injected + NavigateTo/SetNavActive wired.
- `GameCard` — `EditRequested` event added; `btnEdit` shown for custom (non-built-in GTA) games.
- `LibraryPage` — `EditGameRequested` event wired from GameCard.
- `InitialSetupWindow.Option2_Click` — sets `OpenAddGameAfterClose = true` and closes; shell navigates to AddGamePage after dialog.
- `CustomGameSetupWizard` modal kept for now (still used if accessed directly); AddGamePage replaces it as the primary UX.

---

## Group D — Carried-forward backlog (pre-existing, still open)

### D-B5 — Import review: split / merge / refine UI  🟣 Opus (design) → 🔵 Sonnet (build)  ✅ COMPLETE (2026-05-29)
The B5 importer ([Services/ModImporter.cs](Services/ModImporter.cs) + `ImportReviewWindow`) can
scan/select/exclude/rename but cannot **split** one detected candidate into several or **merge**
several into one.

**Key enabler (de-risks the whole brief):** `ModImporter.ImportAsync` is already *purely file-list
driven* — it iterates each `candidate.FilePaths` and moves them into a per-mod folder. So split/merge
is **entirely a review-window concern**: it is in-memory reshuffling of which `FilePaths` live in
which `ModImportCandidate`. **No changes to `ScanAsync`/`ImportAsync`/move logic.**

**Approved direction (Opus 4.8):** replace the single flat DataGrid with a **master-detail** layout.
Left = candidate list (the buckets). Right = the files inside the focused candidate, each individually
checkable. Split pulls checked files out into a new bucket; merge folds multiple buckets into one.
Button/menu-driven (no drag-drop — matches the project's robust, minimal-code-behind ethos).

**Mockup (~980×620, same window footprint):**

```
+------------------------------------------------------------------------+
| Review detected mod candidates                                         |
| Select what TMM should manage. Split a bundle apart, or merge related  |
| files into one mod.                                                    |
+--------------------------+---------------------------------------------+
| CANDIDATES               |  FILES IN "OpenAllInteriors"                |
| [x] OpenAllInteriors     |  [+ New mod from checked]  [Move checked ▾] |
|     3 files · scripts\   |  +---------------------------------------+  |
| [x] SkyGfx            ⚠  |  | [x] scripts\OpenAllInteriors.asi      |  |
|     2 files · (root)     |  | [x] scripts\OpenAllInteriors.ini      |  |
| [ ] ginput.ini           |  | [ ] scripts\unrelated_helper.cs       |  |
|     1 file  · (root)     |  +---------------------------------------+  |
|                          |                                             |
|                          |  Name   [OpenAllInteriors______________]    |
| [Merge selected ▾]       |  Group  [__________________]   ⚠ warning    |
| (Ctrl/Shift multi-select)|                                             |
+--------------------------+---------------------------------------------+
|                                          [Cancel]   [Import 2 mods]     |
+------------------------------------------------------------------------+
```

**Mechanics:**
- **Left pane** — `ListBox`/`DataGrid` of candidates. Each row: select `CheckBox` (`IsSelected`, drives
  import), name, `"{FileCount} files · {SourceSummary}"` subline, ⚠ glyph when `Warning != null`.
  Supports `Extended` selection (Ctrl/Shift) so multiple rows can be picked for **Merge**.
  Focused row drives the right pane.
- **Right pane** — list of the focused candidate's files (relative path), each with its own `CheckBox`
  (transient UI check, *not* `IsSelected`). Two actions over the checked files:
  - **+ New mod from checked** → **Split**: removes checked files from the focused candidate, creates a
    new `ModImportCandidate` containing them (auto-named from the first file's stem), selects+focuses it.
  - **Move checked ▾** → reassigns checked files into another existing candidate (menu lists the others).
- **Merge selected ▾** (left pane footer) → folds all left-pane-selected candidates into the first:
  concatenates `FilePaths` (dedup by path), keeps the first's `Name`/`GroupName`, drops the others.
- **Name / Group editors** below the file list edit the focused candidate (replaces today's inline
  DataGrid text editing — cleaner, and leaves room for the warning line).
- **Guardrails:** moving/splitting always removes files from the source so every file belongs to exactly
  one candidate; a candidate that reaches **0 files is auto-removed**; the Import button label reflects the
  count of `IsSelected` candidates and disables at 0.

**Model/VM deltas (the only code outside the window):**
- [Models/ModImportCandidate.cs](Models/ModImportCandidate.cs): implement `INotifyPropertyChanged`; make
  `FilePaths` an `ObservableCollection<string>` and raise `PropertyChanged` for `FileCount` on change; add
  a stable `Guid Id` (identity survives moves/menus). `Name`/`GroupName`/`IsSelected` raise change too.
- New tiny row VM `ImportFileRow { string RelativePath; string AbsolutePath; bool IsChecked }` for the
  right pane (window-private is fine).
- [Views/ImportReviewWindow.xaml(.cs)](Views/ImportReviewWindow.xaml.cs): the master-detail layout +
  `SplitCheckedFiles()`, `MoveCheckedFilesTo(target)`, `MergeSelected()`. Pure in-memory ops on the
  `ObservableCollection<ModImportCandidate>`. `RelativePath` for display = derive from `AbsolutePath`
  against the scanned `gameDir` (pass `gameDir` into the ctor; it's already known at the call site).

**Sonnet brief:** implement exactly the above. No `ModImporter` service changes. Localize new strings
(`en-US.json` + `es-MX.json`). Keep code-behind minimal; consider a thin VM but a window-level
`ObservableCollection` is acceptable here given the existing pattern. **Lower priority — the core import
path already works; this is refinement.**

**Completed:** `ModImportCandidate` upgraded to `INotifyPropertyChanged`; `FilePaths` changed to `ObservableCollection<string>` (JSON-safe); `Guid Id` added; `HasWarning`, `FileCountDisplay` computed properties added. Window-private `ImportFileRow` VM added (AbsolutePath, RelativePath, IsChecked). `ImportReviewWindow` fully rewritten as master-detail: left `ListBox` (Extended selection, per-candidate IsSelected checkbox, name, file count subline, ⚠ badge, AccentSoftBrush selection highlight) + right pane (file list, "+ New mod from checked" split, "Move checked ▾" context-menu move, Name/Group editors, warning badge). `MergeSelected()` in left-pane footer. `gameDir` passed from call site for relative-path display. Guardrails: split blocked if it would leave 0 files; auto-removes 0-file candidates; Import button shows count + disables at 0. Locale keys added to both locale files. Build clean; 60/60 tests pass.

### D-E2 — Proxy-DLL auto-routing hint  🔵 Sonnet  ✅ COMPLETE
`ProxyDllDetector` already flags proxy DLLs on install. E2: at plan time, when a detected proxy
DLL would otherwise route into `plugins/`/`scripts/`, hint/confirm routing to the **game root**
(where loaders must live). Plan-time check in [Services/DeploymentPlanner.cs](Services/DeploymentPlanner.cs)
using `ProxyDllDetector.IsKnownProxy`, surfaced in the deploy preview.

**Completed (2026-05-29):** Added check in `TryResolveFilePlan` — after the final destination is resolved, if `ProxyDllDetector.IsKnownProxy(fileName)` and the destination is a non-root subdirectory, a non-blocking `DeploymentWarning` is added. Warning shows in the existing `pnlWarnings` panel of `DeployPreviewWindow`.

### D-E3 — Multi-proxy version conflict  🔵 Sonnet  ✅ COMPLETE
Detect when two enabled mods ship the **same** proxy DLL (e.g. two `dinput8.dll`) and warn in the
conflict/preview UI — a load-order footgun, not a normal file conflict. Build on `ConflictAnalyzer`
(already groups by destination) + `ProxyDllDetector`.

**Completed (2026-05-29):** Added `ConflictAnalyzer.AnalyzeProxyConflicts` method — groups proxy DLL filenames across all plans and returns `ConflictEntry` per shared name. `DeployPreviewWindow` calls it and appends results to `icWarnings`. `_proxyConflicts` stored as separate field; `txtBlockingNote` only shown for actually-blocking rows.

### D-O2r — Fold built-in QuickScan onto SearchHints  🔵 Sonnet  ✅ COMPLETE
`BackendCore.QuickScan`'s built-in GTA branch still uses hardcoded Steam roots; custom games now
use `SearchHints`. Migrate the built-in GTA `.tmmgame` profiles' `searchHints` (already populated)
into the scan and retire the hardcoded `commonRoots`. **Gotcha:** preserve the IV-family
episode-nesting logic (TLaD/TBoGT inside the IV folder) and the `Settings.GamePaths` write path
for built-ins. Low reward (it already works) — do last, carefully.

**Completed (2026-05-29):** Added `searchHints` to `gtaiv.tmmgame`, `gtatlad.tmmgame`, `gtatbogt.tmmgame` (III/VC/SA already had them). Added `BackendCore.ScanBuiltInsBySearchHints()` using `GetBuiltInCustomGames()` + `SetVanillaPath` (which auto-derives TLaD/TBoGT when IV is found). Called in `QuickScan()` before the legacy `GameProfile.All` loop — found games are skipped by the old loop. Old hardcoded roots kept as fallback (not retired, per "carefully" note).

---

## Group E — Codebase health

### AUDIT1 — File-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ IN PROGRESS (Inventory phase)
**Goal:** Keep the codebase from sprawling as features land. Periodic inventory + flag
consolidation/splitting opportunities.

**Baseline (2026-05-29):** 139 tracked files — **76 `.cs`**, **26 `.xaml`**, **11 `.tmmgame`**, 7 project `.md`. 

**Updated Inventory (2026-05-29, post-A1/A2/A3/F1-F5):**
- **Total source files:** 78 `.cs` (148 including generated), 26 `.xaml`, 11 `.tmmgame`, 12 `.md`
- **Tests:** 60 passing (added `TmmGameOptionsTests.cs` + `BuiltInProfilesTests.cs` this session)
- **Folders:** Services (17 .cs), Models (14 .cs), Views (15+28 .cs), Steps (8+4 .cs), Subpages (12+6 .cs), Converters (4), Helpers (4+1), Theming (2), TMM.Tests (8)

**Top 20 largest source files (excluding generated .g.cs):**

| File | Lines | Notes |
|------|-------|-------|
| ModManagerPage.xaml.cs | 1,160 | **SPLIT CANDIDATE** — deploy/loadouts/import/integrity/groups/sidebar |
| BackendCore.cs | 1,033 | **SPLIT CANDIDATE** — deploy/rollback/baselines/loadouts/settings/initialization |
| LibraryPage.xaml.cs | 578 | — |
| UnifiedShellWindow.xaml.cs | 472 | — |
| DeploymentPlanner.cs | 414 | — |
| DeploymentPlannerTests.cs | 361 | — |
| BackendCoreDeployTests.cs | 359 | — |
| RuleEngineTests.cs | 333 | — |
| ModImporter.cs | 292 | — |
| GameCard.xaml.cs | 291 | — |
| Step3_RoutingRulesPage.xaml.cs | 274 | — |
| GameRegistry.cs | 273 | — |
| DownloadsPage.xaml.cs | 251 | — |
| Step1_GameDetailsPage.xaml.cs | 232 | — |
| TmmGameConfig.cs | 195 | — |
| BackupsPage.xaml.cs | 185 | — |
| RuleEditorWindow.xaml.cs | 180 | — |
| CustomGameSetupWizard.xaml.cs | 173 | Dead/replaced by WIZ2? **Confirm** |
| LoadOrderResolverTests.cs | 172 | — |
| RuleEngine.cs | 164 | — |

**Observations:**
- **ModManagerPage.xaml.cs (1,160 lines):** Exceeds 800-line threshold. Mixes: sidebar logic, integrity display, deploy UI, conflict resolver, loadouts, import UI, group management. **Candidate for `partial class` split:**
  - `Sidebar.cs` — game/path/integrity display, links, disk space
  - `Deploy.cs` — preview, conflict resolution, deployment flow
  - `Loadouts.cs` — loadout UI and operations
  - `Import.cs` — import candidate display and flow
  - Keep main `ModManagerPage.xaml.cs` — mod list/grid, core events

- **BackendCore.cs (1,033 lines):** Monolithic service. Mixes: deploy/rollback pipeline, settings load/save, mod list management, baselines, backups, integrity, loadouts, activity logging, game registry. **Candidate for `partial class` split:**
  - `Deploy.cs` — deployment/rollback/plan execution
  - `Backups.cs` — backup/baseline management
  - `Loadouts.cs` — loadout I/O
  - `Settings.cs` — settings load/save
  - Keep main `BackendCore.cs` — initialization, mod list, core state

- **Orphaned files (post-FirstGamePickerWindow deletion):** None detected; deletion was clean.

- **Thin/single-use files:** None identified; even small files serve clear purposes.

**Next steps (Opus judgment needed):**
1. Confirm split strategy above (esp. BackendCore / ModManagerPage) aligns with user intent.
2. If approved: split as `partial class` in new files, one PR per target file.
3. Update references if any explicit cross-file dependencies emerge.

**Gotcha:** WPF code-behind splits must keep `partial class` + the XAML `x:Class` intact; don't move `InitializeComponent` wiring. Split only where it reduces real cognitive load — never for its own sake.

---

## Group F — Factory-reset / first-run bug fixes  ✅ COMPLETE (2026-05-29)

All five issues reported after a factory reset. Full detail in CHANGELOG [Unreleased].

| Brief | Fix |
|-------|-----|
| F1 🟢 | `JsonHelper.TmmGameOptions` lacked `JsonStringEnumConverter` → 6 GTA profiles silently dropped. Added converter; `TmmGameOptionsTests` regression. |
| F2 🟢 | Welcome-window left panel had 4 hardcoded English literals; swapped to `{helpers:Localization}`. Added keys to both locale files. |
| F3 🔵 | `SelectBuiltinGameWindow.BtnDone` gated on `IsGameReady` — permanently disabled on fresh machines. Done now always enabled; paths set later from Library. |
| F4 🟢 | `"Directory not set"` and open-folder `MessageBox` in `ModManagerPage` hardcoded; replaced with `LocalizationService` lookups + new locale keys. |
| F5 🔵 | Skyrim/FNV/Cyberpunk/RDR2/Witcher 3 used flat `extensionPattern`/`destination` schema → empty `RoutingRule` objects. Rewrote all 5 to condition-based schema; `BuiltInProfilesTests` regression (3 cases, all 11 profiles). |

**Pending follow-up (not a bug, low priority):** `"(not set)"`/`"(none)"` literals in `Step4_ReviewPage.xaml.cs` are still hardcoded English — benign in review-only context but worth localizing eventually.
