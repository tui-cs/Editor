# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

Pre-alpha. The document layer (rope-backed `TextDocument` and supporting types from AvaloniaEdit) is landed and tested. `Editor : View` consumes it and supports keyboard-driven editing (caret nav, insert, delete, backspace, Enter, undo/redo) plus scrolling. Still pending per `specs/00-plan.md`: selection, multi-caret, folding, search, indentation strategies, syntax highlighting, the `VisualLineBuilder` / transformer / background-renderer pipeline. When adding code, follow the plan; don't invent alternative architectures without updating the spec.

## Branch workflow

Active development happens on **`develop`**. `main` is the release/stable branch.

- Work on `develop`. During pre-alpha, direct commits and pushes to `develop` are allowed — no PRs required for routine work.
- Do not push directly to `main`. Promotion from `develop` to `main` is a deliberate release step.
- Two paths trigger `.github/workflows/release.yml`, which builds + tests cross-platform, then packs and pushes both NuGet packages:
  - **Push a `v*` tag** (e.g. `v2.1.0`) — canonical stable release; version = tag minus leading `v`.
  - **Push to `develop`** — rolling pre-release; version = `<Version>` from `Directory.Build.props` + `.${github.run_number}`. With base `2.1.1-develop`, the first run publishes `2.1.1-develop.1`, etc.
  - `workflow_dispatch` is also available with a verbatim version input.

## Versioning

`Directory.Build.props` holds a single `<Version>` shared by both packages. Track Terminal.Gui's version stream — when the latest stable Terminal.Gui is `X.Y.Z`, our develop base is the next-patch pre-release (e.g. TG 2.1.0 → our base `2.1.1-develop`). Bump the base when TG ships a new stable, not on every commit. The `.${run_number}` suffix is the per-build counter, applied automatically by the workflow.

`<TerminalGuiVersion>` (also in `Directory.Build.props`) pins the Terminal.Gui dependency. Bump it when the project is ready to consume a new TG release; CI/release workflows can override via `-p:TerminalGuiVersion=<x>` if needed.

## Build and test

Requires the .NET 10 SDK (preview). Solution file is `Terminal.Gui.Text.slnx` (XML solution format, not `.sln`).

```sh
dotnet restore Terminal.Gui.Text.slnx
dotnet build   Terminal.Gui.Text.slnx
```

Tests are xUnit.v3 and run as **executables** (each test project sets `<OutputType>Exe</OutputType>`). Use `dotnet run`, not `dotnet test`:

```sh
dotnet run --project tests/Terminal.Gui.Text.Tests
dotnet run --project tests/Terminal.Gui.Editor.Tests
dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests
```

Run a single test by passing xUnit.v3 filter args after `--`:

```sh
dotnet run --project tests/Terminal.Gui.Text.Tests -- -method "*MyTestName*"
```

CI verifies formatting with `dotnet format Terminal.Gui.Text.slnx --verify-no-changes --exclude third_party/`. Run the same locally before pushing if you've touched C# files outside `third_party/`.

## Architecture

Two NuGet packages with a strict dependency direction:

- **`src/Terminal.Gui.Text`** — UI-framework-independent document model. Namespace `Terminal.Gui.Text` and subnamespaces. **Must not reference Terminal.Gui.** Holds the rope-backed `TextDocument`, `DocumentLine`, `TextAnchor`, `UndoStack`, `ITextSource`, `TextSegment`, the `Rope`, and supporting utility types. Lifted from AvaloniaEdit (see fork policy below) — `Document/` and `Utils/` are landed; `Folding/`, `Search/`, `Indentation/`, `Highlighting/` are follow-up phases per `specs/00-plan.md`.
- **`src/Terminal.Gui.Editor`** — the `Editor : View` and cell-grid rendering pipeline. Namespace `Terminal.Gui.Views` (matches Terminal.Gui convention, deliberately not `Terminal.Gui.Editor`). References `Terminal.Gui` (version pinned via `$(TerminalGuiVersion)` in `Directory.Build.props`) and `Terminal.Gui.Text`. Split into partials: `Editor.cs` (core: `Document`, `CaretOffset`, edit-tracking arithmetic, content-size + scroll), `Editor.Drawing.cs` (`OnDrawingContent` + cursor positioning), `Editor.Keyboard.cs` (`OnKeyDown` switch — navigation / editing / undo+redo). No selection / folding / highlighting / multi-caret yet.
- **`examples/ted`** — standalone TG demo app exercising `Editor`. Not packed; not a NuGet artifact. Has a File menu, the `Editor` View, and a status bar; grows with the View. Run via `dotnet run --project examples/ted`.

The boundary matters: anything that takes a dependency on `Terminal.Gui` types belongs in `Terminal.Gui.Editor`, never in `Terminal.Gui.Text`.

### Rendering pipeline

**Current** (pre-MVP): `Editor.OnDrawingContent` walks visible `DocumentLine`s directly via `Document.GetLineByNumber`, slices each line by the horizontal `Viewport.X`, and `AddStr`s. Caret math is integer offset → `(line, col)` via `Document.GetLineByOffset`; sticky virtual column preserves the user's intended col across vertical moves through short lines.

