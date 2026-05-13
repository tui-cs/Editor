# Feature Specification: Auto-Indent

**Status**: Done — shipped in PR #95 (merged into `develop` 2026-05-11)
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: indentation ✅
**Blocked by**: —

## Overview

Wire the `IIndentationStrategy` abstraction (lifted in indentation) into the Editor. `Editor.IndentationStrategy` defaults to `DefaultIndentationStrategy`. When the user presses Enter, the Editor calls the strategy's `IndentLine` for the newly created line — providing automatic indentation. The Tab key consults the strategy when `ConvertTabsToSpaces == true` to determine the appropriate indentation level.

## User Scenarios

### Scenario 1 — Enter on indented line creates new indented line

**Given** the caret is at the end of a line indented with 4 spaces, **When** the user presses Enter, **Then** a new line is created with 4 spaces of indentation and the caret is placed after the indentation.

### Scenario 2 — Strategy is pluggable

**Given** a custom `IIndentationStrategy` that always indents to 2 spaces, **When** it is assigned to `Editor.IndentationStrategy`, **Then** Enter produces lines with 2-space indentation regardless of the previous line.

### Scenario 3 — Default strategy is dumb copy

**Given** `Editor.IndentationStrategy` is `DefaultIndentationStrategy` (the default), **When** the user presses Enter, **Then** the new line's indentation is copied verbatim from the previous line (no brace-matching or language awareness).

### Scenario 4 — Tab defers to strategy

**Given** `ConvertTabsToSpaces == true` and a custom strategy, **When** the user presses Tab, **Then** the strategy determines the indent level [NEEDS CLARIFICATION — exact interaction with tab-handling's tab handler TBD].

## Requirements

- **FR-001**: `Editor.IndentationStrategy` property of type `IIndentationStrategy`, defaulting to `DefaultIndentationStrategy`.
- **FR-002**: On Enter, Editor calls `IndentationStrategy.IndentLine` for the new line.
- **FR-003**: Tab key consults the strategy when `ConvertTabsToSpaces == true`.
- **FR-004**: The default strategy copies leading whitespace from the previous line.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Commands.cs`
- Tests in `tests/Terminal.Gui.Editor.Tests/`

## Definition of Done

- [ ] Default strategy drives Enter auto-indent (new line gets previous line's indentation)
- [ ] Strategy is pluggable — assigning a custom strategy changes behavior
- [ ] tab-handling's Tab handler defers to strategy where appropriate
- [ ] Tests cover Enter-on-indented-line, pluggable strategy, and default dumb-copy behavior

## Out of Scope

- Language-specific indentation strategies (e.g. C# brace-matching indent)
- Smart indent / block indent

## Notes

- Blocked on indentation (indentation lift) — `IIndentationStrategy` and `DefaultIndentationStrategy` must exist first.
- FR-003 (Tab + strategy interaction) is marked [NEEDS CLARIFICATION] — the boundary between tab-handling's tab handling and auto-indent's strategy needs a design decision.
