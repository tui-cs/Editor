# Feature Specification: Rendering Pipeline

**Status**: Done
**Created**: 2026-05-10
**Depends on**: None
**Blocked by**: None

## Overview

Stand up the rendering data model: `VisualLineBuilder`, `CellVisualLine`, `CellVisualLineElement`, `TextRunElement`, `TabElement`, `FoldingMarkerElement`, `IVisualLineTransformer`, and `IBackgroundRenderer`. After this, `OnDrawingContent` becomes a thin walker over pre-built visual lines rather than a char-by-char draw loop.

**Delivered by**: `experiment/codex/d1-tabs` branch (merged with tweaks).

## User Scenarios

### Scenario 1 — Document line produces visual line

**Given** a `TextDocument` with a line containing "Hello\tWorld", **When** `VisualLineBuilder` processes the `DocumentLine`, **Then** a `CellVisualLine` is produced containing a `TextRunElement` for "Hello", a `TabElement`, and a `TextRunElement` for "World".

### Scenario 2 — Grapheme-aware element construction

**Given** a line containing emoji or combining characters, **When** the builder processes it, **Then** elements are split on grapheme cluster boundaries via `GraphemeHelper.GetGraphemes()` and column widths are measured in cells.

### Scenario 3 — Cache invalidation on document change

**Given** a cached `CellVisualLine` for line 5, **When** text is inserted on line 5, **Then** the cached visual line is invalidated and rebuilt on next access.

## Requirements

- **FR-001**: `CellVisualLine` — one per `DocumentLine`, holds a list of `CellVisualLineElement`s.
- **FR-002**: `CellVisualLineElement` — base type; `TextRunElement`, `TabElement`, `FoldingMarkerElement` derive from it.
- **FR-003**: `VisualLineBuilder` — converts a `DocumentLine` into a `CellVisualLine`.
- **FR-004**: `IVisualLineTransformer` — mutates element `Attribute`s after build (for highlighting, folding markers).
- **FR-005**: `IBackgroundRenderer` — paints cell rectangles behind content (selection, current line, search hits).
- **FR-006**: `Editor.cs` exposes `LineTransformers` and `BackgroundRenderers` collections.
- **FR-007**: Grapheme-aware via `GraphemeHelper.GetGraphemes()`.
- **FR-008**: Cache one `CellVisualLine` per `DocumentLine`; invalidate by `Document.Changed` offset/length.

## Files in Scope

- `src/Terminal.Gui.Editor/Rendering/CellVisualLine.cs`
- `src/Terminal.Gui.Editor/Rendering/CellVisualLineElement.cs`
- `src/Terminal.Gui.Editor/Rendering/TextRunElement.cs`
- `src/Terminal.Gui.Editor/Rendering/TabElement.cs`
- `src/Terminal.Gui.Editor/Rendering/FoldingMarkerElement.cs`
- `src/Terminal.Gui.Editor/Rendering/VisualLineBuilder.cs`
- `src/Terminal.Gui.Editor/Rendering/IVisualLineTransformer.cs`
- `src/Terminal.Gui.Editor/Rendering/IBackgroundRenderer.cs`
- `src/Terminal.Gui.Editor/Editor.cs`

## Definition of Done

- [x] Pipeline merged with full unit coverage
- [x] R1 and R2 enforceable (rendering rules from the plan)
- [x] `Editor.cs` exposes `LineTransformers` and `BackgroundRenderers`

## Out of Scope

- Word wrap — that is word-wrap
- Highlighting transformer — that is syntax-colorizer
- Migration of `OnDrawingContent` — that is drawing-overhaul

## Notes

- Delivered alongside tab-handling (tabs) in the `experiment/codex/d1-tabs` branch.
- drawing-overhaul, word-wrap, find-and-replace, word-wrap-toggle, folding-ui, and syntax-colorizer all depend (directly or transitively) on this item.
