# TMM Routing Rules & Game Configuration Refactor

> **Big Vision:** Replace all hardcoded game implementations with fully configurable `.tmmgame` profiles. Every game (GTA III, VC, SA, IV, TLaD, TBoGT, custom) is treated identically as an individual `.tmmgame` entry. Users can create/configure games via GUI with mod types, routing rules, load order preferences, and versioning. Built-in games are re-implemented through this same system—becoming the ultimate test of the feature.

**Architectural Change:** No more special-case handling for game series. All predefined routing logic is removed and reimplemented through the GUI for each game individually.

---

## Strategic Goals

1. **Differentiate from MO2:** Direct-deploy simplicity, any-game support, intuitive rule builder
2. **User empowerment:** Anyone can add/configure games without code
3. **Consistency:** All installation methods (downloads, file picker, drag-drop) use identical routing logic
4. **Robustness:** Conflict detection, preview before deploy, user intervention points

---

## Phase 1: Data Models & Serialization

### 1.1 — Condition System (`Models/Condition.cs`) [HAIKU]

**File:** `Models/Condition.cs`

```csharp
public enum ConditionType
{
    FileExtension,      // ".asi", ".dll", ".cs"
    HasFolder,          // "modloader", "cleo", "data"
    FolderCount,        // "= 0", "> 1" (for companion detection)
    FileCount,          // Single vs multiple
    PathContains,       // Substring match in file path
    FilenameMatches,    // Specific filename (e.g., "special_dll.dll")
}

public enum ConditionOperator
{
    Is,                 // FileExtension: is ".asi"
    IsNot,              // FileExtension: is not ".dll"
    Contains,           // PathContains: contains "modloader"
    DoesNotContain,     // PathContains: does not contain
    StartsWith,         // PathContains: starts with "data/"
    EndsWith,           // FilenameMatches: ends with "config.ini"
    MatchesRegex,       // Advanced pattern matching
    Equals,             // FolderCount: = 0
    GreaterThan,        // FolderCount: > 1
    LessThan,           // FolderCount: < 2
}

public enum LogicOperator
{
    AND,                // All conditions must match
    OR,                 // Any condition can match
}

public class Condition
{
    public ConditionType Type { get; set; }
    public ConditionOperator Operator { get; set; }
    public string Value { get; set; }                // ".asi", "modloader", "0", "data/"
    public LogicOperator Logic { get; set; }         // AND, OR (for next condition)
}
```

**Serialized example:**
```json
{
  "type": "FileExtension",
  "operator": "Is",
  "value": ".asi",
  "logic": "AND"
}
```

---

### 1.2 — Routing Rules (`Models/RoutingRule.cs`) [HAIKU]

**File:** `Models/RoutingRule.cs` (create new or expand existing)

```csharp
public enum LoadOrderBias
{
    Lower,              // Load earlier when no specific rule applies
    Higher,             // Load later when no specific rule applies
    None,               // Use defaults
}

public class RoutingRule
{
    public string Name { get; set; }                         // "ASI to scripts"
    public List<Condition> Conditions { get; set; }          // AND/OR chained
    public string TargetPath { get; set; }                   // "scripts/", "{gameRoot}/", "modloader/cleo/{scriptname}/"
    
    public int Priority { get; set; }                        // 0–100 (higher wins conflicts)
    public bool AllowConflict { get; set; }                  // true = ask user if conflict
    public LoadOrderBias LoadOrderBias { get; set; }         // Influence final load order
    
    public bool IsDefault { get; set; }                      // "Catch-all" rule?
}
```

**Special target path tokens:**
- `{gameRoot}` → Game install directory
- `{scriptname}` → Filename without extension (for nested CLEO scripts)
- Literal paths: `scripts/`, `plugins/`, `modloader/cleo/`, etc.

---

### 1.3 — Mod Types (`Models/ModType.cs`) [HAIKU]

**File:** `Models/ModType.cs`

```csharp
public class ModType
{
    public string Name { get; set; }                         // "ASI Plugin", "CLEO Script", "Texture Pack"
    public List<string> FileExtensions { get; set; }         // [".asi", ".dll"], [".cs"], [".dds", ".txd"]
    
    public List<RoutingRule> RoutingRules { get; set; }      // Rules specific to this type
    public LoadOrderBias DefaultBias { get; set; }           // Default load order preference
    
    // Auto-detection: is this mod type the "primary" detector?
    public bool IsPrimary { get; set; }                      // If multiple types match, use this one
}
```

