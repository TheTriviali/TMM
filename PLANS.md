# TMM — Active Plans

> Living plan doc. Each section is a self-contained brief a cold agent can execute.
> Older routing-rules-refactor plans are archived (Phases 3/5/6 complete; see memory).
>
> **Standing rule:** Any feature that works for built-in games must be fully
> configurable by a user adding a custom game through the wizard UI. No field or
> behaviour is complete until it appears in `CustomGameSetupWizard` Step 1 (input)
> and Step 4 (review summary). The `.tmmgame` JSON is only a shortcut for built-in
> profiles — users never edit JSON.

---

## Status snapshot — 2026-05-27

**Just done (this session, Opus):**
- Audited every interactive UI element, found ~25 bugs / non-sequiturs / dead code paths.
- Fixed cheap stuff inline (empty Segoe glyphs in DownloadsPage, dead AboutWindow handlers,
  no-op `if (_isEdit)` in CustomGameSetupWizard, dead `LastSelectedGameKey`, hidden no-op
  Edit buttons on GameCard, hardcoded "Black" foreground on SelectBuiltinGameWindow, dead
  locale keys `GridView/LargeView/ListenView/ShowcaseView`, hardcoded "v2.0" strings,
  removed `task1_prompt.txt`).
- **B2 progress (install-time plan freeze):**
  - Mod metadata now also writes `_tmm/modinfo.json` alongside the legacy `modinfo.txt`, so the new frozen plan sidecar has a stable home next to the mod info.
  - Added `DeploymentPlan.PlanVersion` plus plan save/load helpers in `BackendCore`.
  - Persisted frozen plans to `ModsRaw_{key}/{ModName}/_tmm/deployplan.json`.
  - Custom-game deploy preview now loads persisted plans and only falls back to live planning for legacy mods.
  - Custom mod installs now capture a frozen plan immediately; refresh backfills missing plans.
  - Routing-rule edits in the custom-game wizard now warn about stale plans and offer an explicit replan-all prompt.
  - Direct deploy now records empty directories too, and plan-time layout overrides now cover overlay folders, `modloader` trees, grouped modloader paths, and CLEO companion `.ini` files.
  - Custom game wizard Step 1/Step 4 now surface overlay-folder and companion-sibling metadata.
- **B3 progress (first-touch baseline):**
  - Added `Services/BaselineSnapshot.cs` for per-game baseline capture and storage.
  - Deploy now captures first-touch state before overwriting files.
  - Rollback now reads baseline snapshots first, then falls back to the legacy per-deploy manifest backup if a baseline record is missing.
  - Added a regression test for stacked deploys restoring the original baseline instead of the previous mod state.
- **B4 progress (overlay / empty-dir / symlink hardening):**
  - Overlay-folder routing and group-aware `modloader` layout are already live in the planner.
  - Deploy now records created directories in the rollback manifest and rollback removes empty directories when they are no longer needed.
  - Install-time planning now fails loudly on symlinked mod sources instead of flattening them into the deploy plan.
- **B5 progress (sync/import from existing install):**
  - Added a `ModImporter` scan/import service plus an `ImportReviewWindow` so TMM can detect obvious mod files in an existing install and let the user approve them before moving anything.
  - Import now seeds the first-touch baseline before moving files into `ModsRaw_{key}`.
  - Imported mods are written back through the normal plan/deploy pipeline so the install state is restored after TMM takes ownership.
- **GTA III/VC/SA deprecated cleanup pass:**
  - Deleted unreachable `Views/ArchiveExtractionWindow.xaml(.cs)`.
  - Stripped MD5/vanilla detection: `GameProfile.Vanilla10Md5` /`AdditionalValidMd5s`
    /`HasExeCheck`/`IsValidMd5`/`AllValidMd5s`, `ExeStatus` enum, `BackendCore`
    `VerifyGameStatusAsync` /`GetEffectiveMd5Async`/`GetMd5DiagnosticsAsync`
    /`HasExeModOverride`/`ToggleDeployOverride`/`FindExeInMod`/`GetFileMD5Async`,
    `AppSettings.DeployOverrides`, the III/VC/SA defaults in `AppSettings.GamePaths`,
    `Settings_SteamControls/Settings_MD5*/Settings_SteamVerify/Settings_VerifyTooltip
    /GameSetupRow_SteamAPI` locale keys, the Steam Controls + MD5 Check panels in
    SettingsPage, and the "Steam API Detected (1.0 Downgrade Required)" status path
    in GameSetupRow.
  - Removed unused `BackendCore.Version` field.
  - Canonicalised repo URL to `https://github.com/TheTriviali/TMM`.
