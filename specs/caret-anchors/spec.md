# Feature Specification: Anchor-Backed Caret & Selection

**Status**: Complete
**Created**: 2026-05-10
**Depends on**: None
**Blocked by**: None

## Overview

Replace the Editor's hand-rolled caret-offset tracking with a `TextAnchor` (`AnchorMovementType.AfterInsertion`), and represent selection as two anchors (or a `TextSegment`). This eliminates the manual edit-tracking arithmetic in `OnDocumentChanged` that adjusts `_caretOffset` based on edit offsets and lengths. The caret becomes a thin wrapper over the anchor's offset, and the document's existing anchor infrastructure handles all position updates automatically.

`TextAnchor` is already landed in `Document/` — no new document-model code is needed.

## User Scenarios

### Scenario 1 — Insert before caret advances it

**Given** the caret is at offset 10, **When** 5 characters are inserted at offset 3, **Then** the caret automatically moves to offset 15 via the anchor — no manual arithmetic needed.

### Scenario 2 — Insert at caret with AfterInsertion semantics

**Given** the caret is at offset 10 with `AnchorMovementType.AfterInsertion`, **When** text is inserted at offset 10, **Then** the caret moves to the end of the inserted text (offset 10 + insertion length).

### Scenario 3 — External edit on shared TextDocument

**Given** two consumers share a `TextDocument` and the Editor's caret is at offset 20, **When** an external consumer inserts text at offset 5, **Then** the Editor's caret anchor automatically adjusts to offset 20 + insertion length.

### Scenario 4 — Selection survives edits

**Given** a selection spanning offsets 10–20, **When** text is inserted at offset 5, **Then** both selection anchors shift and the selection remains logically the same region of text.

## Requirements

- **FR-001**: Caret is backed by a `TextAnchor` with `AnchorMovementType.AfterInsertion`.
- **FR-002**: Selection is represented as two anchors or a `TextSegment` of two anchors.
- **FR-003**: `CaretOffset` is a thin wrapper over the anchor's `Offset` property.
- **FR-004**: Remove hand-rolled edit-tracking arithmetic in `OnDocumentChanged` (`if (_caretOffset >= e.Offset)` pattern).
- **FR-005**: All existing caret and selection tests must pass unchanged.
- **FR-006**: R4 (caret tracking rule from the plan) enforceable.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Selection.cs`

## Definition of Done

- [x] `OnDocumentChanged`'s manual `if (_caretOffset >= e.Offset)` arithmetic is gone
- [x] `CaretOffset` is a thin wrapper over the anchor's offset
- [x] All existing caret tests pass unchanged
- [x] All existing selection tests pass unchanged
- [x] New test: external edit on shared `TextDocument` advances caret correctly
- [x] New test: edit at caret with `AfterInsertion` semantics behaves correctly
- [x] R4 enforceable

## Out of Scope

- Multi-caret — that is multi-caret
- Anchor-backed undo/redo grouping changes

## Notes

- `TextAnchor` is already in `Document/` — no document-model changes needed.
- multi-caret (multi-caret) depends on this item.
