# TMM — UI Flow Charts

> Mermaid diagrams of how the program's UI operates. Renders on GitHub.
> Each flow cites the code path that drives it. Keep in sync when navigation changes.
>
> Generated during the 2026-05-29 audit. Source of truth is the code — if a diagram
> and the code disagree, the code wins; fix the diagram.

---

## 1. App startup & first-launch

`App.OnStartup` constructs `BackendCore` (which calls `Logger.Initialize` + `LoadSettings`)
then shows the single main window. First-launch onboarding is gated on
`Settings.FirstLaunch` inside `UnifiedShellWindow.Window_Loaded`.

```mermaid
flowchart TD
    A[App.OnStartup] --> B[new BackendCore\nLogger.Initialize + LoadSettings]
    B --> C[UnifiedShellWindow.Show]
    C --> D[Window_Loaded]
    D --> E{Settings.FirstLaunch?}
    E -- yes --> F[InitialSetupWindow\nlanguage + two game-choice cards]
    F -- built-in card --> I[SelectBuiltinGameWindow]
    F -- custom card --> J[CustomGameSetupWizard]
    I --> K[await core.InitializeAsync]
    J --> K
    E -- no --> K
    K --> L[BuildLibraryEntries -> LibraryPage]
    L --> M[Shell ready]
```

**Updated v0.1-alpha-9 (S7 done):** `FirstGamePickerWindow` was removed; its built-in/custom
choice cards now live directly in `InitialSetupWindow` below the language picker — one screen
instead of two.

---

## 2. Shell navigation

`UnifiedShellWindow` is a single window hosting swappable pages (no separate windows
for the main areas). The left nav switches `_currentPage`.

```mermaid
flowchart LR
    Shell[UnifiedShellWindow] --> Lib[LibraryPage]
    Shell --> Mods[ModManagerPage]
    Shell --> Backups[BackupsPage]
    Shell --> Downloads[DownloadsPage]
    Shell --> Paths[PathsPage]
    Shell --> Settings[SettingsPage]
    Lib -- "Manage mods for a game" --> Mods
    Lib -- "Add game (+)" --> Wizard[CustomGameSetupWizard]
    Shell -. "Activity feed" .-> Activity[ActivityFeedWindow]
```

---

## 3. Add a custom game (wizard)

Per the standing rule, every built-in capability must be reachable here. The `.tmmgame`
JSON is only a shortcut for bundled profiles.

```mermaid
flowchart TD
    W0[CustomGameSetupWizard] --> S1[Step 1 — Game details\nname, dir, Steam AppId,\nintegrity exe size/MD5,\noverlay folders, companions]
    S1 --> S2[Step 2 — Mod types]
    S2 --> S3[Step 3 — Routing rules]
    S3 --> S4[Step 4 — Review summary]
    S4 --> Save[GameRegistry persists\nCustomGames/key.json]
    Save --> Lib[New card in LibraryPage]
```

---

## 4. Install a mod → freeze the deployment plan

Per architectural principle #1, rules run **once** at install and the resulting
`DeploymentPlan` is frozen to `_tmm/deployplan.json`. Deploys execute the saved plan.

```mermaid
flowchart TD
    Add[Add mod\narchive extract / folder add / drag-drop] --> Copy[Copy into ModsRaw_key/ModName/]
    Copy --> Proxy[ProxyDllDetector.Scan\nnotify if proxy DLL found]
    Copy --> OnAdd[BackendCore.OnModAddedAsync]
    OnAdd --> Plan[DeploymentPlanner.PlanDeploymentAsync\nRuleEngine routes each file]
    Plan --> Freeze[(Save deployplan.json)]
    Refresh[RefreshCustomAsync] --> Ensure[EnsureDeploymentPlansAsync\nbackfill any mod missing a plan]
    Ensure --> OnAdd
```

---

## 5. Deploy (with preview + conflict resolution)

`BtnDeployCustom_Click` resolves load order, builds frozen plans, shows the preview,
then deploys only the rows the user kept. Cross-mod conflicts are arbitrated in the
resolver; baseline capture happens just before each overwrite.

