---
description: Manage translation files and language keys
---

# /locale — Translation Management

Create, validate, and maintain language files.

## What it does

1. **Create new language** — Generate a new translation file from en-US template
2. **Validate keys** — Check that all language files have matching keys
3. **Find untranslated** — List keys that are missing or show fallback `[key]` in UI
4. **Generate report** — Show translation completion % per language

## Invocation

```
/locale list            # Show all available languages and status
/locale create es-ES    # Create new Spanish translation (from en-US template)
/locale validate        # Check all language files for missing/extra keys
/locale report          # Show translation completion percentage per language
/locale find-missing    # List keys that will show as [key] fallback in UI
```

## File locations

Language files live in: `Assets/Localization/{language-code}.json`

Example structure (en-US.json):
```json
{
  "Window_MainTitle": "TMM — Mod Manager",
  "Button_Deploy": "Deploy",
  "Label_Language": "Language",
  ...
}
```

## Workflow for adding a new language

1. **Create the file**:
   ```
   /locale create pt-BR
   ```
   → Creates `Assets/Localization/pt-BR.json` with all en-US keys (English values)

2. **Translate** — Edit the JSON file, replace English values with Portuguese

3. **Validate** — Check for typos or missing keys:
   ```
   /locale validate
   ```

4. **Test** — Run app and select language from dropdown:
   ```
   /run --fresh
   ```
   Then pick "pt-BR" from language selector

5. **Report** — See completion status:
   ```
   /locale report
   ```

## Key naming convention

Follow pattern: `{Component}_{Purpose}`

Examples:
- `Window_MainTitle` — main window title
- `Button_Deploy` — deploy button label
- `Error_InvalidPath` — error message
- `Tooltip_DeployInfo` — tooltip text
- `Label_Appearance` — settings label

## Typical workflow

```bash
# Add a new language for a community member
/locale create ja-JP
# They edit the JSON file...
/locale validate        # Verify it's correct
/run --fresh            # Test in the app
```