---

### 1.4 — Updated CustomGameProfile (`Models/CustomGameProfile.cs`) [HAIKU]

**File:** `Models/CustomGameProfile.cs` (expand)

```csharp
public enum ReleaseTag
{
    Release,
    Beta,
    Alpha,
    Custom,             // User-defined tag
}

public enum RobustnessLevel
{
    Experimental,       // Early, frequent changes
    Stable,             // Tested, reliable
    Mature,             // Production-ready, change rarely
}

public class CustomGameProfile
{
    // Existing
    public string GameKey { get; set; }                      // "III", "IV", "CUSTOM_abc123"
    public string Name { get; set; }                         // "Grand Theft Auto III"
    public string InstallDir { get; set; }
    public string? ExePath { get; set; }
    public uint? SteamAppId { get; set; }
    
    // NEW: Mod configuration
    public List<ModType> ModTypes { get; set; }              // User-defined mod types
    public List<RoutingRule> RoutingRules { get; set; }      // Game-wide rules (in addition to type-specific)
    
    // NEW: Versioning
    public Version Version { get; set; }                     // 1.0.0
    public ReleaseTag ReleaseTag { get; set; }               // release, beta, alpha
    public string? CustomTag { get; set; }                   // +gta3-optimized
    public RobustnessLevel Robustness { get; set; }
    
    // NEW: Built-in flag
    public bool IsNative { get; set; }                       // true = shipped with TMM, false = user-created
}
```

---

### 1.5 — Updated ModItem (`Models/ModItem.cs`) [HAIKU]

**File:** `Models/ModItem.cs` (expand)

```csharp
public class ModItem
{
    // Existing
    public string Name { get; set; }
    public string FolderPath { get; set; }
    public bool IsEnabled { get; set; }
    
    // NEW: Type & load order
    public ModType? DetectedType { get; set; }               // Auto-detected from file contents
    public string? LoadAfter { get; set; }                   // Relative ordering: "ModX"
    public string? LoadBefore { get; set; }                  // Relative ordering: "ModY"
    public LoadOrderBias LoadOrderBias { get; set; }         // User preference for this mod
    
    // Calculated at deploy time
    public int FinalLoadOrder { get; set; }                  // 0–255, resolved from rules + preferences
}
```

---

## Phase 2: GUI — Sentence Builder & Custom Game Wizard

