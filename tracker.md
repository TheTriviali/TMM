# TMM Tracker — Context for Claude

Open `tracker.html` in a browser (manually load `tracker.json` when prompted — fallback
was removed). This file gives a new session enough context to work without reading stale
handoff docs.

**Two roles:**
- **C (Claude)** — checked when code change is done and build is clean.
- **N (Noah)** — checked after visual/functional verification in the running app.

An item is only complete when both are checked.

---

## Design decisions in effect

Frozen — don't re-open without a conversation.

1. **"Available Games" section** — not needed. "Add a game" button in Library header is
   sufficient.

2. **Mod types + routing rules merged** — wizard Steps 2 and 3 are redundant. Redesigned
   wizard combines them: each row is a mod type with its extensions and target folder.

3. **Help + Troubleshooting merged, global** — single "Help & Troubleshooting" rail entry
   (FAQ, log/error viewer, About). Replaces separate Troubleshooting entry and Help/About
   overflow items.

4. **Config tab = quick path-setting + "Advanced config" link** — lightweight panel with a
   path-setting control and an "Advanced config" button that opens the full wizard in edit
   mode. NOT a full wizard launch. No distinction between built-in and user-added games.

5. **No "custom game" distinction — all games are first-class** — `CUSTOM_` key prefix,
   `CustomGameProfile` type, and code paths branching on "is this a custom game" are
   architectural debt to remove. Built-in games are pre-seeded profiles from `.tmmgame`
   assets.

