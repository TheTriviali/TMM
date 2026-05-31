# Changelog

All notable changes to TMM are listed here, newest first.

---

## [v0.1-alpha-12] — 2026-05-30 *(Mod list enrichment)*

- **Colour spine:** 4 px left border per row keyed to mod category via `CategorySpineBrushConverter`
- **Inline conflict badge:** per-row clash count; click to expand destination → winner detail below the mod name. `ModItem` gains `ConflictSummary` + `IsConflictExpanded` (`[JsonIgnore]`, `INotifyPropertyChanged`)
- **Filter chips:** All / Enabled / Conflicts / Favorites — `CollectionView` predicate, counts update on every list mutation, active chip highlighted with `AccentBrush`
- **Bulk-action bar:** slides in on multi-select; Enable / Disable / Set group / Remove; collapses on deselect-all
- **Hover actions:** Open Folder + Properties buttons fade in on row hover
- New converters in `Converters/ModListConverters.cs`: `CategorySpineBrushConverter`, `ConflictBadgeVisibilityConverter`, `BoolToVisibilityConverter`
- `App.xaml`: `ConflictRedBrush` + `ConflictSoftBrush` added as global resources
- ListView switched from `GridView` columns to full custom `ItemTemplate` DataTemplate

---

## [v0.1-alpha-11] — 2026-05-30 *(Backport phase 1: backend + Library Home)*

- **Mod categories (H1):** `ModItem.Category` — 5-value preset (Gameplay / Visual / Audio / Map / Other) with stable color map; persists via `modinfo.json`. Custom games can override the preset in the wizard (Step 1 + Step 4 review)
- **Per-mod conflict summary (H2):** `ConflictAnalyzer.AnalyzeByMod()` — `OverwritesCount`, `OverwrittenByCount`, per-destination `Clashes`; covers file and proxy-DLL conflicts; 7 unit tests
- **Pending-changes tracker (H3):** `BackendCore.PendingChanges()` diffs the current mod list against the last `DeployManifest` — loadout switches don't reset the baseline
- **Batch operations (H4):** `BatchEnable`, `BatchDisable`, `BatchSetGroup`, `BatchRemove` in `ModManagerPage.Batch.cs` — one save + one summary toast per call
- **Library Home view (M3):** replaces grid — Continue hero card (active game, Play/Manage, pending badge), quick-stats strip (games set up / mods installed / backup usage), game card wrap-panel, recent-activity feed. Shell view switcher is now Home/List only; legacy "grid"/"showcase" values migrate to "home" on launch. `AppSettings.CachedModsInstalledBytes` added

---

## [v0.1-alpha-10] — 2026-05-29 *(Notifications, Add/Edit Game, import review, first-run fixes)*

### Added
- **Add/Edit Game full-page (WIZ2):** `Views/Subpages/AddGamePage.xaml(.cs)` replaces the modal wizard as the primary add/edit UX. Scrollable sections (Essentials / Mod Types / Routing / Review) with left jump-rail, completion dots, and live summary bar. Edit mode pre-fills from saved profile. ✎ pencil nav button added to shell
- **Import review master-detail (D-B5):** Left pane — candidate list with `Extended` multi-select, include checkbox, file count, warning badge. Right pane — per-file checkboxes, "New mod from checked" split, "Move checked" reassign, "Merge selected" fold. Inline name + group editors
- **Proxy-DLL routing warning (D-E2):** Non-blocking `DeploymentWarning` in `DeployPreviewWindow` when a proxy DLL resolves to a non-root destination
- **Multi-proxy conflict detection (D-E3):** `ConflictAnalyzer.AnalyzeProxyConflicts` — surfaces a `ConflictEntry` per shared proxy filename (e.g. two mods shipping `dinput8.dll`)
- **Built-in games use SearchHints (D-O2r):** All six GTA `.tmmgame` profiles now have `searchHints`; `ScanBuiltInsBySearchHints()` runs before the legacy loop so found games are skipped
- **Notifications (NOTIF1–4):** Persistent ring (500 entries, 200 persisted to `notifications.json`). Full-page Notifications tab (bell icon). Verbose mode toggle in Settings (off by default). `ShowVerbose` instrumentation at all key backend sites (deploy, rollback, plan freeze, import, backup prune, etc.)

