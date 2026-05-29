# Changelog

All notable changes to TMM are listed here, newest first.

---

## [Unreleased]

### Fixed
- **All six GTA built-in profiles silently failed to load (critical, F1):** On a fresh launch only
  the five flat-schema games (Skyrim/FNV/Cyberpunk/RDR2/Witcher 3) appeared — GTA III/VC/SA/IV/TLaD/TBoGT
  were missing. `JsonHelper.TmmGameOptions` lacked a `JsonStringEnumConverter`, so the GTA profiles'
  condition-based `routingRules` (enum names like `"PathContains"`/`"StartsWith"`/`"AND"`) threw on
  deserialize and were swallowed by the catch in `GameRegistry.LoadBuiltInProfilesAsync`. Added the
  converter (accepts both string and numeric enum forms, so older numeric exports still load) plus a
  `TmmGameOptionsTests` regression (57/57 tests pass). Remaining first-run issues (welcome-sidebar
  localization, setup-card behavior, hardcoded "Directory not set", flat-schema routing) are tracked
  as Group F in PLANS.md.

---

## [v0.1-alpha-9] — 2026-05-29 *(Build restore, audit cleanup, profile portability, .tmmpack import)*

### Fixed
- **Broken build (critical):** Commit `b09c02a` shipped a non-compiling `master` — the C4 conflict resolver referenced `ConflictAnalyzer` / `ConflictEntry` / `ConflictParticipant` from `DeployPreviewWindow` and `ConflictResolverWindow`, but those types were never committed. Reconstructed in `Services/ConflictAnalyzer.cs` (groups enabled mods' plan files by destination, flags paths with ≥2 distinct writers, ranks participants by `FinalLoadOrder` so the resolver's default winner matches a plain Deploy).
- **`.tmmgame` fields silently dropped:** `ProfileMigration.FromExport` never mapped `NexusSlug`, `AcceptedExeMd5s`, or `ExpectedExeBytes`, so those profile fields were lost on load (the sidebar "find mods" Nexus link never worked for built-in games). Added the mappings, plus `SearchHints`, across `TmmGameExport` / `FromExport` / `ExportConfigAsync`.
- **Integrity panel never showed for built-in games:** `ModManagerPage.InitCustomGame` now syncs a built-in game's resolved path (`Settings.GamePaths`) into `config.GameDirectory`, so the integrity panel, sidebar path, and deploy button resolve for built-ins too.
- **2 CS0108 warnings:** Removed redundant local `BtnClose_Click` overrides in `ActivityFeedWindow` and `LoadoutDiffWindow` (both inherit the identical `TmmWindow` handler).

### Added
- **`.tmmpack` import (D4):** `Services/TmmPackInstaller.cs` (`ReadManifest` + `ImportAsync`) consumes a pack into the currently-selected game — extracts mods into `ModsRaw_{key}` with a zip-slip guard, collision-safe unique names, a forward-version reject, a per-mod `OnModAddedAsync` plan freeze, and loadout reconstruction (remapping renamed mods). New `BackendCore.SaveLoadoutAsync(gameKey, ModLoadout)` overload. Wired as "Import .tmmpack…" in the Loadouts menu. Closes the export-only gap.
- **Profile search hints (O2):** `CustomGameProfile.SearchHints` — default install locations (relative to a drive root) that travel inside a shared `.tmmgame`. `QuickScan` now probes them across every fixed drive for the configured exe and auto-fills the game directory, so a shared profile self-locates on another machine. Editable in wizard Step 1, reviewed in Step 4.
- **Restored GTA III/VC/SA integrity hashes (O1):** `acceptedExeMd5s` re-added to the three bundled profiles (recovered from commit `eb2b953`; Vice City keeps its ModDB v1.0 downgrader variant).
- **Loadout name validation:** `BackendCore.IsValidLoadoutName` rejects filenames with illegal characters in save/rename, with graceful UI messaging.
- **UI flow charts:** New `UIFLOWS.md` — Mermaid diagrams for startup/first-launch, navigation, the add-game wizard, install→plan-freeze, deploy+conflict-resolve, rollback, import, and loadouts.

