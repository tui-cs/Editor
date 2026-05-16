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

### DEC-005: No-selection behavior for Cut/Copy (FR-005)

**Decision**: When there is no selection, Cut and Copy are **no-ops** (do nothing).

**Rationale**: VS Code's most-common preset (the default) copies/cuts the current line when there is no selection, but this is a power-user feature that surprises newcomers and makes accidental clipboard overwrites common. Matching the "no-op on empty selection" behavior is safer, simpler to implement, and avoids the implicit "current line" semantic that would require additional UI feedback. This can be revisited later as an opt-in `ClipboardLineMode` property if demand arises.

**Date**: 2026-05-13

---

## Open

### OPEN-001: Independent `Terminal.Gui.Editor` NuGet from day one

**Question**: Distribute `Terminal.Gui.Editor` as an independent NuGet package from day one, or hold until a second consumer materializes?

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

---

### DEC-005: Word-wrap continuation-line indent policy

**Decision**: Continuation lines render flush at column 0 for v1 (no leading indent).

**Rationale**: Matches VS Code's default behavior with `editor.wrappingIndent: "none"`. Simplifies implementation — no need to compute or track the original line's indentation level for each wrap segment. Revisit in a future version if users need indented continuation lines.

**Date**: 2026-05-13

---

### DEC-006: Vertical multi-caret keybindings (VS Code parity, no fallback chord)

**Decision**: Vertical multi-caret uses the VS Code chords — `Ctrl+Alt+CursorUp` / `Ctrl+Alt+CursorDown` for add-caret-above/below and `Shift+Alt + LeftButton` drag for the column-of-carets gesture (carets only; per-row selection is a follow-up). The keys ship as a `[ConfigurationProperty]` `PlatformKeyBinding` entry in `Editor.DefaultKeyBindings`; there is **no** editor-specific fallback chord. Users whose terminal/WM grabs the chord override it through `View.ViewKeyBindings` config like any other binding. Because TG's `Command` enum (consumed via the pinned `Terminal.Gui` package) has no vertical-multi-caret slot, the two commands are registered as Editor-local `Command` ids (`(Command) 1001` / `1002`) via `AddCommand` and bound through the same configurable path as every other Editor binding — not an inline `if` in `OnKeyDownNotHandled`.

**Rationale**: Matches `specs/vertical-multi-caret/spec.md` Resolved Decisions (2026-05-15). VS Code parity preserves muscle memory; the TG-standard `[ConfigurationProperty]` + `PlatformKeyBinding` mechanism makes the chord fully user-overridable without bespoke editor knobs. Upstream follow-up: TG should reserve a documented view-local `Command` range so consumers don't pick magic ints — filed as a TG issue per Constitution tenet "This Is TG" (workarounds require a great TG issue).

**Date**: 2026-05-16

---

### DEC-007: `ClearAdditionalCarets` stays `public`

**Decision**: `Editor.ClearAdditionalCarets ()` remains `public` (resolves spec Open Decision "ClearAdditionalCarets visibility").

**Rationale**: It is already shipped multi-caret API documented in `specs/public-api.md`, and `Editor` itself is a `src/` consumer (Esc handler, plain-click handler, the `Shift+Alt` column-drag reset). R9 requires a `src/`/`examples/` consumer (tests don't count) — that bar is met, so demoting to `internal` would be a gratuitous breaking change to documented surface.

**Date**: 2026-05-16
