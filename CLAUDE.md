# Claude.md — TMM Chat Context

Quick reference for AI sessions. For detailed architecture, see [CODEBASE_GUIDE.md](CODEBASE_GUIDE.md).

> **New Chat Reminder:** At natural phase boundaries (phase complete, major feature done, topic shift), remind Noah to open a fresh chat. Fresh chats are cheaper and faster — CLAUDE.md + PLANS.md carry all the context needed. Suggest it proactively; don't wait to be asked.

## What is TMM?

**TMM** (Triviali's Mod Manager) — lightweight mod manager for GTA III series + Skyrim + custom games.  
Direct-deploy architecture: mods go straight to game directories, no VFS staging.

**Tech:** WPF + C# (.NET 10-windows), Windows 11 native (Mica backdrop, no legacy baggage).

---

## File Structure

### Entry Point & Configuration
```
App.xaml                    WPF application root (resources, styles)
App.xaml.cs                 Startup, global crash handler, BackendCore initialization
AssemblyInfo.cs             Assembly metadata, version info
TMM.csproj                  Project file (.NET 10-windows, NuGet dependencies)
TMM.sln                     Solution file
```

### Services (Backend Orchestration)
```
Services/
├── BackendCore.cs          Core orchestrator
│                             DeployModsAsync: copy mods to game dir + create backup
│                             RollbackDeployAsync: restore from backup manifest
│                             LoadGameProfiles: initialize GameRegistry
│                             ExtractArchiveSafeAsync: unzip + validate mod structure
│                             SmartArchivePostProcess: detect folders, unwrap single root
├── GameRegistry.cs         Game profile management
│                             Built-in games: III, VC, SA, IV, TLaD, TBoGT (GameProfile defs)
│                             Custom games: load/save from %APPDATA%/TMM/CustomGames/
│                             Path auto-detection: QuickScan (Steam registry lookup)
├── NotificationService.cs   User-facing notifications
│                             Toast queue: success, error, warning, info
│                             Auto-dismiss + manual dismiss
└── SteamLauncher.cs        Steam protocol integration
                              steam://run/{appId} invocation for game launch
```

### Models (Data Structures)
```
Models/
├── AppSettings.cs          Persisted application config
│                             ThemePreset, WindowState, FirstLaunch, GamePaths
│                             LastOpenedGame, BackupRetentionDays
│                             Serialized to %APPDATA%/TMM/settings.json
├── GameProfile.cs          Built-in game definitions (read-only)
│                             Properties: GameKey, DisplayName, SteamAppId, InstallDir
│                             Routing rules for file placement (e.g., .asi → plugins/)
├── CustomGameProfile.cs    User-defined game profile
│                             Game name, path, exe path, Steam App ID (optional)
│                             Routing rules, metadata
│                             Serialized to %APPDATA%/TMM/CustomGames/{key}.json
├── ModItem.cs              Single mod metadata
│                             Name, folder path, enabled/disabled toggle
│                             Load order index, file count, total size
├── DeployManifest.cs       Backup snapshot for rollback
│                             Timestamp, list of deployed files (paths + hashes)
│                             Original file hashes (for collision detection)
│                             Serialized to %APPDATA%/TMM/Backups/{key}/{timestamp}.json
├── RoutingRule.cs          File routing configuration
│                             Extension pattern (e.g., .asi, .dll)
│                             Target directory (relative to game install)
│                             Enabled/disabled toggle, priority
├── LibraryEntry.cs         Downloaded mod/content metadata
│                             Name, author, version, URL, size, date added
├── TmmGameConfig.cs        Game configuration export/import
│                             Bundle of GameProfile + mod list + settings
├── ReleaseStatus.cs        Update/release status enumeration
                              (stable, beta, prerelease, archived, custom)
```

### Views (UI Windows & Pages)
```
Views/
├── Game Hubs (Navigation)
│   ├── UnifiedShellWindow.xaml      Main navigation shell (new unified UI)
│   │   ├── Subpages/
│   │   │   ├── ModManagerPage.xaml   Mod management dashboard (per-game)
│   │   │   ├── LibraryPage.xaml      Downloaded mods browser
│   │   │   ├── DownloadsPage.xaml    Active download queue
│   │   │   ├── BackupsPage.xaml      Backup history + rollback
│   │   │   └── SettingsPage.xaml     Appearance, paths, advanced
│   │   └── Controls/
│   │       └── GameCard.xaml         Reusable game card (icon, status, action buttons)
│
├── Game Dashboards (DEPRECATED - To be removed in routing rules refactor)
│   ├── MainDashboardWindow.xaml      GTA III/VC/SA mod manager (hardcoded logic)
│   │   └── MainDashboardWindow.xaml.cs
│   ├── Gta4DashboardWindow.xaml      GTA IV/TLaD/TBoGT 3-column layout (hardcoded logic)
│   │   └── Gta4DashboardWindow.xaml.cs
│   └── CustomGameDashboardWindow     User-added game manager (will become generalized)
│       └── CustomGameDashboardWindow.xaml.cs
│
├── Setup & Configuration
│   ├── InitialSetupWindow.xaml       First-run path auto-detection wizard
│   │   └── InitialSetupWindow.xaml.cs  QuickScan, manual path override
│   │   └── GameSetupRow.xaml         Reusable path input row (used in Setup + Settings)
│   │   └── GameSetupRow.xaml.cs
│   └── CustomGameConfigWindow.xaml   New game profile editor
│       └── CustomGameConfigWindow.xaml.cs  Name, path, routing rules, validation
│
├── Utilities & Dialogs
│   ├── SettingsWindow.xaml           Advanced settings (appearance, paths, cache, debug)
│   │   └── SettingsWindow.xaml.cs
│   ├── ThemeManagerWindow.xaml       Theme preset browser + customization
│   │   └── ThemeManagerWindow.xaml.cs  Live preview, apply, export/import
│   ├── AboutWindow.xaml              About dialog + version info
│   │   └── AboutWindow.xaml.cs
│   ├── ModPropertiesWindow.xaml      Mod detail viewer + metadata editor
│   │   └── ModPropertiesWindow.xaml.cs
│   ├── RenameWindow.xaml             Simple text input dialog (rename mod/game)
│   │   └── RenameWindow.xaml.cs
│   ├── ArchiveExtractionWindow.xaml  Progress dialog for mod extraction
│   │   └── ArchiveExtractionWindow.xaml.cs  Handles .zip, .rar, .7z extraction
│   ├── EpisodePicker.cs              Dropdown selector (GTA IV episode choice)
│   └── GameSetupRow.xaml             Reusable game path input (Initialize + Settings)
```

### Theming & Styling
```
Theming/
├── ThemeEngine.cs          Theme application controller
│                             Load theme from preset or custom JSON
│                             Apply to WPF app resources (colors, fonts, Mica backdrop)
│                             Persist to AppSettings
└── AccentPresets.cs        Built-in theme color palette definitions
                              Dark, Light, Auto (system), Custom
                              Accent colors, contrast levels
```

### Utilities & Helpers
```
Helpers/
├── Helpers.cs              General utility functions
│                             Path validation, string escaping, file operations
│                             JSON serialization helpers, error formatting
├── TmmWindow.cs            Base window class (custom behavior)
                              Mica backdrop styling, keyboard shortcuts, state preservation

Converters/
└── NotificationTypeIconConverter.cs  XAML converter: notification type → icon
```

### Documentation & Configuration
```
CLAUDE.md                   This file — quick reference for AI chat sessions
CODEBASE_GUIDE.md           Detailed architecture guide + search index for file lookup
PLANS.md                    Active refactor/design decisions + todo list
SANITYCHECK.md              Pre-release verification checklist
TEST_FLOW.md                Comprehensive manual testing methodology (13 phases)
README.md                   Public project overview + feature list
CHANGELOG.md                Version history + breaking changes
.vscode/
├── launch.json             Debug launch configuration
└── tasks.json              Build tasks for VS Code
```

### Data Storage (Runtime)
```
%APPDATA%/TMM/
├── settings.json           Persisted AppSettings (theme, paths, preferences)
├── ModsRaw_III/            Mod files for GTA III (organized by mod name)
├── ModsRaw_VC/             Mod files for GTA VC
├── ModsRaw_SA/             Mod files for GTA SA
├── ModsRaw_IV/             Mod files for GTA IV
├── ModsRaw_TLaD/           Mod files for GTA: The Lost and Damned
├── ModsRaw_TBoGT/          Mod files for GTA: The Ballad of Gay Tony
├── ModsRaw_CUSTOM_{key}/   Mod files for custom games (key = auto-generated UUID)
├── Backups/
│   ├── {gameKey}/
│   │   ├── {timestamp1}.json  Deployment snapshot (file manifest + hashes)
│   │   └── {timestamp2}.json
│   └── ...
└── CustomGames/
    ├── {key}.json          Custom game profile (serialized CustomGameProfile)
    └── ...
```

---

## Key Behaviors (For New Tasks)

### Paths
- **Settings:** `%APPDATA%\TMM\settings.json`  
- **Mods stored:** `%APPDATA%\TMM\ModsRaw{key}\{ModName}\` (e.g., `ModsRaw_III\`, `ModsRaw_CUSTOM_abc123\`)  
- **Backups:** `%APPDATA%\TMM\Backups\{key}\{timestamp}.json`  
- **Custom game registry:** `%APPDATA%\TMM\CustomGames\{key}.json`  

### Deploy Flow
1. User clicks "Deploy" in dashboard
2. `BackendCore.DeployModsAsync` iterates enabled mods in LoadOrder
3. Files are routed via `RoutingRule` (e.g., `.asi` → `plugins\` if exists)
4. Backup created before overwriting
5. `DeployManifest` saved for rollback

### Game Keys & Built-in Games

**Built-in games (`.tmmgame` profiles):**
- `"III"` → Grand Theft Auto III
- `"VC"` → Grand Theft Auto: Vice City
- `"SA"` → Grand Theft Auto: San Andreas
- `"IV"` → Grand Theft Auto IV
- `"TLAD"` → GTA: The Lost and Damned (Chinatown Wars)
- `"TBOGT"` → GTA: The Ballad of Gay Tony

**Custom games:**
- `"CUSTOM_abc123"` (auto-generated UUID key)

**Note:** All built-in games are treated as standalone `.tmmgame` profiles with user-configurable mod types + routing rules (no special hardcoded logic). See PLANS.md Phase 5 for re-implementation details.

### First Run
- `BackendCore.InitializeAsync()` loads settings + registers games
- If `AppSettings.FirstLaunch == true` → show `InitialSetupWindow`
- Path wizard auto-detects Steam paths via `QuickScan`

---

## For Deep Dives

**Architecture details:** [CODEBASE_GUIDE.md](CODEBASE_GUIDE.md) — table of contents + search index  
**Implementation plan:** [PLANS.md](PLANS.md) — current refactors + design decisions  
**Sanity checks:** [SANITYCHECK.md](SANITYCHECK.md) — verification checklist for major changes  

---

## Token-Saving Tips

- **Don't ask me to re-read files you already understand.** Use the search index in CODEBASE_GUIDE to say: *"See CODEBASE_GUIDE.md search index → 'deploy mods'"* instead of asking me to look it up.
- **Reference PLANS.md for context** on ongoing work — it's up-to-date with design decisions.
- **Use the search index.** Need to find where X happens? Grep the search index first.
- **For file-specific help,** ask for `FileName.cs:LineNumber` — saves me reading the whole file.
- **File creation:** Be selective about new files. Only create if obviously needed (e.g., new code module, required config). Don't create auxiliary docs unless explicitly requested. Prefer updating existing files (PLANS.md, CODEBASE_GUIDE.md) over new ones.

## Local LLM Development (Continue + Ollama)

**When to use local models (Qwen 14B/7B via Continue):**
- ✅ Code generation for straightforward classes (no cross-file dependencies)
- ✅ Code analysis & refactoring suggestions (read existing code, flag improvements)
- ✅ Test scenario design (generate test cases)
- ✅ Documentation writing (guides, comments, architecture notes)
- ✅ Quick implementations (simple CRUD, boilerplate)

**When to use Claude API (this session):**
- ✅ Multi-file reasoning (impact analysis across services)
- ✅ Complex architecture decisions (trade-offs, design patterns)
- ✅ Refactoring coordination (orchestrate changes across 5+ files)
- ✅ Integration testing & debugging
- ✅ Code review of critical paths (BackendCore, RuleEngine)

**Workflow:**
1. Use Continue (local Qwen) for initial implementation
2. When done, ask Claude API to audit the code:
   - Does it follow CLAUDE.md standards?
   - Cross-file consistency?
   - Performance implications?
   - Security/error handling?
3. Claude provides feedback; local model refines

**Note:** Local models are fast (5–20 tok/sec) and free. Use generously for iteration, save API tokens for high-value decisions.

### Claude API Audit Checklist (When Code Returns from Local)

Use this when reviewing code generated by local Qwen models:

**Code Quality:**
- [ ] Nullable reference types enabled? (`<Nullable>enable</Nullable>`)
- [ ] Public APIs have XML docs (`///`)?
- [ ] No `.Result` or `.Wait()` (async discipline)?
- [ ] Proper error handling (specific catches, not bare `catch (Exception)`)?

**Architecture:**
- [ ] Follows SOLID principles (single responsibility)?
- [ ] Dependency injection used (constructor parameters, not new)?
- [ ] No hardcoded values (magic strings, paths)?
- [ ] Event cleanup / IDisposable patterns correct?

**Cross-File Consistency:**
- [ ] Naming conventions consistent (PascalCase methods, camelCase locals)?
- [ ] Enum names/values match other enums?
- [ ] Model properties align with serialization strategy?
- [ ] Service method signatures consistent with existing patterns?

**Performance & Security:**
- [ ] No N+1 queries or unnecessary iterations?
- [ ] String concatenation optimized (StringBuilder for loops)?
- [ ] No PII/secrets in error messages or logs?
- [ ] Proper validation on user input?

**Testing:**
- [ ] Code is testable (dependencies injectable)?
- [ ] Edge cases considered (null, empty, large collections)?
- [ ] Sufficient logging for debugging?

If all ✓, code is ready. If issues found, flag them + suggest fixes.

### Effective Prompts for Continue (Local Qwen Models)

**Generate C# Boilerplate:**
```
Write a complete C# class for {ClassName}:
- Properties: {list with types}
- JSON serialization support (System.Text.Json)
- Validation: {constraints}
- Include XML documentation comments

Use namespace TMM.Models, enable nullable reference types.
```

**Analyze Existing Code:**
```
Review {FileName}.cs for:
1. Game-specific hardcoded logic (flag with line numbers)
2. What should move to .tmmgame profiles
3. Refactor opportunities (SOLID violations, duplication)
4. Backwards compatibility concerns

Output: Markdown list with priority + complexity.
```

**Generate Test Cases:**
```
Design test scenarios for {Feature} covering:
1. Happy path (expected input)
2. Edge cases (empty, null, max values, special chars)
3. Error conditions (invalid input, missing resources)
4. Integration points (dependencies, side effects)

For each: Setup → Action → Expected → Failure mode.
```

**Add Error Handling:**
```
Add comprehensive error handling to this method:
- Specific exception catches (not bare Exception)
- Validation on inputs
- Structured logging (include context)
- User-facing error messages via NotificationService

Keep business logic readable.
```

**Code Review:**
```
Review this code against TMM standards:
- Nullable reference types enabled?
- Async/await discipline correct?
- SOLID principles followed?
- Performance implications?
- Test coverage suggestions?

Flag issues + suggest fixes.
```

---

## Workflow Summary for Next Session

1. **Start Continue with Qwen 14B** for analysis/reasoning
2. **Use Qwen 7B autocomplete** for quick fills
3. **Generate code locally**, iterate fast
4. **When ready, paste to Claude API** for audit
5. **Claude audits** using checklist above
6. **Push to repo** with confidence

This splits work: local for speed, Claude API for correctness.

---

## Recent Changes

See `git log --oneline` or check master branch. Latest: TMM rename (TGTAMM→TMM files, GitHub repo, default branch master).

---

---

## Architectural Notes (Updated for Routing Rules Refactor)

**Major Change:** All game-specific logic (MainDashboardWindow, Gta4DashboardWindow, hardcoded routing rules) has been removed. Every game—built-in or custom—is now a `.tmmgame` profile with user-configurable mod types and routing rules. Games are distinguished only by their game key and loaded from `.tmmgame` files, not by code branch.

**Impact on Services:**
- `BackendCore` no longer has special cases for GTA III series vs IV series
- `GameRegistry` loads all games from `.tmmgame` profiles (built-in from Resources/, custom from %APPDATA%/TMM/CustomGames/)
- All routing logic is rule-based (RuleEngine, DeploymentPlanner), not hardcoded
- UI is now unified: single dashboard per game (via CustomGameDashboardWindow logic, generalized)

---

## C# & .NET Core Standards

### Code Quality Requirements
- **Nullable reference types:** Enable throughout all projects (`<Nullable>enable</Nullable>`)
- **Analyzer compliance:** StyleCop, code analysis, compiler warnings as errors
- **XML documentation:** Public APIs must include `///` doc comments
- **Async/await discipline:** All I/O operations use async Task patterns (avoid `.Result`, `.Wait()`)
- **Performance:** Use `ValueTask`, `Span<T>`, `ArrayPool<T>` for hot paths
- **Modern C# features:** Record types (immutable DTOs), pattern matching, global using directives

### Naming Conventions
- **Classes/Interfaces:** PascalCase (e.g., `BackendCore`, `INotificationService`)
- **Methods/Properties:** PascalCase (e.g., `DeployModsAsync`, `GameRegistryPath`)
- **Local variables/parameters:** camelCase (e.g., `modList`, `gamePath`)
- **Constants:** UPPER_SNAKE_CASE or PascalCase (e.g., `MAX_BACKUP_SIZE` or `DefaultTheme`)
- **Private fields:** `_camelCase` prefix (e.g., `_backendCore`, `_settings`)
- **Booleans:** Prefix with `is`, `has`, `can`, `should` (e.g., `isEnabled`, `hasBackup`, `canDeploy`)
- **Async methods:** Suffix with `Async` (e.g., `DeployModsAsync`, not `Deploy`)
- **Event handlers:** `On{EventName}` (e.g., `OnWindowLoaded`, `OnButtonClicked`)

### Code Organization & Structure
- **File-scoped types:** Prefer `file class` over `namespace` wrapping for single-purpose classes (C# 11+)
- **Using statements:** Alphabetically ordered, global usings in GlobalUsings.cs
- **Method order:** Public methods first, then protected, then private; properties before methods
- **Region usage:** Use regions sparingly; prefer clear method names over comments + regions
- **File size:** Keep files under 500 lines; split large classes into partial or separate files
- **Dependency injection:** Use constructor injection; avoid service locator except at composition root (App.xaml.cs)

### Collections & LINQ
- **Prefer LINQ over loops:** Use `.Where()`, `.Select()`, `.First()` instead of foreach when expressiveness is clear
- **Lazy evaluation:** Understand deferred vs. immediate (`.ToList()` only when needed)
- **Empty collections:** Return `[]` or `Array.Empty<T>()` instead of `null`
- **Range/indices:** Use C# 8+ range operator `[..]` for slicing instead of `.Skip()/.Take()`
- **LINQ method chains:** Break long chains over multiple lines for readability

**Example:**
```csharp
// Good: Clear intent
var enabledMods = mods
    .Where(m => m.IsEnabled)
    .OrderBy(m => m.LoadOrder)
    .Select(m => m.Name)
    .ToList();

// Avoid: Unclear, imperative style
var enabledMods = new List<string>();
foreach (var mod in mods)
{
    if (mod.IsEnabled)
        enabledMods.Add(mod.Name);
}
```

### Null Handling & Validation
- **Null-forgiving operator (`!`):** Use sparingly; document why if necessary
- **Null coalescing (`??`):** Prefer over ternary for null checks
- **Pattern matching:** Use `is null` / `is not null` instead of `== null`
- **Argument validation:** Check nulls at method entry with `ArgumentNullException` if required; otherwise trust callers
- **Return null only when semantically meaningful** (e.g., "not found" vs. "failed to compute")

**Example:**
```csharp
public async Task<DeployManifest?> LoadManifestAsync(string path)
{
    if (!File.Exists(path))
        return null;  // Semantically: "manifest doesn't exist"
    
    var json = await File.ReadAllTextAsync(path);
    return JsonSerializer.Deserialize<DeployManifest>(json);
}
```

### Async/Await Patterns
- **Always use `async`/`await`:** Never `.Result` or `.Wait()` (deadlock risk)
- **ConfigureAwait(false):** Use in library code; acceptable to omit in UI code (WPF context preserved)
- **CancellationToken:** Accept optional `CancellationToken` parameter in long-running operations
- **ValueTask vs Task:** Use `ValueTask<T>` only for hot paths with frequent synchronous completion
- **Task composition:** Prefer `await` chaining over `Task.WhenAll()` for sequential operations

**Example:**
```csharp
public async Task DeployModsAsync(CancellationToken ct = default)
{
    var backups = await LoadBackupsAsync(ct).ConfigureAwait(false);
    foreach (var backup in backups)
    {
        await RestoreAsync(backup, ct).ConfigureAwait(false);
    }
}
```

### Error Handling & Logging
- **Exceptions:** Throw only for exceptional, non-recoverable conditions; use `Result<T>` or booleans for expected failures
- **Custom exceptions:** Only if callers need specific catch blocks; otherwise use built-in exceptions
- **Catch blocks:** Be specific (`catch (DirectoryNotFoundException)` not bare `catch (Exception)`)
- **Structured logging:** Include context (mod names, paths, game keys) in log messages
- **User feedback:** Surface via `NotificationService` toast, not exception dialogs

**Example:**
```csharp
try
{
    await File.DeleteAsync(path);
}
catch (FileNotFoundException)
{
    // Expected if mod was pre-deleted; skip silently
}
catch (UnauthorizedAccessException ex)
{
    notificationService.ShowError($"Permission denied: {path}");
    Debug.WriteLine($"Delete failed: {ex.Message}");
}
```

### WPF-Specific Patterns
- **DataContext:** Set via XAML binding or constructor (not code-behind when possible)
- **Dependency properties:** Use for bindable properties; local variables for non-binding state
- **INotifyPropertyChanged:** Implement only on ViewModel; Models are POCOs (plain old C# objects)
- **Routed events:** Prefer bubbling/tunneling over code-behind event handlers when possible
- **Converters:** Stateless, reusable XAML converters; avoid logic in code-behind
- **AttachedBehaviors:** Use for cross-cutting concerns (e.g., keyboard shortcuts)
- **Command pattern:** Implement `ICommand` or use Prism/MVVM Toolkit if available

**Example:**
```csharp
// Window code-behind: minimal
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();  // One-time setup
    }
}

// ViewModel: handles logic
public class MainViewModel : INotifyPropertyChanged
{
    private bool _isDeploying;
    public bool IsDeploying
    {
        get => _isDeploying;
        set => SetProperty(ref _isDeploying, value);
    }
}
```

### Resource Management & Disposal
- **Using declarations:** C# 8+ `using var obj = new Resource()` instead of `using (var obj = ...)`
- **IDisposable:** Implement properly with finalizer only if holding unmanaged resources
- **WPF event cleanup:** Unsubscribe from events in `Window.Closing` if handler captures long-lived objects
- **File I/O:** Use `File.ReadAllTextAsync` / `WriteAllTextAsync` for simplicity; `FileStream` only if streaming large files

**Example:**
```csharp
public async Task ProcessModsAsync(string[] paths)
{
    using var tempDir = new TemporaryDirectory();  // Auto-deletes on dispose
    
    foreach (var path in paths)
    {
        var content = await File.ReadAllTextAsync(path);
        // process...
    }
}  // tempDir disposed automatically
```

### Performance Considerations
- **String concatenation:** Use `StringBuilder` for loops; `$"..."` string interpolation for single statements
- **Collections:** Know the cost—`List<T>.Contains()` is O(n), prefer `HashSet<T>`
- **Hot paths:** Profile before optimizing; target <5s deploy/rollback for 50+ mods
- **Lazy initialization:** Use `Lazy<T>` for expensive properties
- **Memory pressure:** Avoid allocations in tight loops; use `ArrayPool<T>` for temporary buffers

### Testing Standards
- **Coverage target:** Aim for >80% on critical paths (deploy, rollback, settings persistence)
- **Unit tests:** Isolate services with dependency mocks (Moq)
- **Naming:** `[MethodName]_[Scenario]_[ExpectedOutcome]` (e.g., `DeployModsAsync_WithMissingFile_SkipsAndLogs`)
- **Arrange-Act-Assert:** Clear three-step structure
- **Integration tests:** Real file I/O, backup/restore cycles
- **UI tests:** Manual verification flows (see TEST_FLOW.md)
- **Performance:** Ensure deploy/rollback complete in <5s for typical mod collections

**Example:**
```csharp
[Fact]
public async Task DeployModsAsync_WithDisabledMod_SkipsDeployment()
{
    // Arrange
    var mod = new ModItem { Name = "TestMod", IsEnabled = false };
    var backend = new BackendCore();
    
    // Act
    await backend.DeployModsAsync(new[] { mod });
    
    // Assert
    Assert.False(File.Exists(Path.Combine(gameDir, "TestMod")));
}
```

### Pragmatic Exceptions
**The following trade simplicity for purity:**
- Bare `catch (Exception)` if handling is the same for all exception types
- Skipping null checks for objects known to be non-null (document assumption with comment)
- Service locator pattern in App.xaml.cs for root composition (vs. full DI container)
- Static fields for singletons if no concurrency concerns (e.g., `ThemeEngine.Instance`)
- Code-behind logic for trivial event handlers (single line, no shared state)

**When in doubt:** Favor readability and maintainability over dogmatic adherence to patterns.

---

## CI/Tests

Currently no formal unit test suite. Comprehensive manual verification required—see [TEST_FLOW.md](TEST_FLOW.md) for structured testing methodology covering:
- Feature coverage (every documented UI path)
- Deploy/rollback cycles with integrity checks
- Theme application and persistence
- Game path auto-detection and manual override
- Edge cases (missing files, invalid paths, concurrent operations)
- Visual consistency and usability scoring

---

---

## Architecture Reference (Consolidated from CODEBASE_GUIDE.md)

### Table of Contents

#### Entry Point
```
App.xaml.cs
  on startup →
    register global crash handler (ShowCrashDialog → MessageBox + clipboard)
    create BackendCore
    show GameLauncherWindow
```

#### Windows / Views

**GameLauncherWindow** — Main Hub
```
shows cards for: GTA III Series, GTA IV Series, each custom game, + Add button
each card has: title, subtitle, status dot, Manage button
clicking Manage →
  GTA III  → if FirstLaunch: show InitialSetupWindow → open MainDashboardWindow
  GTA IV   → if no IV paths set: show InitialSetupWindow → open Gta4DashboardWindow
  Custom   → open CustomGameDashboardWindow
  Add      → open CustomGameConfigWindow → register via GameRegistry
cards also have Edit / Delete buttons for custom games
```

**MainDashboardWindow** — GTA III Series (III / VC / SA)
```
single mod list for whichever game is active
toolbar: install mod, refresh, rescan, deploy, rollback, launch, open appdata, settings
per-game: path label + browse button, search filter, status dot
deploy → BackendCore.DeployModsAsync
rollback → BackendCore.RollbackDeployAsync (picks latest snapshot)
context menu on mod: rename, set load order, toggle, open folder, delete, properties
drag-drop reorder within list
keyboard: F2=rename, Space=toggle, Del=delete, F5=deploy, Ctrl+↑/↓=move
```

**Gta4DashboardWindow** — GTA IV Series (IV / TLaD / TBoGT)
```
three-column layout: one column per episode
each column: status dot, path label + browse, search filter, mod list, deploy + rollback + launch buttons
toolbar: install mod (asks which episode), refresh, rescan, deploy all, open appdata, settings, back
mod install → shows EpisodePicker to choose which episode → extracts archive → SmartArchivePostProcess
SmartArchivePostProcess:
  single-root unwrap (strip outer folder)
  known-folder detection (plugins/, scripts/, modloader/, bin/)
  if no known structure + readme found → offer to open readme
```

**CustomGameDashboardWindow** — User-Added Games
```
single mod list for a custom game profile
toolbar: install mod, refresh, launch (if ExePath set), settings, back
archive install → ExtractArchiveSafeAsync → stage in ModsRaw{key}/
deploy → BackendCore.DeployModsAsync
```

**InitialSetupWindow** — First-Run Path Wizard
```
shows GameSetupRow for each of: III, VC, SA, IV, TLaD, TBoGT
each row: browse button, detected path, status indicator
IV row change → auto-derives TLaD + TBoGT paths via SetVanillaPath
runs QuickScan on load to pre-populate known paths
Finish button requires at least one game ready → sets FirstLaunch=false
```

**SettingsWindow**
```
tabs: Appearance, Paths, Advanced
Appearance: theme picker → ThemeManagerWindow, font, Mica toggle
Paths: shows GameSetupRow for each game (same as InitialSetupWindow)
Advanced: factory reset, diagnostics (MD5 check, drive space)
```

**ThemeManagerWindow**
```
lists all built-in theme presets grouped by category
live preview on hover/select
apply → ThemeEngine.ApplyTheme
export preset → .mmtheme JSON file
```

**CustomGameConfigWindow**
```
form: game name, game directory (browse), exe path (browse), steam app id,
      routing rules (sentence builder), file extensions
validation: steamAppId must be numeric, extensions must start with ".", routes need both fields
test routing panel: browse a test file → shows where it would land (conditional dir check included)
export .tmmgame / import .tmmgame
returns CustomGameProfile on confirm
```

**Supporting Windows**
```
ModPropertiesWindow      — read-only view of mod metadata (name, order, path, enabled)
RenameWindow             — single text input dialog (rename + set load order)
AboutWindow              — version, credits
ArchiveExtractionWindow  — progress display during archive extraction
GameSetupRow             — reusable path browse row (used by InitialSetupWindow + SettingsWindow)
```

#### Services

**BackendCore** — Core Orchestrator
```
AppDataPath       → %APPDATA%\TMM\
Settings          → AppSettings (loaded from settings.json)
Mods[key]         → ObservableCollection<ModItem> per game

InitializeAsync() → load settings, create mod dirs, register all game profiles with GameRegistry
QuickScan()       → check fixed drives at known Steam/ProgramFiles paths for each game exe
SetVanillaPath(profile, path)
  → saves path; if IV, auto-derives TLaD (TLAD\ or TLaD\) and TBoGT (EFLC\ or TBoGT\)
IsGameReady(profile) → true if path is set and non-empty

DeployModsAsync(profile, mods, progress, ct)
  → creates backup manifest → copies enabled mods in load order to game dir
  → uses RoutingRules / ConditionalRoutes to route files to subdirs
RollbackDeployAsync(manifest, progress)
  → restores game dir from backup snapshot
GetRollbackManifests(key) → list of DeployManifest sorted newest first

RefreshAllModListsAsync() → reloads Mods[key] from disk for all games
ExtractArchiveSafeAsync(path, dest, ct) → uses SharpCompress, handles zip/rar/7z
ForceDeleteDirectory(path) → recursive delete ignoring readonly flags
GetDriveSpaceInfo() → "X.X GB free on C:"
OpenAppData() → shell-opens AppDataPath
```

DeploymentProgress record struct:
```csharp
public readonly record struct DeploymentProgress(string Stage, int Current, int Total)
```

**GameRegistry** — Game Roster (Singleton)
```
Instance → thread-safe singleton

GetAllGames()        → all built-in + custom GameProfiles
GetCustomGames()     → Dictionary<string, CustomGameProfile> of user-added games
GetGameProfile(key)  → GameProfile? by key
GetCustomGameConfig(key) → CustomGameProfile? by key

AddCustomGameAsync(config)         → assigns key, saves to disk, adds to registry
UpdateCustomGameAsync(key, config) → edits existing entry
DeleteCustomGameAsync(key)         → removes from registry + disk
```

**NotificationService**
```
Show(message, type, durationMs) → adds NotificationItem to Queue; DispatcherTimer removes it after durationMs
Queue → ObservableCollection<NotificationItem> (UI binds to this for toast display)
Helpers: ShowSuccess / ShowWarning / ShowError / ShowInfo
```

NotificationItem + NotificationType enum defined inline at top of NotificationService.cs.

**SteamLauncher**
```
Invoke(action, appId) → runs Steam protocol commands (install/validate/uninstall/rungameid)
```

#### Models

| Model | Purpose |
|---|---|
| `GameProfile` | Immutable record: Key, DisplayName, ExeName, SteamAppId, Vanilla10Md5, ConditionalRoutes. Static instances: III, VC, SA, IV, TLaD, TBoGT. Also defines `ExeStatus` enum (Unknown/Vanilla/Downgraded). |
| `CustomGameProfile` | User-defined game: GameName, GameDirectory, ExePath, SteamAppId, ModTypes, RoutingRules, Version, ReleaseTag, Robustness, IsNative |
| `ModItem` | Single mod: Name, IsEnabled, LoadOrder, RawFolderPath, DetectedType, LoadAfter, LoadBefore, LoadOrderBias, FinalLoadOrder |
| `ModType` | Mod category: Name, FileExtensions, RoutingRules, DefaultBias, IsPrimary |
| `Condition` | Rule condition: Type, Operator, Value, Logic (from Phase 1 models refactor) |
| `RoutingRule` | File routing: Name, Conditions, TargetPath, Priority, AllowConflict, LoadOrderBias, IsDefault |
| `AppSettings` | All persisted settings: GamePaths, FirstLaunch, theme/font fields, DeployOverrides, CustomGameKeys |
| `DeployManifest` | Backup snapshot: Timestamp, ModNames, per-file backup paths. Used for rollback |
| `ConditionalRoute` | Legacy backward-compat route (`.tmmgame` v1.0 import only). Defined in `TmmGameConfig.cs` |

#### Helpers

**Helpers/Helpers.cs** — three static helpers in one file:

| Class | Methods |
|---|---|
| `ShellHelper` | `OpenFolder(path)`, `OpenUrl(url)` — shell-execute wrappers |
| `UiColors` | Static Color + SolidColorBrush constants: DisabledGray, ReadyGreen, NotReadyRed, PendingOrange |
| `JsonHelper` | `PrettyOptions` + `TmmGameOptions` — shared `JsonSerializerOptions` instances |

#### Theming

**ThemeEngine**
```
ApplyTheme(settings)        → sets all DynamicResource brushes in App.Resources
ApplyFont(window, settings) → sets FontFamily on window
TryApplyMica(window, enabled) → enables Windows Mica backdrop via WindowChrome
Text contrast: hardcoded WCAG algorithm
Mica intensity: hardcoded 0.75
```

### Key Conventions

**Game Keys:** `"III"` `"VC"` `"SA"` `"IV"` `"TLaD"` `"TBoGT"` + custom keys (e.g. `"CUSTOM_abc123"`)

**Mod storage path:** `%APPDATA%\TMM\ModsRaw{key}\{ModName}\`  
**Mod metadata:** `modinfo.txt` (JSON-serialized ModItem) inside each mod folder  
**Settings file:** `%APPDATA%\TMM\settings.json`  
**Backup snapshots:** `%APPDATA%\TMM\Backups\{key}\{timestamp}.json`  
**Custom game registry:** `%APPDATA%\TMM\CustomGames\{key}.json`

**IV path auto-derive:** Setting IV path checks for `TLAD\` or `TLaD\` → sets TLaD; checks `EFLC\` or `TBoGT\` → sets TBoGT

**Deploy flow:**
1. `DeployModsAsync` iterates enabled mods in LoadOrder
2. Backs up any existing files at destination
3. Copies mod files respecting RoutingRules / ConditionalRoutes (e.g. `.asi` → `plugins\` if that folder exists)
4. Saves DeployManifest for rollback

**Resource keys (App.xaml):**  
`AccentBrush` `AccentTextBrush` `AccentLabelBrush` `BgBrush` `PanelBrush` `HeaderBrush`  
`TextBrush` `SubTextBrush` `ControlBgBrush` `CheckeredRowBrush`  
Styles: `IconButtonStyle` `CardButtonStyle` (GameLauncherWindow-local)  
Window-local styles: `ColActionBtn` `ToolIconBtn` `ModListStyle` `ModListTemplate`

### Search Index (Quick Lookup)

**crash handler / error popup** → `App.xaml.cs` `ShowCrashDialog`  
**game path storage / where paths are saved** → `AppSettings.GamePaths` → `settings.json`  
**IV auto-derive TLaD TBoGT** → `BackendCore.SetVanillaPath`  
**deploy mods / copy mods to game folder** → `BackendCore.DeployModsAsync`  
**rollback / undo deploy** → `BackendCore.RollbackDeployAsync` + `DeployManifest`  
**mod list on disk / how mods are stored** → `%APPDATA%\TMM\ModsRaw{key}\`  
**mod metadata persistence** → `modinfo.txt` in mod folder, JSON of `ModItem`  
**custom game add/edit/delete** → `GameRegistry` + `CustomGameConfigWindow`  
**theme application** → `ThemeEngine.ApplyTheme` → `App.Resources` DynamicResource brushes  
**all themes list / theme presets** → `ThemeManagerWindow`  
**first run / onboarding flow** → `AppSettings.FirstLaunch` → `InitialSetupWindow`  
**archive extraction** → `BackendCore.ExtractArchiveSafeAsync` (SharpCompress)  
**smart archive unwrap** → `Gta4DashboardWindow.SmartArchivePostProcess`  
**ASI routing to plugins folder** → `ConditionalRoute` on IV/TLaD/TBoGT profiles  
**Steam launch** → `SteamLauncher.Invoke` (install/validate/uninstall/rungameid commands)  
**drag-drop reorder** → `MainDashboardWindow` + `Gta4DashboardWindow` List_Drop handlers  
**context menu on mod** → `ModContextMenu` resource in each dashboard XAML  
**backup snapshots location** → `%APPDATA%\TMM\Backups\`  
**custom game registry location** → `%APPDATA%\TMM\CustomGames\`  
**game exe names** → `GameProfile.ExeName` (gta3.exe, gta-vc.exe, gta-sa.exe, GTAIV.exe, TLAD.exe, EFLC.exe)  
**status dot color logic** → `SetDotColor` in dashboard windows  
**Mica backdrop** → `ThemeEngine.TryApplyMica`  
**notification toasts** → `NotificationService.Show` → `NotificationService.Queue`  
**factory reset** → `BackendCore.FactoryReset` called from `SettingsWindow`  
**drive space** → `BackendCore.GetDriveSpaceInfo`  
**shell open folder/url** → `ShellHelper.OpenFolder` / `ShellHelper.OpenUrl` in `Helpers/Helpers.cs`  
**UI color constants** → `UiColors` in `Helpers/Helpers.cs`  
**JSON options** → `JsonHelper.PrettyOptions` / `JsonHelper.TmmGameOptions` in `Helpers/Helpers.cs`  
**exe status / downgrade detection** → `ExeStatus` enum in `GameProfile.cs`  
**routing rules (custom games)** → `RoutingRule` in `CustomGameProfile.cs`  
**legacy routing import** → `ConditionalRoute` in `TmmGameConfig.cs`  
**resource brushes missing / XAML static resource error** → check `App.xaml` resources section; window-local styles (e.g. `CardButtonStyle`) are not available in other windows

---

## Feedback Loop

If a chat is inefficient or you want me to remember something for future sessions, update:
- `PLANS.md` if design decisions or phases shift
- `TEST_FLOW.md` if testing procedures change
- `.claude/memory/` (user-facing memory system) for session-specific context
