# TMM — UX Review Handoff (for Sonnet)

> **Purpose:** Opus just landed a batch of setup/library fixes. This doc hands the baton to
> Sonnet for a *zoom-out* design & usability pass. It captures current state, what changed,
> and the open questions worth a fresh pair of eyes — so you don't have to re-derive context.
>
> **Date:** 2026-05-29 · **Branch:** master · **Build:** clean (0 warnings), 60/60 tests pass.

---

## 1. What TMM is (one paragraph)

Lightweight, direct-deploy mod manager for the GTA III series + arbitrary user-added games
(Skyrim, FNV, Cyberpunk, RDR2, Witcher 3 ship as built-in profiles). WPF + C# (.NET 10).
No VFS — mods are copied into the game directory. Deployment plans freeze at install;
rollback restores to a first-touch baseline. Custom games are first-class: anything a
built-in game can do must be configurable via the custom-game wizard (no hand-edited JSON).

## 2. The primary surfaces

| Surface | File | Role |
|---|---|---|
| Library | `Views/Subpages/LibraryPage.xaml(.cs)` | Game grid / list / showcase. Entry point. |
| Game card | `Views/Controls/GameCard.xaml(.cs)` | One game; Play / Manage / Default / overflow. |
| Mod Manager | `Views/Subpages/ModManagerPage.xaml` + partials | Per-game mod list, deploy, downloads drawer. |
| Shell | `Views/UnifiedShellWindow.xaml.cs` | Nav, `BuildLibraryEntries`, routing between pages. |
| Downloads | `Views/Subpages/DownloadsPage.xaml.cs` | WebView2 browser + archive list (also embedded as a drawer in Mod Manager). |

Library view mode (`grid` | `list` | `showcase`) persists in `AppSettings.LibraryViewMode`.

## 3. What just changed (so you're not surprised / don't re-litigate)

**Group E (setup/download flow) — audited & one bug fixed this session:**
- E1 Downloads follows the active/default game · E2 unified folder-open handlers + factory-reset
  re-creates base dirs · E4 reusable `BackendCore.InstallArchiveForGameAsync` + Install button on
  Downloads page · E5 Downloads drawer auto-opens when archives exist.
- **E3 fix:** inline "Set game folder" banner + clickable sidebar path now persist correctly for
  *built-in* games (was only updating custom games' in-memory config).
- Removed 5 stray mod test files accidentally committed into the repo root.

**Library/card changes this session:**
- **Card buttons now work.** A transparent hover overlay was swallowing all clicks; the whole
  card acted as one button. Fixed via `IsHitTestVisible="False"`.
- **"Show groups" toggle removed** from Mod Manager. Grouping is now automatic: the mod list
  groups whenever any mod has a `GroupName` (set per-mod via right-click → Set Group, or picked
  up on import), flat otherwise.
- **Card colors:** built-in gradients varied (VC magenta, SA orange, TLAD crimson, TBoGT gold;
  III green & IV blue unchanged). Users can set a per-card gradient via right-click →
  "Set Card Color…" (preset swatches + custom hex), persisted in `AppSettings.CardColorOverrides`.

## 4. Open UX questions for your pass (suggestions, not constraints)

These are the things Opus noticed but deliberately left for a design-level review:

1. **Card information hierarchy.** In list mode the row packs: drag grip · Default pill · title ·
   status pill · subtitle · mod count · ready dot · Play · Manage · ⋯. Is that the right priority
   order? The ready dot is tiny and easy to miss; "Default" is a pill *and* drives navigation
   (default game opens straight to its Mod Manager) — is that link discoverable?

2. **Three view modes (grid/list/showcase) — do all three earn their place?** Showcase had a
   symmetry fix (B2) but is it actually useful vs. just grid+list? Consider whether the view
   switcher is worth the complexity.

3. **Color vs. artwork.** Users can now set a gradient *or* drop a PNG (`Set Artwork`). Two
   overlapping customization paths — should they be unified into one "Appearance" sub-menu /
   dialog? Right now they're separate context-menu items.

4. **Status taxonomy.** Cards show BETA / ALPHA / PRE-ALPHA / TESTING badges (per-profile
   `LibraryStatus`). For *user-added* games this is meaningless — should it be hidden unless
   relevant, or repurposed (e.g. "Ready / Needs path / No mods")?

5. **First-run & empty states.** Welcome flow (A1/A2/A3) now routes built-in users straight to
   the library and can set a default game. Worth walking the cold-start path end-to-end for
   friction: factory reset → library → set folder → download → install → deploy.

6. **Mod Manager density.** It has a collapsible left sidebar, a deploy overlay, a right-hand
   Downloads drawer, a set-folder banner, search, and the mod list. That's a lot of chrome
   competing for one workspace. Is the drawer/sidebar/banner interplay coherent at small window
   sizes?

7. **Discoverability of right-click.** Set Group, Set Color, Set Artwork, Export, Archive all
   live in context menus / the ⋯ overflow. Power-user-friendly, but a new user may never find
   them. Is the ⋯ affordance prominent enough?

## 5. How to run it

- `/run` (or `/run --fresh` for a clean first-launch state) launches the app.
- `/test` runs the 60-test suite. `/fmt` checks style.
- Build note: a running TMM.exe locks `bin\TMM.exe`; close the app before a full `dotnet build`,
  or the compile succeeds but the final copy-to-bin step errors (not a real failure).

## 6. Guardrails (don't break these)

- **Custom-game parity:** any new library/card feature must also work for wizard-added games.
- **Deploy plans freeze at install; rollback → first-touch baseline.** Don't add per-deploy
  rule re-evaluation.
- **No hand-edited JSON for users.** `.tmmgame` is a bundling shortcut only.
- Localize new strings in both `Assets/Localization/en-US.json` and `es-MX.json`.
- Nullable enabled; specific catches (not bare `Exception`); minimal WPF code-behind.

---

*Drafted by Opus 4.8 as a starting point. Edit freely — this is scaffolding, not scripture.*
