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

Requires the .NET 10 SDK (preview). Solution file is `Terminal.Gui.Editor.slnx` (XML solution format, not `.sln`).

```sh
dotnet restore Terminal.Gui.Editor.slnx
dotnet build   Terminal.Gui.Editor.slnx
```

Tests are xUnit.v3 and run as **executables** (each test project sets `<OutputType>Exe</OutputType>`). Use `dotnet run`, not `dotnet test`:

```sh
dotnet run --project tests/Terminal.Gui.Editor.Tests
dotnet run --project tests/Terminal.Gui.Editor.Tests
dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests
dotnet run --project tests/Terminal.Gui.Editor.PerformanceTests -c Release
```

The `PerformanceTests` project is stopwatch-based and only meaningful in Release. It runs in
the dedicated `.github/workflows/perf.yml` workflow (ubuntu-latest only), separately from the
correctness-focused `ci.yml`. See the "Testing tiers" section below.

Run a single test by passing xUnit.v3 filter args after `--`:

```sh
dotnet run --project tests/Terminal.Gui.Editor.Tests -- -method "*MyTestName*"
```

CI verifies formatting with `dotnet format Terminal.Gui.Editor.slnx --verify-no-changes --exclude third_party/`. Run the same locally before pushing if you've touched C# files outside `third_party/`.

### Verifying the *look* (ANSI snapshots) — MANDATORY for render changes

If a change affects what `Editor` **renders** (selection, multi-caret, highlighting, tabs,
newline glyphs, scrolling, layout) you do **not** need a human to eyeball it — and you must not
ship it unverified. Use the ANSI snapshot harness: `AnsiSnapshot.Verify (fx.Driver, name)`
captures the screen as pure ANSI (`IDriver.ToAnsi ()`), records a `__snapshots__/*.ans`
golden, and on mismatch prints the render inline plus a `.ans.actual` you `cat` to see the
exact look and self-verify. Mouse gestures go through `Inject`. **Before writing any
render/integration test, read `tests/Terminal.Gui.Editor.IntegrationTests/Testing/README.md`** —
it has the workflow, the copy-paste template, and the rules that bite (render-before-verify
ordering, `*.ans` must stay `binary` in `.gitattributes`, keep viewports small,
`UPDATE_SNAPSHOTS=1` to accept an intended change).

## Architecture

Two NuGet packages with a strict dependency direction:

- **`src/Terminal.Gui.Editor`** — UI-framework-independent document model. Namespace `Terminal.Gui.Editor` and subnamespaces. **Must not reference Terminal.Gui.** Holds the rope-backed `TextDocument`, `DocumentLine`, `TextAnchor`, `UndoStack`, `ITextSource`, `TextSegment`, the `Rope`, and supporting utility types. Lifted from AvaloniaEdit (see fork policy below) — `Document/` and `Utils/` are landed; `Folding/`, `Search/`, `Indentation/`, `Highlighting/` are follow-up phases per `specs/00-plan.md`.
- **`src/Terminal.Gui.Editor`** — the `Editor : View` and cell-grid rendering pipeline. Namespace `Terminal.Gui.Views` (matches Terminal.Gui convention, deliberately not `Terminal.Gui.Editor`). References `Terminal.Gui` (version pinned via `$(TerminalGuiVersion)` in `Directory.Build.props`) and `Terminal.Gui.Editor`. Split into partials: `Editor.cs` (core: `Document`, `CaretOffset`, edit-tracking arithmetic, content-size + scroll), `Editor.Drawing.cs` (`OnDrawingContent` + cursor positioning), `Editor.Keyboard.cs` (`OnKeyDown` switch — navigation / editing / undo+redo). No selection / folding / highlighting / multi-caret yet.
- **`examples/ted`** — standalone TG demo app exercising `Editor`. Not packed; not a NuGet artifact. Has a File menu, the `Editor` View, and a status bar; grows with the View. Run via `dotnet run --project examples/ted`.

The boundary matters: anything that takes a dependency on `Terminal.Gui` types belongs in `Terminal.Gui.Editor`, never in `Terminal.Gui.Editor`.

### Rendering pipeline