### Changed
- **Unified onboarding (S7):** Deleted `FirstGamePickerWindow`; its built-in/custom choice cards merged into `InitialSetupWindow` (language + game choice on one screen, one fewer dialog).
- **Centralized version string:** New `Helpers/AppInfo.DisplayVersion` is the single source, consumed by `TmmPackBuilder` and `AboutWindow` (which previously showed a misleading `v1.0.0` for an alpha build).

### Removed
- **Dead code:** `BackendCore.DeployModsAsync` (no callers — legacy built-in path superseded by the unified custom path) and its only consumer `ApplyConditionalRoutes`; `ModImporter.GetCandidateGroup` (unused).

---

## [v0.1-alpha-8] — 2026-05-28 *(Block D complete + Block E1 + Block F polish)*

### Added — Loadouts (Block D)
- **D1 — Loadout snapshots:** Save/load enabled states + ordering via `BackendCore.SaveLoadoutAsync` / `ApplyLoadoutAsync`. New `Loadouts_{gameKey}/{Name}.json` files persist per-game.
- **D2 — .tmmpack export:** `TmmPackBuilder` bundles a loadout (manifest + loadout.json + mod source folders) into a portable archive.
- **D3 — Loadout management:** Rename, delete, overwrite confirmation, per-loadout submenu in the new Loadouts toolbar button.
- **Loadout diff viewer:** `LoadoutDiffWindow` compares two loadouts side-by-side — additions, removals, enable/disable changes, load-order reorders.

### Added — Smart DLL Wizard (Block E1)
- **Proxy DLL detection:** `ProxyDllDetector` flags ~20 known proxy DLLs (dinput8, d3d9, dsound, ScriptHook variants, SKSE/F4SE loaders) during mod install. Logs detection details and surfaces a toast.

### Added — Conflict Resolution (C4)
- **Smart conflict resolver UI:** `ConflictResolverWindow` lets users pick a winner per-conflict, overriding the highest-load-order default. Non-winners are auto-skipped in the deploy plan.

### Added — Quality of Life (Block F)
- **Mod favorites:** Star/pin mods with a clickable column + context-menu toggle. Persisted on `ModItem.IsFavorite`.
- **Recent activity feed:** `ActivityLogger` records the last 20 deploys/rollbacks/imports/loadout operations; `ActivityFeedWindow` surfaces them from the BackupsPage.
- **Backup size monitoring:** BackupsPage shows total backup folder size with an "Over quota" badge once `Settings.BackupSizeWarnBytes` is exceeded (default 5 GB).
- **Loadout overwrite confirmation:** Saving a loadout with an existing name now prompts before overwriting.

### Added — Reliability
- **Log rotation:** New `Logger` service caps `TMM.log` at 5 MB and rotates up to 3 historical files. Co-exists with legacy `BackendCore.Log` path.
- **Crash dialog log attach:** Recent 40 log lines are appended to the clipboard report when the unexpected-error dialog fires.

### Internal
- **GitHub repository:** Project canonicalized at `TheTriviali/TMM`.

---

## [v0.1-alpha-7] — 2026-05-27 *(UI Audit, GTA Deprecated Cleanup & BackupsPage)*

### Added
- **BackupsPage:** Fully implemented — game selector ComboBox, per-game backup list with timestamps and mod summaries, restore button with confirmation dialog and progress overlay. Previously showed only an empty-state placeholder.
- **Integrity verification:** Generic per-game exe integrity checking (`IntegrityChecker` service, `ExpectedExeBytes`/`AcceptedExeMd5s` on `CustomGameProfile`, wizard Step 1 Expander with auto-detect, Step 4 review summary, ModManagerPage sidebar status row).

### Removed
- **GTA-specific deprecated logic:** MD5/vanilla detection fields (`GameProfile.Vanilla10Md5`, `AdditionalValidMd5s`, `HasExeCheck`, etc.), `ExeStatus` enum, `BackendCore` methods (`VerifyGameStatusAsync`, `GetEffectiveMd5Async`, `GetMd5DiagnosticsAsync`, `HasExeModOverride`, `ToggleDeployOverride`, `FindExeInMod`, `GetFileMD5Async`), `AppSettings.DeployOverrides`, Steam Controls + MD5 panels in SettingsPage.
- **ArchiveExtractionWindow:** Deleted unreachable `Views/ArchiveExtractionWindow.xaml(.cs)`.
- **Stale docs:** Deleted `TASK_BREAKDOWN.md`, `tool_usage_guide.md`, `TEST_FLOW.md` (all described removed or obsolete functionality).

