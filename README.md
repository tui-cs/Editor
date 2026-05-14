# Terminal.Gui.Editor

![Terminal.Gui.Editor — ted demo app](docs/images/hero.gif)

A full-featured text editor `View` for [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui), built on a hard fork of [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s pure-data layers.

Ships as a single NuGet package: **[`Terminal.Gui.Editor`](https://www.nuget.org/packages/Terminal.Gui.Editor)**.

- **Document layer** (`Terminal.Gui.Document` and sub-namespaces) — UI-framework-independent: rope-backed `TextDocument`, `TextAnchor`, `UndoStack`, `FoldingManager`, search strategies (regex / whole-word / case-sensitive), indentation strategies, and the xshd-driven highlighting engine.
- **Editor view** (`Terminal.Gui.Editor` namespace) — an `Editor : View` subclass consuming the document layer through a cell-grid rendering pipeline (`VisualLineBuilder` → `CellVisualLine`, with pluggable `IVisualLineTransformer`s and `IBackgroundRenderer`s).

`Editor` ships alongside Terminal.Gui's `TextView` — it is **not** a drop-in replacement and has no source-compat obligation to it. TG marks `TextView` as `[Obsolete]` pointing here in the release that coincides with our beta ([gui-cs/Terminal.Gui#5303](https://github.com/gui-cs/Terminal.Gui/issues/5303)).

## Features

Editing
- Typing, Backspace / Delete, Enter, with Unicode + grapheme-aware width.
- Caret backed by a `TextAnchor` (`AnchorMovementType.AfterInsertion`); sticky virtual column across vertical moves through short lines.
- Selection via Shift+arrows / Shift+Home/End / Shift+PageUp/Down / Shift+Ctrl+Home/End, plus `Ctrl+A` Select All; replace-on-typing.
- Mouse: click to position the caret, drag to select, wheel to scroll (incl. horizontal wheel), right-click context menu in `ted`.
- Clipboard: `Ctrl+X` cut, `Ctrl+C` copy, `Ctrl+V` paste — selection-aware, single-step undo, aborts cut if the clipboard write fails.
- Undo/redo with sane granularity: `Ctrl+Z` / `Ctrl+Y` (also `Ctrl+Shift+Z`). Compound operations (Enter + auto-indent, replace-all, paste over selection) collapse into one undo step via `Document.RunUpdate ()`.
- Read-only mode: `Editor.ReadOnly = true` blocks edits, undo/redo, and clipboard mutations while keeping navigation and selection live.

Indentation & tabs
- `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs` (renders a glyph in the first cell of each tab).
- `Tab` / `Shift+Tab` indent / unindent — with selection, indents the selected line range.
- Indentation-aware Backspace.
- Pluggable `IIndentationStrategy`; the default copies the previous line's leading whitespace on Enter (wrapped in a single undo group).

Rendering pipeline
- Cell-grid pipeline: `VisualLineBuilder` → `CellVisualLine` → `CellVisualLineElement` (`TextRunElement`, `TabElement`).
- Pluggable `IVisualLineTransformer`s (syntax highlight, folding markers, …) and `IBackgroundRenderer`s (selection, current line, search hits).
- LRU visual-line caches with ranged invalidation from `Document.Changed`; incremental max-width tracking avoids the O(N) all-lines walk on every edit.
- Word wrap: `Editor.WordWrap` soft-wraps at whitespace boundaries (hard-breaks when none), continuation rows flush at column 0.

Folding
- `FoldingManager` + `FoldingTransformer` collapse / expand regions.
- Foldings auto-expand if the caret moves inside one.
- `BraceFoldingStrategy` and `XmlFoldingStrategy` included; consumers can add their own.
- `Ctrl+M` toggles the fold under the caret.
- `FoldingGutter` paints +/− indicators; click to toggle.

Gutter
- `Editor.GutterOptions` is a `[Flags]` enum: `LineNumbers | Folding`. Combine to show both.
- Gutter is a real `View` subview of `Padding`, so popovers and menus clip it correctly.

Find & Replace
- Pluggable `ISearchStrategy` — `Normal` (string), `RegEx`, `WholeWords`, with `MatchCase`.
- `Ctrl+F` raises `FindRequested`; `Ctrl+H` raises `ReplaceRequested` (ted opens its Find/Replace dialog).
- `F3` / `Shift+F3` jump to the next / previous match (wrap-around).
- `SearchHitRenderer` highlights every match in the viewport; invalidated automatically on document edits.
- `ReplaceAll` is one undo step.

Syntax highlighting
- xshd-driven `DocumentHighlighter` + `HighlightingColorizer`, installed automatically when you set `HighlightingDefinition`.
- Built-in definitions (looked up via `HighlightingManager.Instance`): C#, C++, Java, JavaScript, Python, PowerShell, TSQL, VB, JSON, HTML, XML, CSS, Markdown.
- Themes follow the active Terminal.Gui scheme; `UseThemeBackground` toggles between the highlighter's background and the TG `VisualRole.Normal` background.
- TextMate grammars are a post-beta follow-up (see [`specs/textmate-grammars/spec.md`](specs/textmate-grammars/spec.md)).

## Quickstart

Install the package (requires the .NET 10 SDK and Terminal.Gui):

```sh
dotnet add package Terminal.Gui.Editor
```

Drop the editor into a Terminal.Gui app:

```csharp
using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Editor;
using Terminal.Gui.Highlighting;
using Terminal.Gui.ViewBase;

using IApplication app = Application.Create ();
app.Init ();

Editor editor = new ()
{
    Width = Dim.Fill (),
    Height = Dim.Fill (),
    GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding,
    ConvertTabsToSpaces = true,
    HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".cs")
};

editor.Document = new TextDocument (File.ReadAllText ("Program.cs"));

Window win = new () { Title = "My Editor" };
win.Add (editor);

app.Run (win);
```

## ted — the demo app

A complete TUI editor called `ted` ships in [`examples/ted`](examples/ted): File / Edit / Options menus, find & replace dialog, language + theme selectors, line numbers, fold indicators, word-wrap toggle, tab controls, mouse, context menu, save-changes prompt. Run it on any file:

```sh
dotnet run --project examples/ted -- path/to/file.cs
dotnet run --project examples/ted -- --read-only path/to/file.cs
```

For a pre-built, production-ready editor built on this package, see [clet](https://github.com/gui-cs/clet).

## Status

**Alpha** — shipped 2026-05-12 off the `develop` rolling pre-release stream. See [`specs/plan.md`](specs/plan.md) for the beta roadmap and remaining work (multi-caret is the headline item still in flight; `[Obsolete]` on TG `TextView` lands with the beta).

## Repository layout

```
specs/        Planning and design docs (spec-kit)
src/          Terminal.Gui.Editor library (document layer + Editor view)
tests/        xUnit.v3 test projects (correctness + perf smoke)
benchmarks/   BenchmarkDotNet suite + CI baseline
examples/     ted — standalone demo app
third_party/  AvaloniaEdit fork policy + per-file modification log
```

## Build

Requires the .NET 10 SDK (preview). Solution file is `Terminal.Gui.Editor.slnx` (XML solution format).

```sh
dotnet restore Terminal.Gui.Editor.slnx
dotnet build   Terminal.Gui.Editor.slnx

# Correctness suites — run on every push/PR across ubuntu/macos/windows.
dotnet run --project tests/Terminal.Gui.Editor.Tests
dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests

# Perf smoke + BenchmarkDotNet baseline gate — ubuntu-latest only in CI
# (.github/workflows/perf.yml). Run locally in Release config.
dotnet run --project tests/Terminal.Gui.Editor.PerformanceTests -c Release
```

## License

MIT — see [`LICENSE`](LICENSE).

Portions of the document layer are adapted from [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit) (MIT). See [`third_party/AvaloniaEdit/UPSTREAM.md`](third_party/AvaloniaEdit/UPSTREAM.md) for the pinned upstream commit and per-file modification log.
