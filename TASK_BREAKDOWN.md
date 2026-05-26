# Task Breakdown — Routing Rules Refactor

Organized by agent tier and dependency order. **Run tasks within each tier in parallel**, respect tier order.

## Architectural Change

**All predefined game-specific routing logic is being removed.** Each game (GTA III, VC, SA, IV, TLaD, TBoGT, custom) is now a standalone `.tmmgame` profile with user-configurable routing rules. No more special cases in BackendCore or GameRegistry for game series—all games treated identically.

---

## HAIKU Tasks — Data Models (Phase 1)

Simple data classes with clear requirements. Run all in parallel.

### H1.1 — Create `Models/Condition.cs`
- [ ] File: `Models/Condition.cs`
- [ ] Enums: `ConditionType`, `ConditionOperator`, `LogicOperator`
- [ ] Class: `Condition` with properties: Type, Operator, Value, Logic
- [ ] JSON serialization support (Newtonsoft or System.Text.Json)
- [ ] Reference: PLANS.md → 1.1

### H1.2 — Create `Models/RoutingRule.cs` (Expanded)
- [ ] File: `Models/RoutingRule.cs`
- [ ] Enums: `LoadOrderBias`
- [ ] Class: `RoutingRule` with properties: Name, Conditions, TargetPath, Priority, AllowConflict, LoadOrderBias, IsDefault
- [ ] JSON serialization support
- [ ] Reference: PLANS.md → 1.2

### H1.3 — Create `Models/ModType.cs`
- [ ] File: `Models/ModType.cs`
- [ ] Class: `ModType` with properties: Name, FileExtensions, RoutingRules, DefaultBias, IsPrimary
- [ ] JSON serialization support
- [ ] Reference: PLANS.md → 1.3

### H1.4 — Update `Models/CustomGameProfile.cs`
- [ ] Add enums: `ReleaseTag`, `RobustnessLevel`
- [ ] Add properties: ModTypes, RoutingRules, Version, ReleaseTag, CustomTag, Robustness, IsNative
- [ ] Update JSON serialization
- [ ] Reference: PLANS.md → 1.4

### H1.5 — Update `Models/ModItem.cs`
- [ ] Add properties: DetectedType, LoadAfter, LoadBefore, LoadOrderBias, FinalLoadOrder
- [ ] Update JSON serialization
- [ ] Reference: PLANS.md → 1.5

---

## HAIKU Tasks — GUI (Phase 2)

Simple, self-contained UI pages with clear layouts.

### H2.1 — Create `Views/Steps/Step1_GameDetailsPage.xaml` + Code-behind
- [ ] File: `Views/Steps/Step1_GameDetailsPage.xaml`
- [ ] File: `Views/Steps/Step1_GameDetailsPage.xaml.cs`
- [ ] Inputs: GameName (TextBox), InstallDir (TextBox + Browse button), ExePath (TextBox + Browse button), SteamAppId (TextBox)
- [ ] Validation: Check directory exists, show ✓/✗ status
- [ ] Auto-detect game paths on load (call QuickScan from GameRegistry)
- [ ] Property: `IsValid { get; }` — used by wizard to enable Next button
- [ ] Reference: PLANS.md → 2.2 Step 1

### H2.2 — Create `Views/Steps/Step4_ReviewPage.xaml` + Code-behind
- [ ] File: `Views/Steps/Step4_ReviewPage.xaml`
- [ ] File: `Views/Steps/Step4_ReviewPage.xaml.cs`
- [ ] Display summary: Game name, path, exe, Steam ID, mod types, rule counts
- [ ] Inputs: Robustness selector ([Experimental] [Stable] [Mature]), Custom tag (TextBox), Native toggle
- [ ] Buttons: [Test Archive], [Save Profile], [← Back], [Cancel]
- [ ] Version display (read-only, auto-calculated from robustness)
- [ ] Reference: PLANS.md → 2.2 Step 4

---

## SONNET LOW Tasks — Data & Serialization (Phase 1)

Moderate complexity: integration points between models and storage.

