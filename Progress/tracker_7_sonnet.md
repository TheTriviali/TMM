# Section 7 — Mod Installation & Plan Editor

**Model:** Sonnet  
**Start:** Section 6 complete  
**Stop:** Plan Editor verified end-to-end on a real mod install, build clean

- `[ ]` open · `[C]` Claude done, needs Noah check · `[✓]` complete

---

- [ ] `install-preview` — Deployment Plan Editor (two-pane routing tool, replaces DeployPreviewWindow)
- [ ] `install-tar-gz` — add `.tar.gz` archive support alongside `.zip`/`.rar`

---

## Plan Editor Spec

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
