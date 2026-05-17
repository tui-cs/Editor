# Feature Specification: Find & Replace

**Status**: Done — engine + ted UI (PRs #76/#79) plus hit-highlight renderer + F3/Shift+F3/Ctrl+F/Ctrl+H keybindings (PR #104, merged into `develop` 2026-05-13); issue #100 closed
**Created**: 2026-05-10
**Last updated**: 2026-05-17
**Depends on**: search ✅ (lift landed in PR #76), rendering-pipeline ✅
**Blocked by**: —

## Overview

Migrate the existing find/replace implementation from bespoke `string.IndexOf` to the `ISearchStrategy` abstraction (search lift). Expose `Editor.SearchStrategy` as the single seam, surface regex / whole-word / case-sensitivity through ted's `FindReplaceDialog`, and keep `ReplaceAll` collapsing into a single undo step. Hit-highlight rendering and the F3 / Ctrl+F / Ctrl+H keybindings remain as a follow-up slice.

The existing `Editor.FindReplace.cs` already had `FindNext` / `FindPrevious` / `ReplaceNext` / `ReplaceAll` — this work replaces their internals with the `ISearchStrategy` seam, adds the property entry point, and wires the toggle UI in ted.

## User Scenarios

### Scenario 1 — Regex search ✅

**Given** the find dialog with regex mode enabled, **When** the user searches for `\d{3}`, **Then** all three-digit number sequences are found.

### Scenario 2 — Whole-word search ✅

**Given** the find dialog with whole-word mode enabled, **When** the user searches for "cat", **Then** only standalone "cat" matches, not "catalog" or "scatter".

### Scenario 3 — Replace all produces one undo step ✅

**Given** a document with 5 occurrences of "foo", **When** the user does Replace All with "bar", **Then** all 5 replacements happen and a single Ctrl+Z undoes all of them.

### Scenario 4 — Hit highlighting (deferred)

**Given** a search for "TODO" with 3 matches in the visible viewport, **When** the search is active, **Then** all 3 matches are visually highlighted via `SearchHitRenderer`.

### Scenario 5 — Hit-highlight invalidation (deferred)

**Given** active search highlights, **When** the document is edited (adding or removing a match), **Then** the highlights update immediately.

### Scenario 6 — F3 wraparound ✅ (wraparound works; F3 keybinding deferred)

**Given** the caret is past the last match, **When** the user invokes Find Next, **Then** the search wraps around to the first match in the document.

### Scenario 7 — Regex backreferences in replacement ✅

**Given** the regex `(\w+)=(\d+)` and replacement `$2:$1`, **When** the user replaces a match `count=42`, **Then** the document contains `42:count`.

## Requirements

- **FR-001** ✅ Replace `_document.Text.IndexOf` calls with `ISearchStrategy` from search.
- **FR-002** ✅ `Editor.SearchStrategy` property is the single search seam; string-based overloads are convenience wrappers that build a `SearchMode.Normal` strategy and delegate.
- **FR-003** (deferred) — Implement `SearchHitRenderer : IBackgroundRenderer` to highlight matches.
- **FR-004** ✅ `ReplaceAll` materializes matches once via `FindAll`, replaces in reverse under one `Document.RunUpdate ()` scope — both for the perf benefit (~4× faster, ~100× less allocation on N=1000 matches per the quick-find benchmark) and the single-step-undo invariant (R5).
- **FR-005** (deferred) — Keybindings: F3 (find next), Shift+F3 (find previous), Ctrl+F (open find), Ctrl+H (open replace).
- **FR-006** (deferred) — Hit highlights invalidate on document change. Depends on FR-003.

## Files in Scope

Landed in this slice:

- `src/Terminal.Gui.Editor/Editor.FindReplace.cs` — engine swap + `SearchStrategy` property.
- `examples/ted/FindReplaceDialog.cs` — Match case / Whole word / Regex checkboxes + regex-error status label.
- `tests/Terminal.Gui.Editor.Tests/EditorFindReplaceTests.cs` — 10 new tests covering property round-trip, regex through Editor, whole-word, backreference substitution, reverse-replace safety, single-step undo with regex.
- `benchmarks/Terminal.Gui.Editor.Benchmarks/FindBenchmarks.cs` + `Program.cs` — BDN engine comparison + `--quick-find` Stopwatch microbench.

Deferred to follow-up:

- `src/Terminal.Gui.Editor/Editor.Commands.cs` (find/replace commands)
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs` (F3/Shift+F3/Ctrl+F/Ctrl+H bindings)
- `src/Terminal.Gui.Editor/Rendering/SearchHitRenderer.cs` (new)

## Definition of Done

Engine + UI slice (this PR):

- [x] `Editor.FindReplace.cs` no longer references `_document.Text.IndexOf`.
- [x] `SearchStrategy` is the single seam for all search operations.
- [x] Regex search test passes.
- [x] Whole-word search test passes.
- [x] `ReplaceAll` undo collapses (single Ctrl+Z undoes all replacements).
- [x] Find-next wraparound test passes.
- [x] ted's `FindReplaceDialog` exposes Match case / Whole word / Regex toggles, builds an `ISearchStrategy` from them, surfaces invalid-regex errors on a status label.

Follow-up slice (separate PR):

- [ ] Hit highlights paint via `SearchHitRenderer : IBackgroundRenderer`.
- [ ] Hit highlights invalidate on document change.
- [ ] Keybindings (F3, Shift+F3, Ctrl+F, Ctrl+H) wired.

## Out of Scope

- TextMate-based search scoping.
- Multi-file search.
- Rope-walking matcher (would fix per-keystroke document materialization in incremental search; out of scope for this lift slice — the lift carries AvaloniaEdit's `RegexSearchStrategy` verbatim, which itself materializes `document.Text` once per `FindAll`).

## Notes

- rendering-pipeline and search are both done; FR-003 / FR-005 / FR-006 are deferred not because of blockers but because the engine + UI slice is large enough to merit its own review.
- The `Editor.SearchStrategy` property is a new public surface — `specs/public-api.md` updated.
- Benchmarks (Stopwatch microbench, `dotnet run --project benchmarks/Terminal.Gui.Editor.Benchmarks -c Release -- --quick-find`):

  | Matches | New (ms) | Old (ms) | Speedup | Old alloc | New alloc | Memory ratio |
  |---|---|---|---|---|---|---|
  | 10 | 0.53 | 0.58 | ~equal | 2.2 MB | 493 KB | 4.5× less |
  | 100 | 0.73 | 2.52 | 3.5× faster | 19.9 MB | 621 KB | 32× less |
  | 1000 | 3.55 | 14.84 | 4.2× faster | 191.8 MB | 1.86 MB | 103× less |

  Per-call `FindNext` cost is roughly equal between engines — the per-keystroke incremental-search materialization remains. The win is concentrated in `ReplaceAll`, where the new path replaces N rope materializations with 1.
