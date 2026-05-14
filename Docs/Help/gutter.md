# Gutter

The gutter is a narrow column on the left side of the editor that can show line numbers and fold indicators. It is a real `View` subview, so it participates in the normal Terminal.Gui layout, focus traversal, and pointer events.

## Line numbers

When line numbers are enabled, each document line's number is displayed in the gutter to the left of the text.

Toggle in `ted`: **Options → Line Numbers**.

## Fold indicators

When fold indicators are enabled, the gutter shows a `+` next to lines that have a collapsed fold, and a `-` next to lines that start an expanded foldable region.

- Click `+` to expand the fold.
- Click `-` to collapse the fold.

Toggle in `ted`: **Options → Fold Indicators**.

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