**Current** (pre-MVP): `Editor.OnDrawingContent` walks visible `DocumentLine`s directly via `Document.GetLineByNumber`, slices each line by the horizontal `Viewport.X`, and `AddStr`s. Caret math is integer offset → `(line, col)` via `Document.GetLineByOffset`; sticky virtual column preserves the user's intended col across vertical moves through short lines.

**Planned** (per `specs/00-plan.md` §6): `DocumentLine` → `VisualLineBuilder` → `CellVisualLine` (one or more `CellVisualLineElement`s). `IVisualLineTransformer`s mutate element `Attribute`s (highlighting, folding markers); `IBackgroundRenderer`s paint cell rectangles (selection, current line, search hits). All measurement is in **cells**, not pixels — use grapheme clusters and `string.GetColumns()`. AvaloniaEdit's `TextRunProperties` (typeface, brushes, font size) collapses to a single `Terminal.Gui.Attribute`. Visual lines cached + selectively invalidated from the `Document.Changed` offset+length range. Caret eventually becomes a `TextAnchor` (`AnchorMovementType.AfterInsertion`); selection a `TextSegment` of two anchors; multi-caret runs commands inside a single `Document.OpenUpdateScope ()` so undo collapses to one step.

See `specs/00-plan.md` §6 for the planned pipeline and full `Editor` public API sketch.

## AvaloniaEdit fork policy

Code is lifted from AvaloniaEdit into the relevant `src/Terminal.Gui.Editor/` subfolders (`Document/`, `Utils/` so far; `Folding/`, `Search/`, `Indentation/`, `Highlighting/` to follow). The pinned upstream commit and per-file modification log live in `third_party/AvaloniaEdit/UPSTREAM.md` (with the upstream MIT `LICENSE` alongside).

For lifted files:

- **Preserve original formatting and copyright headers.** House-style reformatting defeats the merge story.
- Add the line `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` under the original header.
- Targeted edits only: strip `using Avalonia.*`, remove `Dispatcher.UIThread.VerifyAccess ()` calls, replace `IBrush`/`Avalonia.Media.Color` with `Terminal.Gui.Color`, drop typeface/font-size from `HighlightingColor`.
- Log every modification in `third_party/AvaloniaEdit/UPSTREAM.md` along with the pinned upstream commit.

The fork is **hard** — re-syncs are manual and deliberate, triggered only by upstream fixes we want.

## Coding standards (for new code outside `third_party/`)

Adopts Terminal.Gui's house style. Three enforcement layers:

1. **`.editorconfig` + `dotnet format`** — formatting, var, expression-bodied, collection expressions, modern syntax preferences. CI runs `dotnet format --verify-no-changes`.
2. **`Terminal.Gui.Editor.slnx.DotSettings` + `dotnet jb cleanupcode`** — ReSharper-driven cleanup ("TG.Editor Full Cleanup" profile). Catches what `dotnet format` misses (XML doc spacing, using sorting, name qualifier removal, expression-bodied conversions). CI runs `dotnet jb cleanupcode` and fails on any diff.
3. **A Stop hook in `.claude/settings.json`** that runs both tools on .cs files modified during the session before the agent reports done. Output is suppressed unless the cleanup actually changed something.

**Before declaring work complete, an agent must run `dotnet tool restore && dotnet format Terminal.Gui.Editor.slnx --exclude third_party/ && dotnet jb cleanupcode Terminal.Gui.Editor.slnx --profile="TG.Editor Full Cleanup"` (the Stop hook does this automatically). If the cleanup adjusts files, those changes are part of the work — re-stage and continue.**

### Formatting and spacing

- **Space before `()` and `[]`**: `Method ()`, `array [i]`, `new ()`, `nameof (x)`, `typeof (T)`.
- **Allman braces** everywhere. No single-line `if (x) Foo ();`.
- **Blank line before** `return` / `break` / `continue` / `throw`; blank line after a control block before the next statement.
- **XML doc tags carry a space before `/>`**: `<see cref="X" />`, `<paramref name="x" />`, `<see langword="true" />`. (`dotnet jb cleanupcode` enforces this; `dotnet format` does not.)

