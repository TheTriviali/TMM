# TMM Tracker

**C** = code done, build clean. **N** = verified in running app. Both required to close an item.

- `[ ]` open  
- `[C]` Claude done — needs Noah verification  
- `[✓]` complete

---

## Design Decisions (frozen — don't re-open without a conversation)

1. **Library "Your games" shows configured games only.** Empty section with hint on fresh start. Hero card also restricted. Stats: "Games with folder" + "Last deployed."

2. **Mod types + routing rules merged.** Wizard Steps 2/3 combined: each row is a mod type with extensions and target folder.

3. **Help + Troubleshooting merged, global.** Single "Troubleshooting & Help" rail entry. No separate Help/About overflow.

4. **Config tab = quick path-setting + "Advanced config" link.** Not a full wizard launch.

5. **No "custom game" distinction.** `CUSTOM_` prefix and branching code paths are debt to remove. All games are first-class profiles.

6. **Nav rail structure.** Left strip: Library, Mod Manager (top). Troubleshooting & Help, Settings (bottom). Always-visible fixed width. Title bar: app icon + page name, no "TMM —" prefix.

7. **Unified mod list filter bar.** Search + tab row (All / Enabled / Conflicts / Favorites) in one horizontal control.

8. **Install Mod button inline.** Action bar alongside Deploy and Play.

9. **Library as polished .tmmgame file browser.** Each card shows backing `.tmmgame` filename.

10. **Conflicts tab → "Conflict Manager".** Main Mods tab shows conflict existence at a glance; tab is for resolving them.

11. **Routing rules never regenerate frozen plans.** Rules are a starting point for new installs only.

12. **Plan Editor always mandatory.** Shown on every install. User had the chance to review — not TMM's fault if a mod lands in the wrong place.

13. **Smart partial redeploy.** Only re-deploy mods whose plan or state changed.

14. **"Needs redeploy" state.** Surfaced on: mod row badge, Deploy button, library game card.

15. **Direct Install Override — WON'T IMPLEMENT.** Game root routing destination covers the use case.

---

## Pending Verification (Haiku — no code changes)

Run the app and check. Mark `[✓]` as each passes.

**Start:** any time — these are independent  
**Stop:** all items checked

### Run `/run --fresh` and check welcome screen:
- [C] Library link removed from welcome screen; language picker at bottom-left
- [C] Language globe icon — blue tint, Segoe Fluent F2B7 glyph *(status bar globe still pending — separate item below)*

### Run `/run` and check shell:
- [C] Hamburger toggle gone; nav rail always visible at fixed width
- [C] T logo gone from nav rail
- [C] Titlebar shows wrench icon + page name (no "TMM —")
- [C] Troubleshooting & Help is above Settings in bottom section
- [C] Window edges are draggable for resize
- [C] Library home — "Your games" shows configured games only; stats show "Games with folder" + "Last deployed"

### Run `/run` → open Mod Manager → hover mod rows:
- [C] No ghost checkbox artifact on hover

### Run `/run` → open Downloads tab:
- [C] Address bar does not span full width

### Run `/run` → deploy a mod with es-MX locale:
- [C] Toast notification renders non-English characters correctly

---

## Section 1 — Settings Dual-Pane

**Model:** Sonnet  
**Start:** fresh chat  
**Stop:** all categories render without full-page scroll, build clean, Noah verifies  

Left pane: category list. Right pane: selected category's controls.  
Categories: General (startup + notifications) · Appearance (accent colors) · File Paths (locations panel) · Advanced (diagnostics, factory reset, version label)  
Unsaved changes banner sits at top of right pane only.

- [ ] Redesign `Views/Subpages/SettingsPage.xaml(.cs)` to dual-pane layout

---

## Section 2 — Critical Navigation Blockers

**Model:** Sonnet  
**Start:** Section 1 complete  
**Stop:** all three working in running app, build clean  

- [ ] `modlist-tab-navigation` — mod manager tabs cannot be clicked at all; fix first (`Views/Subpages/ModManagerPage.xaml`)
- [ ] `ui-game-dir-guard` — gate mod management actions on game directory being set
- [ ] `ui-lag-needs-folder` — UI reflects path update immediately (no lag)

---

## Section 3 — First-Run Flow

**Model:** Sonnet  
**Start:** Section 2 complete  
**Stop:** cold-start walkthrough passes (factory reset → library → set folder → install → deploy → launch game)  

- [ ] `firstrun-wizard-revamp` — short wizard with .tmmgame dropdown; auto-launches after welcome screen; second-game path via library-path-setting
- [ ] `firstrun-startup-prefs` — library-first vs mod-manager-first preference (already wired in Settings, needs first-run exposure)
- [ ] `firstrun-default-game` — expose default game control; last-opened as fallback
- [ ] `firstrun-default-change-notif` — notify user when default game changes

---

## Section 4 — Library Refinements

**Model:** Sonnet  
**Start:** Section 3 complete  
**Stop:** all items verified in running app, build clean  

- [ ] `library-scroll-arrows` — circular overflow indicators when card row overflows
- [ ] `library-tmmgame-filenames` — show `.tmmgame` filename in small text on each library card
- [ ] `library-context-menus` — right-click card → open config editor
- [ ] `library-path-setting` — set game dir from library card without entering full wizard
- [ ] `nav-config-tab-justify` — Config tab: path-setting control + "Advanced config" button (no full wizard)
- [ ] `ui-browse-window-title` — show game name in directory-browse dialog title bar
- [ ] Status bar globe icon — apply same blue tint fix as welcome screen globe (leftover from Pending Verification)

---

## Section 5 — Mod Manager & Modlist

**Model:** Sonnet  
**Start:** Section 4 complete  
**Stop:** all items verified in running app, build clean  

- [ ] `modlist-tab-rename-conflicts` — rename "Conflicts" tab to "Conflict Manager"
- [ ] `modlist-sorting` — sort by load order / name / group, asc/desc toggle
- [ ] `modlist-entry-controls` — per-row controls: folder icon, up/down arrows, re-install, edit plan

---

## Section 6 — Mod Installation & Plan Editor

**Model:** Sonnet  
**Start:** Section 5 complete  
**Stop:** Plan Editor verified end-to-end on a real mod install, build clean  

This is the largest remaining feature. Full spec in the appendix below.

- [ ] `install-preview` — Deployment Plan Editor (two-pane routing tool, replaces DeployPreviewWindow)
- [ ] `install-tar-gz` — add `.tar.gz` archive support alongside `.zip`/`.rar`

---

## Deferred — No Active Work

Analysis complete. Revisit post-v1.

- VFS — high complexity, deferred indefinitely
- Nexus Mods API — feasible via REST, post-v1
- FOMOD installer — feasible, do after install-preview ships
- Single-layer deploy undo — feasible via backup system, post-v1

---

## Appendix — Deployment Plan Editor Spec

Two-pane file routing tool. Opens on every mod install; also accessible from mod list via "Edit Plan."

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