> **Design Philosophy:** Multi-step wizard (inspired by Lizard's Autodesigner) replaces monolithic dialog. Each step is focused, visually clean, and progressively builds the game profile. Mod type editing is inline (Step 2). Rule creation uses modal RuleEditorWindow (Step 3).

### 2.1 — Rule Editor Window (`Views/RuleEditorWindow.xaml`) [HAIKU]

**File:** `Views/RuleEditorWindow.xaml` + `RuleEditorWindow.xaml.cs`

**Modal dialog for creating/editing routing rules.**

**Features:**
- Condition builder with color-coded dropdowns (FileExtension, HasFolder, PathContains, etc.)
- Add/remove AND/OR operators between conditions
- Priority slider (0–100)
- Target path input with suggestions: {gameRoot}, scripts/, plugins/, modloader/cleo/{scriptname}/, etc.
- Load order bias selector: Lower / Higher / None
- "Ask user on conflicts" toggle checkbox
- Preview: "~X files would match this rule"
- [Save Rule] / [Cancel] buttons

**Opened from:** Step 3 (RoutingRulesPage) when user clicks [Edit] on existing rule or [+ Add Rule to {ModType}]

---

### 2.2 — Custom Game Setup Wizard (`Views/CustomGameSetupWizard.xaml`) [HAIKU THEN SONNET]

**Vision:** Multi-step wizard with clean, modern UI inspired by Lizard's Autodesigner. Each step is focused, visually distinct, and progressively builds the game profile.

**Main Window:** `Views/CustomGameSetupWizard.xaml` + `CustomGameSetupWizard.xaml.cs`

**Features:**
- Progress bar showing current step (e.g., "Step 2 of 4: Mod Types [━━━━●────────────] 50%")
- Back/Next navigation
- Dynamic content area that swaps between step pages
- Validation: disable Next until current step is valid
- Summary screen before final save

**Step Pages (each a UserControl):**

#### Step 1: Game Details (`Views/Steps/Step1_GameDetailsPage.xaml`)
- **Inputs:**
  - Game name (required, text input)
  - Install directory (required, path picker with validation)
  - Executable path (optional, file picker)
  - Steam App ID (optional, numeric input)
- **Behavior:**
  - Path auto-detection on load (Steam registry scan)
  - Validate path exists before allowing Next
  - Show status: ✓ Valid / ✗ Invalid

#### Step 2: Mod Types (`Views/Steps/Step2_ModTypesPage.xaml`)
- **Display:**
  - List of created mod types as clean cards
  - Each card shows: name, file extensions, rule count
  - [+ New Mod Type] button to add new
- **Inline Editing:**
  - Click card to edit inline (expand/collapse)
  - Name field, extension list (add/remove), [Delete] button
  - Validation: require at least 1 extension
- **Default types** (optional, suggested):
  - ASI Plugin (.asi, .dll)
  - CLEO Script (.cs)
  - DLL Plugin (.dll)
- **Behavior:**
  - Allow Next even with no custom types (use defaults)
  - Warn if no types selected: "Add at least 1 mod type"

#### Step 3: Routing Rules (`Views/Steps/Step3_RoutingRulesPage.xaml`)
- **Display:**
  - Rules grouped by mod type (collapsible sections)
  - Each rule shows: name, priority, conditions summary, load bias
  - [Edit] button opens `RuleEditorWindow` modal
  - [Delete] button with confirmation
- **Add Rules:**
  - [+ Add Rule to {ModType}] button per type
  - Opens `RuleEditorWindow` to create new rule
- **Conflict Detection:**
  - Scan for overlapping rules (same files, equal priority)
  - Show warning chips: "⚠ Rule 1 & Rule 3 overlap (allow conflict: yes/no toggle)"
  - Allow conflicts only if both rules have `AllowConflict=true`
- **Default Rules** (optional, suggested):
  - "ASI to scripts (no companions)"
  - "ASI with companions to root"
  - "CLEO scripts to modloader/cleo/{scriptname}/"

#### Step 4: Review & Save (`Views/Steps/Step4_ReviewPage.xaml`)
- **Summary:**
  - Game name, path, exe, Steam ID
  - Separator line
  - Mod types list (name, extensions, rule count)
  - Separator line
  - Robustness level selector: [Experimental] [Stable] [Mature]
  - Custom tag input (optional, e.g., "+gta3-optimized")
  - Native toggle (for testing): [☐ Mark as Native]
  - Version display (auto-calculated, read-only)
- **Actions:**
  - [Test Archive] — Show file browser, test routing rules on sample mod
  - [← Back] — Go back to edit
  - [Save Profile] — Create .tmmgame file, show success + location
  - [Cancel] — Discard and close

---

### 2.2.1 — Mod Type Inline Editor (Step 2)

**Within Step2_ModTypesPage:**
- Click card → expands inline
- Shows inputs: name, file extension list with [+ Add Extension] / [×] remove
- [Delete Type] button
- [Done] / [Cancel] buttons to collapse

**No separate modal needed** — keep Step 2 self-contained.

---

### 2.3 — Deploy Preview Dialog (`Views/DeployPreviewWindow.xaml`) [SONNET]

**File:** `Views/DeployPreviewWindow.xaml` + `DeployPreviewWindow.xaml.cs`

**Shows:**
- Detected mod type
- Matched rules
- File deployment mapping (file → destination)
- Load order preference
- [Override] buttons per file
- [Create custom rule] shortcut
- [Deploy] or [Cancel]

---

## Phase 3: Backend Logic & Conflict Resolution

### 3.1 — Rule Matching Engine (`Services/RuleEngine.cs`) [SONNET]

**File:** `Services/RuleEngine.cs` (new)

```csharp
public class RuleEngine
{
    // Given a file path, return all matching rules (may be multiple)
    public List<RoutingRule> FindMatchingRules(
        string filePath, 
        CustomGameProfile gameProfile) 
        => /* iterate rules, test conditions */;
    
    // Resolve priority conflicts
    public RoutingRule ResolveConflict(List<RoutingRule> candidates)
        => candidates.OrderByDescending(r => r.Priority).First();
    
    // Test if a condition matches a file
    private bool EvaluateCondition(Condition cond, string filePath, string modFolderPath)
        => /* extension check, folder presence, regex, etc. */;
    
    // Recursively evaluate AND/OR chains
    private bool EvaluateConditionChain(List<Condition> conditions, string filePath)
        => /* handle Logic.AND vs OR */;
}
```

---

### 3.2 — File Deployment Calculator (`Services/DeploymentPlanner.cs`) [SONNET]

**File:** `Services/DeploymentPlanner.cs` (new)

```csharp
public class DeploymentPlanner
{
    // Given a mod, return list of (file, destinationPath) tuples + warnings
    public async Task<DeploymentPlan> PlanDeploymentAsync(
        ModItem mod,
        CustomGameProfile gameProfile)
    {
        var plan = new DeploymentPlan
        {
            ModName = mod.Name,
            Files = new List<FileDeploymentEntry>(),
            Warnings = new List<DeploymentWarning>(),
        };
        
        foreach (var file in Directory.EnumerateFiles(mod.FolderPath, "*", SearchOption.AllDirectories))
        {
            var matches = ruleEngine.FindMatchingRules(file, gameProfile);
            
            if (matches.Count > 1 && !AllowConflict(matches))
            {
                plan.Warnings.Add(new DeploymentWarning { /* ... */ });
                continue;  // Don't deploy, ask user
            }
            
            var rule = matches.FirstOrDefault() ?? gameProfile.DefaultRule;
            plan.Files.Add(new FileDeploymentEntry
            {
                SourcePath = file,
                DestinationPath = ResolveTargetPath(rule.TargetPath, file),
            });
        }
        
        return plan;
    }
}

public class DeploymentPlan
{
    public string ModName { get; set; }
    public List<FileDeploymentEntry> Files { get; set; }
    public List<DeploymentWarning> Warnings { get; set; }  // Conflicts, unsupported files, etc.
}

public class FileDeploymentEntry
{
    public string SourcePath { get; set; }
    public string DestinationPath { get; set; }
    public bool Skip { get; set; }  // User can mark to skip
}
```

---

### 3.3 — Load Order Resolution (`Services/LoadOrderResolver.cs`) [SONNET]

**File:** `Services/LoadOrderResolver.cs` (new)

```csharp
public class LoadOrderResolver
{
    // Given a list of mods + their preferences, calculate final 0–255 order
    public void ResolveFinalLoadOrders(List<ModItem> mods, CustomGameProfile gameProfile)
    {
        // 1. Sort by explicit LoadAfter/LoadBefore relationships
        // 2. Within unspecified mods, sort by LoadOrderBias (Lower first, Higher last)
        // 3. Assign 0–255 positions
        
        var ordered = TopologicalSort(mods);  // Respects LoadAfter/LoadBefore
        
        int position = 0;
        int step = 255 / mods.Count;
        
        foreach (var mod in ordered)
        {
            if (mod.LoadOrderBias == LoadOrderBias.Lower)
                mod.FinalLoadOrder = position;
            else if (mod.LoadOrderBias == LoadOrderBias.Higher)
                mod.FinalLoadOrder = 255 - position;
            else
                mod.FinalLoadOrder = position;
            
            position += step;
        }
    }
}
```

---

## Phase 4: Integration with BackendCore

### 4.1 — Updated Deploy Flow [SONNET]

**File:** `Services/BackendCore.cs` (update `DeployModsAsync`)

**New flow:**
1. Show `DeployPreviewWindow` with deployment plan
2. User reviews and confirms (or overrides per-file)
3. If conflicts: ask user which rule to apply
4. Resolve load orders
5. Copy files to game directory
6. Create backup manifest
7. Show success notification

---

## Phase 5: Built-in Game Re-implementation

> **Note:** Each game is a standalone `.tmmgame` profile. No special series handling. All 6 games use identical system.

### 5.1 — Re-implement GTA III `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gta3.tmmgame` (new)

**Process:**
1. Open Custom Game Maker GUI
2. Configure:
   - Name: "Grand Theft Auto III"
   - Install Dir: Auto-detect from Steam registry
   - Exe: gta-iii.exe
   - Steam App ID: 12310
3. Create mod types:
   - "ASI Plugin" (.asi)
   - "CLEO Script" (.cs)
   - "DLL Plugin" (.dll)
4. Create routing rules (via sentence builder):
   - ASI (no companions): extension is .asi AND folder count = 0 → scripts/
   - ASI (with companions): extension is .asi AND folder count > 0 → {gameRoot}/
   - CLEO Scripts: extension is .cs → modloader/cleo/{scriptname}/
   - DLLs: extension is .dll → {gameRoot}/
   - Modloader: folder named "modloader" exists in root → copy entire modloader/ to {gameRoot}/
5. Set robustness: Mature, custom tag: (none)
6. Save with Native flag: ✓
7. Test with real GTA III mods

### 5.2 — Re-implement GTA VC `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gtavc.tmmgame` (new)

**Same structure as GTA III**
- Name: "Grand Theft Auto: Vice City"
- Steam App ID: 12311
- Same mod types + routing rules (identical logic)
- Save with Native flag

### 5.3 — Re-implement GTA SA `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gtasa.tmmgame` (new)

**Same structure as GTA III**
- Name: "Grand Theft Auto: San Andreas"
- Steam App ID: 12312
- Same mod types + routing rules
- Save with Native flag

### 5.4 — Re-implement GTA IV `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gtaiv.tmmgame` (new)

**Same structure, potentially episode-agnostic**
- Name: "Grand Theft Auto IV"
- Steam App ID: 12313
- Same mod types + routing rules (shared logic, no per-episode rules for now)
- Save with Native flag

### 5.5 — Re-implement GTA: Chinatown Wars `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gtatcw.tmmgame` (new)

**Same structure**
- Name: "Grand Theft Auto: Chinatown Wars"
- Steam App ID: 269170
- Same mod types + routing rules
- Save with Native flag

### 5.6 — Re-implement GTA: The Ballad of Gay Tony `.tmmgame` Profile [USER + HAIKU]

**File:** `Resources/Profiles/gtatbogt.tmmgame` (new)

**Same structure**
- Name: "Grand Theft Auto: The Ballad of Gay Tony"
- Steam App ID: 12314
- Same mod types + routing rules
- Save with Native flag

---

## Phase 6: Testing & Deployment

### 6.1 — Integration Tests [SONNET]

- Rule matching for each GTA game type
- Conflict detection
- Load order resolution
- File deployment (real I/O)

### 6.2 — Manual Testing (TEST_FLOW.md) [USER]

- Deploy custom game configs
- Test rule conflicts
- Verify GTA III/IV re-implementation matches original behavior

---

## Dependency Order

1. **Phase 1** (all 1.1–1.5 in parallel): Data models
2. **Phase 2** (all 2.1–2.3 in parallel, after Phase 1): GUI
3. **Phase 3** (3.1–3.3 in parallel, after Phase 2): Backend logic
4. **Phase 4** (4.1–4.2 sequentially, after Phase 3): BackendCore integration
5. **Phase 5** (5.1–5.6 in parallel, after Phase 4): Re-implement all 6 built-in games (ultimate test)
6. **Phase 6** (6.1–6.2 after Phase 5): Integration tests & verify

---

## Open Questions

- [ ] Load order: 0–255 or MO2 convention?
- [ ] Rule priority: numeric (0–100) or named tiers?
- [ ] Version auto-increment: based on robustness level?
- [ ] `.tmmgame` storage: Resources/ folder or %APPDATA%/TMM/Profiles/?
- [ ] Regex support in conditions: yes or too complex?
- [ ] Rule templates: pre-made rules users can use as starting point?

---

## Success Criteria

✓ User can create custom game config through GUI  
✓ Routing rules work for GTA III/IV mod scenarios  
✓ Conflict detection prevents silent failures  
✓ Deploy preview lets user intervene before deployment  
✓ GTA III/IV re-implementation via GUI produces identical behavior to original hardcoded logic  
✓ Any user can add new game support without touching code  
