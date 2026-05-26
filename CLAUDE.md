# Claude.md — TMM Quick Reference

**TMM:** Lightweight mod manager for GTA III series + custom games. WPF + C# (.NET 10-windows).  
Direct-deploy: mods → game directories (no VFS).

---

## Quick Navigation

| Task | File | Search |
|------|------|--------|
| Deploy mods | `BackendCore.cs` | CODEBASE_GUIDE.md → "deploy mods" |
| Custom games | `GameRegistry.cs` | CODEBASE_GUIDE.md → "custom game" |
| Theme system | `ThemeEngine.cs` | CODEBASE_GUIDE.md → "theme" |
| Window flow | `App.xaml.cs` | CODEBASE_GUIDE.md → "crash handler" |

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
**Detailed arch:** [CODEBASE_GUIDE.md](CODEBASE_GUIDE.md) — file index + search  

**When asking for help:**
- Use `FileName.cs:LineNumber` for specific changes
- Use CODEBASE_GUIDE search index instead of asking me to re-read
- Reference PLANS.md for context on ongoing phases

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

[CODEBASE_GUIDE.md](CODEBASE_GUIDE.md) → Detailed architecture + file index  
[PLANS.md](PLANS.md) → Active phases, design decisions, success criteria  
[SANITYCHECK.md](SANITYCHECK.md) → Pre-release verification checklist