6. **Sidebar navigation structure** — vertical rail on left. Top: Logo (themed), Library,
   Mod Manager. Bottom: Help & Troubleshooting, Settings. Sidebar is resizable. A hamburger
   toggle occupies the former Install Mod button position (moved to action bar per #8).

7. **Unified mod list filter bar** — filter searchbox + tab row (All / Enabled / Conflicts
   / Favorites) consolidated into one horizontal control.

8. **Install Mod button inline** — moves to action bar alongside Deploy and Play. Vacated
   position → sidebar hamburger toggle.

9. **Library as polished .tmmgame file browser** — each card exposes the backing `.tmmgame`
   filename in small text.

10. **Conflicts tab → "Conflict Manager"** — main Mods tab shows conflict existence at a
    glance; dedicated tab is for actively managing/resolving them.

11. **Routing rules never regenerate existing frozen plans** — rules are a starting point
    for new installs only. User owns the plan after that.

12. **Plan Editor always mandatory** — shown on every install even when routing rules
    produced zero conflicts and zero unassigned files. The program is not to blame if a mod
    is installed in the wrong directory; the user had the chance to review.

13. **Smart partial redeploy** — only re-deploy mods whose plan or state has changed. Diff
    old DeployManifest vs new plan to detect orphaned files.

14. **"Needs redeploy" state** — surfaced on 3 surfaces: mod row badge, Deploy button
    state, library game card. Clears on successful deploy. Load order reorder sets the flag;
    reverting to exact previous order clears it.

15. **Direct Install Override — WON'T IMPLEMENT** — routing rules with a "game root"
    destination cover the use case (dxvk etc.) cleanly. Ensure game root is a prominent,
    obvious routing destination option.

---

## Deployment Plan Editor — full spec

Expands `DeployPreviewWindow` into a two-pane file levelling tool.

**Flow (fresh install):**
1. Archive extracted to `ModsRaw_{key}/{ModName}/`
2. Routing rules auto-populate a DeploymentPlan as the default assignment
3. Plan Editor opens — user confirms or adjusts
4. Confirm freezes the plan; Cancel aborts the install entirely

**Flow (edit existing mod plan):**
- Accessible from mod list at any time via "Edit Plan" control — no re-install required
- After editing, mod is flagged "needs redeploy"

**Flow (archive-gone re-plan):**
- Re-install option: if archive still present, ask user (warn manual changes will be
  overwritten); if archive gone, skip re-import and open Plan Editor directly to re-plan
  already-extracted files

**Left pane (game directory):**
- Read-only file tree showing existing game dir structure and which other mods claim each
  file
- Users CAN create new folders here for mod organization (created on disk at deploy time)
- Conflicts with other installed mods (enabled or disabled — all installed mods count)
  highlighted visually
- Explorer quick-link to open game dir

**Right pane (mod files — ModsRaw source layout):**
- Shows mod's extracted files in their ModsRaw folder structure
- Each file has an inline annotation showing its routing-rule-assigned destination
- Drag a file → drop onto a folder in the left pane → annotation updates to new destination
- Operations: reassign destination, create custom-named folders, rename files (renames file
  in ModsRaw immediately), exclude/delete from plan (file stays in ModsRaw, not deployed)
- No drag-to-add/replace files — use Explorer quick-link instead
- Explorer quick-link to open mod dir

**Unrecognized file rules (applied in order):**
1. Same directory level as a recognized file → carry to same destination as its neighbor
2. Filename contains "readme" (case-insensitive) → rename to `readme_{modname}.txt`,
   deploy to game root
3. Otherwise → game root, flagged for user review in Plan Editor

**Conflict bubble:** Badge on Conflict Manager tab appears when any conflict exists; clears
only when all conflicts are resolved.

**Orphaned files (smart partial redeploy):** When the updated plan no longer includes a
file that the old plan deployed, TMM shows a pre-deploy blocking dialog listing orphaned
files and asks permission to remove them before proceeding.

**Batch install:** Multiple archives dropped at once → pre-confirmation dialog: "Install X
mods in sequential order?" → TMM sorts alphabetically → Plan Editors appear one at a time.
Mid-sequence cancel: only that mod aborts, remaining batch continues (no prompt — batch
install is a convenience feature).

**Single-layer undo for deploy actions:** Deferred to feasibility backlog
(`feasibility-deploy-undo`).

---

## Architecture guardrails

- **Plans freeze at install.** Plans evaluated once on install, stored in
  `_tmm/deployplan.json`. Subsequent deploys execute the saved plan verbatim. Re-import or
  Plan Editor edit is the only way to update a plan.

- **First-touch baseline.** Rollback restores to the state TMM first observed. Vanilla
  installs restore to vanilla; imported pre-modded games restore to import-time state.

- **No hand-edited JSON for users.** `.tmmgame` is a bundling shortcut only. All config
  must be reachable through the wizard UI.

- **All-game parity.** Any feature that works for built-in games must be fully configurable
  via the wizard. Not complete until it appears in wizard input + review steps.

- **Nullable enabled.** Specific catches (not bare `Exception`). Minimal WPF code-behind.
  New strings localized in both `en-US.json` and `es-MX.json`.

---

## Priority ordering

### Phase 1 — Critical UI State & Navigation Blockers
- `modlist-tab-navigation` — **tabs cannot be clicked at all; fix first**
- `ui-game-dir-guard` — gate mod management on dir being set
- `ui-lag-needs-folder` — promptly reflect path update in UI

### Phase 2 — First-Launch Flow & Startup Defaults
- `firstrun-wizard-revamp` — short wizard with .tmmgame dropdown; auto-launches after
  welcome screen; second-game path handled by library-path-setting
- `firstrun-startup-prefs` — library-first vs mod-manager-first preference
- `firstrun-default-game` — expose default game control; last-opened as fallback
- `firstrun-default-change-notif` — notify on default game change

### Phase 3 — Library & Config Refinements
- `library-scroll-arrows` — circular overflow indicators in Library tab only
- `library-tmmgame-filenames` — expose .tmmgame filenames on cards
- `library-context-menus` — open config editor from library card
- `library-path-setting` — set game dir from library without entering wizard
- `nav-config-tab-justify` — quick path-setting + "Advanced config" link
- `ui-browse-window-title` — show game name in dir-browse title bar

### Phase 4 — Mod Manager Layout & Modlist Features
- `ui-dark-scrollbar` — custom dark scrollbar for scrollable areas
- `ui-sidebar-structure` — resizable sidebar + hamburger toggle
- `modlist-tab-rename-conflicts` — rename to "Conflict Manager"
- `modlist-sorting` — tiering, sort by load order/name/group, asc/desc
- `modlist-entry-controls` — folder icon, up/down arrows, re-install/edit-plan per mod

### Phase 5 — Mod Installation & Archives
- `install-preview` — **Deployment Plan Editor** (significant feature — full spec above)
- `install-tar-gz` — add .tar.gz archive support
- `install-direct-override` — WON'T IMPLEMENT

### Phase 6 — Long-Term Feasibility (analysis only, no code)
- `feasibility-vfs` — VFS assessment (backup tab → archive mgmt if implemented)
- `feasibility-nexus` — Nexus API assessment (credentials in Settings menu)
- `feasibility-deploy-undo` — single-layer undo for deploy actions

---

## Key files for reference

| What | Where |
|---|---|
| Deploy logic | `Services/BackendCore.cs`, `Services/BackendCore.Deploy.cs` |
| Game registry | `Services/GameRegistry.cs` |
| Game config model | `Models/CustomGameProfile.cs` (rename target: `GameConfig`) |
| Wizard (add/edit) | `Views/CustomGameSetupWizard.xaml(.cs)` (rename target: `GameSetupWizard`) |
| Mod list | `Views/Subpages/ModManagerPage.xaml` + partials |
| Shell / nav | `Views/UnifiedShellWindow.xaml(.cs)` |
| Theme | `ThemeEngine.cs` |
| Localization | `Assets/Localization/en-US.json`, `es-MX.json` |
| Settings model | `Models/AppSettings.cs` |

---

## Section notes

### Game Management (Unified)
Interdependent — do in order:
1. `games-unified-model` + `games-model-rename` + `games-registry-clean`
2. `games-key-slug` + `games-folder-naming` (depends on #1)
3. `games-wizard-unified` + `games-edit-mode` (depends on #1)
4. `games-wizard-redesign` (depends on #3; largest UI change)

### Mod Manager Tab Navigation
**Tabs cannot be clicked at all right now** — top Phase 1 priority. After fixed:
- Conflict Manager tab → ConflictAnalyzer, conflict highlighting
- Backups tab → per-deploy rollback list
- Downloads tab → mod download history / pending installs
- Config tab → quick path-setting + "Advanced config" link (Decision #4)

### QA / Verification
`qa-first-run-walkthrough` before any public release. Catches encoding issues (deploy toast
charset), first-run UX gaps, and integration bugs tests cannot.
