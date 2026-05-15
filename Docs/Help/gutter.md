# Gutter

The gutter is a narrow column on the left side of the editor that can show line numbers and fold indicators.

## Line numbers

When line numbers are enabled, each document line's number is displayed in the gutter to the left of the text.

## Fold indicators

When fold indicators are enabled, the gutter shows an arrow next to foldable lines:

- `▸` (right-pointing arrow) — the region is collapsed. Click to expand it.
- `▾` (down-pointing arrow) — the region is expanded. Click to collapse it.
- `│` — the line is part of an expanded fold (continuation line).

## Combining gutter options

Both options are independent and can be combined freely:

| Configuration | Gutter content |
|---|---|
| Neither enabled | No gutter column (text area fills the full width) |
| Line numbers only | Line numbers |
| Fold indicators only | Fold indicators |
| Both enabled | Line numbers, then fold indicators |

## Mouse wheel in the gutter

Scrolling the mouse wheel over the gutter scrolls the editor just as it would over the text area.
