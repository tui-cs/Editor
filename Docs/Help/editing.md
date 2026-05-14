# Editing

## Typing text

Simply start typing to insert text at the caret position. The editor handles the full Unicode range including multi-byte characters, emoji, and wide East-Asian characters — each grapheme cluster occupies the correct number of terminal cells.

## Deleting text

| Action | Key |
|---|---|
| Delete the character to the left of the caret | `Backspace` |
| Delete the character to the right of the caret | `Delete` |

When text is selected, both `Backspace` and `Delete` replace the selection with nothing (i.e. delete the selected range in one step).

### Indentation-aware Backspace

When the caret is in the leading whitespace of a line and no text is selected, `Backspace` removes a full indentation level's worth of spaces at once rather than a single character. This makes it easy to un-indent a line without pressing `Backspace` repeatedly.

## Inserting a new line

Press `Enter` to insert a line break at the caret position.

If an **indentation strategy** is active (see [Indentation](indentation.md)), the new line is automatically indented to match the previous line's leading whitespace.

The entire `Enter` operation — newline insertion plus any auto-indent — is a single undo step, so pressing `Ctrl+Z` once undoes it completely.

## Typing over a selection

If text is currently selected, typing any printable character or pressing `Enter` replaces the selection with the new input in a single undo step.

## Read-only mode

When the editor is in read-only mode all edit operations (typing, `Backspace`, `Delete`, `Enter`, paste) are disabled. Navigation and selection continue to work normally. See [Read-Only Mode](read-only.md).
