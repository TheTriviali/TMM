# TMM Tracker — Context for Claude

This file gives a Claude session enough context to work from the tracker without reading
stale handoff docs. Open `tracker.html` in a browser to use the live checklist.

---

## What the tracker replaces

`tracker.html` + `tracker.json` replace PLANS.md and gonext.md as the single source of
truth for open work. State persists in `localStorage`; use the "Save JSON" button to sync
`tracker.json` back to disk after checking items off.

**Two roles:**
- **C (Claude)** — checked when the code change is done and the build is clean.
- **N (Noah)** — checked after visual/functional verification in the running app.

An item is only complete when both are checked.

---

## Design decisions in effect

These are frozen — don't re-open them without a conversation.

1. **"Available Games" section** — not needed. "Add a game" button in Library header is sufficient.

2. **Mod types + routing rules merged** — wizard Steps 2 and 3 are redundant. The redesigned
   wizard combines them: each row is a mod type with its extensions and target folder.
   Routing rules carry the type label as metadata.

3. **Help + Troubleshooting merged, global** — single "Help & Troubleshooting" rail entry.
   Content: FAQ, log/error viewer, About. Replaces separate Troubleshooting entry and
   Help/About overflow items.

4. **Per-game Settings = game wizard (edit mode), same for all games** — the Config tab in
   each workspace opens the wizard in edit mode. No distinction between built-in and
   user-added games.

5. **No "custom game" distinction — all games are first-class** — `CUSTOM_` key prefix,
   `CustomGameProfile` type, and code paths that branch on "is this a custom game" are
   architectural debt to remove. Built-in games are just pre-seeded profiles from `.tmmgame`
   assets. User-added games behave identically in every respect.

---

## Architecture guardrails (don't break these)

- **Deployment plans freeze at install.** Plans are evaluated once on install and stored in
  `_tmm/deployplan.json`. Subsequent deploys execute the saved plan verbatim — no
  re-evaluation. Re-import is the only way to regenerate a plan.

- **First-touch baseline.** Rollback restores to the state TMM first observed. For vanilla
  installs that's vanilla; for imported pre-modded games it's the import-time state.

- **No hand-edited JSON for users.** `.tmmgame` is a bundling shortcut for shared profiles
  only. All configuration must be reachable through the wizard UI.

- **Custom-game parity (now: all-game parity).** Any feature that works for built-in games
  must be fully configurable via the game wizard. A feature is not complete until it appears
  in `Step1_GameDetailsPage` (input) and `Step4_ReviewPage` (review) — or their successors
  after the wizard redesign.

- **Nullable enabled.** Specific catches (not bare `Exception`). Minimal WPF code-behind.
  New strings localized in both `en-US.json` and `es-MX.json`.

---

## Key files for reference

| What | Where |
|---|---|
| Deploy logic | `Services/BackendCore.cs`, `Services/BackendCore.Deploy.cs` |
| Game registry | `Services/GameRegistry.cs` |
| Game config model | `Models/CustomGameProfile.cs` (rename target: `GameConfig`) |
| Wizard (add/edit) | `Views/CustomGameSetupWizard.xaml(.cs)` (rename target: `GameSetupWizard`) |
| Mod list | `Views/Subpages/ModManagerPage.xaml` + partials |
| Shell / nav | `Views/UnifiedShellWindow.xaml(.cs)` |
| Theme | `ThemeEngine.cs` |
| Localization | `Assets/Localization/en-US.json`, `es-MX.json` |
| Settings model | `Models/AppSettings.cs` |

---

## Section notes

### Game Management (Unified)
The `games-*` items are interdependent. Suggested order:
1. `games-unified-model` + `games-model-rename` + `games-registry-clean` — collapse the
   architectural split first (these touch the same files).
2. `games-key-slug` + `games-folder-naming` — key generation + migration (depends on #1).
3. `games-wizard-unified` + `games-edit-mode` — wizard rename + edit flow (depends on #1).
4. `games-wizard-redesign` — merge Steps 2+3 (depends on #3; largest UI change).

### QA / Verification
`qa-first-run-walkthrough` should be done before any public release. It catches encoding
issues (deploy toast charset), first-run UX gaps, and integration bugs the unit tests miss.

### Feasibility Surveys
These are analysis tasks only — no code. Each produces a short written assessment for a
design conversation, not an implementation.
