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

### A1 — Welcome: "built-in" option just opens the Library; retire the picker + Skip  🟢 Haiku
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

### A2 — Prompt to set a default when managing a game with no default set  🟢 Haiku
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

### A3 — Launch into the default game's Mod Manager instead of the Library  🟢 Haiku  *(depends on A2 for a default to exist)*
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

## Group D — Codebase health (standing)

### AUDIT1 — Periodic file-count & module-size audit  🔵 Sonnet (inventory) → 🟣 Opus (decisions)  ⏳ STANDING
Re-run when files sprawl. Last pass (2026-05-29) split `BackendCore.cs` and
`ModManagerPage.xaml.cs` into per-concern partials (see CHANGELOG). Next time, re-inventory the
top-20 largest source files and flag anything over ~800 lines for a partial-class split. **Gotcha:**
WPF code-behind splits must keep `partial class` + the XAML `x:Class` intact; never move
`InitializeComponent` wiring. Split only where it reduces real cognitive load.
