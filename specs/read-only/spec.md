# Feature Specification: Read-Only Mode

**Status**: Ready
**Created**: 2026-05-10
**Depends on**: None
**Blocked by**: None

## Overview

Add a `ReadOnly` property to the Editor. When `true`, all edit commands (typing, paste, Backspace, Delete, Enter, Tab, undo/redo) become no-ops — the `Document` is not modified. Navigation and selection continue to work normally. This enables use cases like log viewers, read-only file browsing, and diff displays.

## User Scenarios

### Scenario 1 — Typing is a no-op

**Given** `ReadOnly = true`, **When** the user types characters, **Then** nothing is inserted and the document remains unchanged.

### Scenario 2 — Paste is a no-op

**Given** `ReadOnly = true`, **When** the user presses Ctrl+V with clipboard content, **Then** nothing is pasted.

### Scenario 3 — Backspace, Delete, Enter, Tab are no-ops

**Given** `ReadOnly = true`, **When** the user presses Backspace, Delete, Enter, or Tab, **Then** the document remains unchanged.

### Scenario 4 — Undo/Redo are no-ops

**Given** `ReadOnly = true`, **When** the user presses Ctrl+Z or Ctrl+Y, **Then** nothing happens.

### Scenario 5 — Navigation still works

**Given** `ReadOnly = true`, **When** the user presses arrow keys, Home, End, Page Up/Down, **Then** the caret moves normally.

### Scenario 6 — Selection still works

**Given** `ReadOnly = true`, **When** the user Shift+clicks or Shift+arrows, **Then** text is selected normally.

## Requirements

- **FR-001**: `Editor.ReadOnly` boolean property (default `false`).
- **FR-002**: When `ReadOnly = true`, all edit commands are no-ops: typing, paste, Backspace, Delete, Enter, Tab, Shift+Tab, Undo, Redo.
- **FR-003**: When `ReadOnly = true`, navigation commands work: arrow keys, Home, End, Ctrl+Home, Ctrl+End, Page Up, Page Down, mouse click.
- **FR-004**: When `ReadOnly = true`, selection commands work: Shift+arrows, Shift+click, Ctrl+A, drag-to-select.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs`
- `src/Terminal.Gui.Editor/Editor.Commands.cs`

## Definition of Done

- [ ] Typing, paste, Backspace, Delete, Enter, Tab, Undo, Redo are all no-ops when `ReadOnly = true`
- [ ] Navigation and selection still work when `ReadOnly = true`
- [ ] ted demo opens a file in read-only mode via a flag and behaves correctly
- [ ] Unit tests cover each blocked edit command and each allowed navigation/selection command

## Out of Scope

- Per-segment read-only ranges (open decision per `specs/00-plan.md` §10)
- Visual indicator / styling for read-only mode (e.g. grayed-out text)

## Notes

- No dependencies — can be implemented at any time.
- Simple guard-clause pattern: check `ReadOnly` at the top of each edit command handler.
