# AvaloniaEdit Upstream Tracking

`Terminal.Gui.Text` carries a hard fork of [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s
pure-data layers. This file pins the upstream commit, lists what was lifted, and records every
modification we made. Re-syncs are deliberate, manual, and against this log — see
`specs/00-plan.md` §5 for fork policy.

## Pinned commit

- **SHA**: `d7a6b63`
- **Subject**: Merge pull request #574 from udlose/performance/insertion-context
- **Date pulled**: 2026-05-08

## Lifted folders

| AvaloniaEdit | → | Terminal.Gui.Text |
|---|---|---|
| `src/AvaloniaEdit/Document/` | → | `src/Terminal.Gui.Text/Document/` |
| `src/AvaloniaEdit/Utils/` (subset) | → | `src/Terminal.Gui.Text/Utils/` |
| `src/AvaloniaEdit/Search/` (subset) | → | `src/Terminal.Gui.Text/Search/` |

## Skipped from `Document/`

- `DataObjectCopyingEventArgs.cs` — Avalonia clipboard interop. The Editor View will plug Terminal.Gui clipboard semantics in directly; we don't need this surface in the document layer.

## Skipped from `Search/`

`src/AvaloniaEdit/Search/` contains both the pure search engine and the Avalonia UI panel. Only the engine is lifted:

- `SearchCommands.cs` — Avalonia routed commands. Replaced by Terminal.Gui key bindings in `Terminal.Gui.Editor`.
- `SearchPanel.cs` / `SearchPanel.xaml` — Avalonia `TemplatedControl`. Replaced by `ted`'s `FindReplaceDialog`.
- `SearchResultBackgroundRenderer.cs` — Avalonia `IBackgroundRenderer`. Will be reimplemented atop Terminal.Gui.Editor's `IBackgroundRenderer` pipeline as part of find-and-replace.

## Skipped from `Utils/`

The Avalonia-UI-specific helpers that the document layer doesn't depend on:

- `DataObjectEx.cs` — Avalonia clipboard.
- `ExtensionMethods.cs` — Avalonia UI extension methods. We did re-export the single `PeekOrDefault<T>(this ImmutableStack<T>)` helper that Rope uses, in `Utils/ImmutableStackExtensions.cs`.
- `PixelSnapHelpers.cs` — Avalonia visual.
- `RichTextWriter.cs` — Avalonia.Media.
- `TextFormatterFactory.cs` — Avalonia.Media.TextFormatting.

## Modifications to lifted files

Each lifted file carries `// Adapted for Terminal.Gui from AvaloniaEdit d7a6b63` as its first line, above the original copyright header (which is preserved verbatim). Beyond that:

| File | Modification |
|---|---|
| All `Document/*.cs`, `Utils/*.cs`, `Search/*.cs` | `namespace AvaloniaEdit.Document` → `namespace Terminal.Gui.Text.Document`; `namespace AvaloniaEdit.Utils` → `namespace Terminal.Gui.Text.Utils`; `namespace AvaloniaEdit.Search` → `namespace Terminal.Gui.Text.Search`; `using AvaloniaEdit.Document` / `using AvaloniaEdit.Utils` rewritten to match. |
| `Document/DocumentLineTree.cs` | Stripped `using Avalonia.Threading;` and the five `Dispatcher.UIThread.VerifyAccess()` call sites (commented out with rationale). The document is no longer thread-affined — that's a UI concern, owned by `Terminal.Gui.Editor`. |
| `Document/TextSegmentCollection.cs` | Same `Avalonia.Threading` strip + one `VerifyAccess()` site stripped. |
| `Search/ISearchStrategy.cs` | Namespace transform only. No Avalonia references upstream. |
| `Search/RegexSearchStrategy.cs` | Namespace transform; `using AvaloniaEdit.Document` → `using Terminal.Gui.Text.Document`. No Avalonia references upstream. Contains both `RegexSearchStrategy` and `SearchResult` (kept as a single file matching upstream layout). Added `#nullable disable` directive after the "Adapted for" line — upstream predates nullable reference types (`IEquatable<T>.Equals` override, `SearchResult.Data` auto-property, and `FindAll().FirstOrDefault()` all trip CS warnings under nullable enable; suppressing per-file matches the fork policy of "minimal targeted edits to lifted source"). **Correctness deviation**: `Equals(ISearchStrategy)` now includes `_matchWholeWords` in the comparison. Upstream omits it, so two strategies that differ only by whole-word matching compare equal — breaks consumer caching/dedup. Surfaced in Copilot review of PR #76. |
| `Search/SearchStrategyFactory.cs` | Namespace transform only. No Avalonia references upstream. Required to construct `RegexSearchStrategy` (which is `internal` upstream and remains so here). **Correctness deviation**: `Create` now rejects empty patterns with `ArgumentException`. Upstream accepts them, compiling to a regex that matches at every position (`TextLength+1` zero-length results) — a DoS hazard in `FindAll` / `ReplaceAll`. Whitespace patterns remain legitimate (they match literal whitespace in Normal mode and the space character in Regex mode). Surfaced in Copilot review of PR #76. |

## New supporting files

- `Utils/ImmutableStackExtensions.cs` — single `PeekOrDefault<T>(this ImmutableStack<T>)` extension method, lifted verbatim from AvaloniaEdit's `Utils/ExtensionMethods.cs`. The rest of `ExtensionMethods.cs` is Avalonia-UI-specific and intentionally not carried.

## Re-sync procedure

To pull a newer AvaloniaEdit revision:

1. Update the pinned commit in this file.
2. For each file in the table above, diff `<new commit>:src/AvaloniaEdit/<path>` against the lifted copy in `src/Terminal.Gui.Text/<path>`.
3. Apply upstream changes, re-asserting the namespace transforms and Avalonia strips listed in **Modifications**.
4. Re-run `tests/Terminal.Gui.Text.Tests` to catch regressions.
5. Update the **Modifications** table if the strip/transform set changed.
