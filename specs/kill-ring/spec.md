# Feature Specification: Kill-Ring (Kill-to-EOL / Kill-to-BOL with Append)

## Overview

Emacs-style kill commands that delete text between the caret and a line boundary,
placing the killed text on the clipboard. Consecutive kills **append** to the
clipboard instead of replacing, accumulating a "kill ring" run that any non-kill
command breaks.

## Commands

| Command                    | Behavior                                                                                                     |
|----------------------------|--------------------------------------------------------------------------------------------------------------|
| `Command.CutToEndOfLine`   | Delete caret → end of line text. If already at EOL, delete the line delimiter (join with next line).          |
| `Command.CutToStartOfLine` | Delete line start → caret.                                                                                   |

Both commands:
- Are no-ops when `ReadOnly` is true.
- When a selection exists, delete the selection (same as `DeleteLeft` / `DeleteRight` behavior) and do **not** participate in the kill ring.
- Execute within a single `Document.RunUpdate()` so each kill is one undo step.
- Place killed text on the clipboard via `App.Clipboard`. If the clipboard write fails, the document is not modified.

## Kill-Ring Append Semantics

- A `_lastCommandWasKill` flag tracks whether the immediately preceding command was a kill.
- The flag is **cleared** at the top of `OnKeyDown` (before the base class dispatches any command).
- Each kill command **sets** the flag after executing.
- When the flag is set at the time of a new kill:
  - `CutToEndOfLine` **appends** killed text after the existing clipboard content.
  - `CutToStartOfLine` **prepends** killed text before the existing clipboard content (so the clipboard accumulates in document order).
- Any non-kill command (movement, insertion, undo, etc.) breaks the run because `OnKeyDown` clears the flag before dispatch.

## Key Bindings

**Unbound by default.** Neither `Command.CutToEndOfLine` nor `Command.CutToStartOfLine`
appears in `Editor.DefaultKeyBindings`. Users opt in via the `[ConfigurationProperty]`
`Editor.DefaultKeyBindings` configuration (e.g., binding Ctrl+K to `CutToEndOfLine`).

## Files Changed

- `src/Terminal.Gui.Editor/Editor.cs` — `_lastCommandWasKill` field.
- `src/Terminal.Gui.Editor/Editor.Commands.cs` — `AddCommand` registrations, `CutToEndOfLine()`, `CutToStartOfLine()`, `WriteKillToClipboard()`.
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs` — `OnKeyDown` override to clear the kill flag.
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorKillRingTests.cs` — integration tests.

## Decision Record

See `specs/decisions.md` DEC-005 for the "no-selection cut/copy is a no-op" policy.
The kill commands are a separate code path that does not conflict with DEC-005 —
they operate on line boundaries, not on the selection.
