# Feature Specification: Indentation Model

**Status**: Ready
**Created**: 2026-05-10
**Depends on**: None
**Blocked by**: None

## Overview

Bring `IIndentationStrategy` and `DefaultIndentationStrategy` from AvaloniaEdit into `src/Terminal.Gui.Text/Indentation/`. This establishes the pluggable indentation abstraction that the Editor (auto-indent) will consume for auto-indent on Enter and Tab behavior.

The lift is minimal — no Avalonia GUI dependencies are expected.

## User Scenarios

### Scenario 1 — Default indentation copies leading whitespace

**Given** a `TextDocument` where line 3 starts with four spaces, **When** `DefaultIndentationStrategy.IndentLine` is called for line 4, **Then** line 4 receives the same four-space prefix.

### Scenario 2 — No-op on first line

**Given** a `TextDocument`, **When** `IndentLine` is called for line 1 (no previous line to copy from), **Then** no indentation is applied and the line is unchanged.

### Scenario 3 — Mixed tabs and spaces are preserved

**Given** a `TextDocument` where the previous line starts with `\t  ` (tab + two spaces), **When** `IndentLine` is called for the next line, **Then** the same `\t  ` prefix is applied.

## Requirements

- **FR-001**: Lift `IIndentationStrategy` and `DefaultIndentationStrategy` from AvaloniaEdit.
- **FR-002**: Transform namespace to `Terminal.Gui.Text.Indentation`.
- **FR-003**: Strip any `using Avalonia.*` directives (not expected, but verify).
- **FR-004**: Preserve original formatting and copyright headers per fork policy.
- **FR-005**: Add `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` header line.
- **FR-006**: Append modification rows to `third_party/AvaloniaEdit/UPSTREAM.md`.

## Files in Scope

- `src/Terminal.Gui.Text/Indentation/*.cs`
- `third_party/AvaloniaEdit/UPSTREAM.md` (append rows)

## Definition of Done

- [ ] All indentation types compile and are in `Terminal.Gui.Text.Indentation` namespace
- [ ] Tests in `tests/Terminal.Gui.Text.Tests/Indentation/` pass — `IndentLine` copies leading whitespace from previous line; no-op on first line; respects mixed tabs+spaces
- [ ] `UPSTREAM.md` updated with per-file modification log
- [ ] No Avalonia residue

## Out of Scope

- Wiring into Editor (Tab/Shift+Tab keys, Enter auto-indent) — that is tab-handling and auto-indent
- Language-specific indentation strategies (e.g. C# brace matching)

## Notes

- Can be done in parallel with folding, search, and syntax-highlighting.
- auto-indent (indentation strategy plumbing) is blocked on this item.
