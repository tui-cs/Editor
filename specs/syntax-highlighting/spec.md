# Feature Specification: Syntax Highlighting Engine

**Status**: Subsumed — shipped as part of [syntax-colorizer](../syntax-colorizer/spec.md) (PR #94). Retained for historical reference only. Per constitution R9 ("lifts must ship with their ted consumer") lift-only specs are no longer scheduled separately.
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: None
**Blocked by**: —

## Overview

Bring the AvaloniaEdit highlighting engine into `src/Terminal.Gui.Editor/Highlighting/`: `HighlightingManager`, `IHighlighter`, `HighlightingColor`, the xshd loader, and `DocumentHighlighter`. This gives the document model a complete syntax-highlighting tokenizer that is UI-framework-independent, producing color/style annotations that the Editor's rendering pipeline (syntax-colorizer) can consume.

Key adaptation: AvaloniaEdit's `IBrush` / `Avalonia.Media.Color` is replaced with `Terminal.Gui.Color`. Typeface and font-size fields are dropped from `HighlightingColor` (irrelevant in a cell-grid TUI). Bold, italic, and underline are mapped to `TextStyle.Bold | TextStyle.Italic | TextStyle.Underline`. The xshd loader's `Bold`/`Italic`/`Underline` attributes map to the corresponding `TextStyle` flags.

## User Scenarios

### Scenario 1 — Load a known xshd and tokenize

**Given** a bundled C# `.xshd` definition, **When** `DocumentHighlighter` tokenizes `public class Foo { }`, **Then** `public` and `class` are marked with keyword color, `Foo` with default, and `{` `}` with punctuation color.

### Scenario 2 — Round-trip color mapping is lossless

**Given** an xshd color defined as `#FF0000` (red), **When** it is mapped to `Terminal.Gui.Color` and read back, **Then** the RGB values are preserved without loss.

### Scenario 3 — Style flags map correctly

**Given** an xshd rule with `Bold="true" Italic="true"`, **When** the highlighting color is loaded, **Then** `HighlightingColor.Style` includes `TextStyle.Bold | TextStyle.Italic`.

### Scenario 4 — Manager registers and retrieves definitions

**Given** `HighlightingManager` with C# and XML definitions loaded, **When** requesting a definition by name or file extension, **Then** the correct `IHighlighter` is returned.

## Requirements

- **FR-001**: Lift `HighlightingManager`, `IHighlighter`, `HighlightingColor`, xshd loader, and `DocumentHighlighter` from AvaloniaEdit.
- **FR-002**: Transform namespace to `Terminal.Gui.Text.Highlighting` (and subnamespaces as needed).
- **FR-003**: Strip all `using Avalonia.*` directives.
- **FR-004**: Replace `IBrush` / `Avalonia.Media.Color` with `Terminal.Gui.Color`.
- **FR-005**: Drop typeface and font-size from `HighlightingColor`; keep bold/italic/underline as `TextStyle` flags.
- **FR-006**: Map xshd's `Bold` / `Italic` / `Underline` attributes to `TextStyle.Bold | TextStyle.Italic | TextStyle.Underline`.
- **FR-007**: Preserve original formatting and copyright headers per fork policy.
- **FR-008**: Add `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` header line.
- **FR-009**: Append modification rows to `third_party/AvaloniaEdit/UPSTREAM.md`.
- **FR-010**: Bundle at least one `.xshd` definition (C#) as an embedded resource for testing.

## Files in Scope

- `src/Terminal.Gui.Editor/Highlighting/**/*.cs`
- Bundled `.xshd` resource files
- `third_party/AvaloniaEdit/UPSTREAM.md` (append rows)

## Definition of Done

- [ ] All highlighting types compile and are in `Terminal.Gui.Text.Highlighting` namespace
- [ ] Tests in `tests/Terminal.Gui.Editor.Tests/Highlighting/` pass — load a known xshd (e.g. C#), tokenize a sample, expected ranges + colors; round-trip color → `Terminal.Gui.Color` is lossless
- [ ] `UPSTREAM.md` updated with per-file modification log
- [ ] No Avalonia residue (`grep -r "using Avalonia" src/Terminal.Gui.Editor/Highlighting/` returns nothing)

## Out of Scope

- The `IVisualLineTransformer` that consumes the highlighter — that is syntax-colorizer
- TextMate grammar support — that is textmate-grammars
- Theme switching UI in ted

## Notes

- Can be done in parallel with folding, search, and indentation.
- syntax-colorizer (highlighting colorizer) is blocked on this item.
- The `[Obsolete]` Markdown `ISyntaxHighlighter` stopgap in `Editor` will be removed by syntax-colorizer, not by this item.
