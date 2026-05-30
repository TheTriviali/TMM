# gonext — Open backlog as of v0.2-alpha-1

> Audited 2026-05-30 against M1/M2/M3 backport (Group I complete).
> Items superseded by the new layout are marked and explained.
> ✅ = done/superseded  🔵 = open  ⚠️ = partial  ❓ = needs clarification before work begins

---

## Layout and General UI

- ✅ **Sidebar toggle removed from mod manager** — The entire 14-button toolbar was retired in M1; the sidebar no longer exists.
- ✅ **Toolbar bloat / game title** — M1 introduced WorkspaceHeader (cover, title, readiness badge, verbs). The old problem of the game name appearing three times is gone. *Note: the original request said "center in OS titlebar with theme color splash" — WorkspaceHeader achieves the same intent in a better place. The OS titlebar still says "TMM — [section]" which is intentional.*
- ✅ **Sidebar relocated / downloads embed** — SUPERSEDED. The sidebar was eliminated entirely; Downloads became a workspace sub-tab in M1. The pane is fully scrollable in that context.

- 🔵 **Remove the Quit button from the rail** — The shutdown button moved from "bottom-left corner" (old description) to the bottom of the global rail (`BtnQuit_Click` in `UnifiedShellWindow.xaml:178`). Original request was to remove it; it is still there.

- 🔵 **Enable window resizing** — `UnifiedShellWindow` uses `WindowStyle="None"` with no `WndProc` resize-region override. `MinWidth="800" MinHeight="500"` are set but dragging the window edge does nothing. Needs hit-test override in `TmmWindow.cs` or `UnifiedShellWindow.xaml.cs`.

- 🔵 **Fix double-click titlebar maximize** — `TmmWindow.TitleBar_MouseDown` calls `DragMove()` only; no `MouseDoubleClick` handler exists. The `BtnMaximize_Click` toggle works via button but titlebar double-click does not.

- 🔵 **Restrict address bar width in Downloads tab** — The Downloads tab re-hosts the in-manager archive drawer. If the WebView2 browser address bar is shown (for mod browsing), it should not span the full width. Verify whether the address bar is visible in the tab context and constrain `MaxWidth` if so.

- ❓ **"My Games" / "Available Games" sections in the library** — M3 replaced the grid with a Home view that shows a "Your Games" wrap-panel (cards for configured games) and a recent-activity feed. The original request also asked for an "Available Games" section (games with no valid path yet, i.e., unconfigured presets). The current Home does not surface unconfigured built-in profiles as browseable entries. *Clarification needed: is a discoverable "Available Games" section still wanted, or does the "Add a game" button in the Library header header satisfy this?*

---

## Settings, Help, and Navigation

- ✅ **Consolidate Help and About** — Both moved to the `⋯` overflow `ContextMenu` in M1 (`WorkspaceHeader`). The global rail now has only: Library · Notifications · Troubleshooting · [separator] · Paths · Settings · Quit.

- ✅ **Bottom toolbar group ordering (Help → About → Settings)** — SUPERSEDED by the new rail structure. Help/About live in the workspace overflow; the global rail bottom has Settings + Quit only. The old ordering concept no longer applies.

