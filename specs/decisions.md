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

### DEC-009: Completion item shape & provider interface

**Decision**: Use a fresh LSP-flavored `IEditorCompletionProvider` interface and `CompletionItem` type (`Terminal.Gui.Editor.Completion` namespace), **not** Terminal.Gui's existing `IAutocomplete` / `PopupAutocomplete`.

**Rationale**: TG's `IAutocomplete` is tightly coupled to `TextView` (it assumes its own `PopupAutocomplete` rendering, owns selection state, and embeds key-handling that conflicts with `Editor`'s command architecture). A clean provider interface — `GetCompletions(document, caretOffset, prefix)` + `ShouldTrigger(key)` — keeps the completion *data* separate from the *UI*. `CompletionItem` follows the LSP shape (Label, InsertText, Detail) rather than reusing `IAutocomplete`'s string list, which simplifies future LSP integration. The popup uses a TG-native `Popover<ListView, CompletionItem?>` anchored at the caret, consistent with how `DropDownList` uses `Popover` for its list display. Accept applies inside a single `RunUpdate` scope so the entire replacement is one undo step.

**Date**: 2026-05-17

---

## Open

### OPEN-001: Independent `Terminal.Gui.Editor` NuGet from day one

**Question**: Distribute `Terminal.Gui.Editor` as an independent NuGet package from day one, or hold until a second consumer materializes?

**Affected features**: All — packaging decision.

---

### OPEN-002: Completion item shape → DEC-009

Resolved — see DEC-009.

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

