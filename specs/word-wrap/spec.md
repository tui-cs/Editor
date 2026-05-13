# Feature Specification: Word Wrap

**Status**: Ready — tracked in issue #102
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: rendering-pipeline ✅
**Blocked by**: None

## Overview

Implement opt-in soft word wrap for the Editor. A `WordWrapStrategy` walks grapheme clusters, accumulates column counts, and breaks lines at the last whitespace boundary before the viewport edge. For runs with no whitespace (e.g. CJK text), a hard break is applied. The output is a list of wrap segments that `VisualLineBuilder` uses to emit one `CellVisualLine` per wrap segment when `Editor.WordWrap == true`.

## User Scenarios

### Scenario 1 — Break at whitespace

**Given** `WordWrap = true` and a viewport 40 columns wide, **When** a document line is 80 characters with spaces at various points, **Then** the line wraps at the last whitespace boundary before column 40 and produces two `CellVisualLine`s.

### Scenario 2 — Hard-break CJK run

**Given** `WordWrap = true` and a viewport 20 columns wide, **When** a document line contains 30 CJK characters with no whitespace, **Then** the line hard-breaks at column 20 (10 double-width characters) producing multiple `CellVisualLine`s.

### Scenario 3 — Leading indent on continuation

**Given** a wrapped line where the original starts with 4 spaces of indentation, **When** the line wraps, **Then** continuation lines receive a visual leading indent [NEEDS CLARIFICATION — exact indent policy TBD].

### Scenario 4 — Caret under wrap

**Given** a long line that wraps into 3 visual lines, **When** the caret is at a document offset in the second wrap segment, **Then** the cursor is positioned on the second visual line at the correct column.

## Requirements

- **FR-001**: Implement `WordWrapStrategy` that walks grapheme clusters and produces wrap segments.
- **FR-002**: Break at last whitespace boundary before viewport edge; hard-break if no whitespace found.
- **FR-003**: Integrate with `VisualLineBuilder` — when `WordWrap == true`, emit one `CellVisualLine` per wrap segment.
- **FR-004**: Add `Editor.WordWrap` boolean property (default `false`).
- **FR-005**: Caret hit-testing must account for wrap segments — correct visual line and column.

## Files in Scope

- `src/Terminal.Gui.Editor/Rendering/WordWrapStrategy.cs` (new)
- `src/Terminal.Gui.Editor/Rendering/VisualLineBuilder.cs` (integration)
- `src/Terminal.Gui.Editor/Editor.cs` (`WordWrap` property)

## Definition of Done

- [ ] With `WordWrap = true`, lines wider than viewport produce multiple `CellVisualLine`s
- [ ] Break-at-whitespace test passes
- [ ] Hard-break CJK run test passes
- [ ] Caret hit-testing correct under wrap (caret is on the right visual line at the right column)
- [ ] Leading-indent on continuation line test passes
- [ ] Existing non-wrap tests unaffected

## Out of Scope

- ted UI toggle for word wrap — that is word-wrap-toggle
- Folding + wrap interaction

## Notes

- rendering-pipeline is done, so this is now unblocked and ready for implementation.
- word-wrap-toggle (word wrap toggle in ted) depends on this item.
- The continuation-line indent policy is flagged as [NEEDS CLARIFICATION] — should match common editor behavior but exact specification is pending.