### Fixed
- All six GTA built-in profiles silently failed to deserialize on fresh launch — `JsonHelper.TmmGameOptions` lacked `JsonStringEnumConverter`; condition-based routing rule enum names threw and were swallowed
- "Select a Built-in Game" done button was permanently greyed out on machines without GTA installed
- Welcome window left panel stayed English after language switch (hardcoded branding strings)
- "Directory not set" message was a hardcoded literal (now localized)
- Five non-GTA built-in profiles (Skyrim, FNV, Cyberpunk, RDR2, Witcher 3) had non-functional routing — flat schema didn't map to `RoutingRule`; all five rewritten to condition-based schema

---

## [v0.1-alpha-9] — 2026-05-29 *(Build restore, audit cleanup, profile portability)*

- **Fixed broken build:** `ConflictAnalyzer` / `ConflictEntry` / `ConflictParticipant` were referenced but never committed — reconstructed in `Services/ConflictAnalyzer.cs`
- **`.tmmpack` import (D4):** `TmmPackInstaller` — zip-slip guard, collision-safe names, per-mod plan freeze on import, loadout reconstruction
- **Profile search hints (O2):** `CustomGameProfile.SearchHints` travel inside `.tmmgame`; `QuickScan` probes them across all fixed drives
- **Restored GTA III/VC/SA integrity hashes (O1):** `acceptedExeMd5s` back in bundled profiles
- **Unified onboarding (S7):** `FirstGamePickerWindow` deleted; built-in/custom choice merged into `InitialSetupWindow`
- **Centralized version string:** `Helpers/AppInfo.DisplayVersion`
- **Removed:** `BackendCore.DeployModsAsync` + `ApplyConditionalRoutes` (no callers)
- **New:** `UIFLOWS.md` — Mermaid diagrams for all major flows

---

## [v0.1-alpha-8] — 2026-05-28 *(Loadouts, .tmmpack export, conflict resolver)*

- **Loadouts (D1–D3):** Save/load/rename/delete named mod configs (enable state + order) — `Loadouts_{gameKey}/{Name}.json`; overwrite confirmation
- **Loadout diff viewer:** `LoadoutDiffWindow` side-by-side comparison (additions, removals, enable/disable changes, reorders)
- **.tmmpack export (D2):** `TmmPackBuilder` bundles loadout + mod sources into a portable archive
- **Proxy-DLL detection (E1):** `ProxyDllDetector` flags ~20 known proxy DLLs (dinput8, d3d9, ScriptHook variants, SKSE/F4SE loaders) at install time
- **Conflict resolver UI (C4):** `ConflictResolverWindow` — per-conflict winner override; non-winners auto-skipped in deploy plan
- **Favorites:** star/pin per mod, persisted on `ModItem.IsFavorite`
- **Recent activity feed:** `ActivityLogger` (last 20 ops) surfaced from `BackupsPage`
- **Backup size monitoring:** total size + "Over quota" badge at 5 GB default
- **Log rotation:** 5 MB cap, 3 historical files; crash dialog appends last 40 lines to clipboard report

---

## Earlier (v0.1-alpha-1 through v0.1-alpha-7)

| Version | Date | Highlights |
|---------|------|-----------|
| v0.1-alpha-7 | 2026-05-27 | BackupsPage, generic integrity verification, removed GTA-specific MD5/exe-check logic |
| v0.1-alpha-6 | 2026-05-25 | UI refinements, .NET 10 `MenuItemRole` compat fix, navigation order |
| v0.1-alpha-5 | 2026-05-25 | TGTAMM → TMM rebrand (namespace, AppData path, GitHub repo, branch) |
| v0.1-alpha-4 | 2026-05-23 | Theme system simplified to `AccentPresets` (dark-only, 2-tone); dead windows removed; 8 small files consolidated |
| v0.1-alpha-3 | 2026-05-23 | Test Routing dry-run panel, `GameRegistry` built-in profiles, `DeployPreviewWindow` |
| v0.1-alpha-2 | 2026-05-23 | Theme presets curated from 75 → 25; dead code removal |
| v0.1-alpha-1 | 2026-05-22 | First release: routing sentence builder, GTA IV setup wizard, built-in profiles |

---

## Pre-alpha (v0.01 / v0.02, April–May 2026)

Foundation work: initial WPF framework, basic GTA mod install, VFS → direct-deploy architectural switch, multi-game GTA dashboards, toast notifications, HSV theming system, drag-drop load orders, MD5 verification. Project originally named TGTAMM (GTA Trivial Archive Mod Manager); renamed to TMM in v0.1-alpha-5.
