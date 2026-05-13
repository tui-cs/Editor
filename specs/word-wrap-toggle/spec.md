# Feature Specification: Word Wrap Toggle

**Status**: Subsumed — folded into [word-wrap](../word-wrap/spec.md) per constitution R9 (the ted toggle ships in the same PR as the engine). Retained for historical reference only.
**Created**: 2026-05-10
**Last updated**: 2026-05-13
**Depends on**: word-wrap
**Blocked by**: —

## Overview

Expose the `Editor.WordWrap` property (implemented in word-wrap) through the ted demo application. Add a status bar toggle or menu item that lets users turn word wrap on and off at runtime. Verify that toggling wrap changes the visible row count and that the caret survives at the same logical offset.

## User Scenarios

### Scenario 1 — Toggle wrap on

**Given** a document with long lines and `WordWrap = false`, **When** the user toggles word wrap on via the status bar, **Then** long lines wrap and the visible row count increases.

### Scenario 2 — Toggle wrap off

**Given** `WordWrap = true` with wrapped lines visible, **When** the user toggles word wrap off, **Then** lines unwrap and horizontal scrolling is restored.

### Scenario 3 — Caret survives toggle

**Given** the caret is at a specific logical offset, **When** word wrap is toggled, **Then** the caret remains at the same logical document offset (though its visual position may change).

## Requirements

- **FR-001**: Expose `Editor.WordWrap` property toggle in ted's UI (status bar or menu).
- **FR-002**: Toggling wrap changes visible row count.
- **FR-003**: Caret remains at the same logical offset after toggle.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs` (property exposure if needed)
- `examples/ted/TedApp.cs`
- Integration tests

## Definition of Done

- [ ] Toggling wrap changes visible row count
- [ ] Caret survives at same logical offset after toggle
- [ ] Visibly works in ted demo
- [ ] Integration test covers toggle behavior

## Out of Scope

- The wrap strategy itself — that is word-wrap
- Wrap + folding interaction
- Persistent setting storage

## Notes

- Blocked on word-wrap (word wrap strategy) — cannot toggle something that doesn't exist yet.
- This is a thin UI wiring task once word-wrap is complete.
