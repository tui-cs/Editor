# Word Wrap

Word wrap makes long lines soft-wrap at the edge of the viewport so you can read them without scrolling horizontally. The underlying document is not modified — wrap is purely visual.

## Toggling word wrap

In `ted`, use **Options → Word Wrap** to toggle word wrap on or off.

When word wrap is on:

- Lines that exceed the viewport width are broken at the last whitespace boundary before the edge. If a word is longer than the viewport, it is broken at the edge.
- Continuation rows (the extra visual rows produced by wrapping) are flush with the left edge of the text area (column 0).
- The horizontal scrollbar is hidden because all content fits within the viewport width.

When word wrap is off (the default):

- Long lines extend beyond the right edge of the viewport; scroll horizontally to see them.
- The horizontal scrollbar is shown when content is wider than the viewport.

## Notes

- Caret navigation respects visual rows when word wrap is on. Pressing `↑` / `↓` moves the caret one *visual* row, not one document line.
- `Home` / `End` move to the start/end of the *document* line, not the visual row.
- Word wrap has no effect on the document content — `Ctrl+Z` cannot undo a wrap toggle.
