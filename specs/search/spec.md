# Feature Specification: Search Engine

**Status**: Ready
**Created**: 2026-05-10
**Depends on**: None
**Blocked by**: None

## Overview

Bring `ISearchStrategy`, `RegexSearchStrategy`, and `SearchResult` from AvaloniaEdit into `src/Terminal.Gui.Text/Search/`. This provides a clean, testable search abstraction that the Editor's find/replace feature (find-and-replace) will consume instead of the current bespoke `string.IndexOf` approach.

The lift is straightforward — no Avalonia GUI dependencies are expected beyond a pro-forma check.

## User Scenarios

### Scenario 1 — Case-sensitive search

**Given** a `TextDocument` containing "Hello hello HELLO", **When** searching with case sensitivity enabled for "Hello", **Then** only one result at offset 0 is returned.

### Scenario 2 — Case-insensitive search

**Given** a `TextDocument` containing "Hello hello HELLO", **When** searching case-insensitively for "hello", **Then** three results are returned.

### Scenario 3 — Whole-word search

**Given** a `TextDocument` containing "cat catalog scatter", **When** searching for whole-word "cat", **Then** only one result at offset 0 is returned.

### Scenario 4 — Regex search

**Given** a `TextDocument` containing "abc 123 def 456", **When** searching with `RegexSearchStrategy` for `\d+`, **Then** two results are returned at the positions of "123" and "456".

### Scenario 5 — Search across line boundaries

**Given** a multi-line `TextDocument`, **When** searching for a pattern that spans a line boundary (e.g. `"end\nstart"`), **Then** the result is returned with the correct offset and length spanning both lines.

### Scenario 6 — Search returns anchored ranges

**Given** a search that found results, **When** the document is edited before the results, **Then** the result offsets remain valid (results are anchored `TextSegment`s).

## Requirements

- **FR-001**: Lift `ISearchStrategy`, `RegexSearchStrategy`, `SearchResult` from AvaloniaEdit.
- **FR-002**: Transform namespace to `Terminal.Gui.Text.Search`.
- **FR-003**: Strip any `using Avalonia.*` directives (pro-forma check).
- **FR-004**: Preserve original formatting and copyright headers per fork policy.
- **FR-005**: Add `// Adapted for Terminal.Gui from AvaloniaEdit <commit-sha>` header line.
- **FR-006**: Append modification rows to `third_party/AvaloniaEdit/UPSTREAM.md`.

## Files in Scope

- `src/Terminal.Gui.Text/Search/*.cs`
- `third_party/AvaloniaEdit/UPSTREAM.md` (append rows)

## Definition of Done

- [ ] All search types compile and are in `Terminal.Gui.Text.Search` namespace
- [ ] Tests in `tests/Terminal.Gui.Text.Tests/Search/` pass — case sensitivity, whole-word, regex flags, search across line boundaries, search returns anchored ranges
- [ ] `UPSTREAM.md` updated with per-file modification log
- [ ] No Avalonia residue

## Out of Scope

- Find/replace UI — that is find-and-replace
- Hit-highlight rendering — that is syntax-colorizer / find-and-replace's `SearchHitRenderer`

## Notes

- Can be done in parallel with folding, indentation, and syntax-highlighting.
- find-and-replace (find/replace migration) is blocked on this item.
