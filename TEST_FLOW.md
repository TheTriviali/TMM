# TMM Testing Flow — Comprehensive Checklist

---

## Quick Checklist

- [ ] **Setup:** First launch, AppData structure, steam detection
- [ ] **Navigation:** Game hub tabs, window switching, persistence
- [ ] **Deploy/Rollback:** Both GTA III & IV series, custom games, file conflicts
- [ ] **Themes:** Application, persistence, visual consistency
- [ ] **Settings:** Paths, config, serialization
- [ ] **Error handling:** Invalid paths, disk space, permissions, concurrent ops
- [ ] **Edge cases:** Special chars, long paths, large collections, symlinks
- [ ] **Integration:** Deploy→Rollback→Deploy cycles, state persistence
- [ ] **Performance:** <5s for 50 mods, <10s for 100+, <300MB memory
- [ ] **Accessibility:** Keyboard nav, screen readers, usability scoring

---

## Detailed Test Phases

### Phase 1: Setup & AppData
- [ ] First launch (fresh install) triggers `InitialSetupWindow`
- [ ] Steam path auto-detection works
- [ ] `settings.json`, `ModsRaw_*`, `Backups/`, `CustomGames/` created
- [ ] Subsequent launches skip setup, restore window state & theme

### Phase 2: Game Hub Navigation
- [ ] GTA III (III/VC/SA) tab: all games present, click opens correct dashboard
- [ ] GTA IV (IV/TLaD/TBoGT) tab: all games present, 3-column layout
- [ ] Custom Games tab: shows all user-added games, Add/Delete buttons work
- [ ] Multiple windows can be open, focus restoration correct

### Phase 3: Deploy & Rollback (GTA III/VC/SA)
- [ ] **Pre-Deploy:** Game path validated, button disabled if no mods enabled
- [ ] **Deploy:** Completes <5s, backup created, toast notification, button states update
- [ ] **Rollback:** Completes <5s, game dir restored, original files intact
- [ ] **Conflicts:** Pre-existing files backed up, overwrite confirmed, rollback recovers

### Phase 4: Deploy & Rollback (GTA IV Series)
- [ ] Same behavior as Phase 3 but with 3-column layout
- [ ] Each game key (IV/TLaD/TBoGT) uses separate backup directories
- [ ] Load order persists across sessions

### Phase 5: Custom Games
- [ ] **Create:** Name + path picker, path validation, saved to `CustomGames/{key}.json`
- [ ] **Dashboard:** 2-column layout, mods in `ModsRaw_CUSTOM_{key}/`, deploy/rollback work
- [ ] **Edit:** Path changes apply immediately, invalid paths show error

### Phase 6: Themes & Visual Consistency
- [ ] Light/dark themes apply to all windows
- [ ] Mica backdrop renders on Windows 11
- [ ] WCAG AA text contrast verified
- [ ] Font hierarchy, spacing, color palette consistent
- [ ] Theme persists across sessions

### Phase 7: Settings & Configuration
- [ ] Theme selector, path editors, logging level
- [ ] Settings saved to `%APPDATA%\TMM\settings.json` (valid JSON)
- [ ] Corrupt settings handled gracefully
- [ ] QuickScan finds Steam paths, manual override works

### Phase 8: Error Handling
- [ ] **Invalid path:** Clear error, browse button available
- [ ] **Disk space:** Warning if <500MB free, user can proceed or cancel
- [ ] **File permissions:** Read error → skip with warning, write error → abort with message
- [ ] **Corrupted backup:** Manifest unreadable → disable rollback, warn user
- [ ] **Concurrent ops:** Deploy/rollback buttons disabled during operation, UI responsive

### Phase 9: Edge Cases
- [ ] **Special chars:** Unicode, spaces, dashes, brackets all handled
- [ ] **Symlinks:** Consistent behavior, no silent failures
- [ ] **Long paths:** >260 chars handled or clear error message
- [ ] **Empty dirs:** Deploy succeeds, rollback works
- [ ] **Large collections:** 100+ mods deploy <10s, UI responsive

### Phase 10: Integration Testing
- [ ] **Deploy→Rollback→Deploy cycle:** 5 repetitions, all successful, no stale files
- [ ] **Load order changes:** Reorder, deploy, verify, rollback to original
- [ ] **Theme persistence:** Change theme, deploy, close, reopen → theme + mods persist
- [ ] **Custom game persistence:** Create game, add mods, deploy, close, reopen → all intact

### Phase 11: Performance
- [ ] **Deploy times:** 5 mods <1s, 25 mods <3s, 50 mods <5s, 100+ mods <10s
- [ ] **Memory:** Startup <100MB, +100 mods <300MB, no leaks on repeated cycles
- [ ] **CPU:** Idle <1%, spikes during deploy, returns to idle after

### Phase 12: Accessibility
- [ ] **Keyboard:** Tab order logical, Escape closes dialogs, shortcuts work
- [ ] **Screen readers:** Button labels, list items, errors all announced
- [ ] **Usability:** Learnability (first deploy <2min), efficiency (deploy/rollback <10s), error recovery clear, no dead ends

### Phase 13: Defect Template

```
Title: [Issue]
Severity: Critical / High / Medium / Low
Steps: 1. ... 2. ... 3. Expected vs. Actual
Environment: OS, TMM version, game
Root Cause Hypothesis: UI state / service / file I/O / other
```

---

## Performance Targets

| Metric | Target | Status |
|--------|--------|--------|
| Deploy (50 mods) | <5s | ✓/✕ |
| Rollback (50 mods) | <5s | ✓/✕ |
| Startup memory | <100MB | ✓/✕ |
| Full memory (100 mods) | <300MB | ✓/✕ |
| UI responsiveness | No freezing | ✓/✕ |

---

## Automation Candidates

1. **Unit tests:** `BackendCore.DeployModsAsync`, `GameRegistry.LoadGames`, `RuleEngine`
2. **Integration tests:** Deploy/rollback cycles, settings serialization
3. **UI tests:** Window lifecycle, binding validation
4. **E2E tests:** Full user workflows (deploy → verify → rollback)
5. **Property tests:** Special chars, long paths, load order permutations

Use xUnit + Moq. Mock file I/O where appropriate.
