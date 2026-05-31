# Graveyard — Archived Tracker Content

Scrapped 2026-05-31. Kept for reference only.

---

## tracker_0_decisions.md — Design Decisions

Ported to `CLAUDE.md § Design Decisions`.

---

## tracker_1_haiku.md — Pending Verification

### Run `/run --fresh` — welcome screen
- [C] Library link removed; language picker at bottom-left of right panel
- [C] Language globe icon — blue tint, Segoe Fluent F2B7 glyph

### Run `/run` — shell
- [C] Hamburger toggle gone; nav rail fixed width, always visible
- [C] T logo gone from nav rail top
- [C] Titlebar shows wrench icon + page name (no "TMM —")
- [C] Troubleshooting & Help is above Settings in bottom section
- [C] Window edges are draggable for resize

### Run `/run` — library
- [C] "Your games" shows configured games only; empty-state hint on fresh start
- [C] Stats show "Games with folder" + "Last deployed"

### Run `/run` → Mod Manager → hover mod rows
- [C] No ghost checkbox artifact on hover

### Run `/run` → Downloads tab
- [C] Address bar does not span full width

### Run `/run` → deploy a mod with es-MX locale active
- [C] Toast notification renders non-English characters correctly

---

## tracker_2_sonnet.md — Settings Dual-Pane

Redesign `Views/Subpages/SettingsPage.xaml(.cs)` from a scrollable single column to a two-column layout.

**Left pane:** category list  
**Right pane:** selected category's controls (no full-page scroll)  
**Unsaved changes banner:** top of right pane only

Category mapping:
- **General** — Startup preference, Notifications
- **Appearance** — Accent color preset + custom hex inputs
- **File Paths** — File Locations panel
- **Advanced** — Diagnostics (error log, wipe cache), Danger Zone (factory reset), version label

- [✓] Implement dual-pane layout in `Views/Subpages/SettingsPage.xaml(.cs)`

---

## tracker_3_sonnet.md — Critical Navigation Blockers

- [ ] `modlist-tab-navigation` — mod manager tabs cannot be clicked at all; fix first (`Views/Subpages/ModManagerPage.xaml`)
- [ ] `ui-game-dir-guard` — gate mod management actions on game directory being set
- [ ] `ui-lag-needs-folder` — UI reflects path update immediately (no lag after folder is set)

---

## tracker_4_sonnet.md — First-Run Flow

- [ ] `firstrun-wizard-revamp` — short wizard with .tmmgame dropdown; auto-launches after welcome screen; second-game path via library path-setting
- [ ] `firstrun-startup-prefs` — library-first vs mod-manager-first preference exposed during first run
- [ ] `firstrun-default-game` — expose default game control; last-opened as fallback
- [ ] `firstrun-default-change-notif` — notify user when default game changes

---

## tracker_5_sonnet.md — Library Refinements

- [ ] `library-scroll-arrows` — circular overflow indicators when library card row overflows
- [ ] `library-tmmgame-filenames` — show `.tmmgame` filename in small text on each card
- [ ] `library-context-menus` — right-click card → open config editor
- [ ] `library-path-setting` — set game dir from library card without entering full wizard
- [ ] `nav-config-tab-justify` — Config tab: path-setting control + "Advanced config" button (no full wizard)
- [ ] `ui-browse-window-title` — show game name in directory-browse dialog title bar
- [ ] Status bar globe icon — apply same blue tint + Fluent glyph fix as welcome screen globe

---

## tracker_6_sonnet.md — Mod Manager & Modlist

- [ ] `modlist-tab-rename-conflicts` — rename "Conflicts" tab to "Conflict Manager"
- [ ] `modlist-sorting` — sort by load order / name / group, asc/desc toggle
- [ ] `modlist-entry-controls` — per-row controls: folder icon, up/down arrows, re-install, edit plan

---

## tracker_7_sonnet.md — Mod Installation & Plan Editor

- [ ] `install-preview` — Deployment Plan Editor (two-pane routing tool, replaces DeployPreviewWindow)
- [ ] `install-tar-gz` — add `.tar.gz` archive support alongside `.zip`/`.rar`

### Plan Editor Spec

**Left pane (game directory):** Read-only file tree of existing game dir. Files claimed by other mods highlighted. Users can create new folders (created on disk at deploy time). Explorer quick-link.

**Right pane (mod source files):** Mod's extracted files with routing-rule-assigned destination inline. Drag file → drop on game dir folder → destination updates. Operations: reassign, create folders, rename file (renames in ModsRaw immediately), exclude from plan (file stays, not deployed). Explorer quick-link.

**Unrecognized file rules (in order):**
1. Same level as a recognized file → same destination as neighbor
2. Filename contains "readme" (case-insensitive) → rename to `readme_{modname}.txt`, deploy to game root
3. Otherwise → game root, flagged for review

**Flows:**
- Fresh install: extract → routing rules auto-populate plan → Plan Editor opens → confirm freezes, cancel aborts
- Edit existing: open from mod list at any time → after edit, mod flagged "needs redeploy"
- Archive-gone re-plan: if archive present ask user (warn manual changes lost); if gone, open Plan Editor directly on already-extracted files

**Batch install:** Multiple archives dropped → pre-confirmation dialog → alphabetical order → Plan Editors one at a time → mid-sequence cancel aborts only that mod, rest continue

**Orphaned files:** Pre-deploy blocking dialog lists files no longer in updated plan; asks permission to remove before proceeding.

---

## tracker_deferred.md — Deferred

Ported to `planned_features.md`.
