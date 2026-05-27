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
**Mods:** `%APPDATA%\TMM\ModsRaw_{key}\{ModName}\`  
**Backups:** `%APPDATA%\TMM\Backups\{key}\{timestamp}.json`  
**Custom games:** `%APPDATA%\TMM\CustomGames\{key}.json`

---

## Game Keys

Built-in: `III` `VC` `SA` `IV` `TLAD` `TBOGT`  
Custom: `CUSTOM_abc123` (auto-generated UUID)

---

## Deploy Flow

1. `BackendCore.DeployModsAsync` iterates enabled mods
2. Files routed via `RoutingRule` (e.g., `.asi` → `plugins/`)
3. Backup created before writing
4. `DeployManifest` saved for rollback

---

## For Implementation

**Active work:** [PLANS.md](PLANS.md) — phases, design decisions, success criteria  

**When asking for help:**
- Use `FileName.cs:LineNumber` for specific changes
- Reference PLANS.md for context on ongoing phases

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

**See PLANS.md for routing rules & backend logic standards.**

---

## References

[PLANS.md](PLANS.md) → Active phases, design decisions, success criteria  
[SANITYCHECK.md](SANITYCHECK.md) → Pre-release verification checklist
