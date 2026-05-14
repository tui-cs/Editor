# Terminal.Gui.Editor

![Terminal.Gui.Editor — ted demo app](docs/images/hero.gif)

A reusable text-editing `View` for [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui). Drop it into your TUI app and you get the editing experience users already expect — caret movement, selection, clipboard, undo/redo, search & replace, folding, syntax highlighting, word wrap — without writing any of it yourself.

Ships as a single NuGet package: **[`Terminal.Gui.Editor`](https://www.nuget.org/packages/Terminal.Gui.Editor)**.

## What this is — and isn't

**This is a library.** It exists so that anyone building a Terminal.Gui application — developers, agents, tool authors — can embed a competent multi-line text editor into their UI with a couple of lines of code. The audience is people who need an *edit field that can hold code or prose*, not people shopping for a standalone editor.

Think of `Editor` the way you'd think of a rich-text control in a desktop framework: a building block for the editing surface inside your app — a script box in an IDE-like tool, a config pane in a TUI dashboard, a chat composer, a notes view, the body of a code-review widget, the input area of an LLM agent's terminal front-end. Whatever it is you're building, `Editor` is the part that handles "user types and edits text."

**This is not a standalone editor and not trying to be one.** It is not a competitor to vim, Emacs, Helix, micro, nano, or any other terminal editor — those are products with their own ecosystems, configs, and communities, and the world doesn't need another one. The `ted` app in this repo is a demo of the View, not a product. A real end-user editor built on top of this library — [clet](https://github.com/gui-cs/clet) — lives in its own repo.

It also isn't a replacement for Terminal.Gui's existing `TextView`. `Editor` ships alongside it. TG marks `TextView` as `[Obsolete]` pointing here in the release that coincides with our beta ([gui-cs/Terminal.Gui#5303](https://github.com/gui-cs/Terminal.Gui/issues/5303)), but there is no source-compat bridge — pick `Editor` when you want the richer surface, keep `TextView` when you don't.

## What's in the box

- **Document layer** (`Terminal.Gui.Document` and sub-namespaces) — UI-framework-independent: rope-backed `TextDocument`, `TextAnchor`, `UndoStack`, `FoldingManager`, search strategies (regex / whole-word / case-sensitive), indentation strategies, and the xshd-driven highlighting engine. Adapted from [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s pure-data layers.
- **Editor view** (`Terminal.Gui.Editor` namespace) — an `Editor : View` subclass consuming the document layer through a cell-grid rendering pipeline (`VisualLineBuilder` → `CellVisualLine`, with pluggable `IVisualLineTransformer`s and `IBackgroundRenderer`s for consumers that want to layer their own visual behaviour on top).

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

## ted — the reference app

[`examples/ted`](examples/ted) is a reference TUI app showing how to wire `Editor` into a real window: menubar + editor + status bar, File / Edit / Options menus, find & replace dialog, language + theme selectors, line numbers, fold indicators, word-wrap toggle, tab controls, mouse, context menu, save-changes prompt. It's a *demo of the View*, not a product — its job is to make every feature reachable so you can see what's available before pulling the package into your own app.

Run it against any file:

```sh
dotnet run --project examples/ted -- path/to/file.cs
dotnet run --project examples/ted -- --read-only path/to/file.cs
```

For a user-facing editor built on this library, see [clet](https://github.com/gui-cs/clet).

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
