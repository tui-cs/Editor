<![CDATA[# Feature Specification: Vertical Multi-Caret (Ctrl+Alt+Up/Down, Shift+Alt+Drag)

**Status**: Draft — supersedes the throwaway implementation in PR #125
**Created**: 2026-05-15
**Last updated**: 2026-05-15
**Depends on**: multi-caret ✅, word-wrap ✅, caret-anchors ✅
**Blocked by**: —
**Reference (do not merge)**: [PR #125](https://github.com/gui-cs/Editor/pull/125) — copilot-authored prototype. The functionality is right in the simplest case; the implementation is hacky and the maintainer has documented multiple regressions on it (see § Reference behavior from PR #125 below). Use the test cases from that PR as the executable contract; re-implement the editor changes against this spec. **Note**: PR #125 used `Alt+Up/Down` and `Alt+drag`; this spec adopts the VS Code chords (`Ctrl+Alt+Up/Down`, `Shift+Alt+drag`). The tests must be ported with the new key combinations.

## Overview

Extend the existing multi-caret machinery (`AdditionalCaretOffsets`, `HasMultipleCarets`, `ToggleCaretAt`, `ClearAdditionalCarets`) with two ergonomic ways to create a **vertically-aligned column of carets** anchored on the same visual column across consecutive lines:

1. **Keyboard**: `Ctrl+Alt+Up` / `Ctrl+Alt+Down` extends the caret block one line above the topmost / below the bottommost caret, landing on the same sticky virtual column. Matches VS Code's `editor.action.insertCursorAbove` / `Below`.
2. **Mouse**: `Shift+Alt + LeftButton drag` creates a column of carets spanning the anchor row → active row at the press column. Matches VS Code's column-select drag (this spec ships carets-only first; selection-per-row is a follow-up — see Out of Scope).

Both flows produce a primary caret plus zero or more additional carets, all sharing the multi-caret edit pipeline (single `Document.OpenUpdateScope ()` → one undo step, R5).

The feature also makes Tab work uniformly across all carets in the multi-caret system (an existing gap that vertical-caret usage exposes), and fixes interaction bugs that block the vertical-caret flow from being usable.

**Terminal compatibility**: `Ctrl+Alt+Up/Down` and `Shift+Alt+mouse` rely on the terminal forwarding the modifier flags. Some Linux desktops grab `Ctrl+Alt+arrow` for workspace switching, and a few terminals don't distinguish `Ctrl+Alt+arrow` from `Ctrl+arrow` without `modifyOtherKeys` / the Kitty keyboard protocol. The TG keybinding system makes the chords overridable; users in those environments rebind. See **Keybinding overridable** in Requirements.

## User Scenarios

All scenarios are stated as black-box behavior the user observes. Each has at least one executable test in `tests/Terminal.Gui.Editor.IntegrationTests/` (filenames listed in § Tests below).

### Scenario — Ctrl+Alt+Down adds a caret on the line below

**Given** the caret is at offset 8 in `"longer line\nshrt\nanother line"` (column 8 on line 1),
**When** the user presses `Ctrl+Alt+Down` twice,
**Then** the primary caret stays at offset 8; two additional carets land at offsets 16 and 25 (column 8 on lines 2 and 3 measured by visual column, falling back to end-of-line on the short middle line).

### Scenario — Ctrl+Alt+Up adds a caret on the line above

Symmetric to the previous scenario. **Given** carets at lines `n`, `n-1`, …, `n-k`, **When** the user presses `Ctrl+Alt+Up`, **Then** a new additional caret appears at line `n-k-1` at the sticky visual column. Pressing `Ctrl+Alt+Up` past line 1 is a no-op.

### Scenario — Sticky virtual column survives a short intervening line

**Given** `"abcde\nx\nabcde"` and the primary caret at column 4 on line 1, **When** the user presses `Ctrl+Alt+Down` twice, **Then** the second additional caret lands at column 4 on line 3 (offset `"abcde\nx\nabcd".Length`), not at the column the short line 2 collapsed to.

### Scenario — Sticky virtual column with tabs

**Given** `"a\tbcde\na\tbcde\na\tbcde"` with `IndentationSize` defaulting to 4 and the caret at offset 3 (visual column 5, after `a` + tab), **When** the user presses `Ctrl+Alt+Down` twice, **Then** additional carets land at offset 3 within each subsequent line (`"a\tbcde\n".Length + 3` and `"a\tbcde\na\tbcde\n".Length + 3`) — i.e. the visual column is preserved, accounting for tab expansion.

### Scenario — Shift+Alt+Drag creates a vertical column of carets

**Given** the document `"abcd\nabcd\nabcd"`, **When** the user presses `Shift+Alt + LeftButton` at view position (1, 0) and drags to (1, 2), then releases,
**Then** the primary caret is at offset 1; two additional carets exist at offsets 6 and 11; no selection is active. (Note: VS Code's `Shift+Alt+drag` *selects* a column; we emit carets only — see Out of Scope and Comparison.)

### Scenario — Esc dismisses the vertical block

**Given** vertical carets on lines 1–3 in `"abcd\nabcd\nabcd\nabcd"` produced from `Ctrl+Alt+Down × 2` starting at offset 1, **When** the user presses `Esc`, **Then** `HasMultipleCarets` is false and `CaretOffset == 1`. Pressing `CursorDown` three times moves the primary caret to offset `"abcd\nabcd\nabcd\n".Length + 1` (line 4, column 1). The previous caret block does **not** trap the primary.

### Scenario — Esc after moving inside the block restores normal Down behavior

**Given** vertical carets on lines 1–3 from `Ctrl+Alt+Down × 2`, **When** the user presses `CursorDown` (moves the block), then `CursorDown` again, then `Esc`, then `CursorDown`, **Then** `CaretOffset == "abcd\nabcd\nabcd\n".Length + 1`. Subsequent down-arrow moves are not limited by where the additional carets used to be.

### Scenario — Down through additional caret does not duplicate

**Given** `"aa\naa\naa"` with the primary caret at offset 1 and two additional carets at offsets 4 and 7 (from `Ctrl+Alt+Down × 2`), **When** the user presses `CursorDown` (primary moves onto the offset of the first additional caret) and types `X`, **Then** the document becomes `"aa\naxa\naxa"` — exactly two `X` insertions, one per *distinct* caret, never three. The additional caret that coincided with the primary is normalized away before the edit, not after.

### Scenario — Tab inserts at every caret

**Given** `"ab\nab\nab"` with three vertical carets at column 1 of each line, **When** the user presses `Tab`, **Then** the document becomes `"a\tb\na\tb\na\tb"` (one tab inserted at every caret, in a single undo step).

### Scenario — Tab twice with spaces produces consistent indentation

**Given** `ConvertTabsToSpaces = true`, the document `"using Ted;\nusing Terminal.Gui.App;\nusing Terminal.Gui.Configuration;"`, and three vertical carets after `"using"` on each line (offsets 5, 16, 39 in the original), **When** the user presses `Tab` once, **Then** every caret moves to the next 4-cell stop, padding with spaces so all three carets remain at the same visual column. **When** the user presses `Tab` again, **Then** every caret moves to the *next* 4-cell stop with the same number of spaces inserted on each line. Concretely:

| Step | Document |
|------|----------|
| start | `"using Ted;\nusing Terminal.Gui.App;\nusing Terminal.Gui.Configuration;"` |
| after Tab 1 | `"using    Ted;\nusing    Terminal.Gui.App;\nusing    Terminal.Gui.Configuration;"` |
| after Tab 2 | `"using        Ted;\nusing        Terminal.Gui.App;\nusing        Terminal.Gui.Configuration;"` |

(This is the bug from the PR #125 thread — the second Tab desynchronized because a downstream visual-line cache was holding stale absolute offsets.)

### Scenario — Ctrl+Click after a vertical block puts the new caret where the user clicked

**Given** vertical carets created via `Ctrl+Alt+Down × 2` and the primary at offset 1 in `"abcd\nabcd\nabcd\nabcd"`, **When** a terminal emits the mouse events for a `Ctrl+LeftButton` press at view (3, 3) in the order `PositionReport+Ctrl` then `LeftButtonPressed+Ctrl` (some terminals reorder this way), **Then** the primary caret does not move and an additional caret appears at offset `"abcd\nabcd\nabcd\nabc".Length`. The pre-press `PositionReport` must not hijack the primary while the user is mid-Ctrl-click. (Ctrl+Click for add-caret-at-click is the existing multi-caret binding; this scenario locks in that the new vertical flow doesn't break it.)

### Scenario — Primary caret is visible after exiting multi-caret mode

**Given** a vertical caret block, **When** the user dismisses it (Esc, plain click, plain arrow key without Ctrl+Alt, or any other multi-caret exit), **Then** the primary caret is still drawn and `UpdateCursor ()` positions the terminal cursor on it. The cursor must not "disappear" or render in a hidden style. *(See § Open Decisions — the maintainer reported this in the PR #125 thread; reproduction steps must be captured as a failing test before implementation.)*

## Requirements

Named requirements — every label is also a search anchor used by tests and code review.

- **Add caret above** — `Ctrl+Alt+CursorUp` adds an additional caret one line above the topmost caret in the current caret block at the sticky visual column. The binding is registered via the standard `KeyBindings.Add` mechanism, not an inline if-chain in `OnKeyDownNotHandled`.
- **Add caret below** — `Ctrl+Alt+CursorDown` does the same below the bottommost caret.
- **Top/bottom bounds are no-ops** — `Ctrl+Alt+CursorUp` past line 1 and `Ctrl+Alt+CursorDown` past the last line do nothing (no throw, no change to existing carets).
- **Column-drag press** — `Shift+Alt + LeftButtonPressed` followed by `PositionReport+Shift+Alt` drag events build a column of carets at the press column from anchor row to active row, replacing any prior selection/multi-caret state. The first row (anchor) is the primary; the other rows are additional. `LeftButtonReleased` ends the drag without altering the state already built. Plain `Shift+LeftButton` (no Alt) continues to extend selection per the existing mouse handler; the Alt modifier is what switches the drag into column-of-carets mode.
- **Column-drag tracks live** — extending the `Shift+Alt`-drag downward adds carets; dragging back up removes the ones below the new active row. The end state must be identical to having pressed once at the final position, modulo event timestamps.
- **Visual column, not char offset** — vertical-column placement uses **cell width**, not raw character offset. Tabs, double-width graphemes, and wrap segments are measured via the same primitives the rendering pipeline uses (`CellVisualLine.GetVisualColumn` / `.GetRelativeOffset`).
- **Sticky column through short lines** — when a line is too short to host the sticky visual column, the caret on that line lands at end-of-line; the sticky column is preserved so that later vertical moves through longer lines restore it (matches the existing single-caret virtual-column behavior).
- **Wrap-aware vertical** — when `WordWrap == true`, "above" and "below" mean the previous/next wrap row, not the previous/next document line. Sticky visual column is preserved across wrap segments using the same `WrapMapEntry` machinery the single caret uses.
- **Tab at every caret** — `Tab` and `Shift+Tab` honor `HasMultipleCarets`: every caret gets its own insertion (or replacement, if a per-caret selection is active), the whole operation is one `RunUpdate` scope, and one undo step reverses all of them.
- **Tab keeps the column aligned** — tabs inserted at multiple carets in one operation must leave every caret at the same visual column afterward (see *Tab twice with spaces* scenario). The post-edit visual column is recomputed from the rebuilt visual lines; a downstream cache that hasn't been invalidated must not stale-feed the recompute.
- **Caret normalization** — additional carets are normalized whenever caret offsets or document structure change:
  - any additional caret whose anchor is `IsDeleted` is dropped;
  - any additional caret coinciding with the primary's offset is dropped (no duplicate edits — see *Down through additional caret does not duplicate* scenario);
  - duplicate additional carets at the same offset collapse to one.
  Normalization runs after every primary-caret move and after every document change, **before** the next edit applies.
- **Esc clears multi-caret and refreshes sticky column** — `Esc` while `HasMultipleCarets` clears additional carets and selection, leaves the primary in place, and refreshes the sticky virtual column to the primary's current visual column so subsequent `CursorUp` / `CursorDown` navigate freely past where the block used to be.
- **Plain click clears multi-caret** — a non-modifier `LeftButtonPressed` while `HasMultipleCarets` clears additional carets and selection and places the primary at the click position (existing behavior — re-stated; the vertical flow must not break it).
- **Ctrl+Click reorder defense** — a `Ctrl+LeftButton` press that follows a vertical-caret block toggles a caret at the click position. `PositionReport+Ctrl` events that arrive *before* the matching `LeftButtonPressed+Ctrl` must not move the primary caret. Reuse the same drag-suppression discipline already in place for `Ctrl+Click`, extended to cover the "report-before-press" reorder.
- **Primary caret stays visible** — the primary caret is always drawn after the additional carets are cleared. `UpdateCursor ()` reports its position; no code path leaves the terminal cursor hidden or pointed at a stale offset after dismissing multi-caret.
- **Cache invalidation on offset shift** — internal `DocumentLine` / `CellVisualLine` caches keyed by line number are invalidated for *all* lines whose element offsets shift, not only lines whose line *number* changes. A multi-caret edit that inserts on three lines but adds no newlines must still invalidate any downstream cached line whose absolute offsets moved.
- **ted demonstrates the feature** — `examples/ted` works end-to-end: `Ctrl+Alt+Up`, `Ctrl+Alt+Down`, `Shift+Alt+Drag`, Tab in vertical mode, Esc to exit. No new ted UI affordance is required (the keybindings are discoverable; existing status bar / help text gets a one-line update — see § Files in Scope).
- **Keybindings overridable** — both chords are registered via `KeyBindings.Add (...)` so a user whose terminal or window manager intercepts `Ctrl+Alt+arrow` (e.g. GNOME workspace switching) or doesn't forward `Shift+Alt` mouse modifiers can rebind. The default binding is the VS Code chord; ted ships no alternate default. The keybindings must round-trip through TG's config (i.e. they show up in any keybinding inspector and can be overridden in user settings).

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.MultiCaret.cs` — new private helpers (`AddCaretVertically`, `AddAdditionalCaretAt`, `NormalizeAdditionalCarets`, `TryGetVerticalOffset`, `GetVisualColumnForOffset`, `SetVerticalCaretsFromViewRows`, `MultiCaretInsertTab`). Replace the corresponding helpers in PR #125 with versions that share infrastructure with the single-caret Up/Down logic rather than re-deriving wrap maps and visual columns.
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs` — `Ctrl+Alt+CursorUp` / `Ctrl+Alt+CursorDown` bindings registered via `KeyBindings.Add` (R8-friendly: no inline if-chain in `OnKeyDownNotHandled`). Esc handler routes through `ClearAdditionalCarets ()` which is responsible for sticky-column refresh.
- `src/Terminal.Gui.Editor/Editor.Mouse.cs` — `Shift+Alt`-drag press/drag/release state machine. Factor the existing Ctrl+Click drag-suppression into a small state enum so Ctrl, Shift+Alt, and plain drags don't fight via three orthogonal booleans. Plain `Shift+LeftButton` extend-selection path is preserved; the Alt bit on the mouse event is what dispatches to the column-of-carets branch.
- `src/Terminal.Gui.Editor/Editor.Indentation.cs` — Tab/Shift-Tab fall through to `MultiCaretInsertTab` / `MultiCaretUnindent` when `HasMultipleCarets`.
- `src/Terminal.Gui.Editor/Editor.cs` — `OnDocumentChanged` runs `NormalizeAdditionalCarets`; `SetCaretOffset` runs `NormalizeAdditionalCarets` *after* the offset is committed; visual-line cache invalidation key set includes lines whose absolute offsets shifted, gated only by `lineDelta == 0 && offsetDelta != 0`.
- `examples/ted/MainWindow.cs` (or wherever help text lives) — one-line update mentioning `Ctrl+Alt+Up/Down` and `Shift+Alt+Drag` in the existing key-help string. Mention the VS Code parity. No new menu items, no new dialogs.
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorTests.cs` — keyboard scenario tests.
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorMouseTests.cs` — mouse scenario tests.
- `specs/public-api.md` — append note that `Ctrl+Alt+CursorUp` / `Ctrl+Alt+CursorDown` create vertical carets and `Shift+Alt+Drag` creates a vertical column (R8).
- `specs/decisions.md` — record the VS Code keybinding choice ("match VS Code chords for vertical multi-caret") as a resolved decision.

## Tests

The PR #125 test set is the executable contract for behavior — but the key combinations in those tests must be rewritten from `Alt` to `Ctrl+Alt` (keys) and `Alt` to `Shift+Alt` (mouse). Re-create these in the new branch, with new names, and require them to be failing-first before the implementation lands:

- `CtrlAltDown_Adds_Vertically_Aligned_Carets` (*Ctrl+Alt+Down adds a caret on the line below*)
- `CtrlAltDown_Preserves_Exact_Column_On_Next_Long_Line_After_Short_Line` (*Sticky virtual column survives a short intervening line*)
- `CtrlAltDown_Preserves_Column_With_Tabs` (*Sticky virtual column with tabs*)
- `ShiftAltDrag_Adds_Vertically_Aligned_Carets` (*Shift+Alt+Drag creates a vertical column of carets*)
- `Esc_Dismisses_MultiCaret_And_Down_Can_Move_Past_Previous_Block` (*Esc dismisses the vertical block*)
- `Esc_After_Moving_Within_MultiCaret_Allows_Moving_Below_Last_Former_Multi` (*Esc after moving inside the block*)
- `Vertical_MultiCaret_Does_Not_Duplicate_When_Primary_Moves_Onto_Additional` (*Down through additional caret does not duplicate*)
- `Tab_Inserts_At_All_Carets` (*Tab inserts at every caret*)
- `Tab_Twice_Inserts_Consistently_At_All_Vertical_Carets_With_Spaces` (*Tab twice with spaces*)
- `CtrlClick_After_VerticalCarets_Uses_Click_Position_When_PositionReport_Arrives_First` (*Ctrl+Click after a vertical block*)
- *(new)* `Primary_Caret_Is_Visible_After_Exiting_MultiCaret` (*Primary caret is visible after exiting*) — needs a reliable repro; if `UpdateCursor` isn't visible to the integration host, assert on `CaretOffset` + a render-snapshot showing the caret cell is styled as the primary caret.

Also add a tightly-scoped unit test (`Terminal.Gui.Editor.Tests`) for the visual-line cache rekey: an offset-shift-without-newline-change must invalidate downstream cache entries. This protects the *Tab twice with spaces* scenario from regression at the unit boundary.

## Definition of Done

- [ ] All tests above land **failing first** on a tip-of-`develop` baseline, then pass after the implementation.
- [ ] No new boolean flag in `Editor.Mouse.cs` — the Ctrl/Shift+Alt/plain-drag interaction is expressed as a single state, not three booleans that have to be cleared on every branch.
- [ ] `AddAdditionalCaretAt` and `NormalizeAdditionalCarets` are the only paths that mutate `_additionalCarets`. `ToggleCaretAt` is rewritten in terms of them.
- [ ] `Editor.cs` change to visual-line cache invalidation is exercised by a unit test (not just observed through the *Tab twice* scenario).
- [ ] `examples/ted` demonstrates the feature; help text mentions the keybindings.
- [ ] `dotnet format` and `dotnet jb cleanupcode` are clean.
- [ ] Performance: a 1000-line document with 100 vertical carets typing a character must complete the edit in one `RunUpdate` scope and not regress the existing multi-caret BenchmarkDotNet metric (perf-gate: <1.3× of the current `*VisualLineBuild*` baseline run).

## Out of Scope

- **Column / box selection** (i.e. `Shift+Alt+Drag` producing a *selection per row* the way VS Code does, rather than carets only). This is the natural follow-up. Per-caret selection in the existing multi-caret pipeline already works; column-extend during drag needs a new code path that extends each caret's selection anchor as the drag widens/narrows. Ship the carets-only flow first.
- **Find/replace across multi-caret selections**. Already excluded by multi-caret spec.
- **Reflowing the vertical block under WordWrap toggling**. If the user toggles `WordWrap` while a vertical block is live, the block is dismissed. This spec does not introduce reflow semantics.
- **A ted UI menu / dialog**. The keybindings ship discoverable via help text only.
- **Changing the existing `Ctrl+Click` add-caret-at-click binding to `Alt+Click`** to match VS Code / VS. Tracked as the *Alt+Click alias* open decision below.

## Reference behavior from PR #125

PR #125 (copilot, draft) shipped the same user-visible features (under the older `Alt+Up/Down` / `Alt+Drag` chords) but was rejected for being hacky:

- Maintainer feedback in the PR thread, in order: (1) "basically non-functional. The first time I use it, it basically works. But then all cursor/caret management is messed up." (2) After fix attempt: "still very broken. Eg after dismissing multi-caret, main caret won't move below where last multi was. Tabs don't insert at all carets. Similar issues as before with carets being placed -1 column from row above when alt-down is pressed in some cases." (3) After another fix attempt: "when vert carets are active, trying to add another caret with ctrl-click puts it in the wrong location." (4) After another fix attempt: "tabs are not working right. I vertically select 3 rows and press tab. adds 4 spaces as expected. hit tab again, get this:" (followed by misaligned screenshot). (5) Final, unresolved: "now the main cursor/caret disappears after exiting multi mode."
- Each fix in that PR addressed the symptom of the latest bug, leaving an accreted patch on the multi-caret machinery rather than a designed surface. The mouse handler in particular now carries three `_suppress…UntilRelease` booleans whose interactions are not obvious.
- The user-visible feature set (vertical carets via keyboard, vertical carets via drag, Tab-at-all-carets, normalization, Ctrl+Click after vertical) is the right set. The tests in that PR are the executable spec, modulo the keybinding rename to the VS Code chords. The implementation is throwaway.

## Comparison with VS Code and Visual Studio 2026

Cross-walk of every user-facing behavior against the two reference editors. Where this spec diverges, the divergence is intentional and called out below.

| Behavior | VS Code | Visual Studio 2022/2026 | This spec |
|---|---|---|---|
| Add caret above / below | `Ctrl+Alt+Up` / `Ctrl+Alt+Down` (Win/Linux); `Cmd+Opt+Up/Down` (Mac) | `Alt+Shift+Up` / `Alt+Shift+Down` (`Edit.InsertCaretAbove` / `Below`) | **Match VS Code**: `Ctrl+Alt+Up` / `Ctrl+Alt+Down` |
| Add caret at click | `Alt+Click` | `Alt+Click` (multi-cursor placement) | `Ctrl+Click` (existing — see multi-caret spec and *Alt+Click alias* open decision below) |
| Column / box selection by drag | `Shift+Alt + drag` produces a *selection per row* (column select) | `Shift+Alt + drag` produces a column / box selection | **Match modifier, not semantics**: `Shift+Alt + drag` produces *carets per row, no selection* (column-select variant is out of scope for this iteration) |
| Esc collapses to primary caret | Yes | Yes | Match (*Esc dismisses the vertical block* scenario) |
| Sticky desired column through short lines | Yes | Yes | Match (*Sticky virtual column survives a short intervening line* scenario / *Sticky column through short lines* requirement) |
| Tab inserts at every caret, single undo | Yes | Yes | Match (*Tab inserts at every caret* / *Tab keeps the column aligned* requirements) |
| Caret-on-caret normalization (no duplicate edits) | Yes (carets at same offset collapse silently) | Yes | Match (*Caret normalization* requirement) |
| Wrapped-line vertical navigation | "Above" / "below" follows wrap rows | Same | Match (*Wrap-aware vertical* requirement) |
| WordWrap toggle while multi-caret is live | VS Code preserves carets at nearest valid offset | VS preserves carets | **Diverge**: block is dismissed (Out of Scope) |

### Intentional divergences (and why)

1. **`Shift+Alt+drag` produces carets only, not a column selection.** VS Code's `Shift+Alt+drag` creates a *selection per row* — typing replaces a column of text. This spec ships only the carets-per-row variant first. The full column-select is the natural follow-up; per-caret selection already works in the pipeline, but extend-during-drag is a new code path. **User-visible consequence**: to "replace" a column, the user must `Shift+Alt`-drag, then `Shift+Right`/`Left` to grow each caret's selection, then type. Document this in ted help.

2. **`Ctrl+Click` vs `Alt+Click` for "add caret at click".** Existing multi-caret on `develop` uses `Ctrl+Click`. VS Code and VS use `Alt+Click`. Changing the existing binding is out of scope for this spec — flagged as the *Alt+Click alias* open decision below.

3. **WordWrap toggle dismisses the block.** Both reference editors preserve carets through a wrap toggle. We dismiss because the carets' wrap-row positions are no longer well-defined under the new wrap state and we don't want to silently snap them to surprising offsets. Could revisit post-beta.

### Behaviors we match deliberately

- **Keybinding chords** (`Ctrl+Alt+Up/Down`, `Shift+Alt+drag` modifier) — match VS Code so users coming from VS Code or `code-insiders` keep their muscle memory.
- **Sticky desired column through short lines and tab-expanded columns** — both reference editors track visual column, not character offset.
- **Tab at every caret, one undo step** — both editors.
- **Esc dismisses the block, leaves the primary caret in place, allows continued navigation past where the block was** — both editors.
- **No duplicate edits when the primary lands on an additional caret's offset** — both editors silently dedupe.

## Open Decisions

- **Primary caret disappears after exiting** — the maintainer reported this in the PR #125 thread; the implementer could not reproduce. Before this spec moves to **Ready**, the reproduction must be captured as a failing integration test (or the bug confirmed not-reproducible on the latest `develop` and the corresponding requirement marked done). Candidate causes worth probing: `_virtualCaretColumn` refresh inside `ClearAdditionalCarets` racing with `UpdateCursor`; a stale `_caretAnchor` after multi-caret edits; a styled-cell that overdraws the caret cell because a transformer's stale element range survives the multi-caret edit.

- **`ClearAdditionalCarets` visibility** — today it is `public`. With this spec, the only sane caller is `Editor` itself (Esc handler, plain-click handler, `WordWrap` toggle). R9 says public surface needs a consumer; if ted doesn't call it, this should drop to `internal`. Resolve before merging.

- **Alt+Click alias for add-caret-at-click** — existing multi-caret uses `Ctrl+Click`. Both VS Code and VS use `Alt+Click`. This spec does *not* change the binding, but adding `Alt+Click` as an *alias* (both work, no breakage) is a small win for newcomers. Out of scope here; worth filing as a follow-up if the maintainer agrees.

- **Terminal/WM workspace-switch collisions** — `Ctrl+Alt+arrow` is grabbed by some Linux desktops for workspace switching. *Keybindings overridable* says rebind, but should `examples/ted` ship a fallback chord (e.g. `Alt+Shift+Up/Down` matching VS) for environments where the primary chord is unreachable? Default: no second default binding, but if user testing shows the primary is unreachable for a noticeable fraction of users, revisit.

## Resolved Decisions

These were open in earlier drafts of this spec and are now resolved.

- **Keybinding choice for the new chords.** Resolved 2026-05-15: match VS Code. `Ctrl+Alt+Up`/`Down` for add-caret-above/below; `Shift+Alt + drag` modifier for the column-drag. Cross-link to `specs/decisions.md` when that entry is written.

## Notes

- This spec rebuilds the user-facing functionality of PR #125 from the tests it shipped; it is not a "fix-forward" of that branch. The intended workflow is: open a new branch from `develop`, port the PR #125 tests verbatim (renaming as needed, **including swapping the key combos to the VS Code chords**), confirm they fail, then write the implementation against the requirements above.
- The visual-line cache fix (see *Cache invalidation on offset shift* requirement and the *Tab twice* scenario) is the most subtle defect the test set exposes. Treat it as the riskiest piece — write the unit test in `Terminal.Gui.Editor.Tests` before touching the cache.
- R5 (single `Document.OpenUpdateScope ()` per multi-caret edit) is non-negotiable. Tab at N carets is one undo step, not N.
- R8: append two lines to `specs/public-api.md` describing the new keybindings. No new public Editor API is introduced by this spec — the existing `AdditionalCaretOffsets` / `HasMultipleCarets` / `ToggleCaretAt` / `ClearAdditionalCarets` surface is sufficient.
]]>