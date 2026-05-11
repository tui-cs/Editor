# Decision Log

Decisions are recorded here when an open question from the plan is resolved. Each entry captures the decision, rationale, and date.

---

## Resolved

### DEC-001: Line-ending policy

**Decision**: Adopt AvaloniaEdit's per-line preservation as-is.

**Rationale**: `TextDocument` keeps each line's terminator verbatim on load and round-trips it on save — no normalization to `\n`. Mixed-ending files survive a load/save round trip byte-identical except where the user explicitly edited a line.

**Date**: 2026-05-09

---

### DEC-002: First post-alpha highlighter format

**Decision**: TextMate (track textmate-grammars), not xshd.

**Rationale**: xshd lands earlier as the tokenizer model in syntax-highlighting because the lift is cheap; the consumer-facing grammar story is TextMate. Track textmate-grammars ships in the release after alpha.

**Date**: 2026-05-09

---

### DEC-004: Line-number gutter implementation

**Decision**: Line numbers render via `Gutter : View` hosted in `Padding.GetOrCreateView().Add(...)`, **not** `LineNumberMargin : IBackgroundRenderer` as proposed by drawing-overhaul FR-006.

**Rationale**: `IBackgroundRenderer.Draw(view, line, row, viewport)` paints inside the editor's content viewport - its `viewport` argument doesn't reach into `Padding`, which is where the gutter lives. The original `OnDrawComplete`-driven implementation overdrew popovers/menus exactly because it bypassed the View hierarchy; an `IBackgroundRenderer` would repeat that mistake. Hosting `Gutter` as a Padding SubView puts the gutter inside the View tree, so popovers clip it correctly and the layout system handles its frame. `IBackgroundRenderer` remains the right vehicle for selection highlight, current-line highlight, and search-hit highlight - all of which paint inside the viewport.

**Date**: 2026-05-11

---

### DEC-003: Tab handling architecture

**Decision**: Tab handling (tab-handling) requires the visual-line pipeline (rendering-pipeline). The codex branch implemented both together. `TabElement` renders tabs through the pipeline, not via inline char-by-char expansion.

**Rationale**: Issue #37 specified this dependency. The experiment confirmed that implementing tabs without the pipeline creates the same welded-shortcut technical debt pattern described in §4 of the original plan (lessons learned). The codex branch delivered both rendering-pipeline and tab-handling together, proving the pipeline-first approach works.

**Date**: 2026-05-10

---

### DEC-005: Clipboard no-selection behavior

**Decision**: Copy and Cut with no active selection operate on the current document line, preserving that line's existing line terminator when present.

**Rationale**: This matches common editor behavior and Terminal.Gui's `TextView` precedent, and makes the command useful without requiring a pre-existing selection.

**Date**: 2026-05-11

---

## Open

### OPEN-001: Independent `Terminal.Gui.Text` NuGet from day one

**Question**: Distribute `Terminal.Gui.Text` as an independent NuGet package from day one, or hold until a second consumer materializes?

**Affected features**: All — packaging decision.

---

### OPEN-002: Completion item shape

**Question**: Reuse Terminal.Gui's `IAutocomplete`-style types vs. a fresh LSP-flavored `IEditorCompletionProvider`?

**Affected features**: Post-MLP.

---

### OPEN-003: Async I/O placement

**Question**: `LoadAsync (Stream)` / `SaveAsync` on `Editor` vs. on the document?

**Affected features**: File I/O, large-file performance.

---

### OPEN-004: Read-only ranges

**Question**: Lift `TextSegmentReadOnlySectionProvider`, or YAGNI?

**Affected features**: read-only (ReadOnly) — per-segment ranges are out of scope for read-only pending this decision.

---

### OPEN-005: `HighlightingColor` attribute mapping

**Question**: `HighlightingColor` carries Bold/Italic/Underline. Confirmed mapping target is `Terminal.Gui.TextStyle`. Verify all xshd attributes are representable; if not, document drops.

**Affected features**: syntax-highlighting, syntax-colorizer.
