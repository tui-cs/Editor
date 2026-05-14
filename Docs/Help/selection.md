# Selection

## Selecting text with the keyboard

> All keys shown in this guide are the defaults and can be changed. See [Customizing Keybindings and Themes](configuration.md).

Hold `Shift` while pressing any navigation key to extend the selection:

| Action | Key |
|---|---|
| Extend selection left one character | `Shift+←` |
| Extend selection right one character | `Shift+→` |
| Extend selection up one line | `Shift+↑` |
| Extend selection down one line | `Shift+↓` |
| Extend selection to start of line | `Shift+Home` |
| Extend selection to end of line | `Shift+End` |
| Extend selection to start of document | `Shift+Ctrl+Home` |
| Extend selection to end of document | `Shift+Ctrl+End` |
| Extend selection up one page | `Shift+Page Up` |
| Extend selection down one page | `Shift+Page Down` |
| Select all text | `Ctrl+A` |

## Selecting text with the mouse

Click and drag to select a range of text. Release the mouse button to finish the selection.

`Shift+Click` extends the selection from the current caret position to the clicked location.

## Collapsing a selection

Any plain (non-`Shift`) navigation key collapses the selection and moves the caret:
- Moving **left** places the caret at the start of the selection.
- Moving **right** places the caret at the end of the selection.

Clicking anywhere in the text area also collapses the selection.

## Operating on a selection

| Operation | Effect |
|---|---|
| Type any character | Replaces the selected text with the typed character |
| `Enter` | Replaces the selected text with a newline |
| `Backspace` or `Delete` | Deletes the selected text |
| `Ctrl+C` | Copies the selected text to the clipboard |
| `Ctrl+X` | Cuts the selected text to the clipboard |
| `Ctrl+V` | Pastes from the clipboard, replacing the selection |
| `Tab` | Indents all lines in the selected range |
| `Shift+Tab` | Un-indents all lines in the selected range |

All of the above that modify the document are single undo steps.
