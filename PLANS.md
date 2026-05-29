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

**My opinion (Opus 4.8):** delegation is worth it here. The bulk of this work is
mechanical or well-specified (🟢/🔵). Only three pieces genuinely need Opus: the
notification-store design (NOTIF1), the one-window wizard layout/mockup (WIZ1), and the
B5 import-refinement UX. Do those 🟣 briefs first (they unblock the 🔵 implementation
briefs that depend on them), then fan the rest out to Sonnet/Haiku. Suggested order is
noted per group.

---

## Group A — Integrity info + FAQ  (from user request 1 & 2)

### A1 — Soften integrity mismatch to a blue "info" cue  🟢 Haiku
**Goal:** A hash/size mismatch must never look alarming. Show a calm blue ℹ instead of an
amber ⚠, with messaging that the exe simply differs from what the profile/pack author
built for — mods may still work.

**File:** [Views/Subpages/ModManagerPage.xaml.cs](Views/Subpages/ModManagerPage.xaml.cs) — `RefreshIntegrityAsync` (~line 120), the `result.State switch`.

**Steps:**
1. Change the `SizeMismatch` and `Md5Mismatch` arms to a blue brush (e.g. `Color.FromRgb(0x40, 0x9C, 0xFF)`) and an ℹ glyph + soft label, e.g. `"ℹ Executable differs from this profile's expected version"`.
2. Set `Cust_txtIntegrityDetail` to a reassuring line: `"Your game .exe doesn't match what this profile was built for. Mods may still work — this is just informational."`
3. Keep `Ok` → green ✓. Leave `FileMissing` amber (it's actionable: the path isn't set).
4. No XAML change needed (the existing `Cust_IntegrityBorder` already holds state + detail text). Optionally add a "Learn more" `TextBlock`/link that calls the same browser-open helper used by the sidebar links, pointing at the FAQ integrity anchor (see A3) — **defer the link until A2 lands.**

**Gotcha:** the panel is shown only when integrity is configured (`ExpectedExeBytes` or `AcceptedExeMd5s`). Don't change that gate.

### A2 — Write the FAQ guide  🔵 Sonnet
**Goal:** A user-facing FAQ we can link to from inside the app.

**File:** new `docs/FAQ.md` (create the `docs/` folder).

