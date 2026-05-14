# Undo & Redo

## Undoing changes

Press `Ctrl+Z` to undo the most recent edit.

You can press `Ctrl+Z` repeatedly to step back through the edit history one operation at a time.

## Redoing changes

Press `Ctrl+Y` (or `Ctrl+Shift+Z`) to redo the most recently undone operation.

## Undo granularity

The editor groups related operations into single undo steps so that one `Ctrl+Z` undoes a logical action rather than a single character:

| Operation | Undo behaviour |
|---|---|
| Typing a single character | One character per step |
| Pressing `Enter` (with auto-indent) | Entire Enter + indent in one step |
| Paste | Entire paste in one step |
| Cut | Entire cut in one step |
| Typing over a selection | Replace in one step |
| Replace All (find/replace) | All replacements in one step |

## Notes

- Undo and redo are disabled in [read-only mode](read-only.md).
- The undo history is per-document. Switching documents resets the undo/redo state to that document's history.