```mermaid
flowchart TD
    Dep[BtnDeployCustom_Click] --> Resolve[LoadOrderResolver.ResolveFinalLoadOrders]
    Resolve --> Plans[GetDeploymentPlanAsync per enabled mod\nfrozen plan, else live + warn]
    Plans --> Preview[DeployPreviewWindow]
    Preview --> Analyze[ConflictAnalyzer.Analyze\ngroup files by destination]
    Analyze --> HasConf{>=2 mods write\nsame path?}
    HasConf -- yes --> ResolveBtn[Resolve conflicts button]
    ResolveBtn --> CRW[ConflictResolverWindow\npick winner per path\ndefault = highest FinalLoadOrder]
    CRW --> Skip[losers set Skip=true]
    HasConf -- no --> Confirm
    Skip --> Confirm{User confirms?}
    Confirm -- cancel --> End[abort]
    Confirm -- deploy --> Map[BuildFileMap\nexcludes skipped rows]
    Map --> Core[DeployFilesToGameDirAsync]
    Core --> Base[BaselineSnapshot.EnsureCaptured\nfirst-touch original bytes]
    Base --> Backup[per-deploy backup of overwritten files]
    Backup --> Write[write mod files to game dir]
    Write --> Manifest[(save manifest.json + prune to 3)]
    Manifest --> Activity[ActivityLogger.Record]
```

---

## 6. Rollback / restore

Rollback's source of truth is the first-touch `baseline.json`; the per-deploy manifest
is only the index of *which* files to revert. Falls back to per-deploy backup if a
baseline record is missing.

```mermaid
flowchart TD
    RB[BackupsPage Restore\nor ModManager Rollback] --> Confirm{Confirm?}
    Confirm -- no --> End[abort]
    Confirm -- yes --> Iter[For each manifest entry]
    Iter --> HasBase{In baseline.json?}
    HasBase -- "yes, snapshot != null" --> RestoreBase[copy baseline snapshot over file]
    HasBase -- "yes, snapshot == null" --> DelBase[file did not exist at first touch\n-> delete]
    HasBase -- no --> HasBackup{per-deploy backup exists?}
    HasBackup -- yes --> RestoreBackup[copy backup over file]
    HasBackup -- no --> DelNew[newly added by deploy -> delete]
    RestoreBase --> Dirs
    DelBase --> Dirs
    RestoreBackup --> Dirs
    DelNew --> Dirs[remove now-empty directories]
    Dirs --> Done[ActivityLogger.Record]
```

---

## 7. Import from an existing modded install (B5)

Point TMM at a pre-modded directory; heuristically detect mods; move (not copy) them
into `ModsRaw_key` as managed mods, then re-deploy to restore the install untouched.

```mermaid
flowchart TD
    Imp[Import from game folder] --> Scan[ModImporter.ScanAsync\nflag .asi/.dll/.cs*/.fxt/.ini\ngroup CLEO + modloader trees]
    Scan --> Review[ImportReviewWindow\nrename / select / exclude\nlow-confidence warnings]
    Review --> Confirm{User confirms?}
    Confirm -- no --> End[abort]
    Confirm -- yes --> Seed[SeedBaselineAsync\nsnapshot entire game dir = first touch]
    Seed --> Validate[verify all source files exist]
    Validate --> Move[move files into ModsRaw\ntransaction; rollback on failure]
    Move --> Cleanup[remove emptied source dirs]
    Cleanup --> Redeploy[DeployCustomGameModsAsync\nrestore install state]
    Redeploy --> Backfill[RefreshCustomAsync ->\nEnsureDeploymentPlansAsync freezes plans]
```

---

## 8. Loadouts

A loadout snapshots enabled-state + load order. `.tmmpack` bundles a loadout plus the
mod source folders for sharing.

```mermaid
flowchart TD
    subgraph Manage
      Save[Save loadout\nSaveLoadoutAsync] --> File[(Loadouts_key/Name.json)]
      Apply[Apply loadout\nApplyLoadoutAsync] --> SetStates[set IsEnabled + LoadOrder\nreorder list]
      Rename[Rename] --> File
      Delete[Delete] --> File
    end
    subgraph Share
      Export[Export .tmmpack\nTmmPackBuilder] --> Pack[(zip: manifest + loadout + mods/)]
    end
    subgraph Compare
      Diff[Compare loadouts\nLoadoutDiffWindow] --> AB[A vs B: adds / removals /\nenable-disable / reorder]
    end
```

> ⚠️ **Gap noted in audit:** `.tmmpack` can be **exported** but there is no import/consume
> path yet — a shared pack can't currently be loaded back into TMM.
