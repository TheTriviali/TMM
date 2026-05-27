---
description: Run unit tests and verify code correctness
---

# /test — Run Tests

Executes the TMM.Tests project and reports results.

## What it does

1. Builds and runs all tests in `TMM.Tests/`
2. Reports pass/fail count
3. Shows detailed failure messages if any tests fail

## Invocation

```
/test                   # Run all tests
/test --verbose         # Show detailed output
/test --failed          # Run only failed tests from last run
/test --coverage        # Generate code coverage report (if supported)
```

## Current test coverage

- **DeploymentPlanner** — Load order resolution, routing rules
- **RuleEngine** — Condition matching, filter logic
- **GameRegistry** — Custom game persistence
- **GameProfile** — Built-in game definitions

## Typical workflow

```bash
/test                   # Verify nothing is broken
# ... make changes ...
/test                   # Confirm changes don't break tests
```

## Expected output

```
Test Run Successful.
Total tests: 42
Passed: 42
Failed: 0
Skipped: 0
```
