# gui-cs/Text — Implementation Plan

## 1. Purpose & Scope

`gui-cs/Text` provides a terminal-native text-editing stack for [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui), built on a hard fork of [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s pure-data layers (Document, Folding, Search, Indentation, Highlighting). It ships:

1. **`Terminal.Gui.Text`** — a UI-framework-independent document model: rope-backed `TextDocument`, `TextAnchor`, `UndoStack`, `FoldingManager`, `SearchStrategy`, `Highlighting`. Reusable outside Terminal.Gui.
2. **`Terminal.Gui.Editor`** — a `View` subclass (`Editor`) consuming `Terminal.Gui.Text` and rendering on a cell grid, with multi-caret, folding, search, optional TextMate highlighting.

`Editor` is **not** a replacement for `TextView` and has **no source-compat obligation** to it. Both ship side by side.

## 2. Non-Goals

- Backwards compatibility with `Terminal.Gui.TextView`. Different API, different model.
- Rich-text editing (RTL bidi, complex shaping beyond grapheme width). Terminal cell grid is the constraint.
- Pixel/proportional font fidelity. Width is measured in cells via `string.GetColumns()`.
- Ports of AvaloniaEdit's `Editing/`, `Rendering/`, `CodeCompletion/` namespaces — those are Avalonia-UI-specific. Replaced with TG-native equivalents.

## 3. Repository Layout

```
/specs/                           # this folder; planning + design docs
  00-plan.md                      # this file
  01-fork-strategy.md             # AvaloniaEdit fork policy & attribution
  02-rendering.md                 # cell-grid pipeline deep dive
  03-public-api.md                # Editor surface & doc-layer API
  04-testing.md                   # test strategy & coverage targets
  05-decisions.md                 # decision log

/src/
  Terminal.Gui.Text/              # UI-independent document layer
    Document/
    Folding/
    Search/
    Indentation/
    Highlighting/
    Utils/
    Terminal.Gui.Text.csproj      # net10.0, no Terminal.Gui ref
  Terminal.Gui.Editor/            # the View
    Editor.cs
    Editor.Commands.cs
    Editor.Keyboard.cs
    Editor.Mouse.cs
    Editor.Drawing.cs
    Editor.Caret.cs
    Editor.Selection.cs
    Editor.Scrolling.cs
    Editor.Completion.cs
    Rendering/
      VisualLineBuilder.cs
      CellVisualLine.cs
      CellVisualLineElement.cs
      WordWrapStrategy.cs
      IVisualLineTransformer.cs
      IBackgroundRenderer.cs
      SelectionRenderer.cs
      CurrentLineRenderer.cs
      SearchHitRenderer.cs
      LineNumberMargin.cs
    Terminal.Gui.Editor.csproj    # references Terminal.Gui + Terminal.Gui.Text

/tests/
  Terminal.Gui.Text.Tests/        # parallelizable, pure xUnit
  Terminal.Gui.Editor.Tests/      # parallelizable where possible
  Terminal.Gui.Editor.IntegrationTests/

/examples/
  EditorDemo/                     # standalone TG app exercising Editor
  EditorBenchmarks/

/third_party/
  AvaloniaEdit/
    LICENSE                       # MIT, verbatim
    UPSTREAM.md                   # commit SHA, sync notes, modified-files list
```

Two NuGet packages: `Terminal.Gui.Text` and `Terminal.Gui.Editor`. `Terminal.Gui.Text` has no Terminal.Gui dependency and can be consumed by other front-ends.

## 4. Coding Standards

Adopt Terminal.Gui's house style verbatim — see `gui-cs/Terminal.Gui/.claude/rules/`:

- Space before `()` and `[]`: `Method ()`, `array [i]`.
- Allman braces.
- Blank line before `return`/`break`/`continue`/`throw`; blank line after control blocks.
- No `var` except for built-ins (`int`, `string`, `bool`, `double`, `float`, `decimal`, `char`, `byte`).
- `new ()` not `new TypeName ()`.
- Collection expressions: `[...]` not `new () { ... }`.
- Guard clauses; never wrap the happy path.
- One public/internal type per file.
- AI-generated tests marked `// Claude - <model>` or `// CoPilot - <model>`.

**Exception inside `/third_party/`-derived files:** preserve upstream formatting and copyright headers as-is. Reformatting upstream code defeats the merge story. New code in `Terminal.Gui.Text` namespaces but not lifted from upstream follows house style.

## 5. AvaloniaEdit Fork Strategy

### What we lift (verbatim modulo namespace + targeted edits)

| Folder | Why | Edits required |
|---|---|---|
| `Document/` | Rope, `TextDocument`, `DocumentLine`, `TextAnchor`, `UndoStack`, `ISegment`, `TextSegment`, `ITextSource` | Strip `using Avalonia.*`; remove `Dispatcher.UIThread.VerifyAccess ()`; replace Avalonia `WeakReference` helpers with BCL |
| `Folding/` | `FoldingManager`, `FoldingSection` | Strip Avalonia deps; pure model retained |
| `Search/` | `ISearchStrategy`, `RegexSearchStrategy`, `ISearchResult` | None — pure |
| `Indentation/` | `DefaultIndentationStrategy` and interface | None |
| `Highlighting/` | `HighlightingManager`, `IHighlighter`, xshd parser, `DocumentHighlighter` | Replace `IBrush`/`Avalonia.Media.Color` with `Terminal.Gui.Color`; drop typeface/font-size from `HighlightingColor` (terminal has neither) |
| `Utils/` | `ImmutableStack`, `Deque`, `NullSafeStringComparer` | Strip Avalonia deps |

### What we replace

| AvaloniaEdit | Replacement |
|---|---|
| `Editing/TextArea`, `Caret`, `Selection` | `Terminal.Gui.Editor.Editor` + partials |
| `Rendering/TextView`, `VisualLine`, `VisualLineElement`, line transformers | `Rendering/VisualLineBuilder`, `CellVisualLine`, `CellVisualLineElement`, `IVisualLineTransformer` (cell-grid) |
| `CodeCompletion/CompletionWindow` | TG `PopoverMenu` adapter (`Editor.Completion.cs`) |

### Attribution

- `LICENSE.AvaloniaEdit` at repo root (MIT, verbatim).
- Each lifted file keeps original copyright header, plus a single line: `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>`.
- `third_party/AvaloniaEdit/UPSTREAM.md` records the pinned upstream commit, every modified file, and the rationale for each modification. Re-sync is a manual operation against this log.

### Sync policy

Hard fork. We pin to a specific upstream commit and re-sync deliberately, not continuously. Cell-grid changes will diverge over time; chasing upstream drift would dominate the project. Re-sync is triggered only by upstream bug fixes we actively want.

## 6. Architecture

### 6.1 Document layer (lifted)

`TextDocument` is offset-primary, line-secondary. Anchors survive edits. Edits raise `Changing`/`Changed` with offset + removal length + inserted text. `UndoStack` groups via `BeginUpdate`/`EndUpdate`.

No changes to AvaloniaEdit's contract here. We use it as-shipped.

### 6.2 Editor public API (sketch)

```csharp
namespace Terminal.Gui.Views;

public class Editor : View
{
    public TextDocument Document { get; set; }

    public TextSegment Selection { get; set; }

    public int CaretOffset { get; set; }

    public IReadOnlyList<int> AdditionalCaretOffsets { get; }

    public bool ReadOnly { get; set; }

    public bool WordWrap { get; set; }

    public bool ShowLineNumbers { get; set; }

    public int TabSize { get; set; } = 4;

    public bool ConvertTabsToSpaces { get; set; }

    public IIndentationStrategy IndentationStrategy { get; set; }

    public IList<IVisualLineTransformer> LineTransformers { get; }

    public IList<IBackgroundRenderer> BackgroundRenderers { get; }

    public FoldingManager? FoldingManager { get; set; }

    public ISearchStrategy? SearchStrategy { get; set; }

    public IEditorCompletionProvider? CompletionProvider { get; set; }

    public event EventHandler<DocumentChangeEventArgs>? DocumentChanged;

    public event EventHandler? CaretChanged;

    public event EventHandler? SelectionChanged;
}
```

### 6.3 Cell-grid rendering pipeline

```
DocumentLine ──▶ VisualLineBuilder ──▶ CellVisualLine
                       │                    ├── CellVisualLineElement (text run)
                       │                    ├── CellVisualLineElement (folding marker)
                       │                    └── CellVisualLineElement (text run)
                       │
                       ├── IVisualLineTransformer[]   ── set Attribute on element ranges
                       └── WordWrapStrategy           ── splits into wrap segments

Editor.OnDrawingContent:
    for each visible CellVisualLine:
        for each IBackgroundRenderer: paint cell rectangles
        for each CellVisualLineElement:
            walk graphemes via GraphemeHelper.GetGraphemes ()
            AddStr (col, row, grapheme) with current Attribute
            advance col by string.GetColumns ()
```

Three rules govern this:

1. **Cells, not pixels.** All measurement uses grapheme cluster + `string.GetColumns()`. AvaloniaEdit's `TextRunProperties` (typeface, brushes, font size) collapses to `Terminal.Gui.Attribute`.
2. **Visual lines are cached and selectively invalidated.** `Document.Changed` carries an offset+length range; only `CellVisualLine`s whose `DocumentLine` intersects get rebuilt.
3. **Transformers map 1:1 to AvaloniaEdit element generators.** A `HighlightingColorizer` listens to a `DocumentHighlighter` and sets `Attribute` on text-run elements. A `FoldingTransformer` swaps a folded range for a marker element.

### 6.4 Caret & selection

- Caret is a `TextAnchor` with `MovementType = AnchorMovementType.AfterInsertion`. `View.Cursor` is positioned each frame from caret offset → visual line → cell column.
- Selection is a `TextSegment` of two anchors. Editing across the selection is naturally consistent because anchors track edits.
- Multi-caret is a list of `TextAnchor`s. Commands operate over the list inside one `Document.OpenUpdateScope ()` so undo collapses to a single step.

### 6.5 Word wrap

`WordWrapStrategy` walks grapheme clusters, accumulates column count, breaks at the last whitespace boundary that fits, falls back to hard break on long unbroken runs. Output is a list of `(documentLine, startOffset, endOffset, leadingIndent)` wrap segments. Caret-position and selection-rendering both consult the strategy via `OffsetToVisualPosition` / inverse.

### 6.6 Highlighting

Two paths:

- **xshd** (lifted, low effort) — AvaloniaEdit's native format, regex-based, ships with definitions for ~20 languages.
- **TextMate** (port of `AvaloniaEdit.TextMate`, ~2 weeks extra) — far broader grammar coverage; most modern editors use it. Maps `tmTheme` colors to `Terminal.Gui.Color`. Worth doing post-MVP.

A `HighlightingColorizer : IVisualLineTransformer` is the seam in both cases.

### 6.7 Folding

`FoldingManager` is lifted unchanged. A `FoldingTransformer : IVisualLineTransformer` replaces folded segments with a single `CellVisualLineElement` rendering `"⋯"` (configurable). Click on the line-number margin toggles a `FoldingSection`.

### 6.8 Search

`SearchStrategy` lifted. Search hits surface as `IBackgroundRenderer` cell-rectangle highlights plus a list of result anchors `Editor` exposes for navigation.

### 6.9 Completion

TG-native via `PopoverMenu`. `IEditorCompletionProvider` returns ranked items for a given offset; `Editor.Completion.cs` shows the popover, handles selection, inserts on commit. No port of AvaloniaEdit's `CompletionWindow`.

## 7. Phased Milestones

| Phase | Deliverable | Estimate |
|---|---|---|
| 0 | Repo bootstrap: solution, two csproj, CI, this spec set | 2 days |
| 1 | Lift `Document/` + `Utils/`; tests cover rope, anchors, undo, line tracker | 1 week |
| 2 | `VisualLineBuilder`, `WordWrapStrategy`, `Editor.Drawing.cs` rendering plain text | 1.5 weeks |
| 3 | Caret, selection (anchor-backed), keyboard/mouse, undo/redo, clipboard | 1.5 weeks |
| 4 | Lift `Folding/`, `Search/`, `Indentation/`; wire transformers + background renderers | 1 week |
| 5 | Multi-caret, `LineNumberMargin`, `EditorDemo` scenario, tests at coverage parity | 1 week |
| **MVP** | Editor at TextView feature parity + folding + multi-caret + multi-line search | **~6 weeks** |
| 6 | Lift `Highlighting/` (xshd) | 1.5 weeks |
| 7 | Port `AvaloniaEdit.TextMate` (TextMate grammars + tmTheme) | 1.5 weeks |

MVP is the real shipping target. 6 and 7 are clean follow-ups. Estimates assume one engineer; double for review + integration cycles in practice.

## 8. Testing Strategy

Mirror Terminal.Gui's three-tier convention:

- **`Terminal.Gui.Text.Tests`** — parallelizable, no UI, no static state. Targets `TextDocument`, anchors, undo, folding, search, indentation, highlighting tokenizers. **Coverage target: 90%+.** Document logic is pure and easy to cover.
- **`Terminal.Gui.Editor.Tests`** — parallelizable wherever possible. Visual-line builder, wrap strategy, caret math, selection math, command handlers without `Application.Init`. **Coverage target: 75%+.**
- **`Terminal.Gui.Editor.IntegrationTests`** — non-parallel tests that need `Application.Init`-style setup; full key-input → render scenarios.

AI-generated tests marked `// Claude - <model>` or `// CoPilot - <model>` per Terminal.Gui convention. New tests default to the parallel project.

Per-phase coverage gate in CI; PR cannot decrease coverage. Benchmarks (`/examples/EditorBenchmarks`) track edit / render / scroll perf vs. the in-tree TextView baseline on a 1 MB file.

## 9. CI

GitHub Actions:

- Build matrix: Linux + macOS + Windows on net10.0.
- `dotnet test` against both test projects.
- `dotnet format --verify-no-changes` over `/src` excluding `/third_party`.
- Coverage upload (Coverlet → Codecov) once Terminal.Gui's MTP-compatible coverage solution lands; gate is informational until then.
- Release workflow tags + publishes both NuGet packages on `v*` tags.

## 10. Open Decisions

Track in `specs/05-decisions.md`. Initial open list:

1. **Line-ending policy.** Adopt AvaloniaEdit's per-line preservation as-is? (Recommended yes — TextView's normalization is an artifact, not a feature.)
2. **Highlighting bet for the first release after MVP — xshd vs TextMate?** Recommend TextMate; bigger ecosystem, similar effort.
3. **Distribution.** Publish `Terminal.Gui.Text` as an independent NuGet from day one, or hold until a second consumer materializes?
4. **Completion item model.** Reuse Terminal.Gui's existing `IAutocomplete`-style types, or define an `IEditorCompletionProvider` shaped after LSP `CompletionItem` (kind, detail, documentation, insertText)?
5. **Async I/O.** AvaloniaEdit `TextDocument` is sync. Add async `LoadAsync (Stream)` / `SaveAsync` at the `Editor` layer, or push into the document?
6. **Read-only ranges.** Lift AvaloniaEdit's `TextSegmentReadOnlySectionProvider`, or YAGNI?

