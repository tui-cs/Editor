# Feature Specification: TextMate Grammars

**Status**: Post-MLP
**Created**: 2026-05-10
**Depends on**: syntax-colorizer
**Blocked by**: syntax-colorizer

## Overview

Add TextMate grammar and `tmTheme` support, mapped to `Terminal.Gui.Color`. This plugs into the same `HighlightingColorizer` seam established by syntax-colorizer, providing an alternative grammar format alongside xshd. TextMate grammars offer broader language coverage (VS Code's grammar ecosystem) and more granular scope-based theming.

This is a **post-MLP** item — it ships in the release after alpha.

## User Scenarios

### Scenario 1 — Load TextMate grammar and tokenize

**Given** a `csharp.tmLanguage.json` grammar file, **When** `DocumentHighlighter` (or a TextMate-specific highlighter) tokenizes `public class Foo { }`, **Then** expected TextMate scopes are assigned to each token (`keyword.other.cs`, `entity.name.type.class.cs`, etc.).

### Scenario 2 — Apply tmTheme colors

**Given** a loaded `.tmTheme` file mapping scopes to colors, **When** the highlighter runs, **Then** token colors match the theme's scope-to-color mappings, converted to `Terminal.Gui.Color`.

### Scenario 3 — ted loads TextMate grammar

**Given** a `.py` file loaded in ted with a Python TextMate grammar, **When** the file renders, **Then** Python syntax is highlighted using the TextMate grammar and active tmTheme.

## Requirements

- **FR-001**: Port or adapt TextMate grammar loading (`tmLanguage.json` / `tmLanguage.plist`).
- **FR-002**: Port or adapt `tmTheme` loading and scope-to-color mapping.
- **FR-003**: Map TextMate colors to `Terminal.Gui.Color`.
- **FR-004**: Plug into the `HighlightingColorizer` seam (same `IVisualLineTransformer` interface as xshd).
- **FR-005**: Append modification rows to `third_party/AvaloniaEdit/UPSTREAM.md`.

## Files in Scope

- `src/Terminal.Gui.Text/Highlighting/TextMate/*.cs` (new)
- Tests in `tests/Terminal.Gui.Text.Tests/Highlighting/TextMate/`
- `third_party/AvaloniaEdit/UPSTREAM.md` (append rows)

## Definition of Done

- [ ] Load `csharp.tmLanguage.json`, tokenize a sample, expected scopes → expected colors
- [ ] tmTheme loading and scope-to-color mapping works
- [ ] Colors correctly map to `Terminal.Gui.Color`
- [ ] Plugs into `HighlightingColorizer` seam
- [ ] ted demo can load a `.tmTheme` and use a TextMate grammar
- [ ] `UPSTREAM.md` updated

## Out of Scope

- TextMate-based folding (future work)
- TextMate-based indentation rules
- Scope-based search filtering

## Notes

- This is explicitly post-MLP — does not ship in the alpha release.
- Blocked on syntax-colorizer (highlighting colorizer) which provides the `IVisualLineTransformer` seam.
- May use `AvaloniaEdit.TextMate` as a starting point or a standalone TextMate library — approach TBD.
