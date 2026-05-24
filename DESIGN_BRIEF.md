# TMM Unified Shell — Design Brief

## Overview
Complete UI redesign consolidating 5 separate windows (GameLauncher, MainDashboard, GTA4Dashboard, CustomGameDashboard, Settings) into a single **UnifiedShellWindow** with icon-strip navigation and tabbed content areas.

---

## 1. Window Architecture

### UnifiedShellWindow Layout
```
┌──────────────────────────────────────────────────────┐
│  TitleBar (drag, close, min, max)                    │
├─────┬────────────────────────────────────────────────┤
│     │                                                │
│ N   │  Content Area                                  │
│ a   │  (LibraryPage / ModManagerPage / Settings      │
│ v   │   / Downloads / Backups)                       │
│     │                                                │
│     │                                                │
└─────┴────────────────────────────────────────────────┘
  50px
```

**Window size:** 1100×720px (min: 900×600)

### Nav Strip (Left sidebar, 50px wide)
**Icons (top to bottom):** Segoe MDL2 Assets font, white, 22px
- `&#xE80F;` Home (LibraryPage)
- `&#xE896;` Downloads
- `&#xE777;` Backups
- `&#xE713;` Settings
- *(space)*
- `&#xE7E8;` Quit (bottom)

**Active indicator:** Accent brush highlight behind icon

---

## 2. Component Specs

### GameCard (200×130px)
**Purpose:** Single game library card with gradient art banner, title, subtitle, mod count, status chip.