### Fixed
- **Language dropdown:** UnifiedShellWindow now shows display names (e.g. "English", "Español") instead of raw locale codes in the language ComboBox.
- **PathsPage:** Removed dead `SetPath` parameter from `PathRowDef` record; updated subtitle to accurately describe the page as read-only.
- **LibraryViewMode doc-comment:** Removed non-existent `"large"` from the list of valid values.
- **CLAUDE.md:** Removed broken references to `CODEBASE_GUIDE.md` (file never existed).
- **Various UI:** Fixed empty Segoe glyphs in DownloadsPage, dead AboutWindow handlers, no-op `if (_isEdit)` branch in wizard, dead `LastSelectedGameKey`, hidden no-op Edit buttons on GameCard, hardcoded foreground on SelectBuiltinGameWindow, dead locale keys, hardcoded version strings.

---

## [v0.1-alpha-6] — 2026-05-25 *(UI Refinements & Backend Stability)*

### Removed
- **Archive Feature:** Removed archive/unarchive game functionality (btnArchiveChip button, archiveFlyout overlay, BtnArchiveChip_Click handler, UpdateArchiveChipStyle method, _showingArchived field).
- **Large Grid View:** Removed duplicate large grid view option from game library (was 1.25x scaled grid). Library now supports 3 modes: Grid View, List View, Showcase View.

### Changed
- **Game Library Text:** Normalized title casing in LibraryPage showcase view:
  - "MODS INSTALLED" → "Mods installed"
  - "READY TO DEPLOY" → "Ready to deploy"
  - "OTHER GAMES" → "Other games"