**Sections (use `##` headings with stable anchors):**
- **Integrity checks** (`#integrity`) — what the ℹ "executable differs" cue means; why TMM never blocks deploys on it; downgrader variants; that it's warn-only.
- **`.tmmpack` files** — what they bundle (loadout + mod sources), how export/import works, that import targets the *currently selected* game (not the pack's original), collision renaming.
- **Deploy, backup & rollback** — direct-deploy model, the first-touch baseline, what rollback restores, where backups live (`%APPDATA%\TMM\Backups`).
- **Custom games & search hints** — how the wizard works, what search hints do (auto-locate a shared profile on another PC), that users never edit JSON.
- **Loadouts** — snapshots of enabled-state + order; apply/compare/export.
- **Where TMM keeps files** — settings, mods, backups, baselines, loadouts paths.

Pull facts from [CHANGELOG.md](CHANGELOG.md) and [CLAUDE.md](CLAUDE.md). Keep it plain-English, end-user voice (not developer notes).

### A3 — Wire FAQ links in-app  🟢 Haiku  *(depends on A2)*
**Goal:** Make the FAQ reachable.

**Steps:**
1. Add a "Help / FAQ" entry to [Views/AboutWindow.xaml](Views/AboutWindow.xaml) that opens the FAQ. Until the repo ships docs with the app, link to the GitHub blob URL: `https://github.com/TheTriviali/TMM/blob/master/docs/FAQ.md` (open via `Process.Start` with `UseShellExecute = true`, same pattern as the sidebar `BtnLink_Click`).
2. Add the deferred "Learn more" link from A1 → FAQ `#integrity` anchor.

---

## Group B — Verbose notifications + Notifications tab  (from user request 4)

> Do **NOTIF1 (Opus) first** — it sets the data model the other three build on.

### NOTIF1 — Notification history + verbose model  🟣 Opus
**Goal:** Decide and build the storage/eventing model so notifications are (a) optionally
verbose, (b) browsable in bulk. **Design decisions to lock:**
- Separate the transient **toast queue** (auto-expiring, already in `NotificationService.Queue`) from a **persistent history** the tab reads.
- Recommended: keep an in-memory ring (cap ~500) in `NotificationService`, *plus* persist a smaller tail (e.g. 200) to `%APPDATA%\TMM\notifications.json` so history survives restarts. Reuse the rotation feel of `Logger`.
- Add a `category`/`source` string + reuse `NotificationType` for level. Every `Show*` call records to history; a new `ShowVerbose(msg, source)` records always but only raises a toast when `Settings.VerboseNotifications` is true.

**Files:** [Services/NotificationService.cs](Services/NotificationService.cs), [Models/AppSettings.cs](Models/AppSettings.cs).

**Deliverable:** the extended `NotificationService` API + persistence + `AppSettings.VerboseNotifications` (default `false`). Hand NOTIF2/3/4 the finished API.

### NOTIF2 — Settings toggle  🔵 Sonnet  *(depends on NOTIF1)*
Add a "Verbose notifications" switch to [Views/Subpages/SettingsPage.xaml(.cs)](Views/Subpages/SettingsPage.xaml) bound to `Settings.VerboseNotifications`, saving via `core.SaveSettings()`. Mirror an existing SettingsPage toggle. Add locale keys to `en-US.json` + `es-MX.json`.

### NOTIF3 — Notifications tab/page  🔵 Sonnet  *(depends on NOTIF1)*
**Goal:** A dedicated nav page that browses the full notification history.

**Files:** new `Views/Subpages/NotificationsPage.xaml(.cs)`; wire into [Views/UnifiedShellWindow.xaml(.cs)](Views/UnifiedShellWindow.xaml.cs).

**Steps:** add a left-nav button + a `ContentPresenter` placeholder and instantiate the page in `Window_Loaded` exactly like `pageBackupsPlaceholder` / `_pageBackups` (UnifiedShellWindow.xaml.cs ~lines 84-89). Page shows a scrollable, newest-first list (level icon + color, message, source, timestamp), a level filter (All/Info/Success/Warning/Error), and a "Clear history" button. Bind to the NOTIF1 history collection.

**Gotcha:** this supersedes the existing `ActivityFeedWindow` for browsing — leave ActivityFeed alone for now; just don't duplicate. Note any future merge as a follow-up.

### NOTIF4 — Instrument low-level operations  🔵 Sonnet  *(depends on NOTIF1)*
Sprinkle `NotificationService.ShowVerbose(...)` at representative low-level sites so verbose mode is genuinely informative: `Directory.CreateDirectory` of AppData subfolders (BackendCore ctor, GetLoadoutsPath, Baselines, Backups), `SaveSettings`, plan freeze (`OnModAddedAsync`), baseline capture, backup prune, deploy/rollback start/finish, import steps. Keep messages terse (`"Created Backups/III/20260529_…"`). Don't toast these unless verbose is on (that's NOTIF1's job).

---

## Group C — One-window custom game adder  (from user request 3)

### WIZ1 — Single-window add-game: design + mockup approval  🟣 Opus
**Goal:** Replace the 4-step Next/Back wizard with one scrolling window. **Present the
mockup below to the user and get approval before WIZ2.**

**Proposed layout** (reuses the existing four `IWizardStep` UserControls as section bodies —
minimal rework):

```
┌──────────────────────────────────────────────────────────────┐
│  Add a Game                                              [✕]   │
├────────────────┬─────────────────────────────────────────────┤
│  Essentials   ›│   ESSENTIALS                                  │
│  Mod Types    ›│   Name        [______________________]        │
│  Routing      ›│   Install dir [________________] [Browse]     │
│  Advanced     ›│   Executable  [________________] [Browse]     │
│  Review       ›│   Steam AppId [______]  Nexus [__________]    │
│                │                                               │
│  (left rail =  │   ▸ MOD TYPES                    (section)    │
│   jump anchors,│   ▸ ROUTING RULES                (section)    │
│   NOT gated    │   ▸ ADVANCED  overlay · companion ·           │
│   steps; click │      search hints · integrity    (section)   │
│   to scroll)   │   ▸ PROFILE   robustness · tag · native       │
│                │                                               │
├────────────────┴─────────────────────────────────────────────┤
│  ⓘ live validation summary…              [Cancel] [Create ✓]  │
└──────────────────────────────────────────────────────────────┘
```

- One `ScrollViewer` stacking the four existing step `UserControl`s (each already does
  `LoadProfile`/`SaveProfile`/`IsValid`/`ValidationChanged`).
- Left rail = anchor links that `BringIntoView` each section — **not** gated steps.
- No Next/Back. A single **Create** button, enabled live when `Step1.IsValid` (the only
  step with required fields). Aggregate `SaveProfile` across all four on Create.
- The window keeps working as **Edit** too (constructor already takes an existing profile).

**Open question for the user:** keep the collapsible sections collapsed-by-default
(compact, more scrolling) or expanded (everything visible at once)? Recommend
**Essentials expanded, the rest collapsed** for first-run approachability.

### WIZ2 — Implement the single-window adder  🔵 Sonnet  *(depends on WIZ1 approval)*
Build the new host (either rework [Views/CustomGameSetupWizard.xaml(.cs)](Views/CustomGameSetupWizard.xaml.cs) in place or add `Views/AddGameWindow`). Reuse the four step controls as section bodies; drop the step counter/Next/Back; add the anchor rail + single Create with live validation (subscribe to each step's `ValidationChanged`). Update the two call sites that open the wizard (`InitialSetupWindow.Option2_Click`, and wherever Library "+ add game" opens it). Keep Edit-mode parity.

---

## Group D — Carried-forward backlog (pre-existing, still open)

### D-B5 — Import review: split / merge / refine UI  🟣 Opus → 🔵 Sonnet
The B5 importer ([Services/ModImporter.cs](Services/ModImporter.cs) + `ImportReviewWindow`) can
scan/select/exclude/rename but cannot **split** one detected candidate into several or
**merge** several into one. Needs UX design (Opus) then implementation (Sonnet). Lower
priority — the core import path works.

### D-E2 — Proxy-DLL auto-routing hint  🔵 Sonnet
`ProxyDllDetector` already flags proxy DLLs on install. E2: at plan time, when a detected
proxy DLL would otherwise route into `plugins/`/`scripts/`, hint/confirm routing to the
**game root** (where loaders must live). Implement as a plan-time check in
[Services/DeploymentPlanner.cs](Services/DeploymentPlanner.cs) using `ProxyDllDetector.IsKnownProxy`,
surfaced in the deploy preview.

### D-E3 — Multi-proxy version conflict  🔵 Sonnet
Detect when two enabled mods ship the **same** proxy DLL (e.g. two `dinput8.dll`) and warn
in the conflict/preview UI — it's a load-order footgun, not a normal file conflict. Build on
`ConflictAnalyzer` (already groups by destination) + `ProxyDllDetector`.

### D-O2r — Fold built-in QuickScan onto SearchHints  🔵 Sonnet
`BackendCore.QuickScan`'s built-in GTA branch still uses hardcoded Steam roots; custom games
now use `SearchHints`. Migrate the built-in GTA `.tmmgame` profiles' `searchHints` (already
populated) into the scan and retire the hardcoded `commonRoots`. **Gotcha:** preserve the
IV-family episode-nesting logic (TLaD/TBoGT inside the IV folder) and the `Settings.GamePaths`
write path for built-ins. Low reward (it already works) — do last, carefully.
