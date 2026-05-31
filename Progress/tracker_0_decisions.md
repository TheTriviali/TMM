# Design Decisions (frozen — don't re-open without a conversation)

1. **Library "Your games" shows configured games only.** Empty section with hint on fresh start. Hero card also restricted. Stats: "Games with folder" + "Last deployed."

2. **Mod types + routing rules merged.** Wizard Steps 2/3 combined: each row is a mod type with extensions and target folder.

3. **Help + Troubleshooting merged, global.** Single "Troubleshooting & Help" rail entry. No separate Help/About overflow.

4. **Config tab = quick path-setting + "Advanced config" link.** Not a full wizard launch.

5. **No "custom game" distinction.** `CUSTOM_` prefix and branching code paths are debt to remove. All games are first-class profiles.

6. **Nav rail structure.** Left strip: Library, Mod Manager (top). Troubleshooting & Help, Settings (bottom). Always-visible fixed width. Title bar: app icon + page name, no "TMM —" prefix.

7. **Unified mod list filter bar.** Search + tab row (All / Enabled / Conflicts / Favorites) in one horizontal control.

8. **Install Mod button inline.** Action bar alongside Deploy and Play.

9. **Library as polished .tmmgame file browser.** Each card shows backing `.tmmgame` filename.

10. **Conflicts tab → "Conflict Manager".** Main Mods tab shows conflict existence at a glance; tab is for resolving them.

11. **Routing rules never regenerate frozen plans.** Rules are a starting point for new installs only.

12. **Plan Editor always mandatory.** Shown on every install. User had the chance to review — not TMM's fault if a mod lands in the wrong place.

13. **Smart partial redeploy.** Only re-deploy mods whose plan or state changed.

14. **"Needs redeploy" state.** Surfaced on: mod row badge, Deploy button, library game card.

15. **Direct Install Override — WON'T IMPLEMENT.** Game root routing destination covers the use case.
