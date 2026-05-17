# AvaloniaEdit Upstream Tracking

`Terminal.Gui.Editor` carries a hard fork of [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)'s
pure-data layers. This file pins the upstream commit, lists what was lifted, and records every
modification we made. Re-syncs are deliberate, manual, and against this log — see
`specs/00-plan.md` §5 for fork policy.

## Pinned commit

- **SHA**: `d7a6b63`
- **Subject**: Merge pull request #574 from udlose/performance/insertion-context
- **Date pulled**: 2026-05-08

## Lifted folders

| AvaloniaEdit | → | Terminal.Gui.Editor |
|---|---|---|
| `src/AvaloniaEdit/Document/` | → | `src/Terminal.Gui.Editor/Document/` |
| `src/AvaloniaEdit/Utils/` (subset) | → | `src/Terminal.Gui.Editor/Utils/` |
| `src/AvaloniaEdit/Search/` (subset) | → | `src/Terminal.Gui.Editor/Search/` |
| `src/AvaloniaEdit/Folding/` (subset) | → | `src/Terminal.Gui.Editor/Folding/` |
| `src/AvaloniaEdit/Highlighting/` (subset) | → | `src/Terminal.Gui.Editor/Highlighting/` |
| `src/AvaloniaEdit/Highlighting/Xshd/` | → | `src/Terminal.Gui.Editor/Highlighting/Xshd/` |
| `src/AvaloniaEdit/Highlighting/Resources/` (subset) | → | `src/Terminal.Gui.Editor/Highlighting/Resources/` |
| `src/AvaloniaEdit/Indentation/` (subset) | → | `src/Terminal.Gui.Editor/Indentation/` |

## Skipped from `Document/`

- `DataObjectCopyingEventArgs.cs` — Avalonia clipboard interop. The Editor View will plug Terminal.Gui clipboard semantics in directly; we don't need this surface in the document layer.

## Skipped from `Search/`

`src/AvaloniaEdit/Search/` contains both the pure search engine and the Avalonia UI panel. Only the engine is lifted:

- `SearchCommands.cs` — Avalonia routed commands. Replaced by Terminal.Gui key bindings in `Terminal.Gui.Editor`.
- `SearchPanel.cs` / `SearchPanel.xaml` — Avalonia `TemplatedControl`. Replaced by `ted`'s `FindReplaceDialog`.
- `SearchResultBackgroundRenderer.cs` — Avalonia `IBackgroundRenderer`. Will be reimplemented atop Terminal.Gui.Editor's `IBackgroundRenderer` pipeline as part of find-and-replace.

## Skipped from `Folding/`

`src/AvaloniaEdit/Folding/` contains both the data model and Avalonia UI rendering. Only the data model is lifted:

- `FoldingElementGenerator.cs` — Avalonia `VisualLineElementGenerator`. Replaced by `FoldingTransformer : IVisualLineTransformer` in `Terminal.Gui.Editor`.
- `FoldingMargin.cs` — Avalonia `AbstractMargin`. Replaced by fold indicator UI in `Gutter : View`.
- `FoldingMarginMarker.cs` — Avalonia `Control`. Not needed; fold markers rendered directly in `Gutter.OnDrawingContent`.

## Skipped from `Utils/`

The Avalonia-UI-specific helpers that the document layer doesn't depend on:

- `DataObjectEx.cs` — Avalonia clipboard.
- `ExtensionMethods.cs` — Avalonia UI extension methods. We did re-export the single `PeekOrDefault<T>(this ImmutableStack<T>)` helper that Rope uses, in `Utils/ImmutableStackExtensions.cs`.
- `PixelSnapHelpers.cs` — Avalonia visual.
- `RichTextWriter.cs` — Avalonia.Media.
- `TextFormatterFactory.cs` — Avalonia.Media.TextFormatting.

## Skipped from `Highlighting/`

- `HighlightingColorizer.cs` — Avalonia `DocumentColorizingTransformer`. Replaced by a new `HighlightingColorizer : IVisualLineTransformer` in `Rendering/` that uses the cell-grid pipeline.
- `HighlightingDefinitionTypeConverter.cs` — `System.ComponentModel` type converter. Not needed.
- `HtmlClipboard.cs` — Avalonia clipboard interop.
- `HtmlOptions.cs` — References `TextArea` for tab size.
- `HtmlRichTextWriter.cs` — HTML serialization.
- `RichText.cs`, `RichTextModel.cs`, `RichTextModelWriter.cs` — Rich text model types. Not needed for cell-grid rendering.
- `RichTextColorizer.cs` — Avalonia rendering bridge.

## Skipped from `Highlighting/Resources/`