### `var`, types, and modern syntax

- **`var` for built-ins only** (`int`, `string`, `bool`, `double`, `float`, `decimal`, `char`, `byte`). Use the explicit type for everything else (`DocumentLine line = ...`, `Rectangle viewport = ...`). The `dotnet format` rule is `:warning`, so deviation fails CI.
- **`new ()` (target-typed)** when the LHS or context makes the type obvious: `Editor editor = new ();`, `_lines = new List<int> ();`. Do **not** write `new Editor ()` on the right of `Editor editor = ...`.
- **Specify the type on `new` when it is *not* obvious from a few characters of context.** Examples where the explicit form wins: arguments to overloaded methods (`Foo (new SomeRequest { ... })`), return statements where the method's return type is far away (`return new TextSegment { ... };`), or object-initializer-only constructions used as the only argument to a generic.
- **Collection expressions `[...]`** instead of `new[] { ... }` / `new List<T> { ... }`. For `params` arrays of literals: `Foo ([1, 2, 3])`. For empty: `[]`. For spread: `[..a, ..b, x]`. Applies anywhere a collection-expression target type exists (`IEnumerable<T>`, arrays, `Span`, `ImmutableArray`, etc.).
- **Modern string features.** Prefer raw string literals `"""..."""` for any string containing escapes / quotes / multi-line content. Prefer interpolation `$"..."` over `string.Format`. Prefer `string.Create` only when measurable allocation matters. Prefer `ReadOnlySpan<char>` parameters over `string` for perf-sensitive parsers.
- **Pattern matching over null + cast**: `if (x is Foo f)` not `if (x != null && x is Foo)`; `obj is { Prop: var p }` for member extraction; switch expressions over chained `if/else if`.
- **Range/index operators**: `s[..^1]`, `arr[1..]`, not `s.Substring (0, s.Length - 1)`.

### Properties: prefer the C# 13 `field` keyword over `_backingField` + property pairs

When a property has a setter with logic (validation, equality check, side effects), use the `field` keyword:

```csharp
public int IndentationSize
{
    get;
    set
    {
        ArgumentOutOfRangeException.ThrowIfLessThan (value, 1);
        if (field == value) return;
        field = value;
        SetNeedsDraw ();
    }
} = 4;
```

Do **not** introduce a separate `private int _indentationSize;` field. The initializer trails the close-brace as shown.

**When an explicit backing field is unavoidable, declare it immediately above the property it backs — never in a field block at the top of the type.** The `field` keyword is the default and removes the question entirely; but some properties still need a real field (a lifted/forked file that preserves upstream style and does not use `field`; a field shared by several members; a field touched by `ref`/`out` or interlocked ops). In those cases the field/property pair stays visually together:

```csharp
private VisualRole? _role;

/// <summary>...</summary>
public VisualRole? Role
{
    get => _role;
    set
    {
        if (IsFrozen)
        {
            throw new InvalidOperationException ();
        }

        _role = value;
    }
}
```

Fork-policy exception: do **not** reflow a lifted file's *existing* upstream field block to satisfy this — that churns the merge story (see the AvaloniaEdit fork policy). The rule binds *new* fields you add, even inside lifted files.

> **ReSharper bug warning.** ReSharper / Rider's "Convert to auto-property" and "Use auto-property" inspections are unreliable around `field` and may rewrite intentional `field`-backed properties into broken auto-properties. The team-shared `.DotSettings` disables these inspections (`ConvertToAutoProperty`, `ConvertToAutoPropertyWhenPossible`, `ConvertToAutoPropertyWithPrivateSetter` set to `DO_NOT_SHOW`; `CSUseAutoProperty` cleanup step disabled). If you see the suggestion in your IDE, ignore it and check that your local Rider is using the team-shared settings. **`field` is not optional in this codebase.**

For trivial getters with no setter logic, plain auto-properties are fine: `public TextDocument? Document { get; private set; }`.

### Methods vs properties — body style

