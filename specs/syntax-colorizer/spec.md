# Feature Specification: Syntax Colorizer

**Status**: Done — shipped in PR #94 (merged into `develop` 2026-05-11)
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: syntax-highlighting, rendering-pipeline ✅, drawing-overhaul ✅
**Blocked by**: —

## Overview

Implement a `HighlightingColorizer : IVisualLineTransformer` that consumes a `DocumentHighlighter` (from syntax-highlighting) and sets `Attribute` values on `TextRunElement` ranges in the visual-line pipeline. This replaces the `[Obsolete]` Markdown `ISyntaxHighlighter` stopgap with real syntax highlighting. The ted demo's theme dropdown switches to `HighlightingManager`-backed definitions. Issues #28 and #32 close with this work.

## User Scenarios

### Scenario 1 — C# syntax highlighting

**Given** a `.cs` file loaded in ted with the C# xshd definition, **When** the file renders, **Then** keywords, strings, comments, and types are colored according to the active highlighting definition.

### Scenario 2 — Theme swap re-runs highlighter

**Given** an active highlighting definition, **When** the user switches themes in ted's dropdown, **Then** the highlighter re-runs and all visible lines update with the new color scheme.

### Scenario 3 — Editing updates highlighting

**Given** a highlighted line, **When** the user types inside a string literal (e.g. adds characters), **Then** the highlighting updates incrementally — the string range adjusts and surrounding tokens remain correctly colored.

## Requirements

- **FR-001**: Implement `HighlightingColorizer : IVisualLineTransformer` in `Rendering/`.
- **FR-002**: Colorizer consumes `DocumentHighlighter` and sets `Attribute` on `TextRunElement` ranges.
- **FR-003**: Remove `[Obsolete]` `SyntaxHighlighter` / `SyntaxLanguage` / Markdown stopgap from `Editor`.
- **FR-004**: ted's theme dropdown switches to `HighlightingManager`-backed definitions.
- **FR-005**: Issues #28 and #32 close.

## Files in Scope

- `src/Terminal.Gui.Editor/Rendering/HighlightingColorizer.cs` (new)
- `src/Terminal.Gui.Editor/Editor.cs` (remove Markdown stopgap)
- `examples/ted/TedApp.cs` (switch theme dropdown to `HighlightingManager`)

## Definition of Done

- [ ] Load C# xshd, color a sample, expected `Attribute`s on ranges
- [ ] Theme swap re-runs highlighter and visible lines update
- [ ] ted's theme dropdown produces real syntax highlighting on `.cs` files
- [ ] `[Obsolete]` `SyntaxHighlighter` / `SyntaxLanguage` removed
- [ ] Issues #28 and #32 closeable

## Out of Scope

- TextMate grammar support — that is textmate-grammars
- xshd authoring or bundling beyond what syntax-highlighting provides
- Search hit highlighting — that is find-and-replace

## Notes

- Blocked on syntax-highlighting (highlighting lift — provides `DocumentHighlighter`) and drawing-overhaul (drawing migration — `OnDrawingContent` must use the pipeline for transformers to take effect).
- rendering-pipeline is done, so the `IVisualLineTransformer` interface already exists.