- `ASPX-Mode.xshd`, `Boo.xshd`, `Coco-Mode.xshd`, `Patch-Mode.xshd`, `PHP-Mode.xshd`, `Tex-Mode.xshd`, `MarkDownWithFontSize.xshd` — Less commonly used languages. Can be added on demand.
## Skipped from `Indentation/`

- `CSharp/` — C#-specific smart indentation. Language-specific strategies are out of scope for the initial lift (see `specs/indentation/spec.md`).

## Modifications to lifted files

Each lifted file carries `// Adapted for Terminal.Gui from AvaloniaEdit d7a6b63` as its first line, above the original copyright header (which is preserved verbatim). Beyond that:

| File | Modification |
|---|---|
| All `Document/*.cs`, `Utils/*.cs`, `Search/*.cs` | `namespace AvaloniaEdit.Document` → `namespace Terminal.Gui.Document`; `namespace AvaloniaEdit.Utils` → `namespace Terminal.Gui.Document.Utils`; `namespace AvaloniaEdit.Search` → `namespace Terminal.Gui.Document.Search`; `using AvaloniaEdit.Document` / `using AvaloniaEdit.Utils` rewritten to match. |
| All `Indentation/*.cs` | `namespace AvaloniaEdit.Indentation` → `namespace Terminal.Gui.Text.Indentation`; `using AvaloniaEdit.Document` → `using Terminal.Gui.Document`. |
| `Indentation/DefaultIndentationStrategy.cs` | Replaced `ArgumentNullException` throws with `ArgumentNullException.ThrowIfNull` (modern pattern). Replaced `var previousLine = line.PreviousLine;` with `DocumentLine? previousLine = line.PreviousLine;` (house style: explicit type for non-built-in). Null-check replaced with pattern match (`is null`). |
| `Document/DocumentLineTree.cs` | Stripped `using Avalonia.Threading;` and the five `Dispatcher.UIThread.VerifyAccess()` call sites (commented out with rationale). The document is no longer thread-affined — that's a UI concern, owned by `Terminal.Gui.Editor`. |
| `Document/TextSegmentCollection.cs` | Same `Avalonia.Threading` strip + one `VerifyAccess()` site stripped. |
| `Search/ISearchStrategy.cs` | Namespace transform only. No Avalonia references upstream. |
| `Search/RegexSearchStrategy.cs` | Namespace transform; `using AvaloniaEdit.Document` → `using Terminal.Gui.Document`. No Avalonia references upstream. Contains both `RegexSearchStrategy` and `SearchResult` (kept as a single file matching upstream layout). Added `#nullable disable` directive after the "Adapted for" line — upstream predates nullable reference types (`IEquatable<T>.Equals` override, `SearchResult.Data` auto-property, and `FindAll().FirstOrDefault()` all trip CS warnings under nullable enable; suppressing per-file matches the fork policy of "minimal targeted edits to lifted source"). **Correctness deviation**: `Equals(ISearchStrategy)` now includes `_matchWholeWords` in the comparison. Upstream omits it, so two strategies that differ only by whole-word matching compare equal — breaks consumer caching/dedup. Surfaced in Copilot review of PR #76. **Perf deviation** (gui-cs/Text#82): `FindAll` now drives the regex engine via `Regex.Match(text, startat)` + `NextMatch()` from `offset` instead of `_searchPattern.Matches(text)` over the whole document followed by post-filtering. Upstream re-scans the prefix `[0, offset)` on every call — wasted work for incremental advancing search (one FindNext per F3 keystroke). The .NET regex engine preserves `RegexOptions.Multiline` `^` / `$` semantics across `startat` (anchoring at the start position only when it is 0 or follows a newline). Worth mirroring upstream at AvaloniaEdit. |
| `Search/SearchStrategyFactory.cs` | Namespace transform only. No Avalonia references upstream. Required to construct `RegexSearchStrategy` (which is `internal` upstream and remains so here). **Correctness deviation**: `Create` now rejects empty patterns with `ArgumentException`. Upstream accepts them, compiling to a regex that matches at every position (`TextLength+1` zero-length results) — a DoS hazard in `FindAll` / `ReplaceAll`. Whitespace patterns remain legitimate (they match literal whitespace in Normal mode and the space character in Regex mode). Surfaced in Copilot review of PR #76. |
| All `Folding/*.cs` | `namespace AvaloniaEdit.Folding` → `namespace Terminal.Gui.Document.Folding`; `using AvaloniaEdit.Document` → `using Terminal.Gui.Document`. |
| `Folding/FoldingManager.cs` | Stripped `using Avalonia.Threading;`, `using AvaloniaEdit.Editing;`, `using AvaloniaEdit.Rendering;`. Removed `Dispatcher.UIThread.VerifyAccess()` calls (2 sites). Removed entire `#region Manage TextViews` (AddToTextView/RemoveFromTextView/Redraw — Avalonia `TextView` coupling). Removed entire `#region Install` (FoldingManagerInstallation, FoldingMargin, FoldingElementGenerator — Avalonia UI wiring). Replaced with `FoldingChanged` event for UI notification. Added helper methods: `GetHiddenLineCount()`, `IsLineHidden()`, `GetFoldingAtLine()`, `GetNextVisibleLineNumber()`. |
| `Folding/FoldingSection.cs` | Stripped `using AvaloniaEdit.Rendering;`, `using AvaloniaEdit.Utils;`. Removed `CollapsedLineSection[]` field and `ValidateCollapsedLineSections()` method (Avalonia `CollapsedLineSection` coupling). `IsFolded` setter now raises `_manager.RaiseFoldingChanged()` instead of calling `ValidateCollapsedLineSections()` + `_manager.Redraw()`. `OnSegmentChanged` simplified to call `RaiseFoldingChanged`. Removed `RemoveCollapsedLineSection()` method. |
| `Folding/NewFolding.cs` | Namespace transform only. |
| `Folding/XmlFoldingStrategy.cs` | Namespace transform only. |
| All `Highlighting/*.cs`, `Highlighting/Xshd/*.cs` | `namespace AvaloniaEdit.Highlighting` → `namespace Terminal.Gui.Highlighting`; `namespace AvaloniaEdit.Highlighting.Xshd` → `namespace Terminal.Gui.Highlighting.Xshd`; `using AvaloniaEdit.*` rewritten to match. Added `#nullable disable` after the "Adapted for" line — upstream predates NRT and dozens of declarations trip CS8xxx warnings under nullable enable. |
| `Highlighting/HighlightingBrush.cs` | **Complete rewrite.** Replaced the abstract class hierarchy (`HighlightingBrush` / `SimpleHighlightingBrush` / `SystemColorHighlightingBrush`) based on Avalonia `IBrush` with a simple sealed class wrapping `Terminal.Gui.Drawing.Color?`. TUI has no notion of brushes, gradients, or system-theme-resolved colors — a single `Color?` is the correct replacement. |
| `Highlighting/HighlightingColor.cs` | Dropped `FontFamily` and `FontSize` (irrelevant in TUI). Replaced `FontWeight?` with `bool? Bold`, `FontStyle?` with `bool? Italic`. Updated `Equals` / `GetHashCode` / `MergeWith` / `IsEmptyForMerge` / `ToCss` accordingly. |
| `Highlighting/HighlightedLine.cs` | Stripped `WriteTo`, `ToHtml`, `ToRichTextModel`, `ToRichText` methods — all depend on skipped Avalonia types (`RichTextWriter`, `HtmlRichTextWriter`, `HtmlOptions`, `RichTextModel`, `RichText`). |
| `Highlighting/DocumentHighlighter.cs` | Stripped `Dispatcher.UIThread.VerifyAccess()` call sites. The highlighting engine is no longer thread-affined — thread safety is the caller's responsibility. |
| `Highlighting/HighlightingEngine.cs` | Namespace transforms; `SpanStack` alias updated for `Terminal.Gui.Highlighting` namespace. |
| `Highlighting/HighlightingManager.cs` | Namespace transforms. Stripped `TypeConverter` attribute from `IHighlightingDefinition`. |
| `Highlighting/Resources/Resources.cs` | Changed embedded resource prefix from `AvaloniaEdit.Highlighting.Resources.` to `Terminal.Gui.Editor.Highlighting.Resources.` (matches assembly name). Removed registrations for ASPX, Boo, Coco, Patch, PHP, TeX, MarkDownWithFontSize (not lifted). |
| `Highlighting/Xshd/V2Loader.cs` | `ParseFontWeight` → returns `bool?` (maps "Bold"/"ExtraBold"/"Black"/"Heavy" → `true`, everything else → `false`). `ParseFontStyle` → returns `bool?` (maps "Italic"/"Oblique" → `true`). `ParseColor` → uses `new HighlightingBrush(Color.Parse(color))` instead of Avalonia's `SimpleHighlightingBrush`. `AddRange` calls replaced with `foreach` loops (`IList<T>` doesn't have `AddRange`). Added `XmlReaderExtensions.GetBoolAttribute` extension (lifted from AvaloniaEdit's `Utils/ExtensionMethods.cs`). |
| `Highlighting/Xshd/XmlHighlightingDefinition.cs` | `VisitColor` updated to assign `Bold`/`Italic` instead of `FontWeight`/`FontStyle`. `AddRange` calls in `Merge` replaced with `foreach` loops. |
| `Highlighting/Xshd/XshdColor.cs` | Replaced `FontWeight?` / `FontStyle?` / `FontFamily` / `FontSize` with `bool? Bold` / `bool? Italic`. |
| `Highlighting/Xshd/SaveXshdVisitor.cs` | `WriteColorAttributes` updated to write `bold`/`italic` boolean attributes instead of `fontWeight`/`fontStyle`/`fontFamily`/`fontSize`. |
| `Highlighting/Xshd/V1Loader.cs` | Commented-out stub — V1 format is obsolete and unused by any carried `.xshd` file. Kept as placeholder for future lift if needed. |
| `Highlighting/HighlightingColor.cs` | **Fork addition (syntax-theme, `specs/syntax-theme/spec.md`).** Added `VisualRole? Role` — the Terminal.Gui scheme role this color resolves to (populated at load; consumed by `Rendering/HighlightingColorizer`). Not an upstream concept; AvaloniaEdit bakes colors directly. Uses a `_role` backing field with the same `IsFrozen` guard as `Foreground`/`Background` (post-`Freeze()` immutability). `MergeWith` carries `_role` over; `Equals`/`GetHashCode` include `_role` (a fork addition to the lifted equality members — required because role-only colors differ solely by `Role` and `HighlightingEngine.PushColor` coalesces adjacent sections by color equality). |
| `Highlighting/Xshd/XshdColor.cs` | **Fork addition.** Added optional `string Category` — an xshd `category="..."` attribute (a TG `VisualRole` name) that overrides the built-in name→role table for a single `<Color>`. Not present in upstream xshd. A future xshd re-sync must preserve this attribute and the parser/writer plumbing below. |
| `Highlighting/Xshd/V2Loader.cs` | **Fork addition.** `ParseColorAttributes` reads the new `category=` attribute into `XshdColor.Category`. (Plus the pre-existing font-weight/style and `HighlightingBrush` changes noted above.) |
| `Highlighting/Xshd/XmlHighlightingDefinition.cs` | **Fork addition.** The populating `VisitColor` sets `HighlightingColor.Role = XshdRoleMap.ResolveRole (color.Name, color.Category)` (`category=` wins over the name table). (Plus the pre-existing `Bold`/`Italic` change noted above.) |
| `Highlighting/Xshd/SaveXshdVisitor.cs` | **Fork addition.** `VisitColor` round-trips the new `category` attribute. (Plus the pre-existing `bold`/`italic` change noted above.) |

## Skipped from `Highlighting/`

The Avalonia-UI-specific types that the terminal highlighting engine doesn't need:

- `HighlightingColorizer.cs` — Avalonia `DocumentColorizingTransformer`. Will be reimplemented atop Terminal.Gui.Editor's cell-grid rendering pipeline.
- `HtmlClipboard.cs` — Avalonia clipboard HTML formatting.
- `HtmlOptions.cs` — HTML export options (depends on Avalonia `FontFamily`).
- `HtmlRichTextWriter.cs` — HTML rich-text writer (Avalonia `IBrush` dependencies).
- `RichText.cs` / `RichTextModel.cs` / `RichTextModelWriter.cs` — Avalonia rich-text pipeline. Replaced by Terminal.Gui.Editor's `Attribute`-based rendering.
- `HighlightedLine.WriteTo` / `ToHtml` / `ToRichTextModel` / `ToRichText` methods stripped from `HighlightedLine.cs` (depend on skipped types above).

## Skipped from `Highlighting/Resources/`

Resources for languages not yet carried (can be added later by dropping in the `.xshd` and registering in `Resources.cs`):

- `ASPX.xshd`, `Boo.xshd`, `Coco-Mode.xshd`, `Patch-Mode.xshd`, `PHP-Mode.xshd`, `TeX-Mode.xshd`, `MarkDownWithFontSize.xshd`

## New supporting files

- `Utils/ImmutableStackExtensions.cs` — single `PeekOrDefault<T>(this ImmutableStack<T>)` extension method, lifted verbatim from AvaloniaEdit's `Utils/ExtensionMethods.cs`. The rest of `ExtensionMethods.cs` is Avalonia-UI-specific and intentionally not carried.
- `Highlighting/XshdRoleMap.cs` — **not an AvaloniaEdit file.** Terminal.Gui-native bridge from xshd `<Color name>` values to `VisualRole` code-token roles (`specs/syntax-theme/spec.md`). A re-sync never touches it.

## Re-sync procedure

To pull a newer AvaloniaEdit revision:

1. Update the pinned commit in this file.
2. For each file in the table above, diff `<new commit>:src/AvaloniaEdit/<path>` against the lifted copy in `src/Terminal.Gui.Editor/<path>`.
3. Apply upstream changes, re-asserting the namespace transforms and Avalonia strips listed in **Modifications**.
4. Re-run `tests/Terminal.Gui.Editor.Tests` to catch regressions.
5. Update the **Modifications** table if the strip/transform set changed.
