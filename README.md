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

- **Document layer** (`Terminal.Gui.Document` and sub-namespaces) — UI-framework-independent: rope-backed `TextDocument`, `TextAnchor`, `UndoStack`, `FoldingManager`, search strategies, indentation strategies, and the xshd-driven highlighting engine. Adapted from [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s pure-data layers.
- **Editor view** (`Terminal.Gui.Editor` namespace) — an `Editor : View` subclass consuming the document layer through a cell-grid rendering pipeline (`VisualLineBuilder` → `CellVisualLine`, with pluggable `IVisualLineTransformer`s and `IBackgroundRenderer`s for consumers that want to layer their own visual behaviour on top).

## Inherited from Terminal.Gui

`Editor` is a `View`. Everything that's true of any other Terminal.Gui View is true here too — `Editor` doesn't reinvent input, layout, theming, or configuration; it integrates with them. If a feature feels obvious for a TG view to have, it's almost certainly already wired up.

- **Command-based input.** Editing actions are `Command` values (`Command.Cut`, `Command.Copy`, `Command.NewLine`, `Command.Undo`, `Command.Collapse`, …) — keys are bound to commands, not hard-coded. Every binding mentioned below is a *default* that consumers can remap. Mouse actions, scroll wheels, and shortcut activation all flow through the same command pipeline.
- **ConfigurationManager.** Defaults — keybindings, scheme selection, theme — are JSON-configurable per-app and per-user. A consumer can ship a config file, or expose UI that writes one, and `Editor`'s defaults follow along without code changes.
- **Themes / Schemes / VisualRoles.** Colors come from the active TG `Scheme` and its `VisualRole`s. Switch themes (Dark+, Light+, Solarized, custom) at runtime via the Configuration Manager and `Editor` reflows immediately. Syntax-highlight tokens layer on top via `UseThemeBackground` (keep the highlighter's background, or compose it under the TG scheme's `Normal` background).
- **Layout.** `Pos` / `Dim` constraint-based layout — `editor.Y = Pos.Bottom (menu); editor.Width = Dim.Fill (); editor.Height = Dim.Fill (statusBar);`. Anchor, fill, center, stack the editor like any other view.
- **Padding / Border / Margin.** Standard `Adornment`s. The line-number + folding gutter is itself a `View` hosted inside `Padding`, which is why popovers and menus clip it correctly.
- **Scrollbars.** Set `ViewportSettings = ViewportSettingsFlags.HasScrollBars` and TG draws and drives them; `Editor` reports its content size so the bars track correctly.
- **Popovers, menus, dialogs.** `MenuBar`, `StatusBar`, `PopoverMenu`, `Dialog` (see `ted`'s context menu, file dialogs, find/replace dialog) are TG primitives that `Editor` cooperates with — context menus reuse the same `Command` bindings as the keyboard.
- **Clipboard, mouse, focus.** TG's `IClipboard` works on every driver; TG mouse handling delivers click / drag / wheel (incl. horizontal wheel) events the editor turns into caret moves / selection / scroll.
- **Localization.** `Strings.menuFile`, `Strings.btnOk`, etc. come from TG's `Resources` — `ted`'s UI strings are already localizable through the standard TG mechanism.

The defaults below are the ones `Editor` registers when you instantiate it. None of them are mandatory — override `DefaultKeyBindings`, plug your own `Command` handlers, or point ConfigurationManager at a different keymap and the editor follows.

## Features

### Editing

- Typing, Backspace / Delete, Enter, with Unicode + grapheme-aware width.
- Caret backed by a `TextAnchor` (`AnchorMovementType.AfterInsertion`); sticky virtual column across vertical moves through short lines.
- Selection: any TG-bound "extend" movement command (`Command.LeftExtend`, `RightEndExtend`, `PageDownExtend`, …) extends the selection; `Command.SelectAll` selects all; typing or paste replaces selection.
- Clipboard: `Command.Cut` / `Command.Copy` / `Command.Paste` — selection-aware, single-step undo, aborts cut if the clipboard write fails. Uses TG's `IClipboard`, so cut/copy/paste interoperates with whatever the OS clipboard contains.
- Undo / redo with sane granularity (`Command.Undo`, `Command.Redo`). Compound operations (Enter + auto-indent, replace-all, paste over selection) collapse into one undo step via `Document.RunUpdate ()`.
- Read-only mode: `Editor.ReadOnly = true` blocks edits, undo/redo, and clipboard mutations while keeping navigation and selection live.

### Indentation & tabs

- `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs` (renders a glyph in the first cell of each tab) — all bindable, all observable from the consumer's UI.
- `Tab` / `Shift+Tab` indent / unindent. With a selection, indents the selected line range.
- Indentation-aware Backspace.
- Pluggable `IIndentationStrategy`; the default copies the previous line's leading whitespace on Enter (wrapped in a single undo group).

### Rendering pipeline

- Cell-grid pipeline: `VisualLineBuilder` → `CellVisualLine` → `CellVisualLineElement` (`TextRunElement`, `TabElement`).
- Pluggable `IVisualLineTransformer`s (syntax highlight, folding markers, …) and `IBackgroundRenderer`s (selection, current line, search hits). Consumers can layer their own transformers on top.
- LRU visual-line caches with ranged invalidation from `Document.Changed`; incremental max-width tracking avoids the O(N) all-lines walk on every edit.
- Word wrap: `Editor.WordWrap` soft-wraps at whitespace boundaries (hard-breaks when none), continuation rows flush at column 0.

### Folding

- `FoldingManager` + `FoldingTransformer` collapse / expand regions.
- Foldings auto-expand if the caret moves inside one.
- `BraceFoldingStrategy` and `XmlFoldingStrategy` included; consumers can add their own.
- `Command.Collapse` toggles the fold under the caret.
- `FoldingGutter` paints +/− indicators; click to toggle.

### Gutter

- `Editor.GutterOptions` is a `[Flags]` enum: `LineNumbers | Folding`. Combine to show both, or hide both.
- Gutter is a real `View` subview of `Padding` (not painted by hand), so it composes with TG popovers, menus, and focus traversal.

### Find & Replace

- Pluggable `ISearchStrategy` — `Normal` (string), `RegEx`, `WholeWords`, with `MatchCase`. Construct via `SearchStrategyFactory.Create` or assign your own.
- `FindRequested` / `ReplaceRequested` events fire so consumers can open whatever Find UI they want (ted opens a `Dialog`-based form; an agent-driven app could open something else, or skip the UI entirely and drive the API directly).
- Forward / backward navigation through hits with wrap-around.
- `SearchHitRenderer` highlights every match in the viewport; invalidated automatically on document edits.
- `ReplaceAll` is one undo step.

### Syntax highlighting

- xshd-driven `DocumentHighlighter` + `HighlightingColorizer`, installed automatically when you set `HighlightingDefinition`.
- Built-in definitions (looked up via `HighlightingManager.Instance`): C#, C++, Java, JavaScript, Python, PowerShell, TSQL, VB, JSON, HTML, XML, CSS, Markdown.
- Highlight colors compose with the active TG `Scheme` — pick a theme via the Configuration Manager and the editor follows.
- TextMate grammars are a post-beta follow-up (see [`specs/textmate-grammars/spec.md`](specs/textmate-grammars/spec.md)).

### Default keybindings

These are the *defaults*. They are `Command`-bound and remappable via TG's `KeyBindings` API or ConfigurationManager.

| Command | Default key | Notes |
|---|---|---|
| `Command.Cut` / `Copy` / `Paste` | `Ctrl+X` / `Ctrl+C` / `Ctrl+V` | TG `IClipboard` |
| `Command.Undo` / `Redo` | `Ctrl+Z` / `Ctrl+Y` (also `Ctrl+Shift+Z`) | |
| `Command.SelectAll` | `Ctrl+A` | TG base layer |
| `Command.NewLine` | `Enter` | Auto-indents if a strategy is installed |
| `Command.DeleteCharLeft` / `DeleteCharRight` | `Backspace` / `Delete` | |
| `Command.Start` / `End` | `Ctrl+Home` / `Ctrl+End` | |
| `Command.LeftStart` / `RightEnd` | `Home` / `End` | TG base layer |
| `Command.Up` / `Down` / `Left` / `Right` / `PageUp` / `PageDown` | arrows + PgUp/PgDn | TG base layer |
| `Command.*Extend` variants | Shift+ above | Selection-extending |
| `Command.Collapse` | `Ctrl+M` | Toggle fold under caret |
| Find / FindNext / FindPrev / Replace | `Ctrl+F` / `F3` / `Shift+F3` / `Ctrl+H` | `Ctrl+F` / `Ctrl+H` raise events for the consumer's UI |
| Indent / Unindent | `Tab` / `Shift+Tab` | Range-aware with selection |
| Scroll | mouse wheel (incl. horizontal) | Bubbles up from gutter subviews |

Override at the consumer level with the standard TG pattern:

```csharp
editor.KeyBindings.Remove (Key.X.WithCtrl);
editor.KeyBindings.Add (Key.W.WithCtrl, Command.Cut);
```

…or ship a Configuration Manager JSON profile that does the same.

## Quickstart

Install the package (requires the .NET 10 SDK and Terminal.Gui):

```sh
dotnet add package Terminal.Gui.Editor
```

Drop the editor into a Terminal.Gui app — `Editor` is just a `View`, so it gets TG's layout, scheme, scrollbars, and Configuration Manager for free:

```csharp
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Document;
using Terminal.Gui.Editor;
using Terminal.Gui.Highlighting;
using Terminal.Gui.ViewBase;

// Pick up themes / keymaps / preferences from the user's TG config.
ConfigurationManager.Enable (ConfigLocations.All);

using IApplication app = Application.Create ();
app.Init ();

Editor editor = new ()
{
    // TG constraint-based layout — anchor below a menubar, fill above a status bar.
    X = 0,
    Y = Pos.Bottom (menu),
    Width = Dim.Fill (),
    Height = Dim.Fill (statusBar),

    // TG scrollbars come free with this flag.
    ViewportSettings = ViewportSettingsFlags.HasScrollBars,

    GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding,
    ConvertTabsToSpaces = true,
    HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".cs")
};

editor.Document = new TextDocument (File.ReadAllText ("Program.cs"));

Window win = new () { Title = "My Editor" };
win.Add (menu, editor, statusBar);

app.Run (win);
```

See [`examples/ted/TedApp.cs`](examples/ted/TedApp.cs) for the full version — menus, status bar, find/replace dialog, theme dropdown, all wired through standard TG primitives.

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
