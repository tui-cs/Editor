# gui-cs/Text — Implementation Plan

This file is the source of truth for the work remaining on `gui-cs/Text`. It is written to be **handed to a dispatching agent** that farms work-items out to sub-agents in parallel. Each work-item below is self-contained: a brief, the files in scope, acceptance criteria, and explicit dependencies.

This is a **revision** of the original plan. Pre-alpha is well underway: the document layer is lifted from AvaloniaEdit, an `Editor : View` exists with caret, selection, undo/redo, mouse, line numbers, and ad-hoc tab expansion. The work has proven that the architecture is right. It has *not* followed the rendering-pipeline architecture in §6 of the original plan — every feature so far has been welded onto a char-by-char draw loop in `Editor.Drawing.cs`. That has to be unwound before more features land. The order below reflects that.

---

## 0. Target: MLP (Minimum Lovable Product) — the alpha release

**The alpha release of `gui-cs/Text` ships when `Editor` reaches MLP.** "Minimum Lovable Product" — *not* "Minimum Viable Product." Viable would be a `View` you can type into. Lovable is the bar:

- **Most of what people expect from a TUI code editor is in place and works.** Typing, selection, multi-caret, find/replace, syntax highlighting, folding, soft wrap, line numbers, indentation, clipboard, mouse, undo with sane granularity, large-file responsiveness. Nothing on that list is a stub.
- **`examples/ted` is a TUI editor someone would actually want to use.** Open a file, edit it, save it, close it. Find. Replace. Toggle wrap. Pick a theme. It feels finished, not like a demo. Day-to-day editing of `.cs` / `.md` / `.json` files in ted is genuinely pleasant.
- **`gui-cs/clet` can ship a `clet edit` subcommand on top of `Editor` and not be embarrassed.** That is the single concrete external-consumer test for MLP. If a contributor asks "is feature X needed for MLP?" — the answer is yes iff `clet edit` would feel broken without it.

The track-by-track work in §7 and the per-item briefs in §8 are the path to MLP. The Definition of Done in §9 is the gate. Tracks F (TextMate) and any "post-alpha" annotations below ship in **the next release after alpha**, not in the alpha itself.

## 1. Purpose & scope (unchanged)

`Terminal.Gui.Text` — UI-framework-independent document model lifted from AvaloniaEdit. No Terminal.Gui dependency.
`Terminal.Gui.Editor` — `Editor : View` consuming `Terminal.Gui.Text`, rendering on a cell grid.

`Editor` is **not** a replacement for `TextView`. Both ship side-by-side. No source-compat obligation.

## 2. Non-goals (unchanged)

- Backwards-compat with `Terminal.Gui.TextView`.
- RTL bidi or shaping beyond grapheme width.
- Pixel/proportional font fidelity. Width is measured in cells via `string.GetColumns()`.
- Ports of AvaloniaEdit's `Editing/`, `Rendering/`, `CodeCompletion/` namespaces — those are Avalonia-UI-specific.

## 3. Status snapshot (2026-05-09)

### Landed

- **Repo + CI**: solution (`Terminal.Gui.Text.slnx`), two src csprojs, three test csprojs, `examples/ted`, `examples/EditorBenchmarks` placeholder, GitHub Actions for build/test/format/release. net10.0, xUnit.v3 exe-style tests.
- **AvaloniaEdit fork**: pinned at `d7a6b63`; `Document/` and `Utils/` lifted; `third_party/AvaloniaEdit/UPSTREAM.md` records every modification. Document tests cover rope, anchors, line tracker, undo, segment tree, change tracking.
- **Editor partials**: `Editor.cs`, `Editor.Commands.cs`, `Editor.Keyboard.cs`, `Editor.Mouse.cs`, `Editor.Drawing.cs`, `Editor.Selection.cs`, `Editor.FindReplace.cs`. Caret, sticky virtual column, vertical/horizontal/page navigation, Home/End, Backspace/Delete, Enter, Ctrl+Z/Y, Ctrl+A, Shift+arrows for selection, drag-to-select, click-to-place, mouse wheel, line numbers, an obsoleted Markdown `ISyntaxHighlighter` stopgap, a partial `Editor.TabWidth` tab-expansion shortcut, **find/next/previous/replace/replace-all** (bespoke `string.IndexOf` over `_document.Text`, not yet on `ISearchStrategy`; no hit highlighting yet; `ReplaceAll` does N separate edits without `OpenUpdateScope`).
- **ted demo**: file menu (incl. Find / Replace items), `FindReplaceDialog` with find + replace tabs, theme dropdown, tab-width numeric updown, status bar, line-numbers toggle.

### Not landed (the rest of this document)