**Hierarchy:**
- **Art banner (gradient background, full card)**
  - Subtle noise overlay (texture detail, 4% opacity)
  - Large title text (uppercase, 22px Black, white #CCFFFFFF)
  - Status chip (top-right, if applicable)
  - Bottom info strip (semi-transparent black #AA000000)
    - Left: Subtitle (10px, #CCFFFFFF)
    - Right: Mod count (9px, #88FFFFFF)
  - Ready indicator dot (7×7px, bottom-left)

**Border:** 1.5px light border (#22FFFFFF), 10px corner radius, drop shadow (12px blur, 3px depth, 35% opacity)

**Gradients:** GTA III Series (#1B3A1B → #0C1E0C), GTA IV Series (#0C1A2E → #060F1C), Skyrim AE (#1E0A3C → #10051E), etc. See full palette below.

**Interactions:**
- Hover: White overlay 12% opacity + 1.03x scale (120ms ease)
- Click: Navigate to ModManagerPage
- Right-click: Context menu for custom artwork

**Status Chip Colors** (when applicable):
- Beta: RGB(180, 140, 20) yellow, 85% opacity, 9px bold label
- Alpha: RGB(200, 100, 20) orange, 85% opacity
- Testing: RGB(80, 80, 180) blue, 85% opacity
- PreAlpha: RGB(200, 55, 30) red-orange, 85% opacity
- Release: Hidden (chip not shown)

**Ready Dot:**
- Ready: `UiColors.ReadyGreen`
- Not ready: RGB(160, 60, 60) muted red

**Placeholder cards:** 72% opacity (visual demotion)

**Custom Artwork:** If `%APPDATA%\TMM\LibraryArt\{gameKey}.png` exists, replaces gradient with image (UniformToFill stretch). Artwork spec: PNG, ideally 460×215px, max 2MB.

---

### LibraryPage
**Header:**
- "My Library" title (22px Bold)
- Game count subtitle (11px SubText)
- Search box (right-aligned)
  - Search icon (12px, Segoe MDL2 Assets)
  - Placeholder: "Search games..."
  - Background: ControlBgBrush

**Card grid:**
- WrapPanel layout, cards at 200×130 with 6px margins
- Scroll on vertical overflow
- Cards filter by DisplayName / Subtitle / Category on search input

---

### SettingsPage
**Port of SettingsWindow as UserControl:**
- Scrollable StackPanel layout
- Sections: Steam Controls, Diagnostics, Danger Zone
- Each section bordered (SubTextBrush top border, 1px)
- Buttons: 26px height, dark background, no border

**Steam Controls:** ComboBox (150px width) + 3 buttons (Verify / Install / Uninstall)
**Diagnostics:** 3 buttons (MD5 Check / Error Log / Wipe Cache)
**Danger Zone:** Version label + Factory Reset button (red background #442222, red text #FF5555)

---

### DownloadsPage & BackupsPage
**Stub pages (empty state):**
- Large centered icon (48px, Segoe MDL2 Assets)
- "No active downloads" / "No backups yet" heading
- Subtle explanatory text
- Opacity 40% (placeholder appearance)

---

### ModManagerPage (D1 — Sonnet phase)
**Three layout modes:**

1. **GTA III Series** (III, VC, SA) — 3-column dashboard
2. **GTA IV Series** (IV, TLaD, TBoGT) — 3-column dashboard
3. **Custom/Single game** — 1-column dashboard
4. **Placeholder entries** — "Coming soon" overlay

Replicates content from existing dashboards (MainDashboard, GTA4Dashboard, CustomGameDashboard) without window chrome.

**Toolbar:** Deploy, Rollback, Play buttons at top
**Sidebar:** Game paths, essentials inline
**Back button:** Top-left arrow returns to LibraryPage

---

## 3. Color Palette

### System Colors (Dynamic Resources)
- **BgBrush:** Dark background
- **AccentBrush:** Primary accent (navigation, highlights)
- **TextBrush:** Primary text (white or light)
- **SubTextBrush:** Secondary text (muted/dim)
- **PanelBrush:** Card/panel backgrounds
- **ControlBgBrush:** Input field backgrounds

### Gradient Pairs (Library Cards)
| Game                 | Start      | End        | Status  |
|----------------------|------------|------------|---------|
| GTA III Series       | `#1B3A1B`  | `#0C1E0C`  | Beta    |
| GTA IV Series        | `#0C1A2E`  | `#060F1C`  | Alpha   |
| Skyrim AE            | `#1E0A3C`  | `#10051E`  | PreAlpha |
| Fallout NV           | `#3A2008`  | `#1E1004`  | PreAlpha |
| Cyberpunk 2077       | `#0A1A2E`  | `#050D1A`  | PreAlpha |
| Red Dead 2           | `#2E0A0A`  | `#1A0505`  | PreAlpha |
| Witcher 3            | `#0A2E14`  | `#051A0A`  | PreAlpha |

### Fixed Colors
- **Card border:** `#22FFFFFF` (light white overlay)
- **Info strip background:** `#AA000000` (semi-transparent black)
- **Subtitle text:** `#CCFFFFFF` (light white)
- **Mod count text:** `#88FFFFFF` (dimmed white)
- **Status labels:** White text on colored chip backgrounds
- **Ready dot (ready):** `UiColors.ReadyGreen`
- **Ready dot (not ready):** RGB(160, 60, 60)
- **Noise overlay:** TextBrush, 4% opacity
- **Hover overlay:** White, 12% opacity

---

## 4. Typography

| Element             | Size | Weight  | Color              |
|---------------------|------|---------|------------------|
| Window title        | 22px | Bold    | TextBrush        |
| Page subtitle       | 11px | Regular | SubTextBrush     |
| Card title (art)    | 22px | Black   | #CCFFFFFF        |
| Card subtitle       | 10px | Regular | #CCFFFFFF        |
| Status label        | 9px  | Bold    | White            |
| Mod count           | 9px  | Regular | #88FFFFFF       |
| Search placeholder  | 12px | Regular | #66FFFFFF       |
| Section header      | 13px | SemiBold| #FF88EE         |
| Button text         | ~12px| Regular | TextBrush        |

---

## 5. Spacing & Sizing

### GameCard
- **Overall:** 200×130px
- **Border radius:** 10px, drop shadow 12px blur
- **Title margin:** 14px L/R, 24px B
- **Status chip:** 6px top, 8px right (margin)
- **Info strip:** 10px padding H, 6px V
- **Ready dot:** 8px margin from L/B edges

### LibraryPage
- **Header margin:** 24px outer, 12px bottom
- **Search box min-width:** 200px
- **Card panel margin:** 20px outer, 6px between cards

### Buttons
- **Height:** 26px (std), 28px (primary)
- **Padding:** ~16px H (Factory Reset), 6-8px ComboBox

---

## 6. Interaction Patterns

### Navigation
- **Home → LibraryPage:** Slide in from left
- **Card click → ModManagerPage:** Slide in from right
- **Back button → LibraryPage:** Slide out to right
- **Active nav icon:** Highlighted with AccentBrush

### Card Interactions
- **Hover:** White overlay fade in (120ms) + scale 1.03x (120ms)
- **Click:** Invoke CardClicked event
- **Right-click:** Context menu (Set Artwork / Remove Artwork)

### Search
- **Live filter:** On TextChanged, re-render card grid
- **Case-insensitive:** Match on DisplayName, Subtitle, Category

### Artwork Upload
- **Right-click → "Set Custom Artwork..."** → OpenFileDialog (PNG only)
- **Validation:** Max 2MB, min 200×100px, PNG format enforced
- **Success:** Card refreshes, artwork immediately visible
- **Failure:** Toast notification with validation error

---

## 7. Key Design Questions for Review

1. **GameCard proportions:** Is 200×130px ideal for scanning large libraries? Should cards be wider (250px) or more square-ish?

2. **Gradient visibility:** Do the gradient pairs have enough contrast? Should we add vignette/fade edges for text readability?

3. **Status chips:** Are the color choices (yellow/orange/blue/red-orange) distinctive enough? Should PreAlpha be more muted?

4. **Nav strip:** Is 50px wide enough for 22px icons? Should icons be 20px instead?

5. **Info strip opacity:** Is `#AA000000` (semi-transparent black) readable enough on all gradients? Test on light gradients like Cyberpunk.

6. **Hover animation:** Is the white 12% overlay + 1.03x scale too subtle? Should scale be 1.05x or overlay 15%?

7. **Ready dot color:** Is the "not ready" red RGB(160,60,60) visible enough on dark gradients?

8. **Search box placement:** Should it be top-right or centered? Current spec is top-right.

9. **Placeholder dimming:** Is 72% opacity the right level to signal "not yet configured"?

10. **Spacing consistency:** Are 20px outer margins + 6px card gaps balanced across all pages?

---

## 8. Accessibility Considerations

- [ ] Status chip colors distinguishable for colorblind users?
- [ ] White text on gradients meets WCAG AA contrast ratio?
- [ ] Nav icons have tooltips/labels for clarity?
- [ ] Keyboard navigation support (Tab through cards, Enter to select)?
- [ ] Hover/focus states clearly visible?

---

## 9. Responsive Breakpoints (Future)

- **900px min width:** Cards may stack to 2 columns
- **1100×720px standard:** 3–4 cards per row
- **Larger screens:** Could show 5+ cards per row

---

## Next Steps

**Design should validate:**
1. Overall visual coherence (colors, spacing, typography)
2. Contrast & readability across all gradient pairs
3. Icon clarity at nav strip size
4. Animation timing & smoothness
5. Placeholder/disabled states visibility
6. Any suggested refinements to proportions, colors, or spacing

**Implementation will follow exactly after Design sign-off.**
