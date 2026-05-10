# Feature Specification: Folding UI

**Status**: Blocked
**Created**: 2026-05-10
**Depends on**: folding, rendering-pipeline ✅
**Blocked by**: folding

## Overview

Add folding UI to the Editor. Clicking on the line-number margin toggles a `FoldingSection`. A `FoldingTransformer : IVisualLineTransformer` replaces folded ranges with `FoldingMarkerElement` rendering `"⋯"`. Keyboard shortcut Ctrl+M Ctrl+M toggles the fold under the caret. The `LineNumberMargin` (from drawing-overhaul) is extended with fold indicators (+/- icons).

## User Scenarios

### Scenario 1 — Folded section renders as marker

**Given** a `FoldingSection` spanning lines 5–15 that is collapsed, **When** the editor renders, **Then** lines 6–15 are hidden and line 5 shows a `"⋯"` marker at the fold point.

### Scenario 2 — Click to expand

**Given** a collapsed fold with a `"⋯"` marker, **When** the user clicks the fold indicator in the margin, **Then** the fold expands and lines 6–15 become visible again.

### Scenario 3 — Keyboard toggle

**Given** the caret is inside a foldable region, **When** the user presses Ctrl+M Ctrl+M, **Then** the fold toggles (collapses if expanded, expands if collapsed).

### Scenario 4 — Caret inside fold moves to marker edge

**Given** the caret is at an offset inside a collapsed fold, **When** the fold collapses, **Then** the caret moves to the edge of the fold marker (start of the folded region).

## Requirements

- **FR-001**: Implement `FoldingTransformer : IVisualLineTransformer` that replaces folded ranges with `FoldingMarkerElement` rendering `"⋯"`.
- **FR-002**: Clicking fold indicator in the line-number margin toggles a `FoldingSection`.
- **FR-003**: Ctrl+M Ctrl+M keyboard shortcut toggles fold under caret.
- **FR-004**: Extend `LineNumberMargin` with fold indicators (+/- icons).
- **FR-005**: Caret inside a collapsing fold moves to the fold marker edge.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Mouse.cs`
- `src/Terminal.Gui.Editor/Editor.Commands.cs`
- `src/Terminal.Gui.Editor/Rendering/FoldingTransformer.cs` (new)
- `src/Terminal.Gui.Editor/Rendering/LineNumberMargin.cs` (extended)

## Definition of Done

- [ ] Folded section renders as `"⋯"` marker
- [ ] Clicking fold indicator re-expands
- [ ] Ctrl+M Ctrl+M toggles fold under caret
- [ ] Caret inside fold moves to marker edge on collapse
- [ ] ted demo has folding regions wired and demonstrable

## Out of Scope

- Automatic fold-region detection (e.g. `#region` / brace matching) — that requires language-specific folding strategies
- The folding data model itself — that is folding

## Notes

- rendering-pipeline is done, but this is still blocked on folding (folding lift) — the `FoldingManager` / `FoldingSection` types must exist before UI can consume them.
- The `FoldingMarkerElement` type was created in rendering-pipeline but is not yet consumed — this item wires it up.
