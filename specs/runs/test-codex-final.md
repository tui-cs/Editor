# Codex D1 Tabs Final Report

## What I did

- Chose option (b): implemented the slice of the VisualLineBuilder pipeline needed for D1, then implemented tab handling on top of it.
- Added `Terminal.Gui.Views.Rendering` types: `VisualLineBuilder`, `CellVisualLine`, `CellVisualLineElement`, `TextRunElement`, `TabElement`, `IVisualLineTransformer`, `IBackgroundRenderer`, and build context.
- Replaced `Editor.TabWidth` with `Editor.IndentationSize`, and added `ConvertTabsToSpaces`, `ShowTabs`, `LineTransformers`, and `BackgroundRenderers`.
- Moved visible line text drawing through grapheme-aware visual-line elements. `OnDrawingContent` no longer expands `\t` directly or walks rendered text char-by-char.
- Implemented Tab, Shift+Tab, block indent/unindent, and indentation-aware Backspace.
- Updated mouse tab hit-testing to snap to the nearest logical tab edge, with midpoint rounding before the tab.
- Updated ted: status bar uses `Indent Size`, adds a `ShowTabs` checkbox, and Options has `_Convert Tabs To Spaces`.
- Added/updated tests for visual-line tab expansion, grapheme widths, Tab insertion modes, block indent undo grouping, Shift+Tab, indentation Backspace, mouse snapping, ted UI, and tab round trip.
- Added `specs/03-public-api.md` notes for the new `Editor` tab surface and rendering pipeline slice.

## What I skipped

- I did not implement all of B1 as specified in `specs/00-plan.md`: there is no visual-line cache/invalidation, no folding marker element, and no full background-renderer migration for line numbers.
- I did not implement `IIndentationStrategy` / `DefaultIndentationStrategy`; that belongs to A3/D7 and is not present in this checkout.
- I did not migrate line numbers from `OnDrawComplete` to an `IBackgroundRenderer`; that is B2 scope.

## Why

D1 explicitly depends on B1, and shipping another draw-loop tab shortcut would repeat the R1/R2 violation. I implemented the smallest B1-compatible line-element layer that lets tabs be represented as a `TabElement` and makes the D1 behavior testable without taking over the whole B1/B2 work item.

## Validation

- `dotnet build Terminal.Gui.Editor.slnx`
- `dotnet run --project tests/Terminal.Gui.Editor.Tests`
- `dotnet run --project tests/Terminal.Gui.Editor.Tests`
- `dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests`
- `dotnet format Terminal.Gui.Editor.slnx --verify-no-changes --exclude third_party/`

`dotnet tool restore` succeeded. `dotnet jb cleanupcode Terminal.Gui.Editor.slnx --profile="Full Cleanup"` failed because the JetBrains CLI did not recognize the solution cleanup profile from this `.slnx` checkout. Running without the profile against the `.slnx` reported no cleanup items. I ran cleanup against the touched projects directly, which used JetBrains' built-in reformat profile.

PR CI result: macOS and Windows passed. Ubuntu failed in the existing ReSharper cleanupcode step because
`dotnet jb cleanupcode Terminal.Gui.Editor.slnx --profile="Built-in: Full Cleanup" --no-build --exclude="third_party/**/*"`
returned exit code 3 with `No items were found to cleanup.` I confirmed the current `develop` CI fails the same way
on run `25613103283`, so I decided I cannot make CI green from this PR without changing `.github/workflows/ci.yml`
or the repository-wide cleanup setup, both of which are outside the allowed edit set for this task.

## Total tokens spent

Exact token usage is not exposed inside this Codex session. My best estimate from the transcript size is roughly 120k-160k tokens.

## What I would do differently

If this were a normal feature PR instead of an experiment, I would split the rendering pipeline into a separate B1 PR first, with cache invalidation and line-number/background migration handled in follow-up B2, then land D1 as a smaller PR on top.