**Note (2026-05-17)**: effectively settled by syntax-theme Phase 2 (PR #134) — xshd colors now route through TG `Scheme` code-token `VisualRole`s and `HighlightingColor.Style` continues to carry `TextStyle` flags. Confirm no xshd attribute is silently dropped, then move to Resolved.

---

### DEC-008: Single-line / embeddable-input mode (resolves former OPEN-006)

**Decision**: **Yes** — `Editor` adds a single-line / fixed-height input mode: `Multiline` (default `true`), `EnterKeyAddsLine` (default `true`; when `false`, Enter raises `Accepting` instead of inserting a newline), `TabKeyAddsTab` (default `true`; when `false`, Tab traverses focus). Defaults preserve today's multi-line behavior exactly. Tracked in [#147](https://github.com/gui-cs/Editor/issues/147).

**Rationale**: The earlier "tension" rested on the CLAUDE.md non-goal *"`Editor` ships beside `TextView`, not as a replacement."* Maintainer direction (2026-05-17): `Editor` **will** functionally replace `TextView` — just **not** in a source/API- or UI-compatible way. For *feature* purposes that dissolves the tension: a code-aware single-/few-line input (highlighted expression field, REPL line) is a capability `TextView` serves and `Editor` must therefore serve. The behavior is mostly binding-shaped (Enter/Tab semantics + an `Accepting` event + a height/scroll constraint), so the cost is low and the defaults are non-breaking.

**Affected features**: see [`textview-parity-gap/spec.md`](textview-parity-gap/spec.md) Gap 3 (#147). Note: this "functionally replaces `TextView`" framing also reclassifies `IDesignable` (#151) from non-goal to a tracked gap and keeps single-line Enter/Tab as a real feature (not mere rebinding).

**Date**: 2026-05-17

---

### DEC-005: Word-wrap continuation-line indent policy

**Decision**: Continuation lines render flush at column 0 for v1 (no leading indent).

**Rationale**: Matches VS Code's default behavior with `editor.wrappingIndent: "none"`. Simplifies implementation — no need to compute or track the original line's indentation level for each wrap segment. Revisit in a future version if users need indented continuation lines.

**Date**: 2026-05-13

---

### DEC-006: Vertical multi-caret keybindings (VS Code keyboard parity; `Alt`+drag mouse modifier)

**Decision**: Add-caret-above/below use the VS Code keyboard chords `Ctrl+Alt+CursorUp` / `Ctrl+Alt+CursorDown`, shipped as a `[ConfigurationProperty]` `PlatformKeyBinding` entry in `Editor.DefaultKeyBindings` with **no** editor-specific fallback chord (a terminal/WM that grabs the chord is handled by user override via `View.ViewKeyBindings`). The two commands are registered via `AddCommand` against the real `Command.InsertCaretAbove` / `Command.InsertCaretBelow` enum members and bound through the same configurable path — not an inline `if` in `OnKeyDownNotHandled`. (Those members were added upstream by [gui-cs/Terminal.Gui#5318](https://github.com/gui-cs/Terminal.Gui/issues/5318) / PR [#5319](https://github.com/gui-cs/Terminal.Gui/pull/5319) and are consumed by pinning `$(TerminalGuiVersion)` to `2.1.1-develop.98`.)

The column-of-carets mouse gesture uses **`Alt` + LeftButton drag**, **not** VS Code's `Shift+Alt`. Windows Terminal — and the xterm family it emulates — reserves `Shift`+drag as the user's *forced* text-selection override while an application has mouse mode enabled, and `Alt` turns that into a *block/rectangular* selection ([MS docs](https://learn.microsoft.com/en-us/windows/terminal/customize-settings/interaction); cf. microsoft/terminal#9608). So `Shift+Alt`+drag is swallowed by the terminal's own rectangular-select and never reaches the editor; `Alt`+drag is forwarded. The mouse modifier is currently **not** user-configurable (unlike the keybindings) — that gap, and restoring optional `Shift+Alt` parity, is tracked upstream by [gui-cs/Terminal.Gui#4888](https://github.com/gui-cs/Terminal.Gui/issues/4888) (*"Extend the configurable `KeyBindings` to `MouseBindings` (and combos)"*), to be prioritized.

**Rationale**: Keyboard parity preserves muscle memory and is fully user-overridable via the TG-standard `[ConfigurationProperty]` + `PlatformKeyBinding` mechanism. For the *mouse* modifier, terminal reality wins over GUI-editor parity: a TUI lives inside a terminal emulator, so a gesture the terminal eats is simply unusable — and unlike a key, the mouse modifier has no config override yet. `Alt`+drag is terminal-safe today; full configurable parity follows once TG#4888 lands. **Command-enum debt — RESOLVED 2026-05-17:** the two commands were *temporarily* registered as `(Command) 1001/1002` casts (a sanctioned short-term workaround per Constitution "This Is TG", filed as the great TG issue [gui-cs/Terminal.Gui#5318](https://github.com/gui-cs/Terminal.Gui/issues/5318)). That issue shipped the real `Command.InsertCaretAbove` / `Command.InsertCaretBelow` members (TG PR [#5319](https://github.com/gui-cs/Terminal.Gui/pull/5319), in `Terminal.Gui 2.1.1-develop.98`); `$(TerminalGuiVersion)` is now pinned to `2.1.1-develop.98`, the magic-int casts and the workaround block in `Editor.Commands.cs` are deleted, and the bindings use the real members. The broader "should *any* view be able to contribute commands without casting ints" design question is parked (deliberately, as a possibly-YAGNI hypothetical) in [gui-cs/Terminal.Gui#5320](https://github.com/gui-cs/Terminal.Gui/issues/5320).

**Date**: 2026-05-16 (mouse-modifier amendment same day, after Windows Terminal validation; Command-enum debt resolved 2026-05-17 — TG#5318/#5319 shipped, pinned `2.1.1-develop.98`)

---

### DEC-007: `ClearAdditionalCarets` stays `public`

**Decision**: `Editor.ClearAdditionalCarets ()` remains `public` (resolves spec Open Decision "ClearAdditionalCarets visibility").

**Rationale**: It is already shipped multi-caret API documented in `specs/public-api.md`, and `Editor` itself is a `src/` consumer (Esc handler, plain-click handler, the `Alt` column-drag reset). R9 requires a `src/`/`examples/` consumer (tests don't count) — that bar is met, so demoting to `internal` would be a gratuitous breaking change to documented surface.

**Date**: 2026-05-16
