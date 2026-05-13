# Public API Notes

This file tracks public API added during pre-alpha work. It is intentionally brief; `specs/00-plan.md`
remains the source of truth for the target architecture.

## D1 Tabs

`Terminal.Gui.Editor.Editor` exposes the AvaloniaEdit-aligned tab surface directly:

- `int IndentationSize { get; set; } = 4` controls indentation stops in terminal cells. Values less than 1 are rejected.
- `bool ConvertTabsToSpaces { get; set; }` controls what the Tab key inserts. It never rewrites existing document text.
- `bool ShowTabs { get; set; }` renders a visible glyph in the first cell of each tab expansion.

`Editor.TabWidth` was removed rather than shimmed because the project is still pre-alpha and `IndentationSize`
is the planned AvaloniaEdit-compatible name.

## Rendering Pipeline Slice

The first rendering pipeline types live in `Terminal.Gui.Editor.Rendering`:

- `VisualLineBuilder`
- `CellVisualLine`
- `CellVisualLineElement`
- `TextRunElement`
- `TabElement`
- `IVisualLineTransformer`
- `IBackgroundRenderer`
- `VisualLineBuildContext`

`Editor.LineTransformers` and `Editor.BackgroundRenderers` expose the current extension points. This is the
D1-enabling slice only: cache invalidation, folding markers, line-number background rendering, and the full B1/B2
pipeline remain tracked in `specs/00-plan.md`.
