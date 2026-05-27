---
description: Clean build artifacts and cache
---

# /clean — Clean Build Artifacts

Remove build output, cache, and temporary files.

## What it does

1. **Remove bin/** — All compiled binaries
2. **Remove obj/** — Intermediate build files
3. **Remove .vs/** — Visual Studio cache
4. **Reset to clean state** — Prepare for fresh build

## Invocation

```
/clean                  # Remove bin/, obj/, .vs/
/clean --soft           # Remove only bin/, keep obj/ (faster rebuild)
/clean --hard           # Also remove .vs/, node_modules, etc. (full reset)
/clean --settings       # Also remove %APPDATA%/TMM/ (nuclear option)
```

## Typical workflows

**Before a fresh build:**
```bash
/clean --soft
/run                    # Fresh build
```

**Before testing first-launch flow:**
```bash
/clean
/run --fresh            # Clean build + fresh settings
```

**Total reset (debugging build issues):**
```bash
/clean --hard
/run                    # Rebuilds everything from scratch
```

## Size impact

- `bin/` + `obj/` typically: ~500MB–1GB
- `.vs/` (VS cache): ~200MB

Cleaning saves space but next build will be slower (full rebuild instead of incremental).
