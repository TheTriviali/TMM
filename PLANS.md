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

**Suggested order:** do Group A (three Haiku quick-wins) first — they're independent and
unblock nothing. Then Group B and C, each of which needs an Opus design pass the user must
approve before the Sonnet build.

---

## Group A — Welcome flow + default-game navigation  (all 🟢 Haiku)

> These three are small, well-specified, and independent. Knock them out first.

### A1 — Welcome: "built-in" option just opens the Library; retire the picker + Skip  ✅ DONE (commit 17de60d)
**User ask:** *"'select a built in game' interface is clunky — scrap it and just show the
library if they hit that button, removing the need for the skip."*

**Current behavior:** [Views/InitialSetupWindow.xaml.cs](Views/InitialSetupWindow.xaml.cs)
`Option1_Click` opens `SelectBuiltinGameWindow` as a modal; `BtnClose_Click` is the "Skip"
path. The Library ([Views/Subpages/LibraryPage.xaml](Views/Subpages/LibraryPage.xaml)) already
lists every built-in game as a card, so the picker is redundant.

**Changes:**
1. `Option1_Click` → just call `CompleteSetup()` (which sets `FirstLaunch=false`,
   `DialogResult=true`, closes). No window. Leave `OpenAddGameAfterClose=false` so the shell
   lands on the Library (see `UnifiedShellWindow.Window_Loaded` ~line 121-129).
2. Remove the **Skip** button from `InitialSetupWindow.xaml` (the `BtnClose_Click` button near
   line 266 in the right panel) — the built-in card now fills that role. Keep the small **X**
   close overlay (top-right, line 306) so the dialog is still dismissible.
3. Relabel the opt1 card copy so it reads as "browse the library", not "pick one game". Update
   locale keys `Picker_BuiltinTitle` / `Picker_BuiltinDesc` in **both** `Assets/Locales/en-US.json`
   and `es-MX.json` (e.g. *"Go to your library" / "See all supported games and pick one to set up"*).
4. Delete `Views/SelectBuiltinGameWindow.xaml` + `.xaml.cs` (only referenced by `Option1_Click`).
   Confirm no other reference with a project-wide search for `SelectBuiltinGameWindow` before deleting.

**Verify:** fresh-launch (`/run --fresh`) shows the welcome screen; clicking the first card
closes it and shows the Library; clicking the second still routes to Add Game; the X still closes.

### A2 — Prompt to set a default when managing a game with no default set  ✅ DONE (commit 9472fd0)
**User ask:** *"if someone picks to manage a game and no default is set the program should ask
the user if they wanna set that first game as default or not."*

**File:** [Views/UnifiedShellWindow.xaml.cs](Views/UnifiedShellWindow.xaml.cs) → `OnManageRequested`
(~line 458). `Settings.DefaultGameKey` (string?) already exists; `OnDefaultToggled` (~line 488)
shows the set/clear pattern.

**Change:** at the top of `OnManageRequested(entry)`, if
`string.IsNullOrEmpty(_core.Settings.DefaultGameKey)` and `!entry.IsPlaceholder`, show a Yes/No
`MessageBox` ("Set **{entry.DisplayName}** as your default game? TMM will open straight to its
mods next time."). On **Yes**: `_core.Settings.DefaultGameKey = entry.Key; _core.SaveSettings();`
then `RefreshLibrary();`. Either way, continue into the existing manage navigation. Localize the
prompt strings (en-US + es-MX). Ask only once (the guard naturally stops asking once a default exists).

### A3 — Launch into the default game's Mod Manager instead of the Library  ✅ DONE
**User ask:** *"if a game is set as default the manager should show that game's mod manager page
rather than loading to library."*

**File:** [Views/UnifiedShellWindow.xaml.cs](Views/UnifiedShellWindow.xaml.cs) → `Window_Loaded`,
the final `if (setup?.OpenAddGameAfterClose == true) … else SetNavActive("Library")` block (~line 121-129).

**Change:** in the `else` branch, before falling back to the Library, check for a resolvable
default: find the entry in `BuildLibraryEntries()` where `IsDefault && !IsPlaceholder`. If found,
call `OnManageRequested(defaultEntry)` (which loads the page + navigates to ModManager) instead of
`SetNavActive("Library")`. If not found, keep the Library fallback. Do **not** redirect when
`OpenAddGameAfterClose` is true (the add-game flow wins). `pageModManager.BackRequested` already
returns to the Library, so the user isn't trapped.

**Verify:** set a default, restart → opens on that game's Mod Manager; clear the default, restart
→ opens on the Library.

---

## Group B — Library card & showcase visual polish

### B1 — Redesign GameCard action affordances: bigger, labeled, reorder-discoverable  ✅ DONE (commit bb82dbf)
**User ask:** *"right now all the small buttons don't really do anything — make 'em bigger,
label 'em, think about what would be most useful to have. i.e. I want them to be reorderable."*

**Current state:** [Views/Controls/GameCard.xaml](Views/Controls/GameCard.xaml) has five 24×24
(card mode) / 26×26 (list mode) icon-only buttons crammed bottom-right: Play `▶`, Manage `☰`,
Edit `⚙` (custom only), Export `↑` (custom only, no-op TODO), Archive/Delete `⊟`. There's also a
top-left default checkbox and a top-right status chip. Reorder already works via drag
([LibraryPage.xaml.cs](Views/Subpages/LibraryPage.xaml.cs): `GridCard_*` and `ListGrip_*`), but
it isn't discoverable and the user didn't realize it exists.

**✅ APPROVED DESIGN (Opus 4.8, signed off 2026-05-29) — frozen, build to this:**

Promote the two everyday verbs to large **labeled** buttons; demote rare actions into a `⋯`
overflow (also wired as right-click, consistent with the existing `MenuSetArt`/`MenuRemoveArt`
context menu); keep **Default** as a persistent **labeled toggle** on the card face (it's a state,
not an action — and A2/A3 made default-game central to navigation); make the **drag grip visible**.

*Card mode (240×160):*
```
┌──────────────────────────────────┐
│ ⠿                          ◖BETA◗ │  drag grip (faint → solid on hover) · status chip
│           GRAND THEFT             │
│            AUTO: III              │  title (unchanged)
│  ★ Default                    ⋯  │  default = pill toggle (labeled) · overflow
│  ┌──────────────┐ ┌─────────────┐│
│  │   ▶  Play    │ │  ☰  Manage  ││  two labeled primary buttons (~28px tall)
│  └──────────────┘ └─────────────┘│
├──────────────────────────────────┤
│ Rockstar · 2001           12 mods │  info strip (unchanged)
└──────────────────────────────────┘
```
*List mode (full-width × 72px):* `⠿  ★Default-toggle  Title [BETA] subtitle  N mods  ●  [▶ Play] [☰ Manage]  ⋯` — grip stays visible (already is); Play/Manage gain labels; Edit/Export/Archive move into the same `⋯` overflow.

*`⋯` overflow contents (both modes):* **Edit** (custom only) · **Export profile (.tmmgame)**
(custom only) · separator · **Archive / Unarchive / Remove** (existing 3-way logic verbatim).

*Export is WIRED (not removed):* the backend already exists — `GameRegistry.ExportConfigAsync`
([Services/GameRegistry.cs](Services/GameRegistry.cs) ~line 255). Replace the no-op `BtnExport_Click`
([Views/Controls/GameCard.xaml.cs](Views/Controls/GameCard.xaml.cs) ~line 277) with a `SaveFileDialog`
(filter `*.tmmgame`, default name = sanitized DisplayName) → resolve the entry's `CustomGameProfile`
via `Core` + the entry's game key → `await GameRegistry.ExportConfigAsync(profile, dlg.FileName)` →
success/failure toast via `NotificationService`. Custom-only (the button already gates on `isCustom`).

**🔵 Sonnet build:** implement the approved layout in `GameCard.xaml(.cs)`. Keep the existing
event surface (`PlayRequested`, `ManageRequested`, `ArchiveToggled`, `DefaultToggled`,
`EditRequested`, `CardClicked`) — `LibraryPage.CreateCard` wires them and the shell handles them;
don't break those signatures. The `⋯` items route to those same handlers. Preserve
`OnCardBodyClick`'s button-suppression walk (it ignores clicks landing on a `Button` or the default
checkbox — the toggle + overflow are `Button`s, so it still holds). Localize all new labels
(Play / Manage / Default / Edit / Export / Archive, en-US + es-MX). Build clean; manually verify
Play/Manage/Default/Archive/Edit all still fire, Export writes a valid `.tmmgame`, and drag-reorder
still persists via `OrderChanged`. **When done: build clean, verify, then commit** (e.g.
`feat: B1 — labeled GameCard actions + overflow menu, wire Export`).

