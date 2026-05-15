# Folding

Folding lets you collapse regions of text (such as a method body or a block comment) into a single marker line, reducing visual clutter while you work on other parts of the document.

## Toggling a fold

> All keys shown in this guide are the defaults and can be changed. See [Customizing Keybindings and Themes](configuration.md).

### With the keyboard

Place the caret anywhere on a foldable line (or inside a foldable region) and press `Ctrl+M` to toggle the fold:

- If the region is expanded, it collapses and the hidden lines are replaced by a `⋯` marker.
- If the region is collapsed, it expands back to full view.

### With the mouse

If the **fold indicators** gutter column is visible (see [Gutter](gutter.md)), click the `▸` or `▾` indicator next to a line to toggle its fold.

## Folded regions

When a region is collapsed:

- Lines inside the fold are hidden; only the first line plus the `⋯` marker is visible.
- The caret cannot be placed inside a folded region. If you move the caret into a folded region (for example by pressing `Ctrl+End`), the fold expands automatically.

## Folding strategies

The host application determines which folding strategy is active:

- **Brace folding** — detects `{`/`}` pairs and folds between them (suitable for C# and similar languages).
- **XML folding** — detects XML/HTML element pairs.
- **Custom strategies** — the host application can install any `IFoldingStrategy` to define fold regions appropriate for the content.

If no strategy is installed, the gutter fold indicators are not shown and `Ctrl+M` has no effect.
