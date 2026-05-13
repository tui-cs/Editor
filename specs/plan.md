# gui-cs/Editor — Beta Plan

**Updated**: 2026-05-13 | **Target**: Beta | **Bar**: full MLP feature set + Terminal.Gui's `TextView` marked `[Obsolete]`

> **Alpha shipped 2026-05-12** off `develop` (rolling pre-release stream). This plan supersedes the original MLP/Alpha plan and tracks the work remaining for the **beta** cut.

---

## Beta Definition

The beta of `gui-cs/Editor` ships when:

- **Full MLP feature set is in place and works.** Typing, selection, multi-caret, find/replace (with hit-highlight + standard keybindings), syntax highlighting, folding, soft wrap, line numbers, indentation, clipboard, mouse, undo with sane granularity, large-file responsiveness.
- **`examples/ted` is a TUI editor someone would actually want to use.** Open, edit, save, close. Find. Replace. Toggle wrap. Pick a theme. Multi-caret. Cut/copy/paste from any consumer (not just ted).
- **`gui-cs/clet edit` ships against the beta package and is not embarrassing.** Concrete external-consumer test.
- **Terminal.Gui marks `TextView` as `[Obsolete]`** pointing at `gui-cs/Editor`. Tracked in [gui-cs/Terminal.Gui#5303](https://github.com/gui-cs/Terminal.Gui/issues/5303). The deprecation lands in the TG release that coincides with our beta — the warning needs a real artifact to point at.

The `textmate-grammars` feature ships in the release **after** beta.

## Status Snapshot (2026-05-13)

### Done (alpha)

- **Repo + CI**: solution (`Terminal.Gui.Editor.slnx`), two src csprojs, three test csprojs, `examples/ted`, BenchmarkDotNet suite, GitHub Actions for ci / perf / release / downstream-notify. net10.0, xUnit.v3 exe-style tests.
- **AvaloniaEdit fork**: pinned at `d7a6b63`; `Document/`, `Utils/`, `Search/`, `Folding/`, `Indentation/`, `Highlighting/` lifted; `UPSTREAM.md` tracks modifications.
- **Editor partials**: `Editor.cs`, `Editor.Commands.cs`, `Editor.Keyboard.cs`, `Editor.Mouse.cs`, `Editor.Drawing.cs`, `Editor.Selection.cs`, `Editor.FindReplace.cs`. Caret (anchor-backed), sticky virtual column, navigation, editing, undo/redo, selection, mouse, line numbers, gutter (line numbers + folding indicators), find/replace via `ISearchStrategy`.
- **rendering-pipeline** ✅: `VisualLineBuilder` → `CellVisualLine` → `CellVisualLineElement` (`TextRunElement`, `TabElement`). `IVisualLineTransformer`, `IBackgroundRenderer`. Grapheme-aware via `GraphemeHelper`. `Editor.LineTransformers` and `BackgroundRenderers` exposed.
- **tab-handling** ✅: `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs`. Tab / Shift+Tab insert/indent/unindent. `TabElement` in pipeline. Mouse midpoint snap. Indentation-aware Backspace.
- **drawing-overhaul** ✅: `OnDrawingContent` is a thin `CellVisualLine` walker; char-iteration helpers removed; LRU visual-line cache; line numbers via `Gutter : View` Padding SubView (split into `LineNumberGutter` + `FoldingGutter`).
- **caret-anchors** ✅: `CaretOffset` backed by `TextAnchor` with `AnchorMovementType.AfterInsertion`; selection uses anchor + caret anchor; manual offset arithmetic removed.
- **read-only** ✅: `Editor.ReadOnly` blocks edit commands, replacement APIs, undo/redo, tab indentation, and ted paste/cut/undo/redo paths while leaving navigation and selection active.
- **folding-ui** ✅ (PR #96): `FoldingManager`, `FoldingTransformer`, click-to-toggle in `FoldingGutter`, `GutterOptions` flags enum, mousewheel bubbling.
- **syntax-colorizer** ✅ (PR #94): `HighlightingColorizer`, xshd loader integration, ted theme dropdown.
- **auto-indent** ✅ (PR #95): `IIndentationStrategy`, `DefaultIndentationStrategy`, Enter auto-indent wrapped in single undo group.
- **find-and-replace (engine + dialog)**: `Editor.SearchStrategy` swap; regex / whole-word / case-sensitivity toggles; `ReplaceAll` single-step undo via `RunUpdate`. Renderer + keybindings still pending (see Remaining).
- **ted demo**: file menu, find/replace dialog, theme dropdown, tab controls, status bar, gutter toggles, ted-side clipboard wiring (will move into Editor for beta — see Remaining).

### Remaining for beta

**Composition rule** (constitution R9): every feature listed here is end-to-end — `Terminal.Gui.Editor` model layer + `Editor : View` consumer + `examples/ted` UI wiring, in a single PR. AvaloniaEdit lifts and pure-plumbing sub-features are subsumed into the feature that ships them to the user. Lift-only PRs are not accepted.

| Feature | Status | Issue | Notes |
|---------|--------|-------|-------|
| [find-and-replace tail](find-and-replace/spec.md) | Ready | [#100](https://github.com/gui-cs/Editor/issues/100) | `SearchHitRenderer : IBackgroundRenderer` + F3 / Shift+F3 / Ctrl+F / Ctrl+H keybindings + edit-driven highlight invalidation. Engine + ted dialog already landed. |
| [clipboard](clipboard/spec.md) | Ready | [#101](https://github.com/gui-cs/Editor/issues/101) | Lift Cut/Copy/Paste from ted into `Editor` as first-class `Command.Cut/Copy/Paste` with default keybindings and single-step undo. |
| [word-wrap](word-wrap/spec.md) | Ready | [#102](https://github.com/gui-cs/Editor/issues/102) | `WordWrapStrategy` + `VisualLineBuilder` integration + `Editor.WordWrap` + ted toggle. |
| [multi-caret](multi-caret/spec.md) | Ready | [#103](https://github.com/gui-cs/Editor/issues/103) | `AdditionalCaretOffsets`, Ctrl+Click add/remove, per-caret selection, single-step undo across all carets. |
| Terminal.Gui `[Obsolete]` TextView | External | [Terminal.Gui#5303](https://github.com/gui-cs/Terminal.Gui/issues/5303) | TG-side change. Lands in the TG release that ships alongside our beta. |
| [textmate-grammars](textmate-grammars/spec.md) | Post-beta | — | Ships in the release after beta. Builds on `syntax-colorizer`. |

### Subsumed / archived

These lift-only specs are retained for historical reference but are no longer scheduled separately — they ship inside their consumer feature per R9:

- `specs/folding/spec.md` → folded into `folding-ui` (PR #96)
- `specs/syntax-highlighting/spec.md` → folded into `syntax-colorizer` (PR #94)
- `specs/indentation/spec.md` → folded into `auto-indent` (PR #95)
- `specs/search/spec.md` → folded into `find-and-replace`
- `specs/word-wrap-toggle/spec.md` → folded into `word-wrap` (issue #102)

## Repository Layout

```
specs/                              # spec-kit structure
  constitution.md                   # architectural rules & principles
  codex-autonomous-sprint.md        # current Codex-only autonomous runbook
  plan.md                           # this file — Beta roadmap
  public-api.md                     # Editor target API surface
  decisions.md                      # decision log (resolved + open)
  principal-engineering-tenets.md   # PE tenets reference
  <name>/spec.md                    # per-feature specifications
  runs/                             # experiment run data
  archive/                          # superseded docs
src/Terminal.Gui.Editor/             # UI-independent document layer
  Document/  Utils/  Extensions/  Properties/
  Folding/  Search/  Indentation/  Highlighting/
src/Terminal.Gui.Editor/             # the View (namespace Terminal.Gui.Editor)
  Editor.cs / .Drawing / .Keyboard / .Mouse / .Selection / .Commands / .FindReplace
  Rendering/                        # rendering pipeline types
  Gutter/                           # line-number + folding gutter SubViews
tests/
  Terminal.Gui.Editor.Tests/         (parallel, pure)               — ci.yml
  Terminal.Gui.Editor.IntegrationTests/  (parallel, IApplication)  — ci.yml
  Terminal.Gui.Editor.PerformanceTests/(stopwatch perf smoke)      — perf.yml (ubuntu, Release only)
benchmarks/
  Terminal.Gui.Editor.Benchmarks/      (BenchmarkDotNet suite)     — perf.yml
  baseline.json                        (gated metrics)
  compare-baseline.sh                  (CI compare script)
examples/
  ted/                                 (TG demo app)
third_party/AvaloniaEdit/
  LICENSE  UPSTREAM.md                 (commit d7a6b63 pinned)
```

## Dependencies

User-visible features remaining for beta — independent unless an edge is shown.

```
   ── find-and-replace tail (#100)   — independent (engine already landed)
   ── clipboard            (#101)    — independent
   ── word-wrap            (#102)    — independent (rendering-pipeline done)
   ── multi-caret          (#103)    — independent (caret-anchors done)

   TG #5303 (TextView [Obsolete])    — external, on TG release schedule
```

All four `gui-cs/Editor` features can be picked up immediately and worked in parallel.

## Beta Definition of Done

Each criterion is testable. This is the merge-to-`main` + `v*` tag gate.

- [ ] All beta features merged: find-and-replace tail, clipboard, word-wrap, multi-caret.
- [ ] `dotnet build Terminal.Gui.Editor.slnx` clean on Linux/macOS/Windows on net10.0.
- [ ] All test projects pass. Coverage: `Editor.Tests` ≥ 90%. `PerformanceTests` smoke tests + the `*VisualLineBuild*` BenchmarkDotNet gate stay within 3× of `benchmarks/baseline.json`.
- [ ] `Editor.OnDrawingContent` does not iterate `text` by `char`. R1, R2, R4, R5 hold. *(carried from MLP; already true at alpha)*
- [ ] No file under `src/Terminal.Gui.Editor/` references `Terminal.Gui`. *(carried from MLP)*
- [ ] ted exercises: typing, selection, multi-caret, undo/redo, find/replace (with highlights + keybindings), folding, word wrap, line numbers, mouse, clipboard, large-file (10 MB < 200 ms initial render).
- [ ] `specs/public-api.md` and `specs/decisions.md` populated; every open decision resolved. Decisions logged for #101 no-selection Cut/Copy and #102 continuation-line indent policy.
- [ ] README documents MIT licensing, AvaloniaEdit attribution, targets, install, and a usage example.
- [ ] `gui-cs/clet edit` builds and ships against the beta package.
- [ ] Terminal.Gui#5303 lands or is committed for the next TG release with the warning message pointing at `gui-cs/Editor`.

## Risks

| Risk | Mitigation |
|------|------------|
| Multi-caret + word-wrap interaction (caret positioning under wrap with secondary carets) | Land word-wrap first, then multi-caret. Add an integration test covering both enabled together. |
| TG `[Obsolete]` release timing skew | Beta release notes call out the TG version expected to carry the warning. Don't block our cut on theirs — TG#5303 can land in a TG release that follows ours. |
| Performance regression from per-caret rendering | `*VisualLineBuild*` gate covers builder; add a multi-caret draw benchmark before merging #103. |
| Fork maintenance creep | `UPSTREAM.md` well-maintained; each AvaloniaEdit lift appended rows during alpha and that habit continues. |

## How to Use This Plan

1. Read `specs/constitution.md`. Internalize rules R1–R10. Reject any implementation path that violates them.
2. Pick one ready feature from the Remaining table. All four are unblocked and independent — choose by size and risk fit.
3. Read that feature's `specs/<name>/spec.md` and its tracking issue (#100–#103) verbatim before editing.
4. Work on a feature branch off `develop`; merge back to `develop`; route releases through the existing workflow.
5. Track each PR against the Definition of Done in its spec, not the agent's self-report.
6. Update the status table in this file every time an item lands.
7. When all DoD boxes are checked, cut from `develop` to `main` and push a `v*` tag for the beta.