### B2 — Showcase view: fix horizontal symmetry  ✅ DONE (commit be59e55)
**User ask:** *"I'd prefer showcase view to have more horizontal symmetry, it just looks a little
weird right now."*

**File:** [Views/Subpages/LibraryPage.xaml](Views/Subpages/LibraryPage.xaml) (the
`showcaseScrollViewer` / hero + `carouselPanel` section) and `RenderShowcaseView` /
`ApplyShowcaseHero` in [LibraryPage.xaml.cs](Views/Subpages/LibraryPage.xaml.cs) (~line 350-414).
Layout today = a hero block (cover on the left, meta/stats on the right) above a left-aligned
horizontal carousel of 140×186 portrait cards with prev/next chevrons.

**Direction (Opus 4.8, no separate design pass needed):** the imbalance is that the hero and the
carousel don't share a common centered content column and the left/right gutters differ. Make it
symmetric:
- Constrain the hero + carousel to a single shared max-width content column, **horizontally
  centered** in the page, with equal left/right margins.
- Center the carousel row within that column (currently left-aligned); keep the prev/next chevrons
  balanced on each side at equal insets.
- Equalize the hero's internal split (cover vs. meta) and vertical paddings so the two halves read
  as a balanced pair.
- Don't change the carousel scroll math (`CardStep`, `BtnCarousel*_Click`) beyond what centering
  requires.

**Acceptance:** at a few window widths the hero and carousel are centered with matching gutters and
nothing hugs the left edge. If achieving balance needs a real layout rethink rather than
margin/alignment tweaks, stop and escalate to 🟣 Opus for a mockup.

---

## Group C — In-manager downloads panel

### C1 — Embed a hidable Downloads panel in the Mod Manager  ✅ DONE (commit bb53882)
**User ask:** *"the user should have a downloads panel in the mod manager interface that is easily
hidable, maybe only show if the user actually uses the built-in download function for now."*

**Current state:** Downloads is its own shell page (`pageDownloads`, nav button `navBtnDownloads`,
[Views/Subpages/DownloadsPage.xaml.cs](Views/Subpages/DownloadsPage.xaml.cs)) with an embedded
browser + a built-in `DownloadFileAsync` path on `BackendCore`. The Mod Manager
([Views/Subpages/ModManagerPage.xaml](Views/Subpages/ModManagerPage.xaml) + partials) has a
collapsible sidebar and a deploy overlay but no downloads surface.

**✅ APPROVED DESIGN (Opus 4.8, signed off 2026-05-29) — frozen, build to this:**

This panel is the **archive-list half** of `DownloadsPage`, scoped to the game being managed —
**NOT a second browser**. The "built-in download function" = the WebView2 interceptor
([Views/Subpages/DownloadsPage.xaml.cs](Views/Subpages/DownloadsPage.xaml.cs) ~line 115,
`OnDownloadStarting`) that drops `.zip/.rar/.7z` into each game's archive folder
(`GetModsArchivePath(key)`).

