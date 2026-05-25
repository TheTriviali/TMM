# Claude.md — TMM Chat Context

Quick reference for AI sessions. For detailed architecture, see [CODEBASE_GUIDE.md](CODEBASE_GUIDE.md).

## What is TMM?

**TMM** (Triviali's Mod Manager) — lightweight mod manager for GTA III series + Skyrim + custom games.  
Direct-deploy architecture: mods go straight to game directories, no VFS staging.

**Tech:** WPF + C# (.NET 10-windows), Windows 11 native (Mica backdrop, no legacy baggage).

---

## File Structure

```
├── App.xaml / App.xaml.cs          crash handler, entry point
├── Services/
│   ├── BackendCore.cs              orchestrator (deploy, rollback, mod lists, paths)
│   ├── GameRegistry.cs             registry of built-in + custom games
│   ├── NotificationService.cs       toast notification queue
│   └── SteamLauncher.cs            Steam protocol invokes
├── Views/
│   ├── GameLauncherWindow.xaml      hub (GTA III, IV, Custom games)
│   ├── MainDashboardWindow.xaml     GTA III/VC/SA manager
│   ├── Gta4DashboardWindow.xaml     GTA IV/TLaD/TBoGT 3-column layout
│   ├── CustomGameDashboardWindow    user-added games
│   ├── InitialSetupWindow.xaml      first-run path wizard
│   ├── SettingsWindow.xaml          theme, paths, advanced
│   ├── ThemeManagerWindow.xaml      theme preset browser
│   ├── CustomGameConfigWindow.xaml  game profile editor
│   └── [supporting windows]
├── Models/
│   ├── GameProfile.cs              built-in game defs (III, VC, SA, IV, TLaD, TBoGT)
│   ├── CustomGameProfile.cs        user game profiles
│   ├── ModItem.cs                  single mod (name, load order, path)
│   ├── AppSettings.cs              persisted config
│   ├── DeployManifest.cs           backup snapshot for rollback
│   ├── RoutingRule.cs              file extension → output dir routing
│   └── [other models]
├── Theming/
│   └── ThemeEngine.cs              apply themes (colors, fonts, Mica)
├── Converters/ + Helpers/          XAML converters, shell utilities
└── CODEBASE_GUIDE.md               detailed TOC + search index
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

### Game Keys
Built-in: `"III"`, `"VC"`, `"SA"`, `"IV"`, `"TLaD"`, `"TBoGT"`  
Custom: `"CUSTOM_abc123"` (auto-generated)

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

---

## Recent Changes

See `git log --oneline` or check master branch. Latest: TMM rename (TGTAMM→TMM files, GitHub repo, default branch master).

---

## CI/Tests

Currently no formal test suite. Verification is manual (run the app, test features).

---

## Feedback Loop

If a chat is inefficient or you want me to remember something for future sessions, update:
- `CODEBASE_GUIDE.md` if architecture/file structure changes
- `PLANS.md` if design decisions shift
- `.claude/memory/` (user-facing memory system) for session-specific context