- ⚠️ **Troubleshooting interface** — Renamed from the ambiguous question mark to `navBtnTroubleshooting` (icon &#xE897;, a wrench-style icon). The `TroubleshootingPage.xaml` exists. Whether the content still resembles a raw error log needs visual verification. The "rename to Troubleshooting Guide" and "integrate FAQ" asks are not confirmed done.

- 🔵 **Merge File Locations into Settings** — Explicitly deferred in M1 interpretation calls: "Paths kept in the rail" because removing it would orphan the page. The gonext item is still open — Paths is a separate rail entry, not merged into Settings.

- 🔵 **Unified help browser (in-app or GitHub)** — Help and About are separate windows; there is no unified browser. The request to build a `faq.md`-backed in-app help browser or GitHub-linked viewer has not been addressed.

- 🔵 **Settings: explicit manual save (Save / Apply / Cancel + unsaved-changes prompt)** — `SettingsPage` currently auto-saves on change. No Save/Apply/Cancel flow exists. This is a meaningful UX change to eliminate notification/log spam from accidental settings adjustments.

---

## Mod List and Interactions

- ⚠️ **Ghost checkbox artifact on hover** — M2 replaced the `GridView` column layout with a full custom `ItemTemplate` DataTemplate. The ghost checkbox was a hover-state artifact of the old template; it may be resolved by the rewrite, but needs visual verification in the running app.

- 🔵 **Double-click Order field for inline editing** — The custom DataTemplate in M2 shows a numeric order index. No double-click inline-edit handler exists. The request was to allow typing a value directly, with all other entries shifting to accommodate. Not implemented.

- ⚠️ **Multi-attribute sorting** — M2 added filter chips (All · Enabled · Conflicts · Favorites), which covers filtering. Column-header sorting across Order / Mod Name / isEnabled / Group / Size / Status is not implemented. The custom DataTemplate has no sortable column headers.

---

## Mod Installation and Data

- 🔵 **Pre-installation deployment preview (file structure confirmation)** — The user must be shown the parsed file layout and confirm it before the `DeploymentPlan` is frozen. Currently TMM parses and installs without a mandatory confirmation step even if the archive structure is ambiguous. The isolated preview-before-commit flow discussed previously has not been built.

- 🔵 **Strict duplicate mod validation** — No checksum + filename + size deduplication exists at install time. A user can install the same mod twice under different archive names.

- ✅ **Discontinue pre-calculated MD5 hashes by default** — GTA-specific MD5/vanilla/downgrade logic was stripped 2026-05-27 (see memory). Step 1 of the wizard now has an optional "Integrity Verification" section where users input their own hash if desired (`Step1_Integrity_Header`). Pre-calculated hashes are gone.

- 🔵 **Non-English character artifact in deployment notification** — The corrupted characters ("ace"-like) in the deployment toast string have not been confirmed fixed. Needs a deploy run to observe the notification string.

---

## "Add a Game" Menu

- ✅ **Remove sidebar progress tracker** — Replaced with step dots in the wizard header (`dot1`–`dot4` in `CustomGameSetupWizard.xaml`). No sidebar tracker exists.

- 🔵 **Redesign wizard to split-view (2 pages, no scrolling)** — Still a 4-step wizard with `Step1_GameDetailsPage`, `Step2_ModTypesPage`, `Step3_RoutingRulesPage`, `Step4_ReviewPage`. The vertical-length / scrolling problem and the 4-page structure are unchanged. *Note: the wizard is now accessed more frequently (from the workspace Config tab for every game), making this redesign higher priority.*

- ❓ **Mod types vs. routing rules architectural analysis** — Step 2 lets users define mod type names; Step 3 lets them define routing rules by file extension. The original question: should these be consolidated, or should routing rules derive from mod types? The Config tab summary (M1) now surfaces mod types alongside routes, making this question more visible. *Clarification needed before any code change: what is the desired relationship between mod types and routing rules in the redesigned wizard?*

---

## Localization and Typography

- 🔵 **Dynamic scaling for long strings** — No adaptive layout logic exists for translated strings that exceed English lengths. Needs a pass across controls with fixed widths.

- 🔵 **Specific localization fixes (verify in running app):**
  - Truncated string: "table differs from this profile's expe--"
  - Pluralization: "1 mods installed" (library stats strip)
  - "1 mod - 1 enabled - last deployed 8 min ago" (es-MX library view)
  - "app data" and "free space" (sidebar / stats strip — now in Home view)
  - "import" and "loadouts" (top toolbar, es-MX context-specific) — *toolbar was restructured in M1; verify these strings still exist in the new overflow/tab-bar positions*
  - All window titles
  - All color accent preset identifiers

---

## Credits

- 🔵 **Add Opus 4.8 to credits** — `AboutWindow.xaml:135` shows `Claude Opus 4.7`. Opus 4.8 needs to be appended to the AI-assisted credits tag list.

---

## Workflow / Tooling

- 🔵 **Interactive checklist to replace PLANS.md** — Requested: a collaborative HTML or lightweight tool where Claude checks items as implemented and you check items as verified, with notes for regressions. PLANS.md + CHANGELOG.md workflow is still in use. This can be built as a companion `tracker.html` (self-contained, no server) that reads a structured JSON and renders checkboxes for both roles. Worth implementing as part of the next planning session.

---

## Future Localization Roadmap

- 🔵 **Prepare for 8 additional languages** — Target: Simplified Chinese, Russian, Brazilian Portuguese, German, Japanese, Korean, Hindi, Arabic. Begin after Spanish (es-MX) is fully verified. Framework (the `LocalizationHelper` + JSON-per-locale pattern) is already extensible; this is a content + RTL-layout task for Arabic.

---

## Feasibility Survey (analysis tasks — no code yet)

These were requested as analyses to inform future planning, not implementation tasks.

- 🔵 **Virtual File System (VFS)** — Assess technical feasibility and maintenance overhead of adding a mandatory VFS layer (MO2-parity). Consider: impact on the `DeploymentPlan`-freeze architecture, performance on large mod sets, rollback complexity.

- 🔵 **Nexus Mods API integration** — Assess feasibility of native NexusMods API support (mod browsing, metadata, download queue). Consider: API key management, rate limits, per-game category mapping.

- 🔵 **FOMOD installer schema** — Assess feasibility of parsing and presenting FOMOD `ModuleConfig.xml` during the pre-installation deployment preview. Consider: schema coverage, conditional install steps, interaction with the existing `DeploymentPlan` freeze.

---

## Items requiring clarification before work begins

Two items from the original list don't fit cleanly onto the new design and need a quick answer before work is scoped:

1. **"Available Games" section** — The Home view (M3) shows configured games. The original request also wanted a section for built-in profiles that haven't been set up yet (i.e., browseable "Add GTA III", "Add Vice City" cards). Does the current "Add a game" button in the Library header satisfy this, or should the Home view surface installable presets directly?

2. **Mod types vs. routing rules** — Before the Add-a-Game wizard redesign can be planned, the architectural relationship between Step 2 (mod types) and Step 3 (routing rules) needs to be decided. Options: keep separate, merge types into routes as labels, or remove Step 2 and derive types from route file-extension groups automatically.