- **DownloadsPage Sidebar:** Relocated "Downloaded archives" sidebar from left (Grid.Column 0) to right side (Grid.Column 1) of window for improved layout balance.
- **Search Bar Expansion:** Increased library search bar dimensions (MinWidth: 220→320, MaxWidth: 500, height: 26→28, padding: 6,0→8,0) for better visibility and usability.
- **Navigation Order:** Swapped "Mod Manager" and "Downloads" tabs in main navigation strip. Mod Manager now appears before Downloads to prioritize the core modding workflow.
- **Showcase View Icon:** Fixed gallery icon in view mode switcher (&#xE7B9; → &#xE71D;) for proper visual representation.
- **Fullscreen Padding:** Added 8px margin to main window border when app enters fullscreen/maximized state (`MainWindowBorder.Margin = max ? new Thickness(8) : new Thickness(0)`).

### Fixed
- **MenuItemRole Validation (.NET 10 compatibility):** Removed invalid MenuItem ControlTemplate Trigger with `Property="Role" Value="Separator"` from App.xaml. MenuItemRole enum in .NET 10 does not include Separator value (only TopLevelHeader, TopLevelItem, SubmenuHeader, SubmenuItem). This was causing XamlParseException on startup.
- **Inner Exception Logging:** Enhanced App.xaml.cs ShowCrashDialog method to capture and display inner exception details for better debugging of nested exceptions.

---

## [v0.1-alpha-5] — 2026-05-25 *(TGTAMM→TMM Rebrand & Documentation Consolidation)*

### Changed
- **Project Rebrand:** TGTAMM → TMM (Triviali's Mod Manager)
  - Namespace: TGTAMM → TMM throughout all .cs and .xaml files
  - Assembly output: TGTAMM.dll → TMM.dll
  - AppDataPath: `%APPDATA%\TGTAMM\` → `%APPDATA%\TMM\` (auto-migrates old data on first run)
  - csproj/sln files renamed
  - GitHub repo: TheTriviali/TGTAMM → TheTriviali/TMM
  - Default branch: main → master (main branch deleted)

### Documentation
- **CLAUDE.md:** New chat reference guide with quick lookup structure, token-saving tips, and links to detailed docs. Reduces context bloat in new sessions.
- **Consolidated Files:**
  - **THEMING_SIMPLIFICATION.md** → Folded into this changelog as historical record (see v0.1-alpha-4 changes)
  - **DESIGN_BRIEF.md** → Archived; unified shell design specs remain available in git history for future reference if needed

---

## [v0.1-alpha-4] — 2026-05-23 *(Theming System Simplification & Dead Window Removal)*

### Removed (Theming Simplification)
- **ThemeManagerWindow.xaml / .xaml.cs** — Complex theme picker UI removed
- **AppSettings Customization Fields** (kept as deprecated stubs):
  - BgColor, ColorMode, FontFamily, MicaEnabled, AccentBorderEnabled, TitlebarTheme, LastPresetName
- **ThemeEngine Complexity** (200+ lines removed):
  - 25+ theme presets (Dracula, Nord, Catppuccin, GTA themes, light themes)
  - HSV/RGB color palette generation
  - WCAG contrast algorithms
  - Complementary color modes (Triadic, Analogous, SplitComp, Tetradic)
  - Font application logic
  - Mica/Acrylic DWM interop
  - Title bar color customization

### Added (Theming Simplification)
- **AccentPresets.cs** — Streamlined accent color presets (2-tone system with auto-gradients)
- Dark mode mandatory; light mode removed from runtime customization

### Removed (Dead Windows & Consolidation)

### Removed
- **DebugConsoleWindow, CrashReportWindow, HelpWindow, DxvkSettingsWindow:** Four dead/low-value windows deleted. Crash reporter is now an inline `MessageBox` + clipboard copy. Help replaced with `MessageBox` + GitHub URL.
- **GitHub API auto-downloader (`DownloadLatestGithubReleaseAsync`):** DXVK and Modloader one-click install removed. Direct-URL installers (SilentPatch, ASI Loader, Widescreen, CLEO, Project2DFX) are unaffected.
- **TextColorMode setting:** Hardcoded to WCAG algorithm; removed from AppSettings and ThemePreset.
- **MicaIntensity setting:** Hardcoded to 0.75; removed from AppSettings and ThemePreset.
- **IThemeSettings interface:** Removed unused interface; `ThemeEngine.ApplyTheme` takes `AppSettings` directly.
- **DashboardWindowBase:** Removed empty wrapper class; all three dashboard windows now extend `TmmWindow` directly.
- **SCOPE.md, FUTURE_ADDITIONS.md, PLANS.md:** Deleted stale documentation files.

### Changed
- **Code consolidation — 8 tiny files merged into their owners:**
  - `GameState.cs` (`ExeStatus` enum) → `Models/GameProfile.cs`
  - `ConditionalRoute.cs` → `Models/TmmGameConfig.cs`
  - `DeploymentProgress.cs` → `Services/BackendCore.cs`
  - `NotificationItem.cs` → `Services/NotificationService.cs`
  - `ShellHelper.cs` + `UiColors.cs` + `JsonHelper.cs` → `Helpers/Helpers.cs`
- **XAML root elements:** Dashboard window XAML files updated from `<local:DashboardWindowBase>` to `<local:TmmWindow>` to match code-behind base class.

### Documentation
- **CODEBASE_GUIDE.md:** Full rewrite — removed stale window refs, added Helpers section, tightened all entries.
- **SANITYCHECK.md, README.md:** Removed stale references matching removed features.

---

## [v0.1-alpha-3] — 2026-05-23 *(Documentation & Core Features Pass)*

### Added
- **Test Routing Dry-Run Panel:** CustomGameConfigWindow now includes an inline collapsible "Test Routing..." panel that simulates file routing. Users can browse for a test file and see where it would be deployed (including conditional route evaluation with directory existence checks).
- **Skyrim Anniversary Edition Built-in Profile:** Embedded `skyrim_ae.tmmgame` asset auto-loaded via GameRegistry. Sets up routing for ESP/ESM → Data, DLLs → Data\SKSE\Plugins\ (conditional on SKSE directory), scripts → Data\Scripts\. Appears in GameLauncherWindow under "Supported Games" section with read-only profile.
- **Deploy Preview Window:** Modal window shown before deployment that summarizes which mods are being deployed, groups files by destination directory, and shows extension types. Users can review and confirm or cancel before deploy proceeds.
- **Mod Loadouts Foundation:** ModLoadout.cs model and BackendCore persistence layer (SaveLoadoutAsync, LoadLoadoutAsync, DeleteLoadoutAsync) for saving named mod configurations (enable state + load order per game).
- **GameRegistry Built-in Profile Support:** LoadBuiltInProfilesAsync() method uses reflection to load embedded `.tmmgame` files from assembly resources. GetBuiltInCustomGames() returns only built-in profiles; GetCustomGames() excludes them. GameLauncherWindow displays separate "Supported Games" and "Your Games" sections.
- **Future Additions Documentation:** Created `FUTURE_ADDITIONS.md` containing comprehensive specs for deferred features: Smart DLL Wizard (§3), Built-in Profiles (Skyrim/Minecraft, §4), Mod Import (§5), Conflict Resolution Engine (§6), Loadouts UI (§7), Advanced File Detection (§10), and backlog items (§B1–§B6).

### Changed
- **PLANS.md Reorganization:** Moved all future/deferred features (Smart DLL Wizard, additional built-in games, conflict resolution, advanced loadouts UI, file detection) to `FUTURE_ADDITIONS.md`. PLANS.md now focuses exclusively on shipped features and their implementation status. Updated header to reference `FUTURE_ADDITIONS.md` and removed ~1050 lines of archived content.
- **GameLauncherWindow Sections:** Now displays "Supported Games" header (for built-in profiles) and "Your Games" header (for user-created games) with appropriate visibility logic. Built-in game cards show "Manage" button only (no Edit/Delete).
- **CustomGameConfigWindow Edit Buttons:** IsBuiltIn profiles conditionally hide Edit and Delete buttons, only exposing Manage functionality.

### Fixed
- **GTA4DashboardWindow Menu Type Errors:** Fixed switch statement cases comparing string profile keys against GameProfile enum objects. Changed cases from `GameProfile.IV` to string literals `"IV"`, `"TLaD"`, `"TBoGT"`.

### Documentation
- **PLANS.md:** Updated to reference FUTURE_ADDITIONS.md and removed archived sections. Now 353 lines (focused on shipped state).
- **SCOPE.md:** Companion human-readable feature overview (unchanged, both kept in sync).
- **FUTURE_ADDITIONS.md:** New comprehensive 340-line document covering all deferred features with full specs, JSON examples, UI mockups, and implementation notes.

---

## [v0.1-alpha-2] — 2026-05-23

### Changed
- **Theme System Optimization:** Curated built-in presets from 75 → 25 themes, focusing on GTA-inspired, popular editor color schemes, synthwave variants, and light themes. Target demographic: 25–35 year old males (teals, cyans, blues, muted grays, warm tones).
- **Theme Presets Detail:**
  - Kept: 5 GTA-inspired (Vice City Neon, GTA III Era, San Andreas Grove, GTA Online, Dark Teal Default), 8 popular editor palettes (Dracula, Nord, Gruvbox, Catppuccin, One Dark, Monokai, Solarized Dark, GitHub Dark), 4 synthwave (Synthwave Sunset, Outrun, Retrowave, 80s Neon), 4 quality dark (Matrix, Deep Ocean, Obsidian, Slate), 4 light variants (Light Sky, Solarized Light, Nord Light, Light Teal)
  - Removed: 50 low-usage presets (Windows XP/Vista/3.1/Mac9.0 nostalgia themes, "Unique Themes" section, nature-inspired duplicates, vibrant variants)
- **Matrix Theme Added:** Neon green monochrome theme (`#00FF41` accent, `#0D0D0D` background) as successor to "Phosphor"
- **Code Reduction:** ThemeManagerWindow.xaml.cs reduced by ~130 lines total (file: 841→710 lines). Presets section specifically: 237 lines → 60 lines (~170 line savings)
- **Documentation Updated:** README.md (theme count 34+→25, descriptions), CHANGELOG.md (this entry)

### Known Issues (Deferred)
- Theme selector still only in ThemeManagerWindow dialog (pending: move to GameLauncherWindow toolbar for global access from all dashboards)
- HSV color pickers and complement palette tools remain in ThemeManagerWindow (pending: strip advanced features, keep session-only color picker)

---

## [v0.1-alpha-1] — 2026-05-22 *(First Release)*

### Added
- **Conditional Routing Sentence Builder:** "Add Custom Game" window replaces the cryptic 4-column routing DataGrid with a plain-English sentence builder — each rule reads as *"Put .asi files into plugins if plugins\ exists, otherwise ."*
- **Routing Preset Templates:** One-click presets for ASI Loader, Source Engine, Script Extender (SKSE/FOSE), and CLEO. Applying a preset fills in the routing rules automatically.
- **Empty-state hint:** When no routing rules are defined, a helper message explains that most games need no rules at all.
- **App icon:** Simple dark/teal icon across all windows and taskbar.
- **GTA IV crash handler:** Global `DispatcherUnhandledException` handler shows a friendly error message instead of silently closing.
- **GTA IV setup wizard:** Opening GTA IV with no paths configured now shows `InitialSetupWindow` first.
- **TBoGT alternate path detection:** Recognises both `EFLC\` and `TBoGT\` subfolder names when auto-deriving paths from the IV install directory.
- **`CODEBASE_GUIDE.md`:** Full pseudocode table of contents and AI search index for all windows, services, models, and conventions.

### Changed
- **Custom Game Config window** enlarged to 800 × 800 (was 520 × 560).
- **Output Directory Mapping** column header now reads *"Output Folder  (. = game root)"* for clarity.
- **Error dialogs** show a concise one-line message instead of raw exception stack traces.

---

## [v0.02-prealpha-2] — 2026-05-22

### Changed - VFS Removal & Direct Deploy Architecture

**Major Architectural Refactor:**
- **Removed Virtual File System:** No more `TempStaging\` folder or `ModdedFolderName` concept. Mods deploy **directly to game directory** with per-extension output routing.
- **Backup System Simplified:** Before overwriting files, create timestamped backup in `AppData\TMM\Backups\{gameKey}\{timestamp}\`. Last **3 deploys per game** retained (changed from 5-deep to 3-deep for storage efficiency).
- **DeployManifest Tracking:** `.json` file in each backup records exactly which files changed, where they were copied from, and when.
- **Direct Launch:** Game launch now reads from actual game directory, not virtual folder.

**Code Cleanup (Dead Code Removal):**
- Removed `DeepScanDrives()` method (~25 lines) — never called; QuickScan handles all cases
- Removed `RecursiveSearch()` helper (~20 lines) — only used by DeepScan
- Removed `SmartSteamLaunch()` method (~20 lines) — deferred to future Smart DLL Wizard feature
- Removed `CopyDirectoryParallelAsync()` method (~50 lines) — sync `CopyDirectory()` sufficient for single-machine deploy
- Removed `DebugStaging` property from AppSettings — legacy VFS debugging flag
- Simplified `GameState.cs` from 145 lines → 9 lines:
  - Deleted `GameStateManager` singleton class
  - Deleted `GameDetectionState` record
  - Kept only `ExeStatus` enum (Vanilla/Downgraded/Unknown)
- Removed empty `ModList_SelectionChanged()` event handler in CustomGameDashboard
- Removed `SettingsContext` enum (Full/GtaIvOnly/CustomGame) from SettingsWindow — unified to global settings only
- Removed `SmartArchivePostProcess()` method (~55 lines) from Gta4DashboardWindow — deferred to CustomGameEditor for future implementation
- **Total Dead Code Removed:** ~240 lines, 0 errors/0 warnings after cleanup

**UI & Config Updates:**
- **Context menu:** "Open Virtual Folder" renamed to **"Open Backup Folder"** — opens rollback backup directory
- **`GetDriveSpaceInfo()`** updated to show total AppData size instead of VFS references
- **`BtnLaunchModded_Click`** now launches from actual game installation directory
- **Output mapping UI:** DataGrid (Extension / Output Folder columns) in Add Custom Game dialog
- **Status bar in launcher:** Uses `AccentBrush` on dark background panel for readability across all themes

### Added
- **Direct Deploy:** Mods are now copied straight into the game's actual installation directory. No virtual filesystem, no staging folder, no intermediate copies.
- **Automatic Backup & Rollback:** Before overwriting any file, the original is backed up to `AppData\TMM\Backups\{gameKey}\{timestamp}\`. Last 3 deploys per game are retained. `DeployManifest` JSON tracks every file changed.
- **Rollback Button:** New toolbar button (undo icon) lets you restore the last deploy for the active game. Works in both GTA dashboard and Custom Game dashboard.
- **Custom Game Support:** Add any game with a configurable name, directory, executable, and per-extension output subdirectory routing (e.g. `.asi` -> `plugins\`, `*` -> root).
- **Multi-game Launcher:** New `GameLauncherWindow` home screen showing all configured games as cards. Built-in GTA III Series card + custom game cards with Edit/Delete actions.
- **Back to Launcher Button:** Toolbar button in all dashboards to return to the launcher.
- **Reset Button (Launcher):** Clears the download cache with a confirmation dialog.

### Removed
- **Virtual File System (VFS):** `CloneToVirtualAsync()`, `ModdedFolderName`, `Modded{Key}` AppData folders — all removed.
- **TempStaging folder:** No longer created. `TempStagingPath` property removed from `BackendCore`.
- **`WipeTempStaging()`** replaced with `WipeDownloadCache()`.
- **mojibake characters** across all `.cs` and `.xaml` source files cleaned up (double-encoded UTF-8/Windows-1252 sequences replaced with ASCII equivalents).
- **Per-Game Settings Context:** Unified SettingsWindow to global settings only, removed context-aware layouts.

### Notes
- **Project Renamed:** TGTAMM → TMM (Triviali's Mod Manager). AppData migrated automatically from `%APPDATA%\TGTAMM` to `%APPDATA%\TMM` on first launch.
- **`TempStagingPath` → `DownloadCachePath`:** Staging folder concept removed. Download cache is now only used for temporary archive downloads before extraction, not for deployment.
- **DXVK config location:** `dxvk.conf` is now written to the actual game installation directory instead of the old virtual folder.

---

## [v0.02-prealpha-1] — 2026-05-21

### Added
- **Toast notification system** — in-window toasts in bottom-right corner (1280x672 window). Replaces desktop overlay with in-app notifications that stack and auto-dismiss.
- **Dice-roll theme button** — 🎲 button in toolbar applies random preset theme with success toast feedback.
- **Accent-colored window borders** — toggleable option in Settings → Themes to apply accent color to window border (compatible with all themes including Compact).
- **34+ theme presets** — comprehensive collection including Cyberpunk, Vaporwave, Terminal Green, Vice City Pink, San Andreas Dusk, and many more. *(Note: later curated to 25 in v0.1-alpha-2)*
- **Deploy-time override warnings** — when deploying with some games overridden, displays warning toast showing which games still need 1.0 executable.
- **Override context menu access** — "⚡ Toggle Force Deploy Override" available in mod list right-click menus and empty-list context menus.
- **Orange deploy button state** — when override is enabled, deploy button shows orange to signal "ready to deploy, but can't play yet."
- **Error code documentation** — updated all references to "Application Load Error 5:0000065434" for clarity.

### Changed
- **Main window dimensions** — locked to 1280x672 (optimized for 1280x720 displays with 48px taskbar at 100% scaling).
- **Notification architecture** — moved from separate desktop overlay window to integrated bottom-right corner panel within MainDashboardWindow.
- **Toolbar button sizing** — standardized to 38×38px for secondary buttons, 42×42px for primary deploy/play buttons.
- **Mica backdrop intensity** — made configurable per-theme (improved visibility on dark themes).
- **Panel color calculations** — simplified color theory with consistent lift values for better theme consistency.
- **Help window** — clarified distinction between deploy override (enables VFS deployment) vs. 1.0 executable requirement (needed for gameplay).

### Fixed
- **Toolbar icon visibility** — fixed color inconsistencies across themes (AccentBrush/AccentTextBrush theming).
- **Dice button visibility** — now uses AccentLabelBrush for consistent visibility on all themes.
- **Notification stacking** — toasts now properly stack and move within window bounds, disappearing cleanly as timers expire.
- **Corner rounding** — improved window border-radius consistency (10px outer, 9px inner clipping).
- **Win 7/8/9x button styling** — refined appearance and hover states to match theme intent.

---

## [v0.01-prealpha-3] — 2026-05-02

### Added
- **Modular title bar system** — six styles: macOS Dark, macOS Light, Windows Vanilla, Windows 8/10, Windows 7 Aero, Windows 9x Classic, Compact.
- **Multi-game GTA support** — dashboards for GTA III, Vice City, San Andreas, IV, TLaD, TBoGT with game-specific features.
- **Exe-as-Mod downgrading** — install 1.0 executables directly as mods with auto-detection.
- **Drag-and-drop load orders** — visual mod priority list with drop-line indicator.
- **HSV color picker system** — two-pane 2D spectrum pickers with hex input and live preview.
- **DWM Mica/Acrylic support** — Windows 11 native backdrop with adjustable intensity.
- **One-Click Essentials** — auto-download DXVK, SilentPatch, and other GTA essentials.
- **MD5 verification** — accepts all known 1.0 build variants for GTA games.

### Changed
- **Namespace:** TGTAMM throughout all `.cs` and `.xaml` files.
- **Assembly output:** TGTAMM.dll.
- **AppDataPath:** `%APPDATA%\TGTAMM\`.

### Known Issues (Deferred)
- Custom game toolbars missing features present in GTA dashboards
- Theme refresh partially broken after changes in non-primary dashboards
- "Open Mods Store" is a stub (no URL wired)

---

## [v0.01-prealpha-1-2] — 2026-04-30 *(Pre-Alpha Foundation)*

### Added
- **Initial WPF Framework:** Window hierarchy (TmmWindow base class), XAML structure.
- **Basic Settings System:** AppSettings model with JSON persistence to `%APPDATA%\TGTAMM\config.json`.
- **Game Detection:** QuickScan for GTA III Series installations (Steam, disk, portable).
- **Mod Installation:** Manual zip/rar/7z file selection and copy to mod directory.
- **Basic UI:** MainDashboardWindow with mod list, deploy button, simple styling.
- **Minimal Theming:** Dark theme with basic color customization.

### Notes
- This was the absolute foundation — minimal feature set, heavy refactoring in subsequent versions.
- Project originally named TGTAMM (GTA Trivial Archive Mod Manager).

---

## Summary by Version

| Version | Date | Focus | Files Changed |
|---------|------|-------|---------------|
| v0.01-prealpha-1-2 | 2026-04-30 | Foundation | New project |
| v0.01-prealpha-3 | 2026-05-02 | Modular UI, GTA multi-game, theming | 50+ files |
| v0.02-prealpha-1 | 2026-05-21 | Toast notifications, themes, UI polish | 30+ files |
| v0.02-prealpha-2 | 2026-05-22 | **VFS Removal, Direct Deploy, Custom Games, Launcher** | 45+ files |
| v0.1-alpha-1 | 2026-05-22 | **First Release: Routing Sentence Builder, GTA IV Support** | 15+ files |
| v0.1-alpha-2 | 2026-05-23 | **Theme Optimization, Dead Code Removal** | 3 files |
| v0.1-alpha-3 | 2026-05-23 | **Core Features, Test Routing, GameRegistry built-ins** | 15+ files |
| v0.1-alpha-4 | 2026-05-23 | **Dead Window Removal, Code Consolidation** | 20+ files |
| v0.1-alpha-6 | 2026-05-25 | **UI Refinements & Backend Stability** | 6 files |

---

## Planned Features (Roadmap)

### Short Term (v0.1-rc-1)
- Move theme selector from ThemeManagerWindow to GameLauncherWindow toolbar
- Strip ThemeManagerWindow: remove HSV pickers, complement tools (save ~500 lines)
- Unify toolbar features across all three dashboards (labels, color state, rollback, backup folder)
- Wire up Steam launch in CustomGameDashboard

### Medium Term (v0.2-alpha-1)
- Conflict resolution engine (warn on duplicate files between mods)
- Mod profiles & loadouts (save/load preset configurations)
- Smart DLL wizard (auto-detect proxy DLLs, suggest output directories)
- Expanded game profiles (Skyrim, Fallout, etc.)

### Long Term (v0.3+)
- Mod store integration (one-click install from ModDB/Nexus)
- SAMP / MTA support
- Network mod sharing between users
- Advanced conflict resolution with merge suggestions