### S1.1 — Serialization & Versioning (`Services/GameProfileSerializer.cs`)
- [ ] File: `Services/GameProfileSerializer.cs` (new)
- [ ] Methods:
  - `SerializeCustomGameProfile(profile) → json string`
  - `DeserializeCustomGameProfile(json) → CustomGameProfile`
  - `SaveAsNativeProfile(profile, outputPath)` — save to .tmmgame file in Resources/Profiles/
  - `LoadNativeProfile(gameKey) → CustomGameProfile` — load built-in game from Resources/Profiles/
- [ ] Handle version bumping: auto-increment based on RobustnessLevel
- [ ] Handle migration: if old .tmmgame format, upgrade gracefully
- [ ] Reference: PLANS.md → 1.4, 5.1

### S1.2 — Update GameRegistry to Load Native Profiles
- [ ] File: `Services/GameRegistry.cs` (update)
- [ ] Load built-in games from Resources/Profiles/*.tmmgame files
- [ ] Mix built-in + custom games seamlessly
- [ ] Use `IsNative` flag to show user which games are built-in
- [ ] Allow users to mark custom games as Native (for testing)
- [ ] Reference: PLANS.md → Phase 5

---

## SONNET LOW Tasks — GUI (Phase 2)

Moderate complexity: multi-control UI with validation and state management.

### S2.1 — Create `Views/Steps/Step2_ModTypesPage.xaml` + Code-behind
- [ ] File: `Views/Steps/Step2_ModTypesPage.xaml`
- [ ] File: `Views/Steps/Step2_ModTypesPage.xaml.cs`
- [ ] Display: List of mod types as clean cards (ItemsControl or ListBox)
  - Card shows: Name, file extensions (comma-separated), rule count
  - Card styling: light panel, hover effect
- [ ] [+ New Mod Type] button at bottom
- [ ] Click card → expand inline with inputs: Name, Extensions list
  - Extensions: TextBox + [+ Add Extension] button + [×] remove per extension
  - [Delete Type] button (with confirmation)
  - [Done] / [Cancel] to collapse
- [ ] Validation: At least 1 extension per type
- [ ] Data binding: Bind to `ObservableCollection<ModType>`
- [ ] Property: `IsValid { get; }` — allow Next even with 0 types
- [ ] Reference: PLANS.md → 2.2 Step 2

### S2.2 — Create `Views/Steps/Step3_RoutingRulesPage.xaml` + Code-behind
- [ ] File: `Views/Steps/Step3_RoutingRulesPage.xaml`
- [ ] File: `Views/Steps/Step3_RoutingRulesPage.xaml.cs`
- [ ] Display: Rules grouped by ModType (Expander per type, collapsible)
  - Per rule: Name, Priority (visual bar 0–100), Conditions (summary text), Load bias
  - [Edit] button → opens `RuleEditorWindow` modal
  - [Delete] button with confirmation
- [ ] [+ Add Rule to {ModType}] button per type
  - Clicks → opens RuleEditorWindow (create new)
- [ ] Conflict detection:
  - Scan all rules for overlaps (same file extensions, equal priority)
  - Show warning chips: "⚠ Rule 1 & Rule 3 overlap (allow? [toggle])"
  - Only warn if both rules don't have AllowConflict=true
- [ ] Data binding: Bind to rules grouped by ModType
- [ ] Property: `IsValid { get; }` — allow Next even with 0 rules
- [ ] Reference: PLANS.md → 2.2 Step 3

### S2.3 — Create `Views/RuleEditorWindow.xaml` + Code-behind
- [ ] File: `Views/RuleEditorWindow.xaml`
- [ ] File: `Views/RuleEditorWindow.xaml.cs`
- [ ] Inputs:
  - Rule Name (TextBox)
  - Conditions builder:
    - First condition: [Type ▼] [Operator ▼] [Value TextBox]
    - Subsequent: [Logic ▼] [Type ▼] [Operator ▼] [Value TextBox] + [- Remove]
    - [+ Add AND] / [+ Add OR] buttons
  - Target Path (TextBox with suggestions: {gameRoot}, scripts/, modloader/cleo/{scriptname}/)
  - Priority (Slider 0–100, numeric display)
  - Load Order Bias ([Lower] [Higher] [None] radio buttons)
  - Allow Conflict (Checkbox)
- [ ] Color-coding: Condition types in blue, operators in green, values in yellow
- [ ] Preview: "~X files would match this rule" (calculate on demand, non-blocking)
- [ ] Validation: Conditions complete, target path not empty
- [ ] [Save Rule] / [Cancel] buttons
- [ ] Reference: PLANS.md → 2.1

### S2.4 — Create Main Wizard Window `Views/CustomGameSetupWizard.xaml` + Code-behind
- [ ] File: `Views/CustomGameSetupWizard.xaml`
- [ ] File: `Views/CustomGameSetupWizard.xaml.cs`
- [ ] Layout:
  - Title: "Create Custom Game Profile"
  - Progress bar: "Step X of 4: [━━━●────────] XX%"
  - Content area: ContentControl that swaps step pages
  - Navigation: [← Back] [Next →] buttons
- [ ] Logic:
  - Track current step (0–3)
  - Disable Back on step 0, disable Next if current step.IsValid==false
  - Disable Next/Back during async operations (path detection, etc.)
  - On "Next" from step 3 → show Step 4 (Review)
  - On "Save" from step 4 → call GameProfileSerializer.SaveAsNativeProfile()
- [ ] Store wizard state: Dictionary or simple class holding: GameName, InstallDir, ExePath, SteamAppId, ModTypes, RoutingRules, Robustness, CustomTag, IsNative
- [ ] Reference: PLANS.md → 2.2

### S2.5 — Create `Views/DeployPreviewWindow.xaml` + Code-behind
- [ ] File: `Views/DeployPreviewWindow.xaml`
- [ ] File: `Views/DeployPreviewWindow.xaml.cs`
- [ ] Display:
  - Title: "Deployment Plan: {ModName} ({ModType})"
  - Detected type, matched rules, file mapping list
  - Each file: source → destination, [Override ▼] dropdown
  - Load order preference display
  - Folder tree preview (TreeView or text representation)
- [ ] Interactions:
  - [Override ▼] per file: change destination or mark [SKIP]
  - [Create custom rule for this mod] button → hint to user
  - [✓ Deploy] / [✗ Cancel]
- [ ] Data binding: Accept `DeploymentPlan` object from backend
- [ ] Reference: PLANS.md → 2.3

---

## SONNET HIGH Tasks — Backend Logic (Phase 3)

Complex logic: multi-step reasoning, conditional evaluation, algorithm design.

### S3.1 — Implement `Services/RuleEngine.cs`
- [ ] File: `Services/RuleEngine.cs` (new)
- [ ] Core methods:
  - `FindMatchingRules(filePath: string, gameProfile: CustomGameProfile) → List<RoutingRule>`
    - Iterate all rules in gameProfile.RoutingRules + all rules in all ModTypes
    - For each rule, evaluate all conditions (call EvaluateConditionChain)
    - Return rules where ALL conditions match
  - `EvaluateConditionChain(conditions: List<Condition>, filePath: string, modFolderPath: string) → bool`
    - Handle AND/OR logic (recursive or iterative)
    - Short-circuit on AND (if condition fails, return false)
    - Accumulate OR results
  - `EvaluateCondition(condition: Condition, filePath: string, modFolderPath: string) → bool`
    - Dispatch by ConditionType (FileExtension, HasFolder, FolderCount, etc.)
    - Implement each operator (Is, Contains, Equals, MatchesRegex, etc.)
    - Handle edge cases (regex errors, missing folders, etc.)
- [ ] Helper: `ResolveTargetPath(template: string, fileName: string) → string`
  - Replace {gameRoot} with game install path
  - Replace {scriptname} with filename without extension
  - Return final destination path
- [ ] Error handling: Log warnings for bad conditions/paths, don't throw
- [ ] Reference: PLANS.md → 3.1

### S3.2 — Implement `Services/DeploymentPlanner.cs`
- [ ] File: `Services/DeploymentPlanner.cs` (new)
- [ ] Core method:
  - `PlanDeploymentAsync(mod: ModItem, gameProfile: CustomGameProfile) → DeploymentPlan`
    - Enumerate all files in mod.FolderPath (recursively)
    - For each file, call ruleEngine.FindMatchingRules()
    - If 1 rule matches: add to plan
    - If 0 rules match: use game's default rule (or ask user)
    - If 2+ rules match with different priorities: use highest priority
    - If 2+ rules match with same priority: add warning + don't deploy (ask user)
    - Calculate destination path via ruleEngine.ResolveTargetPath()
    - Return DeploymentPlan object (files + warnings)
- [ ] Handle special cases:
  - modloader/ folder in root: deploy entire folder to {gameRoot}/modloader/
  - Archive handling: caller handles extraction, this method works on extracted folder
  - Permission errors: skip file with warning, continue
- [ ] Return types: `DeploymentPlan`, `FileDeploymentEntry`, `DeploymentWarning`
- [ ] Reference: PLANS.md → 3.2

### S3.3 — Implement `Services/LoadOrderResolver.cs`
- [ ] File: `Services/LoadOrderResolver.cs` (new)
- [ ] Core method:
  - `ResolveFinalLoadOrders(mods: List<ModItem>, gameProfile: CustomGameProfile) → void`
    - Respects LoadAfter/LoadBefore relationships (topological sort)
    - Within unspecified mods, sort by LoadOrderBias (Lower = earlier, Higher = later)
    - Assign final 0–255 positions
    - Handle cycles: warn user, break cycle arbitrarily
- [ ] Helper: `TopologicalSort(mods) → List<ModItem>`
  - Build dependency graph from LoadAfter/LoadBefore
  - Detect cycles and log warnings
  - Return sorted list respecting constraints
- [ ] Reference: PLANS.md → 3.3

---

## SONNET HIGH Tasks — Integration (Phase 4)

Complex integration: tie all pieces together, handle conflicts, maintain backwards compatibility.

### S4.1 — Update `Services/BackendCore.cs` — Deploy Flow
- [ ] File: `Services/BackendCore.cs` (update DeployModsAsync)
- [ ] New flow:
  - Resolve load orders (call LoadOrderResolver)
  - Plan deployment (call DeploymentPlanner for each mod)
  - Aggregate warnings (conflicts, missing files, etc.)
  - If conflicts detected: show `DeployPreviewWindow` dialog
    - User reviews, can override per-file
    - User must confirm before deploy continues
  - Copy files to game directory (respect user overrides from preview)
  - Create backup manifest (existing logic)
  - Show success notification
- [ ] Handle conflicts:
  - Multiple rules matching same file (equal priority)
  - Ask user: which rule to apply? / Skip file? / Create custom rule?
  - Remember choice per mod? (optional UI refinement)
- [ ] Maintain backwards compatibility: still work with old .tmmgame files (if they exist)
- [ ] Reference: PLANS.md → 4.1

### S4.2 — Integrate RuleEngine + DeploymentPlanner into BackendCore
- [ ] Update BackendCore constructor: inject RuleEngine, DeploymentPlanner, LoadOrderResolver
- [ ] Update Deploy method signatures to accept gameProfile with routing rules
- [ ] Test with real mods: verify deploy works as expected
- [ ] Reference: PLANS.md → 4.1

---

## HAIKU Tasks — Built-in Game Re-implementation (Phase 5)

Data entry via GUI. User fills out wizard for each game individually, saves as .tmmgame. **All 6 games use identical mod types + routing rules.**

### H5.1 — User: Re-implement GTA III via Custom Game Wizard
- [ ] Open CustomGameSetupWizard
- [ ] Step 1: "Grand Theft Auto III" / auto-detect path / gta-iii.exe / Steam ID 12310
- [ ] Step 2: Create mod types (ASI Plugin, CLEO Script, DLL Plugin)
- [ ] Step 3: Create routing rules:
  - "ASI to scripts (no companions)" — if .asi AND folder_count=0 → scripts/
  - "ASI with companions" — if .asi AND folder_count>0 → {gameRoot}/
  - "CLEO scripts" — if .cs → modloader/cleo/{scriptname}/
  - "DLLs to root" — if .dll → {gameRoot}/
  - "Modloader folder" — if folder named "modloader" exists → {gameRoot}/modloader/
- [ ] Step 4: Robustness = Mature, Native flag = ✓, save
- [ ] Result: `Resources/Profiles/gta3.tmmgame`

### H5.2 — User: Re-implement GTA VC via Custom Game Wizard
- [ ] Same structure as H5.1
- [ ] "Grand Theft Auto: Vice City" / auto-detect / gta-vc.exe / Steam ID 12311
- [ ] Same mod types + routing rules
- [ ] Result: `Resources/Profiles/gtavc.tmmgame`

### H5.3 — User: Re-implement GTA SA via Custom Game Wizard
- [ ] Same structure as H5.1
- [ ] "Grand Theft Auto: San Andreas" / auto-detect / gta-sa.exe / Steam ID 12312
- [ ] Same mod types + routing rules
- [ ] Result: `Resources/Profiles/gtasa.tmmgame`

### H5.4 — User: Re-implement GTA IV via Custom Game Wizard
- [ ] Same structure as H5.1
- [ ] "Grand Theft Auto IV" / auto-detect / GTAIV.exe / Steam ID 12313
- [ ] Same mod types + routing rules
- [ ] Result: `Resources/Profiles/gtaiv.tmmgame`

### H5.5 — User: Re-implement GTA: Chinatown Wars via Custom Game Wizard
- [ ] Same structure as H5.1
- [ ] "Grand Theft Auto: Chinatown Wars" / auto-detect / exe / Steam ID 269170
- [ ] Same mod types + routing rules
- [ ] Result: `Resources/Profiles/gtatcw.tmmgame`

### H5.6 — User: Re-implement GTA: The Ballad of Gay Tony via Custom Game Wizard
- [ ] Same structure as H5.1
- [ ] "Grand Theft Auto: The Ballad of Gay Tony" / auto-detect / exe / Steam ID 12314
- [ ] Same mod types + routing rules
- [ ] Result: `Resources/Profiles/gtatbogt.tmmgame`

---

## SONNET LOW Tasks — Testing (Phase 6)

Moderate complexity: write integration tests covering workflows.

### S6.1 — Write Integration Tests (`Tests/RuleEngineTests.cs`, `Tests/DeploymentPlannerTests.cs`)
- [ ] Test RuleEngine:
  - Condition matching (extension, folder, path, count)
  - AND/OR logic chains
  - Rule priority resolution
- [ ] Test DeploymentPlanner:
  - Single rule match → correct destination
  - No rule match → default handling
  - Multiple rules, same priority → conflict warning
  - Real file I/O: extract sample GTA III mod, plan deployment, verify file mapping
- [ ] Test LoadOrderResolver:
  - Topological sort with LoadAfter/LoadBefore
  - Bias application (Lower/Higher)
  - Cycle detection
- [ ] Reference: PLANS.md → 6.1

---

## Task Order & Parallelization

**Tier 1 (Parallel - Data Models):**
- H1.1, H1.2, H1.3, H1.4, H1.5 (all HAIKU)
- Then S1.1, S1.2 (SONNET LOW, depends on Phase 1 complete)

**Tier 2 (Parallel - GUI Components):**
- H2.1, H2.2 (HAIKU)
- S2.1, S2.2, S2.3, S2.4, S2.5 (SONNET LOW)
- All can run in parallel once Phase 1 done

**Tier 3 (Parallel - Backend Logic):**
- S3.1, S3.2, S3.3 (SONNET HIGH)
- Can start after Phase 2 is visually done (doesn't block on Phase 2 implementation)

**Tier 4 (Sequential - Integration):**
- S4.1, S4.2 (SONNET HIGH, depends on Phase 3 complete)

**Tier 5 (Sequential - Re-implementation & Testing):**
- H5.1, H5.2 (HAIKU, user-driven, depends on Phase 4 + working wizard)
- S6.1 (SONNET LOW, depends on Phase 4 complete)

---

## Summary by Agent Type

| Tier | Count | Tasks |
|------|-------|-------|
| **HAIKU** | 11 | H1.1–H1.5, H2.1, H2.2, H5.1–H5.6 |
| **SONNET LOW** | 8 | S1.1, S1.2, S2.1–S2.5, S6.1 |
| **SONNET HIGH** | 4 | S3.1–S3.3, S4.1, S4.2 |
| **USER** | 6 | H5.1–H5.6 (using GUI to re-implement games) |
| **TOTAL** | ~29 | All phases |

---

## Ready to Delegate

**Next session:** Provide this breakdown to agents:
1. **For Haiku:** All H-prefixed tasks in dependency order
2. **For Sonnet (Low):** All S*Low tasks in dependency order
3. **For Sonnet (High):** All S*High tasks in dependency order
4. **For User:** H5.1, H5.2 (re-implement built-in games via GUI as ultimate test)

Each agent has full context from PLANS.md + this breakdown.
