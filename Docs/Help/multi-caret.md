# Multi-Caret Editing

Place multiple carets in the document and type, delete, or press Enter at all of them simultaneously. Every multi-caret operation is a single undo step.

## Adding and removing carets

| Action | Effect |
|---|---|
| **Ctrl+Click** | Toggle an additional caret at the clicked position. Click an existing additional caret to remove it. |
| **Ctrl+Alt+↑** | Add a caret on the line above the topmost caret, at the sticky visual column (VS Code parity). |
| **Ctrl+Alt+↓** | Add a caret on the line below the bottommost caret, at the sticky visual column (VS Code parity). |
| **Alt + drag** | Build a vertical column from the press row through the drag row. Zero horizontal extent creates carets; horizontal extent creates one selection per row. |
| **Ctrl+Shift+Alt+↑/↓/←/→** | Create or extend a keyboard column selection. `PgUp` / `PgDn` extend by one viewport. |
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

Additional carets are rendered as blinking, reverse-video cells by the `MultiCaretRenderer` (an `IOverlayRenderer`). Additional-caret selections render with the same active selection role as the primary selection. The status bar in `ted` shows the total caret count when in multi-caret mode (e.g. "Ln 4, Col 1 (3 carets)").

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

## VS Code parity and intentional deviations

Column selection matches VS Code behavior: typing over a ranged column replaces each row's selection in one undo step; short rows clamp to the real line end without writing padding; dragging left of the anchor reverses the selection direction; `Esc` or a plain click collapses back to the primary caret.

Intentional deviations:

- **D1 — mouse modifier**: VS Code starts column selection with `Shift+Alt`+drag; this editor uses **`Alt`+drag** because Windows Terminal and xterm-family terminals reserve `Shift`+drag for terminal-side forced/block selection while an app has mouse mode enabled. Configurable mouse modifiers are tracked by [tui-cs/Terminal.Gui#4888](https://github.com/tui-cs/Terminal.Gui/issues/4888).
- **D2 — add caret at click**: VS Code uses `Alt`+Click; this editor keeps the existing **`Ctrl`+Click** binding. `Alt` is the column-drag modifier, so an `Alt`+Click alias would need drag-threshold disambiguation.
- **D3 — keyboard column-select**: not a deviation. `Ctrl+Shift+Alt+Arrow` and `Ctrl+Shift+Alt+PgUp/PgDn` match VS Code behavior when the terminal delivers the chord (TG's Kitty keyboard protocol support makes this available on capable terminals).
- **D4 — sticky Column Selection Mode**: VS Code's modal toggle is out of scope; this editor implements the drag and keyboard gestures, not a persistent mode with menu/status UI.
- **D5 — multi-cursor paste distribution**: VS Code can distribute N clipboard lines over N cursors. This is deferred as a separate follow-up; typing and one-line paste replacement are covered here.

## Limitations (current alpha)

- Find/Replace operates on the primary caret only.
- Toggling Word Wrap while a vertical block is live dismisses the block.
