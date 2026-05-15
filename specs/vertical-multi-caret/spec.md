# Feature Specification: Vertical Multi-Caret (Alt+Up/Down, Alt+Drag)

**Status**: Draft — supersedes the throwaway implementation in PR #125
**Created**: 2026-05-15
**Last updated**: 2026-05-15
**Depends on**: multi-caret ✅, word-wrap ✅, caret-anchors ✅
**Blocked by**: —
**Reference (do not merge)**: [PR #125](https://github.com/gui-cs/Editor/pull/125) — copilot-authored prototype. The functionality is right in the simplest case; the implementation is hacky and the maintainer has documented multiple regressions on it (see § Reference behavior from PR #125 below). Use the test cases from that PR as the executable contract; re-implement the editor changes against this spec.

## Overview

Extend the existing multi-caret machinery (`AdditionalCaretOffsets`, `HasMultipleCarets`, `ToggleCaretAt`, `ClearAdditionalCarets`) with two ergonomic ways to create a **vertically-aligned column of carets** anchored on the same visual column across consecutive lines:

1. **Keyboard**: `Alt+Up` / `Alt+Down` extends the caret block one line above the topmost / below the bottommost caret, landing on the same sticky virtual column.
2. **Mouse**: `Alt + LeftButton drag` creates a column of carets spanning the anchor row → active row at the press column.

Both flows produce a primary caret plus zero or more additional carets, all sharing the multi-caret edit pipeline (single `Document.OpenUpdateScope ()` → one undo step, R5).

The feature also makes Tab work uniformly across all carets in the multi-caret system (an existing gap that vertical-caret usage exposes), and fixes interaction bugs that block the vertical-caret flow from being usable.

## User Scenarios

All scenarios are stated as black-box behavior the user observes. Each has at least one executable test in `tests/Terminal.Gui.Editor.IntegrationTests/` (filenames listed in § Tests below).

### Scenario 1 — Alt+Down adds a caret on the line below

**Given** the caret is at offset 8 in `"longer line\nshrt\nanother line"` (column 8 on line 1),
**When** the user presses `Alt+Down` twice,
**Then** the primary caret stays at offset 8; two additional carets land at offsets 16 and 25 (column 8 on lines 2 and 3 measured by visual column, falling back to end-of-line on the short middle line).

### Scenario 2 — Alt+Up adds a caret on the line above

Symmetric to Scenario 1. **Given** carets at lines `n`, `n-1`, …, `n-k`, **When** the user presses `Alt+Up`, **Then** a new additional caret appears at line `n-k-1` at the sticky visual column. Pressing `Alt+Up` past line 1 is a no-op.

### Scenario 3 — Sticky virtual column survives a short intervening line

**Given** `"abcde\nx\nabcde"` and the primary caret at column 4 on line 1, **When** the user presses `Alt+Down` twice, **Then** the second additional caret lands at column 4 on line 3 (offset `"abcde\nx\nabcd".Length`), not at the column the short line 2 collapsed to.

### Scenario 4 — Sticky virtual column with tabs

**Given** `"a\tbcde\na\tbcde\na\tbcde"` with `IndentationSize` defaulting to 4 and the caret at offset 3 (visual column 5, after `a` + tab), **When** the user presses `Alt+Down` twice, **Then** additional carets land at offset 3 within each subsequent line (`"a\tbcde\n".Length + 3` and `"a\tbcde\na\tbcde\n".Length + 3`) — i.e. the visual column is preserved, accounting for tab expansion.

### Scenario 5 — Alt+Drag creates a vertical column of carets

**Given** the document `"abcd\nabcd\nabcd"`, **When** the user presses `Alt + LeftButton` at view position (1, 0) and drags to (1, 2), then releases,
**Then** the primary caret is at offset 1; two additional carets exist at offsets 6 and 11; no selection is active.

### Scenario 6 — Esc dismisses the vertical block; subsequent navigation starts from the (former) primary

**Given** vertical carets on lines 1–3 in `"abcd\nabcd\nabcd\nabcd"` produced from `Alt+Down × 2` starting at offset 1, **When** the user presses `Esc`, **Then** `HasMultipleCarets` is false and `CaretOffset == 1`. Pressing `CursorDown` three times moves the primary caret to offset `"abcd\nabcd\nabcd\n".Length + 1` (line 4, column 1). The previous caret block does **not** trap the primary.

### Scenario 7 — Esc after moving inside the block restores normal Down behavior past the block

**Given** vertical carets on lines 1–3 from `Alt+Down × 2`, **When** the user presses `CursorDown` (moves the block), then `CursorDown` again, then `Esc`, then `CursorDown`, **Then** `CaretOffset == "abcd\nabcd\nabcd\n".Length + 1`. Subsequent down-arrow moves are not limited by where the additional carets used to be.

### Scenario 8 — Down through additional caret does not duplicate

**Given** `"aa\naa\naa"` with the primary caret at offset 1 and two additional carets at offsets 4 and 7 (from `Alt+Down × 2`), **When** the user presses `CursorDown` (primary moves onto the offset of the first additional caret) and types `X`, **Then** the document becomes `"aa\naxa\naxa"` — exactly two `X` insertions, one per *distinct* caret, never three. The additional caret that coincided with the primary is normalized away before the edit, not after.

### Scenario 9 — Tab inserts at every caret

**Given** `"ab\nab\nab"` with three vertical carets at column 1 of each line, **When** the user presses `Tab`, **Then** the document becomes `"a\tb\na\tb\na\tb"` (one tab inserted at every caret, in a single undo step).

### Scenario 10 — Tab twice with spaces produces consistent indentation

**Given** `ConvertTabsToSpaces = true`, the document `"using Ted;\nusing Terminal.Gui.App;\nusing Terminal.Gui.Configuration;"`, and three vertical carets after `"using"` on each line (offsets 5, 16, 39 in the original), **When** the user presses `Tab` once, **Then** every caret moves to the next 4-cell stop, padding with spaces so all three carets remain at the same visual column. **When** the user presses `Tab` again, **Then** every caret moves to the *next* 4-cell stop with the same number of spaces inserted on each line. Concretely:

| Step | Document |
|------|----------|
| start | `"using Ted;\nusing Terminal.Gui.App;\nusing Terminal.Gui.Configuration;"` |
| after Tab 1 | `"using    Ted;\nusing    Terminal.Gui.App;\nusing    Terminal.Gui.Configuration;"` |
| after Tab 2 | `"using        Ted;\nusing        Terminal.Gui.App;\nusing        Terminal.Gui.Configuration;"` |

(This is the bug from the PR #125 thread — the second Tab desynchronized because a downstream visual-line cache was holding stale absolute offsets.)

### Scenario 11 — Ctrl+Click after a vertical block puts the new caret where the user clicked

**Given** vertical carets created via `Alt+Down × 2` and the primary at offset 1 in `"abcd\nabcd\nabcd\nabcd"`, **When** a terminal emits the mouse events for a `Ctrl+LeftButton` press at view (3, 3) in the order `PositionReport+Ctrl` then `LeftButtonPressed+Ctrl` (some terminals reorder this way), **Then** the primary caret does not move and an additional caret appears at offset `"abcd\nabcd\nabcd\nabc".Length`. The pre-press `PositionReport` must not hijack the primary while the user is mid-Ctrl-click.

### Scenario 12 — Primary caret is visible after exiting multi-caret mode

**Given** a vertical caret block, **When** the user dismisses it (Esc, plain click, plain arrow key without Alt, or any other multi-caret exit), **Then** the primary caret is still drawn and `UpdateCursor ()` positions the terminal cursor on it. The cursor must not "disappear" or render in a hidden style. *(See § Open Decisions OD-1 — the maintainer reported this in the PR #125 thread; reproduction steps must be captured as a failing test before implementation.)*

## Requirements

- **FR-001** — `Alt+CursorUp` adds an additional caret one line above the topmost caret in the current caret block at the sticky visual column. Keybinding lives in `Editor.Keyboard.cs` and is overridable via the standard Terminal.Gui keybinding mechanism.
- **FR-002** — `Alt+CursorDown` does the same below the bottommost caret.
- **FR-003** — `Alt+CursorUp` past line 1 and `Alt+CursorDown` past the last line are no-ops (do not throw, do not move existing carets).
- **FR-004** — `Alt + LeftButtonPressed` followed by `PositionReport+Alt` drag events build a column of carets at the press column from anchor row to active row, replacing any prior selection/multi-caret state. The first row (anchor) is the primary; the other rows are additional. `LeftButtonReleased` ends the drag without altering the state already built.
- **FR-005** — During an Alt-drag, the column tracks the drag cursor live: extending the drag downward adds carets; dragging back up removes the ones below the new active row. (The view must end up identical to having pressed at the final position from the start, modulo timestamps.)
- **FR-006** — Vertical-column placement uses the **visual column** (cell width), not the raw character offset. Tabs, double-width graphemes, and wrap segments are all measured via the same primitives the rendering pipeline uses (`CellVisualLine.GetVisualColumn` / `.GetRelativeOffset`).
- **FR-007** — When a line is too short to host the sticky visual column, the caret on that line lands at end-of-line; the sticky column is preserved so that later vertical moves through longer lines restore it (matches the existing single-caret virtual-column behavior).
- **FR-008** — When `WordWrap == true`, "above" and "below" mean the previous/next wrap row, not the previous/next document line. Sticky visual column is preserved across wrap segments using the same `WrapMapEntry` machinery the single caret uses.
- **FR-009** — `Tab` and `Shift+Tab` honor `HasMultipleCarets`: every caret gets its own insertion (or replacement, if a per-caret selection is active), the whole operation is one `RunUpdate` scope, and one undo step reverses all of them.
- **FR-010** — Tabs inserted at multiple carets in one operation must leave every caret at the same visual column afterward (Scenario 10). The post-edit visual column is recomputed from the rebuilt visual lines; a downstream cache that hasn't been invalidated must not stale-feed the recompute.
- **FR-011** — Additional carets are normalized whenever caret offsets or document structure change:
  - any additional caret whose anchor is `IsDeleted` is dropped;
  - any additional caret coinciding with the primary's offset is dropped (no duplicate edits — Scenario 8);
  - duplicate additional carets at the same offset collapse to one.
  Normalization runs after every primary-caret move and after every document change, **before** the next edit applies.
- **FR-012** — `Esc` while `HasMultipleCarets` clears additional carets and selection, leaving the primary caret in place; the sticky virtual column is refreshed to the primary's current visual column so subsequent `CursorUp` / `CursorDown` navigate freely past where the block used to be (Scenarios 6–7).
- **FR-013** — A plain (non-modifier) `LeftButtonPressed` while `HasMultipleCarets` clears additional carets and selection and places the primary at the click position (existing behavior — re-state for completeness; the vertical flow must not break it).
- **FR-014** — A `Ctrl+LeftButton` press that follows a vertical-caret block toggles a caret at the click position. `PositionReport+Ctrl` events that arrive *before* the matching `LeftButtonPressed+Ctrl` must not move the primary caret (Scenario 11). Reuse the same `_suppressDragUntilRelease` discipline already in place for `Ctrl+Click`, extended to cover the "report-before-press" reorder.
- **FR-015** — The primary caret is always drawn after the additional carets are cleared. `UpdateCursor ()` reports its position; no code path leaves the terminal cursor hidden or pointed at a stale offset after dismissing multi-caret (Scenario 12).
- **FR-016** — Internal `DocumentLine` / `CellVisualLine` caches keyed by line number are invalidated for *all* lines whose element offsets shift, not only lines whose line *number* changes. A multi-caret edit that inserts on three lines but adds no newlines must still invalidate any downstream cached line whose absolute offsets moved.
- **FR-017** — `examples/ted` works end-to-end with this feature: Alt+Up, Alt+Down, Alt+Drag, Tab in vertical mode, Esc to exit. No new ted UI affordance is required (the keybindings are discoverable; existing status bar / help text gets a one-line update — see § Files in Scope).

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.MultiCaret.cs` — new private helpers (`AddCaretVertically`, `AddAdditionalCaretAt`, `NormalizeAdditionalCarets`, `TryGetVerticalOffset`, `GetVisualColumnForOffset`, `SetVerticalCaretsFromViewRows`, `MultiCaretInsertTab`). Replace the corresponding helpers in PR #125 with versions that share infrastructure with the single-caret Up/Down logic rather than re-deriving wrap maps and visual columns.
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs` — `Alt+CursorUp` / `Alt+CursorDown` keybindings (single dispatch each, no inline if-chain). Esc handler routes through `ClearAdditionalCarets ()` which is responsible for sticky-column refresh.
- `src/Terminal.Gui.Editor/Editor.Mouse.cs` — Alt-drag press/drag/release state machine. Factor the existing Ctrl+Click drag-suppression into a small state enum so Ctrl, Alt, and plain drags don't fight via three orthogonal booleans.
- `src/Terminal.Gui.Editor/Editor.Indentation.cs` — Tab/Shift-Tab fall through to `MultiCaretInsertTab` / `MultiCaretUnindent` when `HasMultipleCarets`.
- `src/Terminal.Gui.Editor/Editor.cs` — `OnDocumentChanged` runs `NormalizeAdditionalCarets`; `SetCaretOffset` runs `NormalizeAdditionalCarets` *after* the offset is committed; visual-line cache invalidation key set includes lines whose absolute offsets shifted, gated only by `lineDelta == 0 && offsetDelta != 0`.
- `examples/ted/MainWindow.cs` (or wherever help text lives) — one-line update mentioning Alt+Up/Down and Alt+Drag in the existing key-help string. No new menu items, no new dialogs.
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorTests.cs` — keyboard scenarios (Scenarios 1–4, 6–10, 12).
- `tests/Terminal.Gui.Editor.IntegrationTests/EditorMouseTests.cs` — mouse scenarios (Scenarios 5, 11).
- `specs/public-api.md` — append note that `Alt+CursorUp` / `Alt+CursorDown` create vertical carets (R8).

## Tests

The PR #125 test set is the executable contract. Re-create these in the new branch and require them to be failing-first before the implementation lands:

- `AltDown_Adds_Vertically_Aligned_Carets` (Scenario 1)
- `AltDown_Preserves_Exact_Column_On_Next_Long_Line_After_Short_Line` (Scenario 3)
- `AltDown_Preserves_Column_With_Tabs` (Scenario 4)
- `AltDrag_Adds_Vertically_Aligned_Carets` (Scenario 5)
- `Esc_Dismisses_MultiCaret_And_Down_Can_Move_Past_Previous_Block` (Scenario 6)
- `Esc_After_Moving_Within_MultiCaret_Allows_Moving_Below_Last_Former_Multi` (Scenario 7)
- `Vertical_MultiCaret_Does_Not_Duplicate_When_Primary_Moves_Onto_Additional` (Scenario 8)
- `Tab_Inserts_At_All_Carets` (Scenario 9)
- `Tab_Twice_Inserts_Consistently_At_All_Vertical_Carets_With_Spaces` (Scenario 10)
- `CtrlClick_After_VerticalCarets_Uses_Click_Position_When_PositionReport_Arrives_First` (Scenario 11)
- *(new)* `Primary_Caret_Is_Visible_After_Exiting_MultiCaret` (Scenario 12) — needs a reliable repro; if `UpdateCursor` isn't visible to the integration host, assert on `CaretOffset` + a render-snapshot showing the caret cell is styled as the primary caret.

Also add a tightly-scoped unit test (`Terminal.Gui.Editor.Tests`) for the visual-line cache rekey: an offset-shift-without-newline-change must invalidate downstream cache entries. This protects Scenario 10 from regression at the unit boundary.

## Definition of Done

- [ ] All tests above land **failing first** on a tip-of-`develop` baseline, then pass after the implementation.
- [ ] No new boolean flag in `Editor.Mouse.cs` — the Ctrl/Alt/plain-drag interaction is expressed as a single state, not three booleans that have to be cleared on every branch.
- [ ] `AddAdditionalCaretAt` and `NormalizeAdditionalCarets` are the only paths that mutate `_additionalCarets`. `ToggleCaretAt` is rewritten in terms of them.
- [ ] `Editor.cs` change to visual-line cache invalidation is exercised by a unit test (not just observed through Scenario 10).
- [ ] `examples/ted` demonstrates the feature; help text mentions the keybindings.
- [ ] `dotnet format` and `dotnet jb cleanupcode` are clean.
- [ ] Performance: a 1000-line document with 100 vertical carets typing a character must complete the edit in one `RunUpdate` scope and not regress the existing multi-caret BenchmarkDotNet metric (perf-gate: <1.3× of the current `*VisualLineBuild*` baseline run).

## Out of Scope

- **Column / block selection** (i.e. Alt+Shift+Down creating a *selection* per line rather than a caret per line). That is a follow-up; this spec covers carets-only, no selection at creation time.
- **Find/replace across multi-caret selections**. Already excluded by multi-caret spec.
- **Reflowing the vertical block under WordWrap toggling**. If the user toggles `WordWrap` while a vertical block is live, the block is dismissed (R-future-decision; this spec does not introduce reflow semantics).
- **A ted UI menu / dialog**. The keybindings ship discoverable via help text only.

## Reference behavior from PR #125

PR #125 (copilot, draft) shipped the same user-visible features but was rejected for being hacky:

- Maintainer feedback in the PR thread, in order: (1) "basically non-functional. The first time I use it, it basically works. But then all cursor/caret management is messed up." (2) After fix attempt: "still very broken. Eg after dismissing multi-caret, main caret won't move below where last multi was. Tabs don't insert at all carets. Similar issues as before with carets being placed -1 column from row above when alt-down is pressed in some cases." (3) After another fix attempt: "when vert carets are active, trying to add another caret with ctrl-click puts it in the wrong location." (4) After another fix attempt: "tabs are not working right. I vertically select 3 rows and press tab. adds 4 spaces as expected. hit tab again, get this:" (followed by misaligned screenshot). (5) Final, unresolved: "now the main cursor/caret disappears after exiting multi mode."
- Each fix in that PR addressed the symptom of the latest bug, leaving an accreted patch on the multi-caret machinery rather than a designed surface. The mouse handler in particular now carries three `_suppress…UntilRelease` booleans whose interactions are not obvious.
- The user-visible feature set (Alt+Up/Down, Alt+Drag, Tab-at-all-carets, normalization, Ctrl+Click after vertical) is the right set. The tests in that PR are the executable spec. The implementation is throwaway.

## Open Decisions

- **OD-1 — "Primary caret disappears after exiting multi mode."** The maintainer reported this in the PR #125 thread; the implementer could not reproduce. Before this spec is moved to **Ready**, the reproduction must be captured as a failing integration test (or the bug must be confirmed not-reproducible on the latest `develop` and the requirement marked done). Candidate causes worth probing: `_virtualCaretColumn` refresh inside `ClearAdditionalCarets` racing with `UpdateCursor`; a stale `_caretAnchor` after multi-caret edits; a styled-cell that overdraws the caret cell because a transformer's stale element range survives the multi-caret edit.

- **OD-2 — Alt+Shift+Up/Down semantics.** Reserved. This spec does *not* claim Alt+Shift+arrow; the column-selection variant ships separately.

- **OD-3 — Whether `ClearAdditionalCarets` is a public API.** Today it is `public`. With this spec, the only sane caller is `Editor` itself (Esc handler, plain-click handler, `WordWrap` toggle). R9 says public surface needs a consumer; if ted doesn't call it, this should drop to `internal`. Resolve before merging.

## Notes

- This spec rebuilds the user-facing functionality of PR #125 from the tests it shipped; it is not a "fix-forward" of that branch. The intended workflow is: open a new branch from `develop`, port the PR #125 tests verbatim (renaming as needed), confirm they fail, then write the implementation against the requirements above.
- The visual-line cache fix (Scenario 10 / FR-016) is the most subtle defect the test set exposes. Treat it as the riskiest piece — write the unit test in `Terminal.Gui.Editor.Tests` before touching the cache.
- R5 (single `Document.OpenUpdateScope ()` per multi-caret edit) is non-negotiable. Tab at N carets is one undo step, not N.
- R8: append two lines to `specs/public-api.md` describing the new keybindings. No new public Editor API is introduced by this spec — the existing `AdditionalCaretOffsets` / `HasMultipleCarets` / `ToggleCaretAt` / `ClearAdditionalCarets` surface is sufficient.
