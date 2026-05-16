# Multi-Caret Editing

Place multiple carets in the document and type, delete, or press Enter at all of them simultaneously. Every multi-caret operation is a single undo step.

## Adding and removing carets

| Action | Effect |
|---|---|
| **Ctrl+Click** | Toggle an additional caret at the clicked position. Click an existing additional caret to remove it. |
| **Ctrl+Alt+↑** | Add a caret on the line above the topmost caret, at the sticky visual column (VS Code parity). |
| **Ctrl+Alt+↓** | Add a caret on the line below the bottommost caret, at the sticky visual column (VS Code parity). |
| **Shift+Alt + drag** | Build a vertical column of carets from the press row through the drag row at the press column (carets only). |
| **Escape** | Collapse back to the primary caret (clears all additional carets). |

`Ctrl+Alt+↑/↓` track a *sticky visual column*: a short or tab-indented intervening line doesn't lose the column — the next long-enough line restores it. The chords are configurable per platform via `Editor.DefaultKeyBindings`; a terminal or window manager that grabs `Ctrl+Alt+arrow` is handled by remapping in config, not a separate built-in chord.

The primary caret (the one controlled by normal navigation keys) is never removed by Ctrl+Click.

## Editing with multiple carets

Once two or more carets are active, the following operations apply at every caret position simultaneously:

- **Typing** — inserts the character at each caret.
- **Enter** — inserts a newline and applies the active `IndentationStrategy` at each caret (same as single-caret auto-indent).
- **Backspace** — performs smart indentation-unit delete when the caret is inside leading whitespace; otherwise deletes one character left.
- **Delete** — removes one character to the right of each caret.

All edits are wrapped in a single `Document.RunUpdate` scope, so **Undo (Ctrl+Z)** reverts the entire multi-caret operation in one step.

## Visual feedback

Additional carets are rendered as inverted-attribute cells by the `MultiCaretRenderer` (an `IBackgroundRenderer`). The status bar in `ted` shows the total caret count when in multi-caret mode (e.g. "Ln 4, Col 1 (3 carets)").

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
- `Shift+Alt`+drag produces a column of *carets*, not a column *selection*. To replace a column, drag to place the carets, then `Shift+→`/`←` to grow each caret's selection, then type. Per-row column selection during the drag is the planned follow-up.
- Toggling Word Wrap while a vertical block is live dismisses the block.
