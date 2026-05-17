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

# Multi-Caret Editing

Place multiple carets in the document and type, delete, or press Enter at all of them simultaneously. Every multi-caret operation is a single undo step.

## Adding and removing carets

| Action | Effect |
|---|---|
| **Ctrl+Click** | Toggle an additional caret at the clicked position. Click an existing additional caret to remove it. |
| **Escape** | Collapse back to the primary caret (clears all additional carets). |

The primary caret (the one controlled by normal navigation keys) is never removed by Ctrl+Click.

## Editing with multiple carets

Once two or more carets are active, the following operations apply at every caret position simultaneously:

- **Typing** — inserts the character at each caret.
- **Enter** — inserts a newline and applies the active `IndentationStrategy` at each caret (same as single-caret auto-indent).
- **Backspace** — performs smart indentation-unit delete when the caret is inside leading whitespace; otherwise deletes one character left.
- **Delete** — removes one character to the right of each caret.

All edits are wrapped in a single `Document.RunUpdate` scope, so **Undo (Ctrl+Z)** reverts the entire multi-caret operation in one step.

## Visual feedback

Additional carets are rendered as blinking, reverse-video cells by the `MultiCaretRenderer` (an `IOverlayRenderer`). The status bar in `ted` shows the total caret count when in multi-caret mode (e.g. "Ln 4, Col 1 (3 carets)").

## Programmatic API

```csharp
// Add or toggle a caret at a document offset.
editor.ToggleCaretAt (offset);

// Query state.
bool multi = editor.HasMultipleCarets;
IReadOnlyList<int> offsets = editor.AdditionalCaretOffsets;

// Collapse back to the primary caret.
editor.ClearAdditionalCarets ();
```

Additional carets are backed by `TextAnchor` instances, so they track insertions and deletions elsewhere in the document automatically (same mechanism as the primary caret).

## Limitations (current alpha)

- Selection is not yet per-caret; only the primary caret carries a selection.
- Find/Replace operates on the primary caret only.
- Ctrl+Click is the only gesture for adding carets; column-select (Alt+Shift+Arrow) is planned.
