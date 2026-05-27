# HANDOFF: Block B Implementation (2026-05-27)

## Status
**Design locked.** All 6 briefs (B1-B6) in `PLANS.md → Block B` are specification-complete.  
**Commit hash:** Latest commit includes CLAUDE.md Architectural Principles + PLANS.md Block B.  
**Repo:** TheTriviali/TMM, branch `master`.  

Ready for implementation. Start with **B2**, proceed along critical path: B2 → B3 → B4 → B5.

---

## Starting Point: B2 — Install-time deployment plan freeze

**This is the foundation for all others.** See `PLANS.md:51-64` for the full brief.

### One-sentence summary
Replace per-deploy `PlanDeploymentAsync` calls with a one-time install-time capture + saved-plan execution.

### Files to touch
1. `Services/DeploymentPlanner.cs` — make `DeploymentPlan` JSON-serialisable, add `PlanVersion = 1`
2. `Services/BackendCore.cs` — add `OnModAddedAsync(string gameKey, string modName)` entry point, call from all mod-add paths, implement legacy fallback in `DeployModsAsync`
3. `Views/Subpages/ModManagerPage.xaml.cs` — wire `OnModAddedAsync` from deploy UI if needed
4. Every mod-add path (archive extraction, wizard, manual refresh, etc.) — ensure calls to `OnModAddedAsync`

### Key decision (locked 2026-05-27)
- Plans are serialised to `ModsRaw_{key}/{ModName}/_tmm/deployplan.json`
- Profile-edit invalidation surfaces a user prompt (no silent re-planning)
- Legacy fallback: if a mod has no saved plan, live-plan + log warning

### Gotcha
**Find every mod-add entry point before wiring.** Search `_core.Mods.Add`, `RefreshAllModListsAsync`, archive extraction, drag-drop. Missing one means some mods never get a persisted plan.

### Dependency check
**None.** B2 stands alone. B3, B4, B5, B6 all depend on B2 to land first.

---

## Critical context: Two locked architectural principles

See `CLAUDE.md → Architectural Principles` (lines 63-67). Both are now requirements:

### 1. Deployment rules freeze at install
Once a mod is installed, TMM runs the routing-rules engine **once** and persists the resulting `DeploymentPlan`. Subsequent deploys execute the saved plan verbatim — rules are NOT re-evaluated. This is what B2 builds.

### 2. First-touch baseline
Rollback restores files to the state TMM first observed them. For fresh installs that's vanilla; for imported pre-modded games that's import-time. This is what B3 builds.

Both principles are locked. You are not redesigning them — you are implementing them.

---

## Secondary context: Mod packing conventions (for B5 awareness)

**CLEO scripts:**
- Ideal: `modloader/{modname}/cleoscript.cs`  
- Fallback: `cleo/cleoscript.cs` + `.ini` companion (same basename)

**ASI plugins:**
- Ideal: `modloader/{modname}/plugin.asi`  
- Better: `scripts/plugin.asi`  
- Fallback: game root

**Folder overlays** (mods with `models/`, `data/`, `audio/` folders):
- Merge into game folders (overwrite where filenames collide)  
- Requires baseline to restore vanilla files underneath

**Mod groups** (B6):
- Mod X in group "Cars" → files under `modloader/Cars/X/`

---

## Full spec location
See `PLANS.md` section **Block B**:
- **B2** (lines 51-64) — Install-time plan freeze
- **B3** (lines 65-83) — First-touch baseline snapshot
- **B4** (lines 85-101) — Folder-overlay deploy + empty-dir walking
- **B1** (lines 103-115) — GTA III/VC/SA profile completeness
- **B5** (lines 117-136) — Sync/import from existing modded install
- **B6** (lines 139+) — Mod groups as nested deployment targets

Each brief includes:
- **Goal** — one-sentence intent
- **Current behaviour** — what exists today (audited)
- **Files** — exact paths to touch
- **Plan** — step-by-step implementation
- **Gotcha** — common pitfall
- **Depends on** — predecessor briefs

---

## Task tracking
Six tasks created in the system (B1-B6). Mark each as:
- `in_progress` when starting
- `completed` when done (all tests pass, no open dependencies)

Recommended execution order respects critical path:
1. B2 (foundation)
2. B3 (foundation)
3. B4 (folder-overlay + empty-dir walking)
4. B1 (GTA III profiles — parallel with B4)
5. B5 (sync/import — depends on B2, B3, B4)
6. B6 (mod groups — depends on B2)

---

## When you finish B2

Hand off to the next model with:
- Task B2 marked `completed`
- Any gotchas or decisions logged in PLANS.md or memory
- New commit pushed locally
- Prompt: "Start B3 — First-touch baseline snapshot. See PLANS.md:65-83 for the full spec. Depends on B2 (just completed). Critical path: B3 → B4 → B5."

---

## Transcript and memory
If you need earlier context (error messages, code snippets, user decisions), read:
- **Full session history:** `C:\Users\noahd\.claude\projects\C--Users-noahd-source-repos-tmm-tmm\53c8888a-0a9b-4c34-9a9b-d76e759b9dfd.jsonl`
- **Memory index:** `C:\Users\noahd\.claude\projects\C--Users-noahd-source-repos-tmm-tmm\memory\MEMORY.md`

---

## Go.
You have the spec, the repo state, the architectural principles, and the locked decisions.  
Start with B2. Use line numbers in PLANS.md as landmarks. Ask no clarifying questions — all design is locked.

Good luck. 🎯
