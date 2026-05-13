# Feature Specification: Folding Model

**Status**: Subsumed — shipped as part of [folding-ui](../folding-ui/spec.md) (PR #96). Retained for historical reference only. Per constitution R9 ("lifts must ship with their ted consumer") lift-only specs are no longer scheduled separately.
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: None
**Blocked by**: —

## Overview

Bring `FoldingManager`, `FoldingSection`, and `FoldingSectionCollection` from AvaloniaEdit into `src/Terminal.Gui.Editor/Folding/`. This gives the document model a UI-framework-independent folding layer that tracks collapsible regions as anchored segments. The lift follows the same fork policy as the existing `Document/` and `Utils/` imports: namespace transform, Avalonia dependency removal, and modification logging in `UPSTREAM.md`.

Source: AvaloniaEdit at the SHA pinned in `third_party/AvaloniaEdit/UPSTREAM.md`. Limited to `src/AvaloniaEdit/Folding/*.cs`, excluding any class that references `Avalonia.Controls` or `Avalonia.Media`.

## User Scenarios

### Scenario 1 — Create and remove folding sections

**Given** a `TextDocument` managed by a `FoldingManager`, **When** a consumer creates a `FoldingSection` spanning lines 5–15 and later removes it, **Then** the section is tracked while active and cleanly removed without affecting document content.

### Scenario 2 — Edits inside a fold preserve anchored offsets

**Given** a `FoldingSection` spanning offset 100–200, **When** text is inserted at offset 120 (inside the fold), **Then** the section's `EndOffset` grows by the insertion length and its `StartOffset` remains 100.

### Scenario 3 — Edits across folds keep offsets correct

**Given** multiple folding sections, **When** a bulk edit replaces text that spans across two fold boundaries, **Then** all affected sections update their anchored offsets correctly and no section becomes invalid.

### Scenario 4 — Manager survives whole-document replace

**Given** a `FoldingManager` with several sections, **When** the entire document text is replaced, **Then** the manager remains valid (sections may be cleared or repositioned) and does not throw.

## Requirements

- **FR-001**: Lift `FoldingManager`, `FoldingSection`, `FoldingSectionCollection` from AvaloniaEdit `src/AvaloniaEdit/Folding/*.cs`.
- **FR-002**: Transform namespace from `AvaloniaEdit.Folding` to `Terminal.Gui.Text.Folding`.
- **FR-003**: Strip all `using Avalonia.*` directives.
- **FR-004**: Remove `Dispatcher.UIThread.VerifyAccess ()` calls.
- **FR-005**: Replace any `IBrush` / `Avalonia.Media.Color` references with `Terminal.Gui.Color`.
- **FR-006**: Preserve original formatting and copyright headers per fork policy.
- **FR-007**: Add `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` header line.
- **FR-008**: Append modification rows to `third_party/AvaloniaEdit/UPSTREAM.md`.

## Files in Scope

- `src/Terminal.Gui.Editor/Folding/*.cs`
- `third_party/AvaloniaEdit/UPSTREAM.md` (append rows)

## Definition of Done

- [ ] All folding types compile and are in `Terminal.Gui.Text.Folding` namespace
- [ ] Tests in `tests/Terminal.Gui.Editor.Tests/Folding/` pass — ported from AvaloniaEdit's `FoldingTests`: create/remove sections, edits inside/across folds keep anchored offsets correct, manager survives whole-document replace
- [ ] `UPSTREAM.md` updated with per-file modification log
- [ ] No Avalonia residue (`grep -r "using Avalonia" src/Terminal.Gui.Editor/Folding/` returns nothing)

## Out of Scope

- Any Editor-side UI: margins, click-to-toggle, marker rendering — that is folding-ui
- `FoldingTransformer` or `FoldingMarkerElement` rendering

## Notes

- This is a pure document-model lift with no dependency on `Terminal.Gui`. It belongs entirely in the `Terminal.Gui.Editor` package.
- Can be done in parallel with search, indentation, and syntax-highlighting.
