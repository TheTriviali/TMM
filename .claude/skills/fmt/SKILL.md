---
description: Check code style and formatting
---

# /fmt — Code Style & Formatting

Verify code follows project conventions.

## What it does

1. **Check style** — Runs StyleCop/code analyzers
2. **Report issues** — Lists formatting/naming violations
3. **Fix (optional)** — Auto-correct fixable issues

## Invocation

```
/fmt check              # Report style violations only
/fmt fix                # Auto-fix fixable violations
/fmt check --file Path/To/File.cs
                        # Check specific file
```

## Project conventions (from CLAUDE.md)

- **Nullable reference types** — Always on (`<Nullable>enable</Nullable>`)
- **Public APIs** — XML docs (`///`), async methods suffix `Async`
- **Naming** — PascalCase types/methods, camelCase locals, `_camelCase` private fields
- **Async/await** — Never `.Result`/`.Wait()`; prefer `ConfigureAwait(false)` in libraries
- **Error handling** — Specific catches (not bare `Exception`)
- **LINQ** — Prefer over loops when clear
- **Null handling** — Use `is null`/`is not null`, return `[]` not `null` for empty collections
- **WPF** — Minimal code-behind, DataContext via XAML, stateless converters

## Example violations caught

```csharp
// ❌ Bare Exception
catch (Exception ex) { }
// ✅ Should be:
catch (InvalidOperationException ex) { }

// ❌ .Result (blocks async)
var result = Task.Run(() => DoWork()).Result;
// ✅ Should be:
var result = await Task.Run(() => DoWork());

// ❌ Wrong naming
private int myValue;  // Should be _myValue
// ✅ Correct:
private int _myValue;

// ❌ Missing docs on public API
public void Deploy() { }
// ✅ Should be:
/// <summary>Deploy mods to the game directory.</summary>
public void Deploy() { }
```

## Typical workflow

```bash
/fmt check              # See what needs fixing
# ... make manual fixes or use auto-fix ...
/fmt fix                # Auto-fix style issues
/fmt check              # Verify all fixed
```