- **Properties / accessors / lambdas: expression-bodied** (`=>`) when the body is one expression that fits on a single line.
- **Methods / constructors / operators / local functions: block-bodied** (`{ ... }`) even if the body is a single expression. Block-bodied keeps tracebacks readable, lets debuggers set breakpoints on individual statements, and avoids churn when a future edit needs to add a second line.

```csharp
// Property — expression-bodied is fine.
public bool HasSelection => _selectionAnchor is { } a && a != _caretOffset;

// Method — block body, even for a one-liner.
private void ExtendCaretBy (int delta)
{
    ExtendCaretTo (_caretOffset + delta);
}
```

### Control flow and shape

- **Guard clauses**; never wrap the happy path in `if`. If a method's success path is wrapped in `if (cond) { ... return X; } return Y;`, **invert** to `if (!cond) return Y; ... return X;`. Example before/after lives in PR history (`OnKeyDownNotHandled`, commit `4f600ab`).
- **Return early on null/empty/invalid input.** No nested-if pyramids.
- **One return per logical branch is fine; one return per method is a non-rule** — readability wins.

### Type and file layout

- **One public or internal type per file.** No nested types except inside the file that owns the outer type, and only when the nested type is a private implementation detail (`DocumentLine.LineNode`-style). If a nested type grows interesting, promote it to its own file.
- **No file longer than 1000 lines.** When a file approaches that, split — by partial class (`Editor.Drawing.cs`, `Editor.Mouse.cs`), by helper extraction, or by genuinely splitting the type. The cleanup hook does not enforce this; the reviewer does.
- **C# 14 `extension` blocks**: prefer extension blocks over a static class full of `this`-prefixed extension methods when the extensions form a coherent group on a single receiver type.
- **Namespace per folder.** `src/Terminal.Gui.Editor/Document/` ⇒ `Terminal.Gui.Document`; `src/Terminal.Gui.Editor/Rendering/` ⇒ `Terminal.Gui.Views.Rendering`. Don't put unrelated types in the same namespace just because they share a folder.
- **No static members on `View`-derived types.** A class that derives from `Terminal.Gui.View` (e.g. `Editor`) must not declare `static` members — not fields, not properties, not events, not even "harmless" caches or lookup tables. Terminal.Gui's `Application` lifetime is per-instance (see "Testing tiers"); static state on a View is process-global, survives across `IApplication` instances, and silently couples otherwise-independent windows and parallel tests (the canonical cause of parallel-test hangs). Shared/lookup data lives in a dedicated non-View type (e.g. `XshdRoleMap`), exposed read-only (`private` + `FrozenDictionary`/`IReadOnlyXxx`), and is injected or queried — never hung off the View. `const` is the only exception (it is not state). This is a hard rule; a reviewer blocks on it.

### Testing convention

- **AI-generated tests** marked `// Claude - <model>` or `// CoPilot - <model>` at the top of the file (see `tests/Terminal.Gui.Editor.Tests/SmokeTests.cs` for the format).

## Testing tiers

Four test projects, mirroring Terminal.Gui's convention. **The correctness projects all run fully in parallel** — Terminal.Gui's `Application` lifetime is per-instance (`Application.Create()` returns an `IApplication` whose `Init`/`Begin`/`End`/`Dispose` track via `ThreadLocal<>`, not process globals). Tests must never call the static `Application.Init()` shortcut, and must never enable `ConfigurationManager` (`CM.Enable(...)`) — both reach for process-global state and would force serialization.

- `Terminal.Gui.Editor.Tests` — pure, no UI, no static state. Target ≥90% coverage. Runs in `ci.yml`.
- `Terminal.Gui.Editor.IntegrationTests` — full key-input → render scenarios via `AppFixture<T>`, which boots a per-test `IApplication` from `Application.Create()`. Parallel by default. Runs in `ci.yml`.
- `Terminal.Gui.Editor.PerformanceTests` — stopwatch-based perf smoke tests. **Release only, ubuntu-latest only.** Lives in its own project and its own workflow (`.github/workflows/perf.yml`) because Windows/macOS GitHub-hosted runners are too noisy for wall-time assertions. The BenchmarkDotNet suite in `benchmarks/` runs from the same workflow.