1. **Where:** a **right-hand drawer** = a new 3rd column in the ModManager content grid
   ([Views/Subpages/ModManagerPage.xaml](Views/Subpages/ModManagerPage.xaml) ~line 281, currently
   2 cols: sidebar + workspace), structurally mirroring the collapsible left sidebar. Shown/hidden
   by a `⭳` toggle button on the **right side of the toolbar** (the left side has the sidebar
   toggle, `BtnToggleSidebarCustom_Click` ~line 191).
2. **What it shows:** (a) active/recent downloads with a progress bar (subscribe to the same
   `CoreWebView2DownloadOperation.StateChanged` the page uses); (b) completed archives in *this
   game's* folder — **reuse `BuildArchiveRow`'s rendering by extracting it to a shared helper**, do
   not duplicate; (c) **Install → this game** button per finished archive, routing into the existing
   install pipeline (`BtnInstallModCustom_Click`'s path) pre-targeted to the current game key — this
   is the panel's main value-add; (d) footer **Open archive folder** (reuse `BtnOpenArchiveFolder_Click`).
3. **"Only show if used" trigger — APPROVED: persisted flag.** Add
   `Settings.HasUsedBuiltInDownloads` (bool, default false), set `true` the first time the WebView2
   interceptor successfully saves an archive (in `OnDownloadStarting`'s Completed handler), then
   `SaveSettings()`. When the flag is false, the toolbar `⭳` toggle **and** the drawer column are
   **absent entirely** (not merely collapsed) — users who never download see nothing new. Once true,
   the toggle is available across restarts; the drawer itself still defaults to hidden until toggled.

**🔵 Sonnet build:** implement per the approved design. Keep ModManager
code-behind minimal and put the panel logic in an appropriate partial (the page was split into
`ModManagerPage.Toolbar.cs` / `.Loadouts.cs`; add a `ModManagerPage.Downloads.cs` partial if it
earns its own concern). Localize new strings (en-US + es-MX). Don't regress the standalone
Downloads page. Build clean; verify the panel hides by default, appears once the built-in download
function is used, and toggles cleanly. **When done: build clean, verify, then commit** (e.g.
`feat: C1 — hidable in-manager Downloads drawer`).

**Note:** scope was pinned in the approved design above — resist rebuilding the whole browser
inside the manager; this is the archive-list + install surface only.

---

## Group E — Setup & download flow fixes  ✅ ALL DONE

> **Context.** User walked the cold-start flow (factory reset → "go to your library" → Downloads →
> download SilentPatch III → Mod Manager → Install) and hit five rough edges. Opus diagnosed each
> against the code; the two design forks were resolved by the user on 2026-05-29:
> **(1) flow approach = "context-follow" (lightweight)** — Downloads & Mod Manager silently follow
> the active/default game, no new persistent chrome; **(2) path setup = "inline banner + clickable
> sidebar"** — no wizard/prompt changes. Build to those decisions; do not re-open them.

| Item | Status | Details |
|------|--------|---------|
| **E1** | ✅ | `SetActiveGame()` pre-selects dropdown per active game |
| **E2** | ✅ | `OpenOwnedFolder()` unifies handlers, creates on demand |
| **E3** | ✅ | Inline `Cust_SetFolderBanner` + clickable sidebar Browse |
| **E4** | ✅ | `InstallArchiveForGameAsync()` on Downloads page |
| **E5** | ✅ | Drawer auto-opens when archives present for the game |

---

### ~~E1 — Downloads page follows the active/default game~~ ✅ DONE
**Symptom:** opening the Downloads tab always pre-selects GTA III regardless of what the user is
working on.

**Root cause:** [Views/Subpages/DownloadsPage.xaml.cs](Views/Subpages/DownloadsPage.xaml.cs)
`Initialize` (~line 60) picks `_entries.FirstOrDefault(e => e.IsDefault) ?? _entries[0]`, and the
shell's `NavigateTo("Downloads")` ([Views/UnifiedShellWindow.xaml.cs](Views/UnifiedShellWindow.xaml.cs)
~line 256) never tells the page which game is active — even though the shell already tracks
`_activeModManagerEntry` (~line 21).

**Changes:**
1. Add a public method to `DownloadsPage`:
   ```csharp
   /// <summary>Pre-select a game in the downloads dropdown by key (no-op if not found).</summary>
   public void SetActiveGame(string? gameKey)
   {
       if (string.IsNullOrEmpty(gameKey)) return;
       var match = _entries.FirstOrDefault(e => e.Key == gameKey);
       if (match != null) cmbGame.SelectedItem = match;
   }
   ```
   (`cmbGame`'s `SelectionChanged` already updates `_selectedGameKey` + refreshes the archive list,
   so no extra wiring.)
2. In the shell's `NavigateTo` `case "Downloads":` block, **before** showing the page, call
   `pageDownloads.SetActiveGame((_activeModManagerEntry ?? BuildLibraryEntries().FirstOrDefault(e => e.IsDefault && !e.IsPlaceholder))?.Key);`
   so it follows the game being managed, falling back to the default.

**Verify:** open Mod Manager on a non-III game, then click Downloads → dropdown shows that game.
With no game managed but a default set → shows the default. Build clean. ✅

### E2 — Fix "Location is not available" + unify folder-open handlers  ✅ DONE
**Symptom (screenshot):** right-clicking a mod → *Open mod(s) folder* throws Windows'
"`…\TMM\ModsRawIII` is unavailable" dialog.

**Diagnosis:** startup creates `ModsRaw{Key}` for every built-in game
([Services/BackendCore.cs](Services/BackendCore.cs) ~line 66-73), so the folder normally exists. The
error means a handler shelled out to a **missing** path. `ShellHelper.OpenFolder`
([Helpers/Helpers.cs](Helpers/Helpers.cs) ~line 10) does a raw `Process.Start` with no existence
check. The open-folder handlers in
[Views/Subpages/ModManagerPage.xaml.cs](Views/Subpages/ModManagerPage.xaml.cs) are **inconsistent**:
`MenuOpenModsFolder_Click` (~line 569) and `MenuOpenBackupFolder_Click` (~line 562) `CreateDirectory`
first; `MenuOpenFolder_Click` (~line 545) guards on `Directory.Exists` and **silently does nothing**
if missing (also bad UX); `MenuOpenGameFolder_Click` (~line 552) shows a message box. Most likely
trigger: **`FactoryReset` wipes AppData without re-creating the base folders for the running session**
(see `FactoryReset` in [Services/BackendCore.Settings.cs](Services/BackendCore.Settings.cs) ~line 47)
— so until the next launch/re-init those `ModsRaw*` dirs are gone.

**Changes (reproduce the exact path first, then):**
1. Add a helper in `ShellHelper`:
   ```csharp
   /// <summary>Open a TMM-owned folder, creating it first so it always exists.</summary>
   public static void OpenOwnedFolder(string path)
   { Directory.CreateDirectory(path); OpenFolder(path); }
   ```
2. Route the **TMM-owned** folder handlers through it: `MenuOpenModsFolder_Click`,
   `MenuOpenBackupFolder_Click`, the toolbar Files button (`BtnOpenAppData_Click`), and the new
   Downloads-drawer `BtnOpenDownloadFolder_Click` (already create-then-open — make them all use the
   helper for consistency).
3. Fix `MenuOpenFolder_Click` (per-mod): if `mod.RawFolderPath` is missing, don't no-op silently —
   fall back to opening the parent mods folder via `OpenOwnedFolder(Path.Combine(_core.AppDataPath, _customProfile.RawFolderName))`
   (or show a toast "that mod's folder no longer exists"). Game folder handler keeps its message box
   (external path TMM doesn't own).
4. **Root-cause guard:** make `FactoryReset` either trigger an app restart, or re-run the base-folder
   creation block from `BackendCore`'s ctor (`ModsRaw{Key}` per `GameProfile.All`, `Backups`,
   `DownloadCache`) so a post-reset session isn't left with missing dirs. Pick whichever matches the
   existing reset UX — check whether `FactoryReset` already prompts a restart.

**While here, sanity-check a key-mismatch hypothesis:** confirm the Downloads dropdown and the Mod
Manager resolve "GTA III" to the **same** game key (e.g. `III`, not a grouped `GTA_III_SERIES`). If
they differ, an archive installs under one `ModsRaw*` while the manager reads another — surface that
to Opus rather than papering over it.

**Verify:** factory reset, then (without relaunching) Install a mod and use every Open-folder menu
item — none should throw; missing TMM folders are created on demand. Build clean. ✅

### E3 — Inline "Set game folder" banner + clickable sidebar path  ✅ DONE
**User ask:** *"path picking is awkward cuz u have to go into edit game inside the mod management
window."* **Approved design = inline banner + clickable sidebar** (no wizard/prompt changes).

**Current state:** the only way to set a built-in game's directory is the toolbar **Edit Config**
button (`BtnEditConfigCustom_Click`, [Views/Subpages/ModManagerPage.Toolbar.cs](Views/Subpages/ModManagerPage.Toolbar.cs)
~line 260). The sidebar shows static "Directory not set" text
([Views/Subpages/ModManagerPage.xaml.cs](Views/Subpages/ModManagerPage.xaml.cs) ~line 114-117 sets
`Cust_txtSidebarDir`). Readiness is gated on `GameDirectory` existing (~line 185-186).

**Changes:**
1. Add a dismissible-but-recurring **banner** at the top of the workspace column in
   [Views/Subpages/ModManagerPage.xaml](Views/Subpages/ModManagerPage.xaml) (above the mod list,
   inside the workspace `Grid.Column="1"`), `x:Name="Cust_SetFolderBanner"`, `Visibility="Collapsed"`:
   accent-tinted bar reading *"📁 Set your {game} folder to deploy mods"* + a **[Browse…]** button.
2. Show the banner whenever `string.IsNullOrEmpty(_customConfig.GameDirectory) || !Directory.Exists(_customConfig.GameDirectory)`;
   hide it once a valid folder is set. Drive this from `UpdateSidebarCustom` (or a new
   `UpdatePathAffordances`) so it re-evaluates on every refresh.
3. **Browse handler:** open a folder picker (`Microsoft.Win32.OpenFolderDialog`, .NET 8+/WPF — confirm
   it's available in this project; else use the existing folder-pick pattern from the wizard/Edit
   Config). On pick: `_customConfig.GameDirectory = chosen; GameRegistry.Instance.SaveCustomGameSync(_customProfile.Key, _customConfig);`
   for custom games, or the built-in equivalent (built-ins store the path in
   `Settings.GamePaths[key]` — see `InitCustomGame` ~line 81-89 and `GetVanillaPath`; mirror how Edit
   Config persists it). Then `await RefreshCustomAsync()`.
4. Make the sidebar **"Directory not set"** text a clickable Button/hyperlink that invokes the same
   Browse handler. When a path *is* set, keep showing it (clicking it can open the folder via
   `ShellHelper.OpenOwnedFolder`/existing game-folder handler — optional).
5. Localize new strings (en-US + es-MX): banner text, Browse.

**Watch-out:** built-in vs custom path persistence differ (built-ins → `Settings.GamePaths`, customs →
`CustomGameProfile.GameDirectory` via `GameRegistry`). Reuse exactly what `BtnEditConfigCustom_Click`
does so both kinds persist correctly. **Standing rule:** this must also work for custom games added
via the wizard (it does — same `_customConfig` path).

**Verify:** open a built-in game with no path → banner + sidebar prompt appear; Browse sets the
folder, banner disappears, deploy becomes available, path persists across restart. Repeat for a
custom game. Build clean. ✅

### E4 — Install button on the Downloads page  ✅ DONE
**User ask:** *"no obvious way to install"* after downloading on the Downloads page.

**Current state:** the standalone Downloads page renders archive rows via
`ArchiveRowHelper.BuildRow(file, RefreshArchiveList)` **without an install callback**
([Views/Subpages/DownloadsPage.xaml.cs](Views/Subpages/DownloadsPage.xaml.cs) ~line 209-210), so rows
have only Open-location + Delete. The install pipeline (`InstallModFileCustomAsync` +
`OnModAddedAsync` + refresh) lives in
[Views/Subpages/ModManagerPage.Toolbar.cs](Views/Subpages/ModManagerPage.Toolbar.cs) ~line 41 and is
tightly bound to ModManagerPage state. `ArchiveRowHelper.BuildRow` **already supports** an
`installCallback` (added in C1) — the C1 drawer uses it; the standalone page just doesn't pass one.

**Changes:**
1. **Extract** the archive-install core into a reusable `BackendCore` method, e.g.
   `public async Task<bool> InstallArchiveForGameAsync(string gameKey, string archivePath)` that:
   resolves the `GameProfile`/`CustomGameProfile` for `gameKey`, extracts into
   `ModsRaw{Key}/{modName}` (reuse `ExtractArchiveSafeAsync`), writes `modinfo`, calls
   `OnModAddedAsync`, and refreshes that game's mod list. Have `ModManagerPage.InstallModFileCustomAsync`
   delegate to it (keep its UI-side notifications/proxy-DLL scan) so behavior stays identical and
   there's a single source of truth — do **not** duplicate the extract logic.
2. On the Downloads page, pass an `installCallback` to `BuildRow` that calls
   `_core.InstallArchiveForGameAsync(_selectedGameKey, file)` then `RefreshArchiveList()`, plus a
   success/failure toast ("Installed to {game}"). The button label should read **"Install"** (the
   helper already styles it).
3. Keep the install scoped to the **currently-selected** download game (`_selectedGameKey`), which
   E1 now keeps in sync with the active game.

**Verify:** download an archive on the Downloads page → each row shows Install → clicking it installs
into the selected game and the mod appears in that game's Mod Manager list. Confirm the C1 drawer's
install still works (shared method). Build clean. ✅

### E5 — Auto-open the Downloads drawer when archives are present  ✅ DONE
**User ask:** *"click downloads in toolbar to show downloads list — it should be showing already cuz
i started a download."*

**Current state:** C1's `InitializeDownloadsDrawer`
([Views/Subpages/ModManagerPage.Downloads.cs](Views/Subpages/ModManagerPage.Downloads.cs)) gates the
toggle's *availability* on `Settings.HasUsedBuiltInDownloads` but always starts the drawer
**closed** (`_downloadsDrawerOpen = false`).

**Change:** in `InitializeDownloadsDrawer`, when the flag is set **and** this game's archive folder
(`_core.GetModsArchivePath(_customProfile.Key)`) contains at least one `.zip/.rar/.7z`, start the
drawer **open** (`_downloadsDrawerOpen = true; Cust_DownloadsBorder.Visibility = Visibility.Visible;`)
and call `RefreshDownloadsDrawer()`. Otherwise keep it closed. Don't change the toggle's
availability logic. Keep it cheap — a single `Directory.EnumerateFiles(...).Any(...)` guarded in a
try/catch.

**Verify:** download something for game X, open X's Mod Manager → drawer is already open showing the
archive; a game with no archives opens with the drawer closed. Build clean. ✅

---

## Group F — Library card & view refinement  (Opus design pass, signed off 2026-05-29)

> **Context.** Opus reviewed the post-B1 GameCard + Library against the questions left open in
> [HANDOFF_UX.md](HANDOFF_UX.md). Three design forks were resolved with the user on 2026-05-29 and
> are **frozen** below — build to them, don't re-open. Core finding driving F1: the card carries
> *two* state signals wearing similar clothes — a tiny `IsReady` dot (the thing users actually need)
> and a prominent `ReleaseStatus` chip (TMM's *profile-maturity*, meaningless on a user-added game).
> The prominent badge spoke to our concern; the critical "can I mod this" signal was a 7px dot.
>
> All three are independent. Suggested order: **F3** (pure deletion, unblocks nothing) → **F1**
> (card state) → **F2** (new dialog).

### F1 — Replace ready-dot + maturity chip with one labeled readiness badge  ✅ DONE
**Decision (user, 2026-05-29):** readiness becomes the card's primary state signal; the
`ReleaseStatus` (BETA/ALPHA/PRE-ALPHA/TESTING) chip is **removed entirely from the card** (all games,
both modes) — not just hidden for customs.

**Remove:**
- `statusChip` + `txtStatus` ([Views/Controls/GameCard.xaml](Views/Controls/GameCard.xaml) ~254-261)
  and `listStatusPill` + `listTxtStatus` (~334-340).
- `ApplyStatusChip` ([Views/Controls/GameCard.xaml.cs](Views/Controls/GameCard.xaml.cs) ~201-227)
  and both call sites (~41, ~108). Leave the `LibraryEntry.Status` **model field** alone (harmless,
  may be reused later) — just stop the card rendering it.

**Add — readiness badge (replaces the `readyDot` / `listReadyDot` ellipses):**
- **States** (derive in `ApplyEntry` from existing fields):
  | Condition | Label (localized) | Tone |
  |---|---|---|
  | `!entry.IsReady` | **Needs folder** | amber `#E0A020` — actionable |
  | `entry.IsReady && entry.ModCount == 0` | **No mods** | muted grey `#80FFFFFF` |
  | `entry.IsReady && entry.ModCount > 0` | **Ready** | `UiColors.ReadyGreen` |
- **Card mode:** put the badge **top-right** (the corner the removed `statusChip` vacated — prime,
  now free) as a small rounded pill (dot + label). Delete `readyDot` from the bottom controls row
  (~101-109); that row collapses to `[★ Default pill] [spacer] [⋯ overflow]`.
- **List mode:** replace `listReadyDot` in Column 3 (~354-360) with the same pill; widen that
  `ColumnDefinition` from `22` to `Auto` (~271).
- **Clickability:** the **Needs folder** badge is a `Button` that fires the existing
  `ManageRequested` event (opens the Mod Manager, where the E3 inline banner already prompts for the
  folder — reuse, no new event). The **Ready** / **No mods** badges are static labels (no button
  chrome). The clickable variant is a `Button`, so `OnCardBodyClick`'s button-suppression walk still
  ignores it — don't touch that logic.
- Add `NeedsFolderAmber` (+ a muted "no mods" colour if not reusing an existing one) to `UiColors`.

**Localize** (en-US + es-MX): `Card_Ready`, `Card_NeedsFolder`, `Card_NoMods`. Remove now-unused
status-label strings only if they're truly orphaned (search first).

**Verify:** a game with no path shows an amber **Needs folder** badge that, clicked, opens its Mod
Manager with the set-folder banner; a path-set game with mods shows green **Ready**; a path-set empty
game shows muted **No mods**; no BETA/ALPHA chip appears anywhere; a custom game reads cleanly (no
maturity noise). Both grid and list. Build clean.

### F2 — Unify card colour + artwork into one "Appearance…" dialog  ✅ DONE
**Decision (user, 2026-05-29):** collapse the four scattered context items (Set Card Color / Reset
Color / Set Artwork / Remove Artwork) into a single discoverable **Appearance…** entry that opens one
dialog covering both.

**Current state:** [Views/Controls/GameCard.xaml](Views/Controls/GameCard.xaml) ~23-27 lists the four
items; handlers `MenuSetArt_Click` / `MenuRemoveArt_Click` / `MenuSetColor_Click` /
`MenuResetColor_Click` ([Views/Controls/GameCard.xaml.cs](Views/Controls/GameCard.xaml.cs) ~330-381).
Backend already exists: `Core.GetCardColor` / `SetCardColor` / `ClearCardColor` and
`Core.SaveLibraryArt` / `DeleteLibraryArt` / `GetLibraryArtPath`. A standalone `ColorPickerWindow`
(start/end hex) is invoked by `MenuSetColor_Click`.

**Build a new `Views/AppearanceDialog.xaml(.cs)`** (modal, `Owner = Window.GetWindow(card)`), seeded
with `entry.Key` + `Core`:
1. **Live preview** at top — a card-sized border that reflects the in-progress gradient/artwork
   (simplest: reuse the gradient brush + image-brush logic from `ApplyEntry` ~102-147).
2. **Colour section** — preset swatches + custom start/end hex (lift `ColorPickerWindow`'s controls
   in; if `ColorPickerWindow` has no other references after this, fold it in and delete it — search
   first). Editing updates the preview live.
3. **Artwork section** — current art thumbnail (via `GetLibraryArtPath`) + **[Choose image…]**
   (PNG `OpenFileDialog`, reuse `MenuSetArt_Click`'s filter/validation) + **[Remove]**. Note in the
   UI that **artwork overrides the gradient** (existing precedence in `ApplyEntry`).
4. **Footer** — **[Reset to default]** (`ClearCardColor` + `DeleteLibraryArt`), **[Cancel]**,
   **[Apply]**. On Apply, call the same backend setters the four handlers use (single source of
   truth — do not add new persistence paths), then `ApplyEntry(Entry)` to refresh the card.

Replace the four context-menu items + the `menu*` art/colour handlers with one **Appearance…** item
wired to open the dialog. Keep the `⋯` overflow / right-click parity (Appearance lives in both).

**Localize** (en-US + es-MX): `Card_Appearance`, `Appearance_Color`, `Appearance_Artwork`,
`Appearance_ChooseImage`, `Appearance_RemoveArt`, `Appearance_Reset`, `Appearance_ArtOverridesColor`,
`Appearance_Apply`, `Appearance_Cancel`. **Custom-game parity:** identical path (keyed by
`entry.Key`) — verify on a wizard-added game too.

**Verify:** right-click / ⋯ → Appearance opens one dialog; changing gradient and/or picking a PNG
previews live and persists on Apply; Reset clears both; Cancel discards; works for a built-in and a
custom game; survives restart. Build clean.

### F3 — Remove the Showcase view mode (keep grid + list)  ✅ DONE
**Decision (user, 2026-05-29):** the showcase carousel doesn't earn its complexity vs. grid+list.
Delete it; the switcher drops to two options.

**Remove (watch for dangling refs — build must stay clean):**
- Shell: `btnViewShowcase` button ([Views/UnifiedShellWindow.xaml](Views/UnifiedShellWindow.xaml)
  ~339-341) and its style line in `UpdateViewModeButtonStyles`
  ([Views/UnifiedShellWindow.xaml.cs](Views/UnifiedShellWindow.xaml.cs) ~377). `BtnViewMode_Click`
  then only ever sees `grid` / `list`.
- **Migration guard:** in `Window_Loaded` *before* `SetViewMode(Settings.LibraryViewMode)` (~118),
  if the persisted value is `"showcase"`, coerce to `"grid"` and `SaveSettings()` — otherwise a user
  who last left showcase active boots into a now-deleted view.
- Library XAML: the entire `showcaseScrollViewer` block
  ([Views/Subpages/LibraryPage.xaml](Views/Subpages/LibraryPage.xaml) ~75-319) — hero, carousel,
  chevrons.
- Library code-behind ([Views/Subpages/LibraryPage.xaml.cs](Views/Subpages/LibraryPage.xaml.cs)):
  `RenderShowcaseView` (~363), `ApplyShowcaseHero` (~391), `ResetCarousel`, `BtnCarouselPrev_Click`,
  `BtnCarouselNext_Click`, `AnimateCarousel`, the `CardStep` const (~434), `_showcaseHeroKey` field
  (~22), the `case "showcase":` switch arm (~141), the `showcaseScrollViewer.Visibility` reset
  (~127), and the showcase-hero click branch (~525, ~586-587) plus any portrait-card builder used
  only by the carousel.

Default `LibraryViewMode` stays `"grid"`; grid + list must remain fully functional incl. drag-reorder.

**Verify:** switcher shows two buttons (grid/list) that both work; a profile last set to showcase
opens on grid after the migration; no build warnings, no dangling `x:Name`/handler refs. Build clean.

---

## Group G — Notification feedback, error handling & error guide  (Opus design pass, signed off 2026-05-29)

> **User ask:** *"Begin adding notification feedback for pretty much anything you do in the
> program, pass or fail, implement error handling at certain points and an error guide alongside."*
>
> **Opus findings (2026-05-29):**
> 1. The notification *engine* ([Services/NotificationService.cs](Services/NotificationService.cs)) is
>    already strong — transient `Queue` (toasts) + persistent `History` (the Notifications tab) +
>    a `ShowVerbose` tier (history-only unless `Settings.VerboseNotifications`) + JSON persistence.
>    **Do not rebuild it.** This group is *instrumentation + hardening + a guide*, not new plumbing.
> 2. **Linchpin gap:** the toast host (the `ItemsControl` bound to `NotificationService.Queue`) lives
>    **only** in [Views/Subpages/ModManagerPage.xaml](Views/Subpages/ModManagerPage.xaml) ~656-690.
>    Toasts raised on Library / Downloads / Settings / Backups / the wizard record to history but
>    are **never visibly shown**. "Feedback for anything you do" is impossible until the host is
>    promoted to the shell. **G1 must land first; everything else depends on it.**
> 3. Coverage is uneven: ~67 `NotificationService.Show*` calls vs ~24 blocking `MessageBox.Show` and
>    ~43 catch blocks spread unevenly. Many user actions are silent; many errors are free-text with
>    no remedy.
>
> **Frozen decisions (user, 2026-05-29) — build to these, don't re-open:**
> - **Error guide = in-app Help page + error codes.** Errors carry a short code (`TMM-E###`) that
>   maps to a catalog entry (title / cause / fix). Error toasts + history rows get a *"What does this
>   mean?"* affordance that deep-links to the new Troubleshooting page scrolled to that code.
> - **Tiered toasts.** Significant outcomes (deploy, rollback, install, import, save settings, create/
>   delete game, backup/restore, loadout apply) **always** toast pass/fail. Chatty internals (folder
>   creation, plan freeze, selection changes, per-file progress) use `ShowVerbose` — history-only
>   unless verbose mode is on. Use the engine as designed; don't make routine clicks noisy.
>
> **Suggested order:** **G1** (foundational, blocks all) → **G2** (the guide page) → then the
> instrumentation passes **G3–G9** in any order (they're independent per-subsystem; each appends its
> own new codes to the catalog + locales). G3/G4 are the highest-value (deploy + install).

### G1 — Promote toast host to the shell + error-code plumbing  ✅ DONE (commit 874fec0)
**Goal:** toasts visible on every page, and a first-class error-code field threaded through the
notification engine.

**Changes:**
1. **Move the toast host to the shell.** Cut the `<!-- Notification toasts -->` `ItemsControl` block
   (+ its `Toast_Close` / `Toast_CloseBtn` handlers) out of
   [Views/Subpages/ModManagerPage.xaml](Views/Subpages/ModManagerPage.xaml) ~656-690 (+ the handlers
   in [ModManagerPage.xaml.cs](Views/Subpages/ModManagerPage.xaml.cs)) and into
   [Views/UnifiedShellWindow.xaml](Views/UnifiedShellWindow.xaml) as a **top-level overlay** — a
   `Panel` in the outermost `Grid` spanning all rows/columns, top-right aligned,
   `IsHitTestVisible="False"` on the container but `True` on each toast (so pages stay clickable
   behind it). Move the two close handlers to `UnifiedShellWindow.xaml.cs`. **Remove from
   ModManagerPage to avoid double toasts.** Verify a toast raised on the Library and on Settings now
   appears.
2. **Add `string? ErrorCode` to `NotificationItem`** ([Services/NotificationService.cs](Services/NotificationService.cs)).
   Add overloads: `ShowError(string message, string source, string? errorCode)` and a matching
   `Show(..., string? errorCode = null)`. Keep existing signatures working (default `errorCode = null`).
3. **Error catalog — new `Services/TmmError.cs`.** A static, readonly catalog: `Code` (`"TMM-E001"`…)
   → record with `Code`, `Source`, and three **locale keys** `TitleKey` / `CauseKey` / `FixKey`
   (resolved via [Services/LocalizationService.cs](Services/LocalizationService.cs), not hard-coded
   English). Seed it with the codes G3–G9 will reference (start with a handful: deploy-failed,
   install-extract-failed, backup-write-failed, game-dir-missing, settings-save-failed — see each
   pass below). Provide `TmmError.Get(code)` and `TmmError.All`.
4. **Deep-link hook.** Add `public static Action<string>? OnErrorGuideRequested;` to
   `NotificationService`. The toast/history "What does this mean?" affordance (only rendered when
   `ErrorCode is not null`) invokes `OnErrorGuideRequested?.Invoke(code)`. The shell wires this in its
   ctor to navigate to the Troubleshooting page (G2) and call `ScrollToCode(code)`. Until G2 lands,
   wiring it to `NavigateTo("Notifications")` is an acceptable stub.
5. Localize the "What does this mean?" label (en-US + es-MX).

**Verify:** raise `ShowError("x", "Deploy", "TMM-E001")` from the Library page → toast shows there,
carries the link, history row shows the code. Build clean. **When done: build clean, commit**
(`feat: G1 — app-wide toast host + error-code plumbing`).

### G2 — In-app Troubleshooting / error-guide page  ✅ DONE (commit d98549b)
**Build `Views/Subpages/TroubleshootingPage.xaml(.cs)`** + a shell nav entry (alongside
`navBtnNotifications`; mirror its `NavigateTo`/`SetNavActive` wiring ~219-330 +
`pageNotificationsPlaceholder` pattern). The page renders **all `TmmError.All`** entries, grouped by
`Source`, each as a card: code chip (`TMM-E012`), localized **title**, **cause**, **fix**. A search
box filters by code/title. Expose `public void ScrollToCode(string code)` that scrolls to + briefly
highlights the matching card (the G1 deep-link target). Localize the page chrome (en-US + es-MX); the
catalog strings are already localized by G1/each pass.

**Verify:** open the page → all codes listed with cause+fix; clicking a real error toast's "What does
this mean?" jumps straight to that entry. Build clean.

### G3–G9 — Per-subsystem instrumentation passes  *(each depends on G1+G2; independent of each other)*
**Shared recipe for every pass** (apply uniformly):
- Wrap the risky operation(s) in a **specific** try/catch (per CLAUDE.md: not bare `Exception` where a
  narrower type fits — `IOException` / `UnauthorizedAccessException` / `JsonException` etc.).
- On **success**, toast `ShowSuccess(localized, source)` for significant actions; `ShowVerbose` for
  minor/internal steps.
- On **failure**, `ShowError(localized, source, "TMM-E###")` **and** `Logger.Error(msg, ex)`. Append
  the new code(s) to `TmmError` + both locales (title/cause/fix).
- **Replace blocking error `MessageBox.Show`** dialogs with toasts. **Keep** `MessageBox` only where
  it asks a *question / confirmation* (Yes/No, overwrite, destructive confirm) — those are decisions,
  not feedback.
- Don't change business logic; this is feedback + error-handling only.

| Pass | Model | Subsystem & files |
|---|---|---|
| **G3** | ✅ DONE (9b87ff3) | **Deploy / rollback** |
| **G4** | ✅ DONE (240592c) | **Install / import** |
| **G5** | ✅ DONE (24f9573) | **Backups** |
| **G6** | ✅ DONE (24f9573) | **Loadouts** |
| **G7** | ✅ DONE (24f9573) | **Settings / paths / factory reset** |
| **G8** | ✅ DONE (588db65) | **Game create / edit / export + wizard** |
| **G9** | ✅ DONE (6864efe) | **Downloads** |

**Each pass, when done:** build clean, verify a representative pass *and* fail path shows the right
toast (and the fail's "What does this mean?" lands on the new catalog entry), then commit
(e.g. `feat: G3 — deploy/rollback notification + error-guide coverage`).

---

## Group H — Mockup backport: delegable backend/wiring  (design in [HANDOFF_BACKPORT.md](HANDOFF_BACKPORT.md))

> **Context.** Opus + user approved backporting the three design mockups (`Mockups/`)
> into the app. The cross-cutting *UI* restructure (tabbed Game Workspace, Library Home
> replacing the grid, enriched list layout) stays Opus-led in the backport chat. The
> briefs below are the **well-specified backend/data pieces those screens need** — land
> them first; they're independent and testable. **Frozen decisions** live in the handoff;
> a few taxonomy/semantics questions there (categories shape, "pending" definition) should
> be answered by the user before H1/H3 are built — escalate if you hit them.
>
> **Deferred, do NOT build:** mod source+version capture, update-available checking.

### H1 — `ModItem` category/tag field + persistence + wizard config  ✅ DONE (fb4d32d)
Fixed-preset single category per mod. `ModItem.Category` persists via modinfo.json.
`ModCategories.cs` holds the 5-value preset + stable colour map. `CustomGameProfile.ModCategories`
lets custom-game profiles override the preset via the wizard (Step 1 + Step 4 review).
Localized en-US + es-MX. Frozen decision: fixed preset, single category.

### H2 — Per-mod conflict aggregation helper  ✅ DONE (9ac628c)
Added `ModConflictSummary` / `ModConflictClash` types and `AnalyzeByMod()` to
`ConflictAnalyzer`. Covers file-destination + proxy-DLL conflicts. 7 unit tests in
`TMM.Tests/ConflictAnalyzerTests.cs`.

### H3 — Pending-changes tracker on `BackendCore`  ✅ DONE (3fa52e3)
Added `PendingChanges(gameKey)` → `PendingChangesSummary` in new `BackendCore.PendingChanges.cs`.
Diffs current enabled mods + load order vs last `DeployManifest`. Pending baseline is
always the last physical deploy; loadout switches do not reset it.

### H4 — Batch mod operations  ✅ DONE (c7327b3)
Added `ModManagerPage.Batch.cs` with `BatchEnable`, `BatchDisable`, `BatchSetGroup`,
`BatchRemove`. One save + one summary toast per batch. `BatchSetGroup` re-plans affected
mods. `BatchRemove` still confirms before deleting.

---

## Group D — Codebase health (standing)

### AUDIT1 — Periodic file-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ STANDING
Re-run when files sprawl. Last pass (2026-05-29) split `BackendCore.cs` and
`ModManagerPage.xaml.cs` into per-concern partials (see CHANGELOG). Next time, re-inventory the
top-20 largest source files and flag anything over ~800 lines for a partial-class split. **Gotcha:**
WPF code-behind splits must keep `partial class` + the XAML `x:Class` intact; never move
`InitializeComponent` wiring. Split only where it reduces real cognitive load.
