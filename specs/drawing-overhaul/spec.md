# Feature Specification: Drawing Overhaul

**Status**: Complete
**Created**: 2026-05-10
**Depends on**: rendering-pipeline ✅
**Blocked by**: None

## Overview

Replace the char-by-char draw loop in `Editor.Drawing.cs` with a `CellVisualLine`-walking loop that consumes the visual-line pipeline established in rendering-pipeline. Remove inline tab-expansion and line-number shortcuts from the draw path. Render line numbers through `Gutter : View`, hosted as a `Padding` SubView, so the gutter participates in the Terminal.Gui view hierarchy instead of painting as a bespoke side-channel.

After this migration, `OnDrawingContent` should be a thin walker — under 30 lines — with zero character-iterating loops.

## User Scenarios

### Scenario 1 — Rendering uses visual lines

**Given** a document with mixed content (text, tabs, grapheme clusters), **When** the editor draws, **Then** it iterates `CellVisualLine` elements rather than raw characters.

### Scenario 2 — Line numbers render as a Padding SubView

**Given** an editor with line numbers enabled, **When** the editor draws, **Then** line numbers are painted by `Gutter : View` - a SubView of `Padding` - so popovers and menus correctly clip the gutter. See `specs/decisions.md` DEC-004 for why this replaces the spec's original `LineNumberMargin : IBackgroundRenderer` proposal.

### Scenario 3 — Grapheme cluster regression on tabbed line

**Given** a line containing `\tHello 🌍`, **When** the editor renders it, **Then** the tab renders at the correct width, "Hello" renders as individual cells, and the globe emoji occupies two cells — all via the visual-line pipeline.

### Scenario 4 — Mouse hit-testing works with pipeline

**Given** the new pipeline-based rendering, **When** the user clicks on a character in the editor, **Then** the caret is placed at the correct document offset (mouse tests pass unchanged).

## Requirements

- **FR-001**: Replace `OnDrawingContent`'s char-iterating draw loop with a `CellVisualLine`-walking loop.
- **FR-002**: Delete `DrawLineContent (...)` char-iterating implementation.
- **FR-003**: Delete `GetVisualWidthForCharacter`, `GetVisualColumnFromLogicalColumn`, `GetLogicalColumnFromVisualColumn`.
- **FR-004**: Delete the `c == '\t' ? ...` ternary tab-expansion logic.
- **FR-005**: Delete `DrawLineNumbers` bespoke margin method.
- **FR-006**: Render line numbers via a `View` SubView of `Padding` (`Gutter`), not a renderer. See DEC-004 for rationale.
- **FR-007**: `OnDrawingContent` body must be < 30 lines and contain zero `for (int i = 0; i < text.Length; ...)` patterns.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.Drawing.cs`
- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Mouse.cs`
- `src/Terminal.Gui.Editor/Gutter.cs`

## Definition of Done

- [x] `OnDrawingContent` body is < 30 lines with zero character-iterating loops
- [x] All existing render tests pass
- [x] All existing mouse tests pass
- [x] Grapheme-cluster regression test on tabbed line added and passes
- [x] Line numbers render via `Gutter : View` (Padding SubView). See DEC-004.
- [x] `DrawLineNumbers`, `DrawLineContent`, `GetVisualWidthForCharacter`, `GetVisualColumnFromLogicalColumn`, `GetLogicalColumnFromVisualColumn` are deleted
- [x] R1, R2 enforceable

## Out of Scope

- Word wrap rendering — that is word-wrap
- Highlighting transformer — that is syntax-colorizer
- Folding UI — that is folding-ui

## Notes

- rendering-pipeline is done, so this is now unblocked and ready for implementation.
- syntax-colorizer (highlighting colorizer) depends on this item.
