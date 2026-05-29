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

### NOTIF1 — Notification history + verbose model  🟣 Opus
**Goal:** Decide and build the storage/eventing model so notifications are (a) optionally
verbose, (b) browsable in bulk. **Lock these decisions:**
- Separate the transient **toast queue** (auto-expiring, already in `NotificationService.Queue`) from a **persistent history** the tab reads.
- Recommended: in-memory ring (cap ~500) in `NotificationService`, *plus* a persisted tail (~200) at `%APPDATA%\TMM\notifications.json` so history survives restarts (rotation feel like `Logger`).
- Add a `source` string + reuse `NotificationType` as level. Every `Show*` records to history; a new `ShowVerbose(msg, source)` records always but raises a toast only when `Settings.VerboseNotifications` is true.

**Files:** [Services/NotificationService.cs](Services/NotificationService.cs), [Models/AppSettings.cs](Models/AppSettings.cs).
**Deliverable:** extended `NotificationService` API + persistence + `AppSettings.VerboseNotifications` (default `false`). Hand NOTIF2/3/4 the finished API.

### NOTIF2 — Settings toggle  🔵 Sonnet  *(depends on NOTIF1)*
Add a "Verbose notifications" switch to [Views/Subpages/SettingsPage.xaml(.cs)](Views/Subpages/SettingsPage.xaml) bound to `Settings.VerboseNotifications`, saving via `core.SaveSettings()`. Mirror an existing toggle. Add locale keys to `en-US.json` + `es-MX.json`.

### NOTIF3 — Notifications tab/page  🔵 Sonnet  *(depends on NOTIF1)*
New `Views/Subpages/NotificationsPage.xaml(.cs)`; wire into [Views/UnifiedShellWindow.xaml(.cs)](Views/UnifiedShellWindow.xaml.cs) with a left-nav button + a `ContentPresenter` placeholder, instantiated in `Window_Loaded` exactly like `pageBackupsPlaceholder`/`_pageBackups` (~lines 84-89). Page = scrollable, newest-first list (level icon + color, message, source, timestamp), level filter (All/Info/Success/Warning/Error), and a "Clear history" button bound to the NOTIF1 history.
**Gotcha:** don't duplicate the existing `ActivityFeedWindow` — leave it alone; note a future merge as follow-up.

### NOTIF4 — Instrument low-level operations  🔵 Sonnet  *(depends on NOTIF1)*
Sprinkle `NotificationService.ShowVerbose(...)` at representative sites so verbose mode is genuinely informative: AppData subfolder `CreateDirectory` (BackendCore ctor, GetLoadoutsPath, Baselines, Backups), `SaveSettings`, plan freeze (`OnModAddedAsync`), baseline capture, backup prune, deploy/rollback start/finish, import steps. Terse messages (`"Created Backups/III/20260529_…"`). Toasts only when verbose is on (NOTIF1 handles that).

---

## Group C — Whole-program add/edit-game experience  (user request 3)

### WIZ1 — Whole-program add/edit-game: design + mockup approval  🟣 Opus
**Goal (revised per user, 2026-05-29):** Make adding/editing a game feel like a *full part of
the program*, not a cramped modal. Approved direction: a **dedicated shell tab** ("Add / Edit
Game", ✎ pencil icon in the left nav) at full shell scale, reusing the existing four
`IWizardStep` UserControls as stacked sections. Edit reuses the same tab, pre-filled, opened by
a ✎ pencil on each Library game card. **Get final approval before WIZ2.**

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

### WIZ2 — Implement the whole-program add/edit page  🔵 Sonnet  *(depends on WIZ1 approval)*
Build `AddGamePage` per the mockup: stack the four step controls in a scroller; add the jump-rail
+ completion dots; single Create/Save with live validation (subscribe to each step's
`ValidationChanged`). Wire the ✎ nav tab + Library entry points (➕ button, per-card pencil),
route `InitialSetupWindow.Option2_Click` here, keep Edit-mode parity. Retire (or thin to a
launcher) the old `CustomGameSetupWizard` modal once the page covers add + edit.

---

## Group D — Carried-forward backlog (pre-existing, still open)

### D-B5 — Import review: split / merge / refine UI  🟣 Opus → 🔵 Sonnet
The B5 importer ([Services/ModImporter.cs](Services/ModImporter.cs) + `ImportReviewWindow`) can
scan/select/exclude/rename but cannot **split** one detected candidate into several or **merge**
several into one. Needs UX design (Opus) then implementation (Sonnet). Lower priority — the core
import path works.

### D-E2 — Proxy-DLL auto-routing hint  🔵 Sonnet
`ProxyDllDetector` already flags proxy DLLs on install. E2: at plan time, when a detected proxy
DLL would otherwise route into `plugins/`/`scripts/`, hint/confirm routing to the **game root**
(where loaders must live). Plan-time check in [Services/DeploymentPlanner.cs](Services/DeploymentPlanner.cs)
using `ProxyDllDetector.IsKnownProxy`, surfaced in the deploy preview.

### D-E3 — Multi-proxy version conflict  🔵 Sonnet
Detect when two enabled mods ship the **same** proxy DLL (e.g. two `dinput8.dll`) and warn in the
conflict/preview UI — a load-order footgun, not a normal file conflict. Build on `ConflictAnalyzer`
(already groups by destination) + `ProxyDllDetector`.

### D-O2r — Fold built-in QuickScan onto SearchHints  🔵 Sonnet
`BackendCore.QuickScan`'s built-in GTA branch still uses hardcoded Steam roots; custom games now
use `SearchHints`. Migrate the built-in GTA `.tmmgame` profiles' `searchHints` (already populated)
into the scan and retire the hardcoded `commonRoots`. **Gotcha:** preserve the IV-family
episode-nesting logic (TLaD/TBoGT inside the IV folder) and the `Settings.GamePaths` write path
for built-ins. Low reward (it already works) — do last, carefully.

---

## Group E — Codebase health

### AUDIT1 — File-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ IN PROGRESS (Inventory phase)
**Goal:** Keep the codebase from sprawling as features land. Periodic inventory + flag
consolidation/splitting opportunities.

**Baseline (2026-05-29):** 139 tracked files — **76 `.cs`**, **26 `.xaml`**, **11 `.tmmgame`**, 7 project `.md`. 

**Updated Inventory (2026-05-29, post-A1/A2/A3):**
- **Total source files:** 76 `.cs` (146 including generated), 26 `.xaml`, 11 `.tmmgame`, 12 `.md`
- **Total size:** ~1.62 MB (source only)
- **Folders:** Services (17 .cs), Models (14 .cs), Views (15+28 .cs), Steps (8+4 .cs), Subpages (12+6 .cs), Converters (4), Helpers (4+1), Theming (2), TMM.Tests (6)

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
