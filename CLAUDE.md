# Claude.md — TMM Quick Reference

**TMM:** Lightweight mod manager for GTA III series + custom games. WPF + C# (.NET 10-windows).  
Direct-deploy: mods → game directories (no VFS).

---

## Quick Navigation

| Task | File |
|------|------|
| Deploy mods | `Services/BackendCore.cs` |
| Custom games | `Services/GameRegistry.cs` |
| Theme system | `ThemeEngine.cs` |
| Window flow | `App.xaml.cs` |

---

## File Locations

**Settings:** `%APPDATA%\TMM\settings.json`  
**Mods:** `%APPDATA%\TMM\ModsRaw_{key}\{ModName}\` (frozen plan in `_tmm\deployplan.json`)  
**Backups:** `%APPDATA%\TMM\Backups\{key}\{timestamp}\manifest.json`  
**Baselines:** `%APPDATA%\TMM\Baselines\{key}\baseline.json` (+ `snapshots\`)  
**Loadouts:** `%APPDATA%\TMM\Loadouts_{key}\{Name}.json`  
**Custom games:** `%APPDATA%\TMM\CustomGames\{key}.json`

---

## Game Keys

Built-in: `III` `VC` `SA` `IV` `TLAD` `TBOGT`  
Custom: `CUSTOM_abc123` (auto-generated UUID)

---

## Deploy Flow

1. **Install:** `BackendCore.OnModAddedAsync` runs the routing rules **once** and freezes a `DeploymentPlan` to `ModsRaw_{key}/{ModName}/_tmm/deployplan.json`.
2. **Deploy:** `ModManagerPage` → `DeployPreviewWindow` (`ConflictAnalyzer` surfaces cross-mod conflicts) → `BackendCore.DeployFilesToGameDirAsync` executes the saved plan (no re-evaluation).
3. **Before overwrite:** first-touch baseline captured (`BaselineSnapshot`) + per-deploy backup.
4. `DeployManifest` saved; rollback restores to the **baseline** (not the previous deploy).

> `DeployModsAsync` (the old per-deploy live-planning path) was removed in v0.1-alpha-9.

---

## Custom Game Rule

**Any feature that works for built-in games must be fully configurable by a user adding a custom game through the wizard UI.** Users never edit JSON — the `.tmmgame` format is only a shortcut for bundled profiles.

A feature is not complete until it appears in:
1. `CustomGameProfile.cs` — the model field
2. `Step1_GameDetailsPage.xaml(.cs)` — user input
3. `Step4_ReviewPage.xaml(.cs)` — review summary before creating

---

## Architectural Principles

**1. Deployment rules freeze at install.** When a mod is installed (fresh or imported), TMM runs the routing-rules engine *once* against the mod's files and persists the resulting file→destination map (a `DeploymentPlan`) as part of the mod's metadata. Subsequent deploys execute the saved plan verbatim — they do NOT re-evaluate rules. The only way to regenerate a plan is an explicit re-import (or, for grouped mods, a group-change which counts as a re-install). This keeps deploys predictable and avoids per-deploy approval prompts.

**2. First-touch baseline.** Rollback restores files to the state TMM first observed them in. For a fresh install that's vanilla; for a sync/imported pre-modded game that's the import-time state. TMM does NOT promise (and cannot deliver) recovery beyond what it first saw. A per-game `baseline.json` manifest captures the original bytes of each game file the first time TMM touches it; per-deploy manifests are a secondary index of "what this deploy changed."

---

## Code Standards

- **Nullable reference types:** Always on (`<Nullable>enable</Nullable>`)
- **Public APIs:** XML docs (`///`), async methods suffix with `Async`
- **Naming:** PascalCase types/methods, camelCase locals, `_camelCase` private fields
- **Async/await:** Never `.Result`/`.Wait()`; prefer `ConfigureAwait(false)` in library code
- **Error handling:** Specific catches (not bare `Exception`), structured logging with context
- **LINQ:** Prefer over loops when clear; understand lazy evaluation
- **Null handling:** Use `is null`/`is not null`, return `[]` instead of `null` for empty collections
- **WPF:** Minimal code-behind, DataContext via XAML/constructor, stateless converters


---

## Mockups

Mockups live in `Mockups/Views/` as `UserControl` XAML files. The `Mockups/` folder is a standalone WPF project; use `DynamicResource` theme keys, no duplication.

**Never produce a mockup unless explicitly asked.** After adding or modifying a mockup, verify it compiles: `/run --mockups`.

---

## Design Decisions

These are frozen — don't re-open without an explicit conversation.

1. **Library shows configured games only.** "Your games" lists only games with a folder set. Empty state shows a hint on fresh start. Stats: "Games with folder" + "Last deployed."
2. **Mod types + routing rules merged in wizard.** Steps 2/3 combined: each row is a mod type with extensions and target folder.
3. **Help + Troubleshooting merged, global.** Single "Troubleshooting & Help" rail entry. No separate Help/About overflow.
4. **Config tab = quick path-setting + "Advanced config" link.** Not a full wizard launch.
5. **No custom-game distinction in logic.** `CUSTOM_` prefix and branching code paths are debt to remove. All games are first-class profiles; built-in vs. custom is a registry concern only.
6. **Nav rail structure.** Left strip: Library, Mod Manager (top); Troubleshooting & Help, Settings (bottom). Always-visible fixed width. Title bar: app icon + page name.
7. **Unified mod list filter bar.** Search + chip row (All / Enabled / Conflicts / Favorites) in one horizontal control.
8. **Install Mod button inline in workspace header** alongside Deploy and Play.
9. **Library cards show `.tmmgame` filename** in small text.
10. **Conflicts tab is "Conflict Manager".** Mods tab shows conflict existence at a glance; the tab is for resolution.
11. **Routing rules never regenerate frozen plans.** Rules run once at install time; plans are immutable after that. (See Architectural Principle 1.)
12. **Plan Editor is always shown on install.** User had the chance to review — TMM is not responsible for mods that land in the wrong place.
13. **Smart partial redeploy.** Only re-deploy mods whose plan or state changed since last deploy.
14. **"Needs redeploy" state** surfaced on mod row badge, Deploy button, and library game card.
15. **Direct Install Override — won't implement.** Game-root routing destination covers the use case.

---

## References

[SANITYCHECK.md](SANITYCHECK.md) → Pre-release verification checklist  
[planned_features.md](planned_features.md) → Deferred / post-v1 features
