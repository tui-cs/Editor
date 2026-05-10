# Feature Specification: Find & Replace

**Status**: Blocked
**Created**: 2026-05-10
**Depends on**: search, rendering-pipeline ✅
**Blocked by**: search

## Overview

Migrate the existing find/replace implementation from bespoke `string.IndexOf` to the `ISearchStrategy` abstraction (search). Add a `SearchHitRenderer : IBackgroundRenderer` (using rendering-pipeline's pipeline) to visually highlight search matches. Wrap `ReplaceAll` in `Document.OpenUpdateScope ()` so it produces a single undo step. Add standard keybindings (F3, Shift+F3, Ctrl+F, Ctrl+H).

The existing `Editor.FindReplace.cs` already has `FindNext`/`FindPrevious`/`ReplaceNext`/`ReplaceAll` — this work replaces their internals with the proper `ISearchStrategy` seam and adds the missing pieces.

## User Scenarios

### Scenario 1 — Regex search

**Given** the find dialog with regex mode enabled, **When** the user searches for `\d{3}`, **Then** all three-digit number sequences are found and highlighted.

### Scenario 2 — Whole-word search

**Given** the find dialog with whole-word mode enabled, **When** the user searches for "cat", **Then** only standalone "cat" matches are highlighted, not "catalog" or "scatter".

### Scenario 3 — Replace all produces one undo step

**Given** a document with 5 occurrences of "foo", **When** the user does Replace All with "bar", **Then** all 5 replacements happen and a single Ctrl+Z undoes all of them.

### Scenario 4 — Hit highlighting

**Given** a search for "TODO" with 3 matches in the visible viewport, **When** the search is active, **Then** all 3 matches are visually highlighted via `SearchHitRenderer`.

### Scenario 5 — Hit-highlight invalidation

**Given** active search highlights, **When** the document is edited (adding or removing a match), **Then** the highlights update immediately.

### Scenario 6 — F3 wraparound

**Given** the caret is past the last match, **When** the user presses F3, **Then** the search wraps around to the first match in the document.

## Requirements

- **FR-001**: Replace `_document.Text.IndexOf` calls with `ISearchStrategy` from search.
- **FR-002**: `Editor.FindReplace.cs` exposes a `SearchStrategy` property as the single search seam.
- **FR-003**: Implement `SearchHitRenderer : IBackgroundRenderer` to highlight matches.
- **FR-004**: `ReplaceAll` wrapped in `Document.OpenUpdateScope ()` for single-step undo.
- **FR-005**: Keybindings: F3 (find next), Shift+F3 (find previous), Ctrl+F (open find), Ctrl+H (open replace).
- **FR-006**: Hit highlights invalidate on document change.

## Files in Scope

- `src/Terminal.Gui.Editor/Editor.cs`
- `src/Terminal.Gui.Editor/Editor.FindReplace.cs`
- `src/Terminal.Gui.Editor/Editor.Commands.cs`
- `src/Terminal.Gui.Editor/Editor.Keyboard.cs`
- `src/Terminal.Gui.Editor/Rendering/SearchHitRenderer.cs` (new)
- `examples/ted/FindReplaceDialog.cs`

## Definition of Done

- [ ] `Editor.FindReplace.cs` no longer references `_document.Text.IndexOf`
- [ ] `SearchStrategy` is the single seam for all search operations
- [ ] Regex search test passes
- [ ] Whole-word search test passes
- [ ] `ReplaceAll` undo collapses (single Ctrl+Z undoes all replacements)
- [ ] Hit highlights paint via `SearchHitRenderer : IBackgroundRenderer`
- [ ] Hit highlights invalidate on document change
- [ ] F3 wraparound test passes
- [ ] Keybindings (F3, Shift+F3, Ctrl+F, Ctrl+H) wired

## Out of Scope

- TextMate-based search scoping
- Multi-file search

## Notes

- rendering-pipeline is done, but this is still blocked on search (search lift).
- The existing `FindReplaceDialog` in ted will need minor updates to expose regex/whole-word toggles.
