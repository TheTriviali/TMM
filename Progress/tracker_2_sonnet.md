# Section 2 — Settings Dual-Pane

**Model:** Sonnet  
**Start:** fresh chat  
**Stop:** all categories render without full-page scroll, build clean, Noah verifies

- `[ ]` open · `[C]` Claude done, needs Noah check · `[✓]` complete

---

Redesign `Views/Subpages/SettingsPage.xaml(.cs)` from a scrollable single column to a two-column layout.

**Left pane:** category list  
**Right pane:** selected category's controls (no full-page scroll)  
**Unsaved changes banner:** top of right pane only

Category mapping:
- **General** — Startup preference, Notifications
- **Appearance** — Accent color preset + custom hex inputs
- **File Paths** — File Locations panel
- **Advanced** — Diagnostics (error log, wipe cache), Danger Zone (factory reset), version label

---

- [ ] Implement dual-pane layout in `Views/Subpages/SettingsPage.xaml(.cs)`