## 11. Risks

- **Cell-grid math under variable-width graphemes is the entire novelty.** Get `WordWrapStrategy` and `VisualLineBuilder` right early; everything downstream depends on them. Heavy unit-test investment in phase 2.
- **AvaloniaEdit's threading assumptions** (`Dispatcher.UIThread.VerifyAccess`) are sprinkled defensively. Need a systematic strip pass with grep-based checklist; one missed call locks the document to a thread it's never on.
- **Highlighting `HighlightingColor` carries font-weight/style** (bold, italic, underline). Map to `TextStyle` flags in `Terminal.Gui.Attribute` rather than dropping. Confirm TG's attribute model supports the styles xshd/TextMate emit.
- **Performance regressions vs TextView** on the small-file common case. Rope is O(log n) but has constant-factor overhead; benchmark from phase 2 onward to catch regressions before they compound.
- **Maintenance burden of the fork.** Sync policy (§5) limits this, but a dedicated owner is needed; otherwise the fork stagnates and bug reports point upstream.

## 12. Definition of Done — MVP

- `dotnet build` clean on all three platforms.
- Both test projects pass; coverage targets met.
- `EditorDemo` runs and exercises: typing, selection, multi-caret, undo/redo, find/replace, folding, word wrap toggle, line numbers, mouse, large-file load (10 MB file < 200 ms initial render).
- One published `Terminal.Gui.Editor` consumer-facing scenario in Terminal.Gui's UICatalog (added via PR to that repo).
- `specs/` reflects final decisions; `05-decisions.md` has each open question resolved with rationale.
- README documents MIT licensing, AvaloniaEdit attribution, supported targets, install, minimal usage example.

---

## Suggested follow-up docs

If you want this split: keep §1–4 + §7 + §10–12 as `00-plan.md`; move §5 to `01-fork-strategy.md`; §6.3 to `02-rendering.md`; §6.2 to `03-public-api.md`; §8 to `04-testing.md`; create empty `05-decisions.md` seeded from §10.