- Visual-line / cell-grid rendering pipeline (`VisualLineBuilder`, `CellVisualLine`, `CellVisualLineElement`, `IVisualLineTransformer`, `IBackgroundRenderer`).
- `Folding/`, `Search/`, `Indentation/`, `Highlighting/` lifts.
- Anchor-backed caret + selection; multi-caret.
- Word wrap.
- Clipboard, `ReadOnly`, indentation strategy.
- Find/Replace **on the proper seam** — current implementation works but bypasses `ISearchStrategy`, lacks hit highlighting, lacks regex/whole-word, lacks F3/Ctrl+F keybindings, and `ReplaceAll` doesn't collapse undo.
- Proper tab handling (issue #37) — current `Editor.TabWidth` is a stopgap.
- TextMate highlighting.

## 4. Lessons learned (these become guardrails)

The features that *are* shipped were directionally correct but bypassed the planned pipeline. Pattern:

1. **Every new visual feature has welded itself onto `Editor.OnDrawingContent`.** PR #22 (line numbers), PR #27 (tab expansion), the `ISyntaxHighlighter` stopgap (issue #28/#32) all do per-char work directly inside the draw loop. The pipeline (`VisualLineBuilder` → `CellVisualLine` → transformers + background renderers) does not exist yet, so contributors had nowhere else to put the code. Result: each shortcut now blocks the feature(s) that need the pipeline (multi-caret across folds, wrap-aware caret, search-hit backgrounds, real syntax highlighting).
2. **Char-by-char iteration regresses graphemes.** `for (int i = 0; i < text.Length; i++) AddStr (..., c.ToString ())` breaks surrogate pairs, ZWJ sequences, and wide-char width math. CLAUDE.md is explicit: *all measurement is in cells, using grapheme clusters and `string.GetColumns ()`*. PR #27's draw loop violated this.
3. **Stopgap public API leaks.** `Editor.SyntaxHighlighter` / `SyntaxLanguage` shipped public, then had to be retro-`[Obsolete]`'d (issue #32) once the real seam (`IVisualLineTransformer`) was specced. `Editor.TabWidth` is now in the same posture vs. AvaloniaEdit's `IndentationSize` (issue #37).
4. **Caret/selection are `int` offsets with hand-rolled edit-tracking arithmetic.** `Editor.cs:223 OnDocumentChanged` reimplements `AnchorMovementType.AfterInsertion` by hand. It works for the single-caret happy path; it will not survive multi-caret, folding (caret inside a folded region), or external edits via a shared `TextDocument`.
5. **Small papercuts come from rapid merging.** Empty-default-doc (issue #30), caret event hygiene (issue #29), dead save/restore code (issue #31), syntax-highlighter contract (issue #32), all landed and were fixed-up in follow-up PRs. The merges were not vetted against §6 of the original plan; the spec was not load-bearing during review.
6. **What works:** the AvaloniaEdit hard fork (clean upstream tracking in `UPSTREAM.md`, targeted Avalonia strips, namespace transforms only); the three-tier test layout (`Text.Tests` pure / `Editor.Tests` logic / `Editor.IntegrationTests` with `Application.Init`); the ted demo as a living acceptance harness; net10.0 + xUnit.v3 exe + slnx tooling.

### Architectural rules — apply to every PR from here on

These are the rules new work must meet. A reviewer (or a dispatching agent) should reject PRs that violate them and link this section.

- **R1. No new feature draws directly inside `OnDrawingContent`.** Visual features land as an `IVisualLineTransformer` (changes attributes on element ranges) or `IBackgroundRenderer` (paints cell rectangles). Once §B1 lands, `OnDrawingContent` is a thin walker over `CellVisualLine` and stops growing.
- **R2. Cells, not chars.** All width math goes through grapheme clusters + `Rune.GetColumns ()` / `string.GetColumns ()`. No `for (int i = 0; i < text.Length; i++)` over rendered text. No `c.ToString ()` per char.
- **R3. Public API surface mirrors AvaloniaEdit names where the concept is the same.** `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs`, `IIndentationStrategy`, `IFoldingStrategy`, `ISearchStrategy`. The wrapper is *superficial*. Bespoke names (`TabWidth`, custom enums) need a written justification in `specs/05-decisions.md`.
- **R4. Caret-after-edit must use `TextAnchor`** with `AnchorMovementType.AfterInsertion`. Hand-rolled offset arithmetic stays only until §C1 lands; no new caret-positioning code may be added that depends on it.
- **R5. Multi-step edits run inside `Document.OpenUpdateScope ()`.** Undo collapses to one user-visible step. No exceptions for "small" multi-edits.
- **R6. Lifted upstream files keep upstream formatting + copyright headers.** Add `// Adapted for Terminal.Gui from AvaloniaEdit <sha>`. Log every modification in `third_party/AvaloniaEdit/UPSTREAM.md`. House-style only applies to non-`/third_party/`-derived files.
- **R7. New tests default to the parallel project.** Promote to `Editor.IntegrationTests` only if `Application.Init`-style state is genuinely required.
- **R8. Public API additions on `Editor` come with a brief in `specs/03-public-api.md`** before merge. Stopgaps (like the current Markdown `ISyntaxHighlighter`) are explicitly marked `[Obsolete]` at introduction, not retro-fitted.

## 5. Repository layout (as-is)

```
/specs/                           # planning + design docs
  00-plan.md                      # this file (source of truth)
  03-public-api.md                # Editor surface (to be created/extracted)
  05-decisions.md                 # decision log (to be created)
/src/Terminal.Gui.Text/           # UI-independent document layer
  Document/  Utils/  Extensions/  Properties/
  (Folding/, Search/, Indentation/, Highlighting/ pending — track A)
/src/Terminal.Gui.Editor/         # the View
  Editor.cs / .Drawing / .Keyboard / .Mouse / .Selection / .Commands
  (Rendering/ pending — track B)
/tests/
  Terminal.Gui.Text.Tests/         (parallel, pure)
  Terminal.Gui.Editor.Tests/       (parallel logic)
  Terminal.Gui.Editor.IntegrationTests/  (Application.Init)
/examples/
  ted/                             (TG demo app)
  EditorBenchmarks/                (placeholder)
/third_party/AvaloniaEdit/
  LICENSE  UPSTREAM.md             (commit d7a6b63 pinned)
```

## 6. Public API target (`Editor`)

The MLP shape, AvaloniaEdit-aligned. Where current properties differ, the right-hand column says what to rename. The dispatching agent should treat any *new* property added to `Editor` as a spec change requiring a §3-table update.

```csharp
namespace Terminal.Gui.Views;

public class Editor : View
{
    public TextDocument Document { get; set; }              // exists
    public int CaretOffset { get; set; }                    // exists; migrate to TextAnchor (§C1)
    public TextSegment? Selection { get; }                  // exists; migrate to anchor pair (§C1)
    public IReadOnlyList<int> AdditionalCaretOffsets { get; } // §C2
    public bool ReadOnly { get; set; }                       // §D2
    public bool WordWrap { get; set; }                       // §D5
    public bool ShowLineNumbers { get; set; }                // exists
    public int IndentationSize { get; set; } = 4;            // rename Editor.TabWidth → IndentationSize (§D1)
    public bool ConvertTabsToSpaces { get; set; }            // §D1
    public bool ShowTabs { get; set; }                       // §D1
    public IIndentationStrategy IndentationStrategy { get; set; }   // §A3 + §D1
    public IList<IVisualLineTransformer> LineTransformers { get; }  // §B1
    public IList<IBackgroundRenderer> BackgroundRenderers { get; }  // §B1
    public FoldingManager? FoldingManager { get; set; }      // §A1 + §D6
    public ISearchStrategy? SearchStrategy { get; set; }     // §A2 + §D4
    public IEditorCompletionProvider? CompletionProvider { get; set; } // post-MLP

    public event EventHandler<DocumentChangeEventArgs>? DocumentChanged;
    public event EventHandler? CaretChanged;
    public event EventHandler? SelectionChanged;
}
```

## 7. Tracks & dependencies

Six tracks. Many work-items run in parallel; the diagram shows the **must-finish-before** edges, not the optional ones.

```
                     ┌─────────────────────────────────────────────┐
                     │                                             │
   ┌── A1 Folding ───┤                                             │
   │                 │                                             │
   ├── A2 Search ────┼── B1 VisualLineBuilder ─┬─ B2 Drawing migr ─┴─ E1 Highlighting Colorizer ─ F1 TextMate
   │                 │                         │
   ├── A3 Indent ────┘                         └─ B3 Wrap ── D5 WordWrap toggle
   │
   └── A4 Highlighting (xshd)  ──────────────────────────── E1
                                                            │
   C1 TextAnchor caret/selection ── C2 Multi-caret ─────────┤
                                                            │
   D1 Tabs (#37, depends on B1)                             │
   D2 ReadOnly       ─ independent                          │
   D3 Clipboard      ─ independent                          │
   D4 Find/Replace UI ── needs A2 + B1                      │
   D6 Folding UI/keys ── needs A1 + B1                      │
   D7 IndentationStrategy plumbing ── needs A3              │
```

**Maximally-parallel start state**: A1, A2, A3, A4, C1, D2, D3 can all be picked up immediately. B1 is the long pole and should be picked up by the strongest agent. Everything blocking on B1 (B2, B3, D1, D4, D6, E1) should not start until B1's interfaces are merged.

## 8. Work-items

Each item is structured as a sub-agent brief. The dispatching agent should give each brief verbatim to the picked-up sub-agent. Items use **track-letter + number** as a stable id.

> **Conventions inside each brief**
> - *Files in scope* lists what the agent should expect to touch / create.
> - *Definition of done* lists the concrete merge bar.
> - *Out of scope* keeps agents from grabbing the next item by accident.
> - *Tests* names the test project that should grow.
> - All items must obey rules **R1–R8** above.

---

### Track A — AvaloniaEdit lifts (parallel)

These four can run as four independent sub-agents. Each is a near-mechanical lift following the `Document/` + `Utils/` precedent already in the repo.

#### A1 — Lift `Folding/`
- *Goal*: bring `FoldingManager`, `FoldingSection`, `FoldingSectionCollection` into `src/Terminal.Gui.Text/Folding/`.
- *Source*: `https://github.com/AvaloniaUI/AvaloniaEdit` at the SHA pinned in `third_party/AvaloniaEdit/UPSTREAM.md`. Limit to `src/AvaloniaEdit/Folding/*.cs` excluding any class that names `Avalonia.Controls`/`Avalonia.Media`.
- *Edits required*: namespace `AvaloniaEdit.Folding` → `Terminal.Gui.Text.Folding`; strip `using Avalonia.*`; remove `Dispatcher.UIThread.VerifyAccess ()`; replace any Avalonia `IBrush`/`Color` with `Terminal.Gui.Color`.
- *Files in scope*: `src/Terminal.Gui.Text/Folding/*.cs`; `third_party/AvaloniaEdit/UPSTREAM.md` (append rows).
- *Tests*: `tests/Terminal.Gui.Text.Tests/Folding/` — port AvaloniaEdit's `FoldingTests` (or write equivalents): create/remove sections, edits inside/across folds keep the section's anchored offsets correct, manager survives whole-document replace.
- *Definition of done*: Folding tests pass; `UPSTREAM.md` updated; no Avalonia residue (grep `using Avalonia`).
- *Out of scope*: any Editor-side UI (margins, click-to-toggle, marker rendering) — that's §D6.
- *Depends on*: nothing.

#### A2 — Lift `Search/`
- *Goal*: bring `ISearchStrategy`, `RegexSearchStrategy`, `SearchResult` into `src/Terminal.Gui.Text/Search/`.
- *Edits required*: namespace transform; no Avalonia deps to strip in this folder beyond a pro-forma check.
- *Files in scope*: `src/Terminal.Gui.Text/Search/*.cs`; `UPSTREAM.md` rows.
- *Tests*: `tests/Terminal.Gui.Text.Tests/Search/` — case sensitivity, whole-word, regex flags, search across line boundaries, search returns anchored ranges (anchors must survive subsequent edits).
- *Definition of done*: tests pass; `UPSTREAM.md` updated.
- *Out of scope*: find/replace UI, hit-highlight rendering — those are §D4 and §E1.
- *Depends on*: nothing.

#### A3 — Lift `Indentation/`
- *Goal*: `IIndentationStrategy`, `DefaultIndentationStrategy` into `src/Terminal.Gui.Text/Indentation/`.
- *Edits required*: namespace transform; no Avalonia deps expected.
- *Files in scope*: `src/Terminal.Gui.Text/Indentation/*.cs`; `UPSTREAM.md` rows.
- *Tests*: `tests/Terminal.Gui.Text.Tests/Indentation/` — `IndentLine` copies leading whitespace from the previous line; no-op on first line; respects mixed tabs+spaces.
- *Definition of done*: tests pass; `UPSTREAM.md` updated.
- *Out of scope*: wiring into `Editor` (Tab/Shift+Tab keys, Enter auto-indent) — those are §D1 and §D7.
- *Depends on*: nothing.

#### A4 — Lift `Highlighting/` (xshd parser + tokenizer model only)
- *Goal*: `HighlightingManager`, `IHighlighter`, `HighlightingColor`, the xshd loader, `DocumentHighlighter`. Land them in `src/Terminal.Gui.Text/Highlighting/`.
- *Edits required*: namespace transform; replace `IBrush`/`Avalonia.Media.Color` with `Terminal.Gui.Color`; **drop typeface/font-size from `HighlightingColor`** (keep bold/italic/underline as TG `TextStyle` flags); strip `using Avalonia.*`. Map xshd's `Bold`/`Italic`/`Underline` to `TextStyle.Bold | TextStyle.Italic | TextStyle.Underline` on a new `TerminalGuiAttribute` projection (or directly on `Terminal.Gui.Drawing.Attribute`).
- *Files in scope*: `src/Terminal.Gui.Text/Highlighting/**/*.cs`; bundled `.xshd` resources; `UPSTREAM.md` rows.
- *Tests*: `tests/Terminal.Gui.Text.Tests/Highlighting/` — load a known xshd (e.g. C#), tokenize a sample, expected ranges + colors; round-trip color → `Terminal.Gui.Color` is lossless for the in-tree palettes.
- *Definition of done*: tests pass; `UPSTREAM.md` updated; no Avalonia residue.
- *Out of scope*: the `IVisualLineTransformer` that consumes the highlighter — that's §E1.
- *Depends on*: nothing (parallel with A1/A2/A3).

---

### Track B — Cell-grid rendering pipeline (mostly serial; this is the long pole)

#### B1 — `VisualLineBuilder`, `CellVisualLine`, `CellVisualLineElement`, `IVisualLineTransformer`, `IBackgroundRenderer`
- *Goal*: stand up the rendering data model the rest of the editor draws through. After this, `OnDrawingContent` is a thin walker. After this, R1 starts being enforceable.
- *Public surface*:
  ```csharp
  namespace Terminal.Gui.Views.Rendering;

  public sealed class CellVisualLine {
      public DocumentLine DocumentLine { get; }
      public IReadOnlyList<CellVisualLineElement> Elements { get; }
      public int VisualLength { get; }      // total cells
  }

  public abstract class CellVisualLineElement {
      public int RelativeOffset { get; }    // logical doc-offset relative to DocumentLine.Offset
      public int LogicalLength { get; }     // # of doc chars this element represents (0 for synthetic)
      public int VisualLength { get; }      // # of cells this element occupies
      public Drawing.Attribute Attribute { get; set; }
      public abstract void Draw (View host, int x, int y, int visibleStart, int visibleEnd);
  }

  public sealed class TextRunElement : CellVisualLineElement { /* grapheme-walked AddStr */ }
  public sealed class TabElement : CellVisualLineElement { /* expansion to next IndentationSize stop */ }
  public sealed class FoldingMarkerElement : CellVisualLineElement { /* "⋯" */ }

  public interface IVisualLineTransformer {
      void Transform (CellVisualLine line);
  }

  public interface IBackgroundRenderer {
      void Draw (View host, CellVisualLine line, int row, Rectangle viewport);
  }
  ```
- *Builder contract*: `VisualLineBuilder.Build (DocumentLine, BuildContext)` returns a `CellVisualLine`. The builder always walks **graphemes**, never `char`s, using `Rune` enumeration + `string.GetColumns ()`. It honors `IndentationSize` for tab elements, fold sections via `FoldingManager` (§A1; if A1 not yet wired, builder runs without a folding manager — guard cleanly), and applies registered transformers in order before returning. Cache one `CellVisualLine` per `DocumentLine`; invalidate by `Document.Changed` offset/length.
- *Files in scope*: new `src/Terminal.Gui.Editor/Rendering/*.cs` (one type per file); minor edits to `Editor.cs` to expose `LineTransformers`, `BackgroundRenderers`.
- *Tests*: `tests/Terminal.Gui.Editor.Tests/Rendering/` — grapheme-aware width (CJK, emoji ZWJ, surrogate pair, combining mark); tab expansion at varied start columns and varied `IndentationSize`; transformer ordering preserved; cache invalidation hits exactly the lines whose offset range was touched.
- *Definition of done*: pipeline merges with full unit coverage; `OnDrawingContent` is **not yet** migrated (B2 does that); no public `Editor` regression.
- *Out of scope*: word wrap (B3), highlighting transformer (E1), folding marker click handling (D6).
- *Depends on*: nothing strictly, but A1 lets `FoldingMarkerElement` ship populated rather than as a stub.

#### B2 — Migrate `Editor.OnDrawingContent` onto the pipeline; back out PR #27 / PR #22 inline shortcuts
- *Goal*: replace the char-by-char draw loop in `Editor.Drawing.cs` with a `CellVisualLine`-walking loop. Remove the inline tab-expansion shortcut and the line-numbers shortcut; reimplement line numbers as an `IBackgroundRenderer` (`LineNumberMargin`).
- *Concrete deletes*:
  - `Editor.Drawing.cs:DrawLineContent (...)` — entire char-iterating implementation. Replace with: for each visible row, get/build the `CellVisualLine`, run `BackgroundRenderers`, then iterate `Elements` and call `Draw` per element with the visible-x clamp.
  - `Editor.cs:GetVisualWidthForCharacter`, `GetVisualColumnFromLogicalColumn`, `GetLogicalColumnFromVisualColumn` — replace with calls to `CellVisualLine.GetVisualColumn` / `GetRelativeOffset`.
  - The `c == '\t' ? new (' ', drawEnd - drawStart) : c.ToString ()` ternary inside the draw loop.
  - `Editor.Drawing.cs:DrawLineNumbers` (the bespoke `OnDrawComplete` margin painter) — replaced by a `LineNumberMargin : IBackgroundRenderer` registered into `BackgroundRenderers` when `ShowLineNumbers == true`.
- *Files in scope*: `src/Terminal.Gui.Editor/Editor.Drawing.cs`, `src/Terminal.Gui.Editor/Editor.cs`, `src/Terminal.Gui.Editor/Editor.Mouse.cs` (mouse → offset uses pipeline helpers), new `src/Terminal.Gui.Editor/Rendering/LineNumberMargin.cs`.
- *Tests*: existing `EditorRenderingTests`, `EditorMouseTests`, line-number tests must pass without modification (or with minimal mechanical updates to assertion shape). Add a grapheme-cluster regression: an emoji-with-ZWJ on a tabbed line renders at the right column.
- *Definition of done*: `OnDrawingContent` body is < 30 lines and contains zero `for (int i = 0; i < text.Length; ...)` over rendered text. Rule R1 and R2 are now enforceable.
- *Out of scope*: tab keyboard behavior (D1), syntax highlighting transformer (E1), word wrap (B3).
- *Depends on*: B1.

#### B3 — `WordWrapStrategy`
- *Goal*: opt-in soft wrap. Walk grapheme clusters, accumulate column count, break at the last whitespace boundary that fits, hard-break long unbroken runs. Output: list of `(documentLine, startOffset, endOffset, leadingIndent)` wrap segments. `VisualLineBuilder` consults the strategy to emit one `CellVisualLine` per wrap segment when `WordWrap == true`.
- *Files in scope*: `src/Terminal.Gui.Editor/Rendering/WordWrapStrategy.cs`; minor `VisualLineBuilder` integration; `Editor.cs:WordWrap` property (§D5 wires the toggle into ted).
- *Tests*: `tests/Terminal.Gui.Editor.Tests/Rendering/WordWrapTests.cs` — break at whitespace, hard-break a CJK run that exceeds viewport, leading-indent on continuation lines, caret column under wrap (`OffsetToVisualPosition`/inverse).
- *Definition of done*: with `WordWrap = true`, lines wider than viewport produce multiple `CellVisualLine`s; caret hit-testing across wrap boundaries is correct.
- *Out of scope*: ted UI toggle (D5).
- *Depends on*: B1.

---

### Track C — Caret & selection on anchors (parallel with B; can land before or after)

#### C1 — Migrate caret + selection from `int` offsets to `TextAnchor`
- *Goal*: caret is a `TextAnchor` with `AnchorMovementType.AfterInsertion`. Selection is two anchors (or a `TextSegment`). Delete the hand-rolled edit-tracking arithmetic in `OnDocumentChanged`. R4 becomes enforceable.
- *Files in scope*: `Editor.cs` (replace `_caretOffset` / `OnDocumentChanged` arithmetic), `Editor.Selection.cs` (replace `_selectionAnchor` int with anchors).
- *Tests*: existing caret + selection tests must pass unchanged. Add: external edit on a shared `TextDocument` advances the caret correctly; edit at exactly the caret with `AfterInsertion` semantics keeps the caret at the *end* of the inserted text.
- *Definition of done*: `OnDocumentChanged`'s manual `if (_caretOffset >= e.Offset)` arithmetic is gone; `CaretOffset { get; set; }` becomes a thin wrapper over the anchor's offset.
- *Out of scope*: multi-caret (C2).
- *Depends on*: nothing (TextAnchor is already lifted in `Document/`).

#### C2 — Multi-caret
- *Goal*: `IReadOnlyList<int> AdditionalCaretOffsets`. Editing commands run inside one `Document.OpenUpdateScope ()` so undo collapses. R5 becomes enforceable.
- *Files in scope*: `Editor.cs`, `Editor.Commands.cs`, new `Editor.MultiCaret.cs`. Update `IBackgroundRenderer` (current line) and `UpdateCursor ()` to handle the additional caret list.
- *Tests*: `tests/Terminal.Gui.Editor.Tests/MultiCaretTests.cs` — Ctrl+Click adds caret; typing inserts at every caret; undo collapses to one step; selection per caret survives editing.
- *Definition of done*: ted demo can demonstrate Ctrl+Click multi-caret typing + undo.
- *Out of scope*: column-mode selection (post-MLP).
- *Depends on*: C1.

---

### Track D — Editor surface (mostly parallel)

#### D1 — Tab handling per issue #37
- *Goal*: implement issue #37 in full. Rename `Editor.TabWidth` → `IndentationSize`; add `ConvertTabsToSpaces`, `ShowTabs`; add Tab / Shift+Tab key handlers (insert; indent block on selection; unindent via `TextUtilities.GetSingleIndentationSegment`); make `\t` a `TabElement` in the visual-line pipeline; mouse hit-test inside a tab span snaps to the *nearest* edge.
- *Files in scope*: `Editor.cs`, `Editor.Keyboard.cs`, `Editor.Mouse.cs`, `Editor.Commands.cs`, `examples/ted/TedApp.cs` (rename status-bar control), tests across all three test projects.
- *Tests*: see issue #37 §9 for the test list (round-trip preserves `\t`, both `ConvertTabsToSpaces` modes, Shift+Tab unindent, block indent on selection, grapheme-cluster on tabbed line).
- *Definition of done*: issue #37 closes; `Editor.TabWidth` is gone or `[Obsolete]` shimmed; `OnDrawingContent` does not special-case `\t` (the `TabElement` does).
- *Out of scope*: full `IIndentationStrategy` plumbing (D7).
- *Depends on*: B1 (needs `TabElement`). Without B1 this becomes another welded shortcut and is rejected.

#### D2 — `ReadOnly`
- *Goal*: when `true`, edit commands are no-ops (still let navigation/selection through). `Document` is not modified.
- *Files in scope*: `Editor.cs`, `Editor.Keyboard.cs`, `Editor.Commands.cs`.
- *Tests*: `tests/Terminal.Gui.Editor.Tests/EditorReadOnlyTests.cs` — typing, paste, Backspace, Enter, Tab, Undo are no-ops; navigation still moves the caret; selection still works.
- *Definition of done*: ted demo opens a file in read-only mode via a flag and behaves correctly.
- *Out of scope*: per-segment read-only ranges (open decision, see §10.6).
- *Depends on*: nothing.

#### D3 — Clipboard (Cut / Copy / Paste)
- *Goal*: Ctrl+C / Ctrl+X / Ctrl+V via Terminal.Gui's `Clipboard`. Selection-aware. Cut + paste run inside `OpenUpdateScope ()`.
- *Files in scope*: `Editor.Commands.cs` (new `Cut`/`Copy`/`Paste` commands), `Editor.Keyboard.cs` (bindings), tests.
- *Tests*: `tests/Terminal.Gui.Editor.IntegrationTests/EditorClipboardTests.cs` — round-trip across selection-and-not-selection cases; paste with multi-line text inserts newlines correctly; cut emits one undo step.
- *Definition of done*: ted demo Edit menu (or status bar) wires Cut/Copy/Paste.
- *Out of scope*: rectangular paste (post-MLP).
- *Depends on*: nothing.

#### D4 — Migrate Find / Replace onto `ISearchStrategy` + hit highlighting + undo grouping + keybindings
- *Status*: **partially landed.** `Editor.FindReplace.cs` ships `FindNext` / `FindPrevious` / `ReplaceNext` / `ReplaceAll` (case-sensitive option; wrap-around). `examples/ted/FindReplaceDialog.cs` provides the dialog; ted's File menu wires Find / Replace items. **What's missing** is the proper seam, hit-highlighting, undo grouping, and editor-level keybindings — see *Goal*.
- *Goal*: replace the bespoke `string.IndexOf` implementation with `ISearchStrategy` (lifted in A2). Add a `SearchHitRenderer : IBackgroundRenderer` (B1) that paints the current hit list across the visible viewport. Wrap `ReplaceAll` in a single `Document.OpenUpdateScope ()` so undo collapses to one step (R5). Add `Editor`-level keybindings: F3 → `FindNext`, Shift+F3 → `FindPrevious`, Ctrl+F → open find dialog, Ctrl+H → open replace dialog.
- *Files in scope*: `Editor.cs` (`SearchStrategy { get; set; }` property), `Editor.FindReplace.cs` (rewrite internals to use `ISearchStrategy`; wrap `ReplaceAll` in `OpenUpdateScope`), `Editor.Commands.cs` (new `FindNext`/`FindPrevious`/`Find`/`Replace` commands + bindings), `Editor.Keyboard.cs` (the binds), new `src/Terminal.Gui.Editor/Rendering/SearchHitRenderer.cs`, `examples/ted/FindReplaceDialog.cs` (expose regex + whole-word toggles to the dialog UI; light wiring only).
- *Tests*: keep the existing unit/integration tests for the public API contracts; add: regex search (depends on A2's `RegexSearchStrategy`); whole-word; replace-all produces exactly **one** undo step (regression for current N-step bug); hit-highlight invalidation on edit; F3 wraparound integration test.
- *Definition of done*: `Editor.FindReplace.cs` no longer references `_document.Text.IndexOf`; `SearchStrategy` property is the single seam; `ReplaceAll` undo collapses; hit highlights paint via `IBackgroundRenderer`; ted demo's existing menu items keep working; coverage on the strategy seam ≥ 75%.
- *Out of scope*: incremental search dropdown (post-alpha).
- *Depends on*: A2 + B1. Until both land, the current bespoke implementation stays — do **not** weld further features onto it.

#### D5 — Word wrap toggle in ted + `Editor.WordWrap`
- *Goal*: expose `Editor.WordWrap`; ted status bar toggle.
- *Files in scope*: `Editor.cs`, `examples/ted/TedApp.cs`, ted integration tests.
- *Tests*: toggling wrap on a long line changes the visible row count; caret survives the toggle at the same logical offset.
- *Definition of done*: visibly works in ted.
- *Out of scope*: per-language wrap policy.
- *Depends on*: B3.

#### D6 — Folding UI (margin click, keys, marker rendering)
- *Goal*: click on the line-number margin toggles a `FoldingSection`. `FoldingTransformer : IVisualLineTransformer` (or builder-level swap) replaces folded ranges with a single `FoldingMarkerElement` rendering `"⋯"` (configurable). Keyboard: Ctrl+M Ctrl+M (or similar) toggles the fold under the caret.
- *Files in scope*: `Editor.cs` (`FoldingManager` property), `Editor.Mouse.cs` (margin click), `Editor.Commands.cs` (toggle command), new `src/Terminal.Gui.Editor/Rendering/FoldingTransformer.cs`, `LineNumberMargin` extension to draw the chevrons.
- *Tests*: integration test asserts a folded section renders as the configured marker; clicking the marker re-expands; caret inside a fold range moves to the marker's logical edge on horizontal navigation.
- *Definition of done*: ted demo has folding regions wired (e.g. via a `BraceFoldingStrategy` against C# samples).
- *Out of scope*: language-specific folding strategies beyond brace-matching.
- *Depends on*: A1 + B1.

#### D7 — `IIndentationStrategy` plumbing
- *Goal*: `Editor.IndentationStrategy` defaults to `DefaultIndentationStrategy`. On Enter, `Editor` calls the strategy's `IndentLine` for the new line. Tab key (when no selection) consults the strategy when `ConvertTabsToSpaces == true`.
- *Files in scope*: `Editor.cs`, `Editor.Commands.cs` (Enter handler delegates), tests.
- *Tests*: Enter on a 4-space-indented line creates a new 4-space-indented line; Enter inside a brace block (with a future smart strategy) — the strategy is pluggable, default is dumb.
- *Definition of done*: default strategy drives Enter; D1's Tab handler defers to the strategy where appropriate.
- *Out of scope*: shipping a smart C# strategy (post-MLP).
- *Depends on*: A3.

---

### Track E — Highlighting integration

#### E1 — `HighlightingColorizer : IVisualLineTransformer`
- *Goal*: a transformer that consumes a `DocumentHighlighter` and sets `Attribute` on `TextRunElement` ranges. Replaces the `[Obsolete]` Markdown stopgap end-to-end. Issue #28 / #32 close.
- *Files in scope*: new `src/Terminal.Gui.Editor/Rendering/HighlightingColorizer.cs`; `Editor.cs` removes/properly deprecates the Markdown stopgap; `examples/ted/TedApp.cs` switches the theme dropdown to drive a `HighlightingManager` instance.
- *Tests*: `tests/Terminal.Gui.Editor.Tests/Rendering/HighlightingColorizerTests.cs` — load C# xshd, color a sample document, expected attributes on expected ranges; theme swap re-runs the highlighter.
- *Definition of done*: ted's theme dropdown produces real syntax highlighting on `.cs` files; `[Obsolete]` `SyntaxHighlighter` / `SyntaxLanguage` are removed (this is an intentional source-break — pre-alpha allows it, document the break in the changelog).
- *Out of scope*: TextMate (F1).
- *Depends on*: A4 + B1 + B2.

---

### Track F — TextMate (post-MLP)

#### F1 — Port `AvaloniaEdit.TextMate`
- *Goal*: TextMate grammar + `tmTheme` support, mapped to `Terminal.Gui.Color`. Plugs into the same `HighlightingColorizer` seam as E1 (just a different `IHighlighter` implementation).
- *Files in scope*: new `src/Terminal.Gui.Text/Highlighting/TextMate/*.cs`; tests; `UPSTREAM.md` rows.
- *Tests*: load `csharp.tmLanguage.json`, tokenize a sample, expected scopes → expected colors.
- *Definition of done*: ted demo can load a `.tmTheme` and use a TextMate grammar.
- *Depends on*: E1.

---

## 9. MLP definition of done

Each criterion below is testable. The dispatching agent should treat the list as the merge-to-`main` gate.

- [ ] All work-items in tracks A, B, C (C1+C2), D1–D7, E1 merged.
- [ ] `dotnet build Terminal.Gui.Text.slnx` clean on Linux/macOS/Windows on net10.0.
- [ ] All three test projects pass. Coverage targets: `Text.Tests` ≥ 90%, `Editor.Tests` ≥ 75%, integration informational.
- [ ] `Editor.OnDrawingContent` does not iterate `text` by `char`. R1, R2, R4, R5 hold.
- [ ] `Editor.TabWidth`, `Editor.SyntaxHighlighter`, `Editor.SyntaxLanguage` are all gone.
- [ ] No file under `src/Terminal.Gui.Text/` references `Terminal.Gui` (run `grep -r "using Terminal.Gui" src/Terminal.Gui.Text/` and assert empty).
- [ ] ted demo exercises: typing, selection (mouse + keyboard), multi-caret, undo/redo, find/replace, folding, word wrap toggle, line numbers, mouse, large-file load (10 MB file < 200 ms initial render — measured by `examples/EditorBenchmarks`).
- [ ] One `Terminal.Gui.Editor` consumer-facing scenario in Terminal.Gui's UICatalog (PR to that repo).
- [ ] `specs/03-public-api.md` and `specs/05-decisions.md` are populated; every open decision in §10 below has a resolution entry.
- [ ] README documents MIT licensing, AvaloniaEdit attribution, supported targets, install, minimal usage example.

## 10. Open decisions

These are blockers if hit by a work-item. The dispatching agent should pause the affected sub-agent and either record a decision in `specs/05-decisions.md` (after asking the human owner) or descope to unblock.

### Resolved

- **Line-ending policy. Adopt AvaloniaEdit's per-line preservation as-is.** `TextDocument` keeps each line's terminator verbatim on load and round-trips it on save — no normalization to `\n`. Mixed-ending files survive a load/save round trip byte-identical except where the user explicitly edited a line. Move the rationale to `specs/05-decisions.md` when that file is created.
- **First post-alpha highlighter is TextMate (track F1), not xshd.** xshd lands earlier as the tokenizer model in A4 because the lift is cheap; the consumer-facing grammar story is TextMate. Track F1 ships in the release after alpha.

### Still open

1. **Distribution of `Terminal.Gui.Text` as an independent NuGet from day one** vs. holding until a second consumer materializes.
2. **Completion item shape.** Reuse Terminal.Gui's `IAutocomplete`-style types vs. a fresh LSP-flavored `IEditorCompletionProvider`.
3. **Async I/O.** `LoadAsync (Stream)` / `SaveAsync` on `Editor` vs. on the document.
4. **Read-only ranges.** Lift `TextSegmentReadOnlySectionProvider`, or YAGNI.
5. **`HighlightingColor` carries Bold/Italic/Underline.** Confirmed mapping target is `Terminal.Gui.TextStyle`. Verify all xshd attributes are representable; if not, document drops in `05-decisions.md`.

## 11. Risks (carryover, with current readings)

- **Cell-grid math under variable-width graphemes is the entire novelty.** B1 is where this risk is paid down. Do not let any item in tracks D, E, F land before B1+B2 — that is what created the lessons in §4.
- **AvaloniaEdit threading assumptions.** `Dispatcher.UIThread.VerifyAccess ()` calls were stripped systematically in `Document/`; track A items must continue this discipline. Grep for `VerifyAccess` after each lift before merge.
- **Performance regression vs `TextView`.** `EditorBenchmarks` is a placeholder; populate it during B1/B2 (rope-edit micro-bench, full-redraw on 10 MB file, scroll-fps proxy) so regressions are visible from then on.
- **Maintenance of the fork.** UPSTREAM.md is currently well-maintained; A1–A4 must each append rows. Re-sync remains manual and deliberate; the goal is divergence, not parity.

---

## 12. How to use this plan (for the dispatching agent)

1. Read §3 and §4. Internalize the rules (R1–R8). Reject any sub-agent output that violates them.
2. Read §7. Pick the maximally-parallel start set: **A1, A2, A3, A4, C1, D2, D3** in flight; **B1** assigned to your strongest sub-agent.
3. For each item dispatched, give the sub-agent the work-item brief verbatim. Append: the rules R1–R8, the path conventions in §5, and a pointer to `CLAUDE.md` for coding standards.
4. When B1 merges, dispatch **B2** (single agent, blocks the rest of B/D/E). When B2 merges, the second wave (B3, D1, D4, D5, D6, D7, E1) becomes parallel-eligible.
5. Track each item's PR against the Definition of Done in its brief, *not* against the agent's self-report.
6. Update §3 (status snapshot) every time an item lands, so the next dispatch sees current state.
7. When all §9 boxes are checked, propose the cut from `develop` to `main` and a `v*` tag.