New tests default to the parallel-by-name project. Promote to `IntegrationTests` only when an `IApplication` (driver, input injection, full layout/draw) is genuinely needed. Promote to `PerformanceTests` only when you need a wall-time assertion — and remember it won't run on Windows/macOS CI, so don't put correctness checks there.

**The one allowed exception:** a test that legitimately mutates a process-global (e.g. `Logging.Logger`, `Trace.EnabledCategories`, anything `static`) must opt out of cross-collection parallelism via a `[CollectionDefinition(name, DisableParallelization = true)]` + `[Collection(name)]` pair. See `tests/Terminal.Gui.Editor.IntegrationTests/HostingTests.cs` for the canonical example. Do **not** add an assembly-wide `xunit.runner.json` to make the whole project serial — that's the wrong tool for one offending class.

### Performance gates

Two layers, both in `.github/workflows/perf.yml`:

1. **`Terminal.Gui.Editor.PerformanceTests`** — stopwatch smoke tests with deliberately loose thresholds (~5× typical wall time). They catch catastrophic regressions, not 10% drift.
2. **`benchmarks/compare-baseline.sh`** — runs the focused `*VisualLineBuild*` BenchmarkDotNet filter and compares against `benchmarks/baseline.json`. Fails on >3× regression, celebrates on <0.8× improvement. **Run `--job short`** (lowercase) — `ShortRun` makes BDN reject it and the comparison silently no-ops.

The full BenchmarkDotNet matrix (`Scrolling`, `EndToEndScroll`, `CaretMovement`, `DocumentAccess`) is opt-in via `workflow_dispatch` on the perf workflow with `full-suite: true`. That's the operator path for refreshing `baseline.json` — run, download artifact, commit the numbers.

### Diagnosing parallel-test hangs

When integration tests run individually but hang when run as a suite, **the cause is almost always shared mutable state, not the parallelism itself**. Do not reach for `xunit.runner.json` with `parallelizeTestCollections: false` to "fix" it — that hides the bug and slows the suite. Walk this checklist instead:

1. **Static `Application.Init()`?** Grep for `Application.Init` (without an `IApplication` receiver). It must always be `app.Init()` on an `IApplication` from `Application.Create()`. The static form is a process-global Init/Shutdown pair and serializes everything.
2. **`ConfigurationManager.Enable(...)`?** Grep for `ConfigurationManager.Enable` / `CM.Enable`. CM is a process-global config store; tests must never enable it. Enabling it from one test poisons every concurrent `Application.Create()`.
3. **Mutating TG-read process globals?** `Logging.Logger`, `Trace.EnabledCategories`, and anything `static` on `Application`/`Terminal.Gui.*` that TG itself reads during draw or lifecycle. A test that swaps these (even with try/finally restore) will deadlock or corrupt parallel tests because TG running on another test's thread reads the half-set value.
4. **A new `View` subclass touching shared state?** Subscribing to a static event, allocating from a static cache, etc.

Bisect by running test classes pairwise (`-class "*A" -class "*B"`) until you find the offending pair, then narrow by `-method`. The hang is reproducible deterministically with the right pair.

The fix is **always** to remove or isolate the global mutation — either eliminate it, or wrap the offending class in `[CollectionDefinition(..., DisableParallelization = true)]` + `[Collection(...)]` so only it serializes. The rest of the suite stays parallel.

## Non-goals

Don't accidentally do these — they were considered and rejected:

- Source/API compatibility with `Terminal.Gui.TextView`. `Editor` ships beside it, not as a replacement.
- RTL bidi or rich text shaping beyond grapheme width.
- Pixel/proportional font fidelity.
- Porting AvaloniaEdit's `Editing/`, `Rendering/`, or `CodeCompletion/` namespaces — those are Avalonia-UI-specific and replaced by TG-native equivalents (`Editor` partials, cell-grid `Rendering/`, `PopoverMenu` for completion).

## Open decisions

`specs/00-plan.md` §10 lists open design questions (line-ending policy, xshd vs TextMate for first highlighter, async I/O placement, read-only ranges, completion item shape). Resolutions go in `specs/05-decisions.md` (not yet created). If a task touches one of these, surface the decision rather than picking unilaterally.