**Planned** (per `specs/00-plan.md` §6): `DocumentLine` → `VisualLineBuilder` → `CellVisualLine` (one or more `CellVisualLineElement`s). `IVisualLineTransformer`s mutate element `Attribute`s (highlighting, folding markers); `IBackgroundRenderer`s paint cell rectangles (selection, current line, search hits). All measurement is in **cells**, not pixels — use grapheme clusters and `string.GetColumns()`. AvaloniaEdit's `TextRunProperties` (typeface, brushes, font size) collapses to a single `Terminal.Gui.Attribute`. Visual lines cached + selectively invalidated from the `Document.Changed` offset+length range. Caret eventually becomes a `TextAnchor` (`AnchorMovementType.AfterInsertion`); selection a `TextSegment` of two anchors; multi-caret runs commands inside a single `Document.OpenUpdateScope ()` so undo collapses to one step.

See `specs/00-plan.md` §6 for the planned pipeline and full `Editor` public API sketch.

## AvaloniaEdit fork policy

Code is lifted from AvaloniaEdit into the relevant `src/Terminal.Gui.Text/` subfolders (`Document/`, `Utils/` so far; `Folding/`, `Search/`, `Indentation/`, `Highlighting/` to follow). The pinned upstream commit and per-file modification log live in `third_party/AvaloniaEdit/UPSTREAM.md` (with the upstream MIT `LICENSE` alongside).

For lifted files:

- **Preserve original formatting and copyright headers.** House-style reformatting defeats the merge story.
- Add the line `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` under the original header.
- Targeted edits only: strip `using Avalonia.*`, remove `Dispatcher.UIThread.VerifyAccess ()` calls, replace `IBrush`/`Avalonia.Media.Color` with `Terminal.Gui.Color`, drop typeface/font-size from `HighlightingColor`.
- Log every modification in `third_party/AvaloniaEdit/UPSTREAM.md` along with the pinned upstream commit.

The fork is **hard** — re-syncs are manual and deliberate, triggered only by upstream fixes we want.

## Coding standards (for new code outside `third_party/`)

Adopts Terminal.Gui's house style. Enforced by `.editorconfig`; the highlights:

- **Space before `()` and `[]`**: `Method ()`, `array [i]`, `new ()`.
- **Allman braces** everywhere.
- **Blank line before** `return` / `break` / `continue` / `throw`; blank line after control blocks.
- **No `var` except for built-ins** (`int`, `string`, `bool`, `double`, `float`, `decimal`, `char`, `byte`).
- **`new ()`** not `new TypeName ()` when the type is inferable.
- **Collection expressions**: `[...]` not `new () { ... }`.
- **Guard clauses**; never wrap the happy path in `if`.
- **One public/internal type per file.**
- **Subscribe to `-ed` events, not `-ing`, unless you actually cancel.** `button.Accepted += ...` for fire-and-forget side-effects; `button.Accepting += ...` only when the handler reads / sets `e.Cancel` or `e.Handled`. Same rule for every other paired event (`Selecting`/`Selected`, etc.). See `specs/00-plan.md` R10.
- **No unused public/internal APIs.** Every public/internal member in `src/` must have a non-test caller in `src/` or `examples/`. If you add a method, wire it in the same PR; otherwise delete it. Tests don't count as a consumer. See `specs/00-plan.md` R9.
- **AI-generated tests** marked `// Claude - <model>` or `// CoPilot - <model>` at the top of the file (see `tests/Terminal.Gui.Text.Tests/SmokeTests.cs` for the format).

## Testing tiers

Three test projects, mirroring Terminal.Gui's convention:

- `Terminal.Gui.Text.Tests` — pure, parallelizable, no UI, no static state. Target ≥90% coverage.
- `Terminal.Gui.Editor.Tests` — parallelizable where possible. Visual-line builder, wrap, caret/selection math, command handlers — anything that doesn't need `Application.Init`. Target ≥75%.
- `Terminal.Gui.Editor.IntegrationTests` — non-parallel, requires `Application.Init`-style setup; full key-input → render scenarios.

New tests default to the parallel project. Promote to `IntegrationTests` only when `Application.Init` or similar global state is genuinely needed.

## Non-goals

Don't accidentally do these — they were considered and rejected:

- Source/API compatibility with `Terminal.Gui.TextView`. `Editor` ships beside it, not as a replacement.
- RTL bidi or rich text shaping beyond grapheme width.
- Pixel/proportional font fidelity.
- Porting AvaloniaEdit's `Editing/`, `Rendering/`, or `CodeCompletion/` namespaces — those are Avalonia-UI-specific and replaced by TG-native equivalents (`Editor` partials, cell-grid `Rendering/`, `PopoverMenu` for completion).

## Open decisions

`specs/00-plan.md` §10 lists open design questions (line-ending policy, xshd vs TextMate for first highlighter, async I/O placement, read-only ranges, completion item shape). Resolutions go in `specs/05-decisions.md` (not yet created). If a task touches one of these, surface the decision rather than picking unilaterally.