- Build is clean (only 3 pre-existing CS0067 warnings on `IWizardStep.ValidationChanged`
  for Steps 2-4, which is correct — they implement the interface but don't validate).

---

## Block B — Sync/import + architectural foundations  (active)

**Approved 2026-05-27** with these decisions:
- **Import ownership:** files move into `ModsRaw_{key}` so post-import everything uses the normal deploy/rollback paths.
- **Companion grouping scope:** same folder + known per-game sibling folders (e.g. `CLEO_TEXT/`, `CLEO_FONTS/` for CLEO scripts). Per-game sibling list lives in the `.tmmgame` profile.
- **Mod groups:** nested deployment targets — adding mod X to group "Cars" auto-routes its files under `modloader\Cars\X\`.
- See [CLAUDE.md → Architectural Principles](CLAUDE.md) for the two rules these briefs assume (install-time plan freeze + first-touch baseline). Both audited 2026-05-27: install-time freeze is NOT current behaviour (must build); first-touch baseline does NOT exist (must build).

**Critical path:** B2 → B3 → B4 → B5. B1 and B6 can branch off independently once B2/B3 land.

### B2 — Install-time deployment plan freeze  (foundation)

**Goal:** Replace per-deploy `PlanDeploymentAsync` calls with a one-time install-time capture + saved-plan execution.

**Current behaviour (audited):** [BackendCore.cs:614](Services/BackendCore.cs#L614) and [ModManagerPage.xaml.cs:285-290](Views/Subpages/ModManagerPage.xaml.cs#L285) both call `new DeploymentPlanner().PlanDeploymentAsync(...)` fresh on every deploy. Nothing is persisted.

**Plan:**
1. Make `DeploymentPlan` (in `Services/DeploymentPlanner.cs`) JSON-serialisable. Add `int PlanVersion = 1` for future migrations.
2. Add `BackendCore.OnModAddedAsync(string gameKey, string modName)` that calls the planner once and writes the result to `ModsRaw_{key}/{ModName}/_tmm/deployplan.json`. Wire it from *every* mod-add entry point: archive extraction (~BackendCore.cs:794), wizard import flow, manual folder-scan refresh, and B5's import path.
3. Change `DeployModsAsync` to load `deployplan.json` per mod. If missing, fall back to live planning *and* log a warning (legacy path until all mods are migrated).
4. Profile-edit invalidation: when a user edits routing rules in the wizard, surface a "N existing mods have stale plans — replan all?" prompt. Don't auto-replan (silent rule changes are exactly what principle #1 forbids).

**Gotcha:** identify *all* mod-add paths before wiring `OnModAddedAsync` — search `_core.Mods.Add`, `RefreshAllModListsAsync`, archive extraction, drag-drop handlers. Missing one means some mods never get a plan.

**Implemented so far:** `DeploymentPlan` persistence is in place, custom-game deploy uses frozen plans, custom installs capture plans immediately, refresh backfills missing plans, routing-rule edits prompt for a mass replan, mod metadata now has a `_tmm/modinfo.json` sidecar, the planner now knows about overlay folders, `modloader` trees, grouped modloader paths, empty directories, and CLEO companion `.ini` files, and the wizard exposes overlay/companion metadata. Remaining work is wiring any other mod-add paths and the bigger B3/B4/B5 foundations.

### B3 — First-touch baseline snapshot  (foundation)

**Goal:** Capture the original bytes of any game file the first time TMM touches it. Rollback restores to that, not to the previous deploy.

**Why now:** audited 2026-05-27. Today's rollback restores from per-deploy manifests, which means stacking mods loses the vanilla baseline (Mod B's deploy backs up Mod A's modifications, not vanilla). See PLANS.md audit notes / agent transcript.

**Files:**
- New `Services/BaselineSnapshot.cs` — encapsulates per-game baseline read/write.
- Storage: `%APPDATA%\TMM\Baselines\{gameKey}\baseline.json` + `\snapshots\{sha256-of-relativePath}.bin`.
- `Services/BackendCore.cs:684-ish` — insert baseline capture before any overwrite.
- `Services/BackendCore.cs:471-501` — rollback now reads `baseline.json` as source of truth.

**Plan:**
1. Schema: `baseline.json` = `{ "<relativePath>": { "snapshotFile": "<hash>.bin"|null, "originalSize": N, "capturedAt": "ISO8601" } }`. `snapshotFile = null` means "file did not exist at first touch" (rollback = delete).
2. On deploy, before overwriting `destFile`: if path not in baseline, snapshot current bytes (or record null if file didn't exist), then proceed with normal per-deploy backup *as well* (per-deploy manifest remains a secondary index of "what this deploy did").
3. Rollback semantics shift: per-deploy manifest tells you *which files to revert*; baseline.json tells you *what to revert them to*. Document this prominently in the rollback confirmation MessageBox and in CHANGELOG.
4. B5 (sync/import) explicitly seeds baseline.json by snapshotting the full game dir at import time — that IS the import's "first touch."

**Gotcha:** disk space. A baseline of a heavily-modded GTA SA could be several GB. Consider an opt-out for very large directories (warn user) or hash-based dedup (probably overkill for v1).

**Implemented so far:** baseline capture is live in `BackendCore`, rollback prefers the baseline store and only falls back to per-deploy backups when needed, and the deploy integration now has a regression test proving stacked deploys still roll back to the original first-touch state.

### B4 — Folder-overlay deploy + rollback + empty-dir walking

**Goal:** Mods that ship folders matching game folders (e.g. `models/`, `data/`) merge into the game folder instead of being routed by extension. Also fix silent gaps around empty directories.

**Files:**
- `Services/DeploymentPlanner.cs` — add overlay detection at plan time.
- `Models/RoutingRule.cs` — add `RuleKind.FolderOverlay` (or a `PreserveRelativePath` flag on the existing rule schema).
- `Services/BackendCore.cs:566` — `Directory.EnumerateFiles` is files-only; switch to file-and-directory walk so empty mod-side directories are deployable/recreatable.
- All 11 `.tmmgame` profiles — add `overlayFolders: ["models", "data", "audio", "text", "anim"]` (per-game list).

**Plan:**
1. At install time (B2's `OnModAddedAsync`), if the mod root contains a top-level folder whose name appears in the game's `overlayFolders` list, every file under that folder gets `PreserveRelativePath = true` in the persisted plan.
2. At deploy, files with `PreserveRelativePath = true` deploy to `{gameRoot}\{originalRelativePath}` — bypassing extension routing.
3. Walk directories (not just files) so empty mod-side directories are recorded in the plan and recreated on deploy.
4. Symlinks: fail-loud at install time with "TMM does not support symlinked mod sources." (Cleaner than flattening to copies silently.)

**Depends on:** B3 — without baseline.json, rollback of an overlay-mod can't restore vanilla files it overwrote.

**Implemented so far:** overlay-folder routing and group-aware `modloader` remapping are already in the planner; direct deploy now carries empty directories through to the target and records them in rollback manifests; rollback removes empty directories when safe; and symlinked mod sources are rejected during planning.

### B1 — GTA III/VC/SA routing profile completeness

**Goal:** Bring the three GTA III-series `.tmmgame` profiles up to modern Mod Loader conventions.

**Files:** `Assets/GameProfiles/gta3.tmmgame`, `gtavc.tmmgame`, `gtasa.tmmgame`.

**Per-game additions (all three):**
- **New rule, priority 95**: any file under a top-level `modloader\` folder → preserve relative path. This catches `modloader\{modname}\*` and `modloader\{group}\{modname}\*` automatically.
- **Extend existing CLEO rule** to match `.cs`, `.cs4`, `.cs5`, `.fxt` (currently only `.cs` and `.fxt`).
- **Add `.ini` companion handling**: routing-rule side ensures `.ini` co-routes with its sibling `.cs/.cs4/.cs5` when basenames match; install-time companion detection handles the pairing.
- **Add `companionSiblings`** field to the profile: `{ "cleo": ["CLEO_TEXT", "CLEO_FONTS"] }`. Used by B5 import heuristic and by post-install plan validation.

**Implemented so far:** all three bundled GTA III/VC/SA profiles now include the `modloader` rule, CLEO variants, overlay folders, and CLEO companion sibling metadata; the planner understands the rules and routes CLEO `.ini` companions alongside the matching script destination.

### B5 — Sync/import from existing modded install

**Goal:** Point TMM at a friend's pre-modded game directory; heuristically detect mods; move them into `ModsRaw_{key}` as proper TMM-managed mods with persisted plans.

**Files:**
- New `Services/ModImporter.cs` — heuristic scan + companion grouping logic.
- New `Views/ImportReviewWindow.xaml(.cs)` — preview detected mods, let user rename / merge / split / exclude before commit.
- `Views/Subpages/ModManagerPage.xaml(.cs)` — add "Import from game folder…" button to the sidebar.
- Calls into B2 (`OnModAddedAsync`) and B3 (baseline seeding).

**Plan:**
1. **Scan:** walk the game dir. Flag any file matching the game's mod-type extensions (from the profile) in mod-type-target folders (cleo/, scripts/, modloader/*) as a candidate. Top-level subfolders under `modloader\` each become one mod (preserve nesting).
2. **Companion grouping (per Q2 decision):** for each candidate, look for matching basenames in (a) the same folder, (b) the game's `companionSiblings` folders. Group as one mod.
3. **Low-confidence handling:** files that match a mod-type extension but live in unexpected locations (mystery `.dll` in game root) get flagged with a warning chip in the review UI — user explicitly confirms or excludes.
4. **Review UI:** table of detected mods with name, file list, suggested group. User can rename, merge, split, or exclude before commit.
5. **Commit:**
   - First, seed `baseline.json` by snapshotting *every* file currently in the game dir (this install IS the user's baseline — see CLAUDE.md principle #2).
   - Then for each accepted mod: create `ModsRaw_{key}/{ModName}/`, move (not copy — these files are leaving the game dir) the mod's files in preserving relative structure, fire `OnModAddedAsync` to write `deployplan.json`.
   - On first deploy after import the saved plan re-deploys the files to where they came from, end state matches start state. The point is: now they're removable.

**Gotcha:** "move not copy" means a failed import can leave the game dir in a half-state. Wrap the whole commit in a transaction (collect all file ops, validate, then execute; on failure, revert moves).

**Implemented so far:** B5 now has a scan/review/import path in the Mod Manager, baseline seeding before move-out, and a restore deploy after import to keep the install state intact while TMM takes ownership. The split/merge/refinement UI is still ahead of us.
The scanner now groups CLEO/script companions together via the game's companion-sibling map, keeps `modloader` trees intact as one candidate, flags loose root files as low-confidence, and the importer does a full move transaction with rollback if anything fails mid-import.

### B6 — Mod groups as nested deployment targets

**Goal:** Let users group mods so the group name becomes a deployment-path segment (`modloader\{group}\{mod}\`).

**Files:**
- `Models/ModItem.cs` — add `string? GroupName { get; set; }` (null = ungrouped).
- `Services/DeploymentPlanner.cs` — when planning a grouped mod under the `modloader\` scheme, prepend `\{GroupName}\` to destination paths.
- `Views/Subpages/ModManagerPage.xaml(.cs)` — group column, drag-into-group UI, "New group" button, collapsible group headers (opt-in via a "Show groups" toggle).

**Plan:**
1. GroupName lives in the mod's sidecar metadata (`ModsRaw_{key}/{ModName}/_tmm/modinfo.json` — same place as the persisted plan from B2).
2. Adding/removing/renaming a group → regenerate the affected mods' `deployplan.json` via `OnModAddedAsync`. This is the *one* sanctioned plan invalidation per principle #1 (group change = re-install for plan purposes).
3. Group affects only the `modloader\` deployment scheme. Files that deploy to game root (engine proxies, root ASIs by user choice) are unaffected.
4. UI: ungrouped is default. "Show groups" toggle reveals collapsible headers. Drag a mod into a group header to assign; drag out to ungroup.

**Implemented so far:** `ModItem.GroupName` is persisted in `_tmm/modinfo.json`, the Mod Manager shows a group column plus a "Show groups" toggle, and Set/Clear Group actions regenerate the affected mod's frozen plan.

**Depends on:** B2 (plan persistence) — without it there's no defined moment to re-apply group rules.

---

## Sonnet queue — discrete fixes, hand off one at a time

Each entry is self-contained: file paths, exact symbols, gotchas. Ship in any order.

### ~~S1 — BackupsPage: implement actual backup list + restore action~~ ✅ DONE (2026-05-27)

**Files:**
- [Views/Subpages/BackupsPage.xaml](Views/Subpages/BackupsPage.xaml) — currently just an empty-state.
- [Views/Subpages/BackupsPage.xaml.cs](Views/Subpages/BackupsPage.xaml.cs) — just `InitializeComponent()`.

**Backend already exists:**
- `BackendCore.GetRollbackManifests(string gameKey)` returns ordered list of `DeployManifest`
  (newest first). Each manifest exposes `Timestamp` and `ModNames`.
- `BackendCore.RollbackDeployAsync(DeployManifest, IProgress<DeploymentProgress>)` executes
  the restore.
- See [Views/Subpages/ModManagerPage.xaml.cs:357-389](Views/Subpages/ModManagerPage.xaml.cs#L357)
  (`RunRollbackAsync`) for the existing rollback flow — reuse the same confirmation
  prompt structure.

**Plan:**
1. Add constructor `BackupsPage(BackendCore core)` to the code-behind (mirror how
   `PathsPage` is wired in [UnifiedShellWindow.xaml.cs:77-80](Views/UnifiedShellWindow.xaml.cs#L77)).
   Update `UnifiedShellWindow.xaml` so `pageBackups` is a `ContentPresenter`
   (replace the current `<local:BackupsPage>` instance + add a new `pageBackupsPlaceholder`
   the same way `pagePathsPlaceholder` works), and instantiate in `Window_Loaded`.
2. In code-behind:
   - Pull library entries via `BuildLibraryEntries()` from UnifiedShellWindow (or pass
     them in) — easier: iterate `_core.Mods.Keys` and resolve display names from
     `GameProfile.ByKey(key) ?? GameRegistry.Instance.GetGameProfile(key)`.
   - Add a game selector ComboBox (mirror the one in [DownloadsPage.xaml.cs:57-65](Views/Subpages/DownloadsPage.xaml.cs#L57)).
   - On selection change, call `core.GetRollbackManifests(key)` and render rows in a
     panel. Each row: timestamp formatted (`yyyy-MM-dd HH:mm`), mod count + first
     few mod names truncated, and a "Restore" button.
   - Restore button → confirmation MessageBox → `RollbackDeployAsync` with the
     existing `ShowDeployOverlay`/`HideDeployOverlay` pattern (or its own simpler
     progress UI; the page can own the overlay since ModManagerPage's overlay isn't reachable from here).
3. XAML: replace the empty-state StackPanel with a `Grid` containing the selector
   + a `ScrollViewer > StackPanel x:Name="backupRowsPanel"`. Keep the empty-state
   inside the panel so it renders when `GetRollbackManifests` returns an empty list
   for the selected game.
4. New locale keys needed (add to both `en-US.json` and `es-MX.json`):
   - `Backups_SelectGame` ("Select game")
   - `Backups_RestoreBtn` ("Restore")
   - `Backups_ConfirmRestore` ("Restore {0} to the snapshot from {1}? Current files will be replaced.")
   - `Backups_ModsList` ("Mods: {0}")

**Gotchas:** `RollbackDeployAsync` expects `IProgress<DeploymentProgress>` — use
`new Progress<DeploymentProgress>(_ => { })` like `ModManagerPage.RunRollbackAsync`
does, or wire the page's own progress UI if added.

### ~~S2 — UnifiedShellWindow language dropdown: show display names, not codes~~ ✅ DONE (2026-05-27)

**File:** [Views/UnifiedShellWindow.xaml.cs:54-56](Views/UnifiedShellWindow.xaml.cs#L54) +
[`CmbLanguage_SelectionChanged`](Views/UnifiedShellWindow.xaml.cs#L150)

**Current:** Sets `cmbLanguage.ItemsSource = languages` (raw `List<string>` of codes)
and reads `cmbLanguage.SelectedItem is string`.

**Reference impl:** [Views/InitialSetupWindow.xaml.cs:25-39](Views/InitialSetupWindow.xaml.cs#L25)
builds `ComboBoxItem { Content = svc.GetDisplayName(code), Tag = code }` and reads
`cmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string code`. Port
that pattern.

### ~~S3 — Steam Controls (future): per-game launcher in Settings~~ ❌ SCRAPPED (2026-05-27)

Triviali's reasoning: a Steam-side "verify files" would silently overwrite mod-modified game files, breaking TMM's first-touch baseline and per-deploy manifests. Decided the convenience isn't worth the data-corruption risk. Original brief preserved below for reference but will not be implemented.



**Note:** The previous Settings → Steam Controls section was removed in this session
because it hardcoded GTA III/VC/SA/IV. If you want it back, build it generically:

- Build dropdown from `GameProfile.All.Concat(GameRegistry.Instance.GetCustomGames().Select(c => c.profile))`
  filtered to entries with non-empty `SteamAppId`.
- Three buttons: `validate`, `install`, `uninstall` calling
  `SteamLauncher.Invoke(action, profile.SteamAppId, _core.Log)`.
- Add a settings-section heading + new locale keys.

**Decision pending:** confirm with Triviali whether this is worth re-adding before
implementing. Library cards already launch games; Steam-side admin is rarely needed
for direct-deploy modding.

### ~~S4 — PathsPage: honest read-only copy~~ ✅ DONE (2026-05-27)

**Files:** [Views/Subpages/PathsPage.xaml.cs:21-26](Views/Subpages/PathsPage.xaml.cs#L21) +
[Assets/Localization/en-US.json `Paths_Subtitle`](Assets/Localization/en-US.json) and es-MX.

**Decision (from Triviali, this session):** make read-only honest, don't wire Browse.

**Changes:**
1. Delete the `SetPath` parameter from `PathRowDef` record (currently dead).
2. Drop the `null = read-only` comment.
3. Rewrite `Paths_Subtitle` in both locale files to:
   - en: `"TMM data file locations."`
   - es: `"Ubicaciones de archivos de datos de TMM."`

### ~~S5 — `LibraryViewMode` doc-comment lie~~ ✅ DONE (2026-05-27)

**File:** [Models/AppSettings.cs:42-45](Models/AppSettings.cs#L42)

Comment claims `"grid" | "large" | "list" | "showcase"`. Only `grid`/`list`/`showcase`
are implemented (see `UnifiedShellWindow.BtnViewMode_Click`). Drop `large` from the
comment.

### ~~S6 — Sidebar "find mods" links are generic homepages~~ ✅ DONE (2026-05-27)

**File:** [Views/Subpages/ModManagerPage.xaml:284-295](Views/Subpages/ModManagerPage.xaml#L284)

NexusMods/ModDB/GitHub buttons currently link to homepages. Two options, pick one:

**Option A (low effort):** Replace with a single "Find Mods" text box → DuckDuckGo
query like `{currentGame.DisplayName} mods`.

**Option B (more value):** Add a `NexusSlug` field to `CustomGameProfile` + the
built-in `GameProfile` records, and rewrite the Nexus button to
`https://www.nexusmods.com/games/{slug}`. Hide buttons for games with no slug. ModDB
has no clean per-game slug, so drop that button.

### S7 — First-launch flow consolidation (cosmetic; low priority)

Currently: `App.OnStartup` → `UnifiedShellWindow` → `InitialSetupWindow` (modal) →
`FirstGamePickerWindow` → `SelectBuiltinGameWindow` *or* `CustomGameSetupWizard`.
Four dialogs deep for one decision. Merge `InitialSetupWindow` + `FirstGamePickerWindow`
into one screen (language at top, two big cards below). Defer unless a user complains.

---

## Opus follow-ups — needs design judgment, save for next big session

### ~~O1 — Custom-game integrity verification~~ ✅ DONE (2026-05-27)

Shipped as a generic per-game feature replacing the deleted GTA-MD5 logic.

**Schema (`Models/CustomGameProfile.cs`):**
- `long? ExpectedExeBytes` — optional fast size check.
- `List<string> AcceptedExeMd5s` — optional hash list (supports downgrader variants).

**Service (`Services/IntegrityChecker.cs`):**
- `IntegrityState` enum: `NotConfigured | Ok | SizeMismatch | Md5Mismatch | FileMissing`.
- `IntegrityChecker.CheckAsync(exePath, profile)` — size first (cheap), then MD5 only if size passes.
- `IntegrityChecker.ComputeMd5Async(filePath)` — public helper.

**UI surfaces:**
- **Step 1 wizard:** collapsible Expander below Steam App ID with size field, hash chip list,
  and "Auto-detect from current exe" button (hashes + measures current binary in one click).
  Validation: MD5 must be 32 hex chars (strips whitespace/dashes automatically).
- **Step 4 review:** one-line summary ("X bytes + N MD5 hashes" / "(not configured)").
- **ModManagerPage sidebar:** colored status row shown only when integrity is configured —
  green ✓ for OK, amber ⚠ for Size/MD5 mismatch, red for file missing.

**Policy:** warn-only. Deploy is never blocked — users may intentionally run modded exes.

**Built-in games:** the `.tmmgame` profiles in `Assets/GameProfiles/` are deserialized as
`CustomGameProfile`, so the new fields are automatically available — just unpopulated until
someone fills them in. To re-establish the old GTA III/VC/SA downgrader checks, edit those
files and add `expectedExeBytes` + `acceptedExeMd5s` (the old hashes are in git history at
`Models/GameProfile.cs` before the 2026-05-27 cleanup commit if you want them back).

### O2 — Strip more hardcoded GTA-specific paths from `BackendCore.QuickScan`

[Services/BackendCore.cs:210+](Services/BackendCore.cs#L210) — `QuickScan` hardcodes
`Grand Theft Auto IV\GTAIV`, `SteamLibrary\steamapps\common`, `Rockstar Games\`, etc.
The IV-family auto-derivation (TLaD/TBoGT inside IV folder) is legitimate game-layout
convenience, but the path roots are GTA-specific.

**Design question:** how does QuickScan work for arbitrary custom games? Maybe
custom games declare a `SearchHints: string[]` array in their `.tmmgame` profile,
and `QuickScan` iterates `GameProfile.All` + custom profiles, using each profile's
own hints? Sonnet can't decide this without architectural input.

---

## Stale docs — ✅ cleaned up (2026-05-27)

`TASK_BREAKDOWN.md`, `tool_usage_guide.md`, `TEST_FLOW.md` deleted.
`CLAUDE.md` CODEBASE_GUIDE.md references removed.
[SANITYCHECK.md](SANITYCHECK.md) and [CHANGELOG.md](CHANGELOG.md) kept.

---

## Done (archived)

- **Phase 3:** RuleEngine, DeploymentPlanner, LoadOrderResolver — built and shipped.
- **Phase 5:** 6 GTA `.tmmgame` profiles created in `Assets/GameProfiles/`,
  `GameRegistry` wired to use `gameKey` for deduplication.
- **Phase 6:** IIISeries/IVSeries DashModes removed; all games use one unified panel
  + DeploymentPlanner.
- **This session (2026-05-27):** UI audit + cheap fixes + GTA-deprecated cleanup
  (see status snapshot above).
- **O1 (2026-05-27):** Generic exe integrity verification — `ExpectedExeBytes` +
  `AcceptedExeMd5s` on `CustomGameProfile`, `IntegrityChecker` service, wizard Step 1
  Expander with auto-detect, Step 4 review summary, ModManagerPage sidebar status row.

### Block D — Mod Loadouts (COMPLETED 2026-05-28)
- **D1: Loadout snapshots** — `ModLoadout` + `BackendCore.SaveLoadoutAsync/ApplyLoadoutAsync`. Save/restore enabled states + load order.
- **D2: .tmmpack export** — `TmmPackBuilder` bundles a loadout (manifest + loadout.json + mod source folders) into a portable zip.
- **D3: Loadout management** — Rename, delete, overwrite-confirmation flows in the loadout context menu. Bonus: `LoadoutDiffWindow` compares any two loadouts side-by-side (additions/removals/enable/disable/reorder).

### Block E1 — Smart DLL Wizard (COMPLETED 2026-05-28)
- `ProxyDllDetector` knows ~20 well-known proxy DLLs (dinput8, d3d9, dsound, ScriptHook variants, SKSE/F4SE loaders). Surfaces a notification + log entry on install.
- E2 (auto-routing hint) and E3 (multi-proxy version conflict) remain on the backlog.

### Block C4 — Smart Conflict Resolver UI (COMPLETED 2026-05-28)
- `ConflictResolverWindow` lets users pick a winner per-conflict, overriding the default highest-load-order behavior. Non-winners are auto-skipped via `FileDeployRow.Skip` in the deploy plan.

### Block F — Polish & QoL (COMPLETED 2026-05-28)
- **F1:** `Logger` service — 5 MB log rotation with 3 historical files.
- **F2:** Crash dialog embeds last 40 log lines into the clipboard report.
- **F3:** Backup size monitoring — `BackendCore.GetTotalBackupSize` + `BackupsPage` quota badge against `Settings.BackupSizeWarnBytes` (default 5 GB).
- **F4:** Recent activity feed — `ActivityLogger` + `ActivityFeedWindow` surface the last 20 deploys/rollbacks/imports/loadout ops; persisted in `AppSettings.RecentActivity`.
- **F5:** Mod favorites — `ModItem.IsFavorite` + clickable star column + context-menu toggle.


