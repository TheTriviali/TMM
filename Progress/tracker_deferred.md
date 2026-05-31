# Deferred — No Active Work

Analysis complete. Revisit post-v1.

- **VFS** — high complexity, deferred indefinitely. Direct-deploy + plan freeze covers the primary use case.
- **Nexus Mods API** — feasible via REST API, key management in Settings. Post-v1.
- **FOMOD installer** — feasible. Do after `install-preview` (Plan Editor) ships.
- **Single-layer deploy undo** — feasible via existing backup system; surface as toast action button expiring after 30s. Post-v1.
