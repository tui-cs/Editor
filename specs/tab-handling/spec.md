# Feature Specification: Tab Handling

**Status**: Done
**Created**: 2026-05-10
**Depends on**: rendering-pipeline ✅
**Blocked by**: None

## Overview

Comprehensive tab handling for the Editor, addressing issue #37. Implements `IndentationSize`, `ConvertTabsToSpaces`, and `ShowTabs` properties. Tab and Shift+Tab insert, indent, and unindent correctly. The visual-line pipeline renders tabs via `TabElement` with proper width calculation. Mouse click on a tab snaps to the nearest midpoint. Indentation-aware Backspace deletes to the previous indent stop. All rendering is grapheme-cluster-aware via `GraphemeHelper`.

**Delivered by**: `experiment/codex/d1-tabs` branch (merged with tweaks).

## User Scenarios

### Scenario 1 — Tab inserts spaces when configured

**Given** `ConvertTabsToSpaces = true` and `IndentationSize = 4`, **When** the user presses Tab, **Then** spaces are inserted to reach the next indent stop.

### Scenario 2 — Tab inserts tab character when configured

**Given** `ConvertTabsToSpaces = false`, **When** the user presses Tab, **Then** a `\t` character is inserted.

### Scenario 3 — Shift+Tab unindents

**Given** a line with leading indentation, **When** the user presses Shift+Tab, **Then** one level of indentation is removed from the line start.

### Scenario 4 — Mouse midpoint snap on tab

**Given** a tab character rendered at columns 0–3, **When** the user clicks at column 2, **Then** the caret is placed at the tab's document offset (before the tab); clicking at column 3 places it after.

### Scenario 5 — Indentation-aware Backspace

**Given** the caret is at column 4 on a line with 4 spaces of indentation, **When** Backspace is pressed, **Then** all 4 spaces are deleted (back to previous indent stop), not just one character.

## Requirements

- **FR-001**: `Editor.IndentationSize` property (default 4).
- **FR-002**: `Editor.ConvertTabsToSpaces` property (default `true`).
- **FR-003**: `Editor.ShowTabs` property for visible tab markers.
- **FR-004**: Tab key inserts tab or spaces based on `ConvertTabsToSpaces`.
- **FR-005**: Shift+Tab unindents the current line or selection.
- **FR-006**: `TabElement` in the visual-line pipeline renders tabs with correct width.
- **FR-007**: Mouse midpoint snap for caret placement on tab characters.
- **FR-008**: Indentation-aware Backspace deletes to previous indent stop.
- **FR-009**: Grapheme-cluster-aware rendering via `GraphemeHelper`.
- **FR-010**: `Editor.TabWidth` shimmed as `[Obsolete]` pointing to `IndentationSize`.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Commands.cs`
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs`
- `src/Terminal.Gui.Editor/Rendering/TabElement.cs`

## Definition of Done

- [x] Issue #37 closes
- [x] `Editor.TabWidth` shimmed `[Obsolete]`
- [x] `OnDrawingContent` does not special-case `\t`
- [x] Tab/Shift+Tab insert/indent/unindent correctly
- [x] Mouse midpoint snap works
- [x] Indentation-aware Backspace works

## Out of Scope

- `IIndentationStrategy` plumbing (Enter auto-indent) — that is auto-indent
- Language-specific indentation

## Notes

- Delivered alongside rendering-pipeline in the `experiment/codex/d1-tabs` branch.
