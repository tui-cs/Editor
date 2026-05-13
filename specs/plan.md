# gui-cs/Editor — MLP Plan

**Updated**: 2026-05-10 | **Target**: Alpha (MLP — Minimum Lovable Product)

---

## MLP Definition

The alpha release of `gui-cs/Editor` ships when `Editor` reaches MLP:

- **Most of what people expect from a TUI code editor is in place and works.** Typing, selection, multi-caret, find/replace, syntax highlighting, folding, soft wrap, line numbers, indentation, clipboard, mouse, undo with sane granularity, large-file responsiveness.
- **`examples/ted` is a TUI editor someone would actually want to use.** Open, edit, save, close. Find. Replace. Toggle wrap. Pick a theme.
- **`gui-cs/clet` can ship a `clet edit` subcommand and not be embarrassed.** This is the concrete external-consumer test.

The textmate-grammars feature ships in the release **after** alpha.

## Status Snapshot (2026-05-10)

### Done

- **Repo + CI**: solution (`Terminal.Gui.Editor.slnx`), two src csprojs, three test csprojs, `examples/ted`, `examples/EditorBenchmarks` placeholder, GitHub Actions. net10.0, xUnit.v3 exe-style tests.
- **AvaloniaEdit fork**: pinned at `d7a6b63`; `Document/` and `Utils/` lifted; `UPSTREAM.md` tracks modifications.
- **Editor partials**: `Editor.cs`, `Editor.Commands.cs`, `Editor.Keyboard.cs`, `Editor.Mouse.cs`, `Editor.Drawing.cs`, `Editor.Selection.cs`, `Editor.FindReplace.cs`. Caret, sticky virtual column, navigation, editing, undo/redo, selection, mouse, line numbers, find/replace (bespoke, pre-`ISearchStrategy`).
- **rendering-pipeline — Rendering pipeline** ✅: `VisualLineBuilder` → `CellVisualLine` → `CellVisualLineElement` (`TextRunElement`, `TabElement`). `IVisualLineTransformer`, `IBackgroundRenderer` interfaces. Grapheme-aware via `GraphemeHelper`. `Editor.LineTransformers` and `BackgroundRenderers` exposed. (Codex branch, merged with tweaks.)
- **tab-handling — Tab handling** ✅: `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs` properties. Tab/Shift+Tab insert/indent/unindent. `TabElement` in pipeline. Mouse midpoint snap. Indentation-aware Backspace. (Codex branch, merged with tweaks.)
- **drawing-overhaul — Drawing overhaul** ✅: `OnDrawingContent` is a thin `CellVisualLine` walker, old char-iteration helpers are removed, visual-line draw caching is in place, and line numbers render through `Gutter : View` as a Padding SubView.
- **caret-anchors — Anchor-backed caret & selection** ✅: `CaretOffset` is backed by a `TextAnchor` with `AnchorMovementType.AfterInsertion`; selection uses an anchor plus the caret anchor; manual document-change offset arithmetic is removed.
- **read-only — Read-only mode** ✅: `Editor.ReadOnly` blocks edit commands, replacement APIs, undo/redo, tab indentation, and ted paste/cut/undo/redo paths while leaving navigation and selection active.
- **ted demo**: file menu, `FindReplaceDialog`, theme dropdown, tab controls, status bar, line-numbers toggle.

### Remaining (per-feature specs in `specs/<name>/spec.md`)

**Composition rule** (constitution R9): every feature listed here is end-to-end — `Terminal.Gui.Editor` model layer + `Terminal.Gui.Editor` consumer + `examples/ted` UI wiring, in a single PR. AvaloniaEdit lifts and pure-plumbing sub-features (the old `search`, `indentation`, `folding`, `syntax-highlighting` rows) are subsumed into the feature that ships them to the user. Lift-only PRs are not accepted.

| Feature | Status | Bundles | Notes |
|---------|--------|---------|-------|
| [find-and-replace](find-and-replace/spec.md) | In progress | `Search/` lift (done) + `Editor.SearchStrategy` + ted toggles + `SearchHitRenderer` + keybindings | Lift plumbing landed; engine swap + ted UI in flight. |
| [auto-indent](auto-indent/spec.md) | Ready | `Indentation/` lift + `Editor.IndentationStrategy` + Enter wiring + ted demo | |
| [folding-ui](folding-ui/spec.md) | Ready | `Folding/` lift + `FoldingTransformer` + click-to-toggle + ted demo | |
| [syntax-colorizer](syntax-colorizer/spec.md) | Ready | `Highlighting/` lift (xshd) + colorizer transformer + ted theme integration | Blocks textmate-grammars (post-alpha). |
| [word-wrap](word-wrap/spec.md) | Ready | `VisualLineBuilder` wrap + `Editor.WordWrap` + ted toggle | |
| [multi-caret](multi-caret/spec.md) | Ready | Anchor-based multi-caret + ted demo | |
| [clipboard](clipboard/spec.md) | Ready | Cut/Copy/Paste commands + ted menu wiring | |
| [textmate-grammars](textmate-grammars/spec.md) | Post-alpha | TextMate parser + colorizer integration | Ships in release after alpha. |

## Repository Layout

```
specs/                              # spec-kit structure
  constitution.md                   # architectural rules & principles
  codex-autonomous-sprint.md        # current Codex-only autonomous runbook
  plan.md                          # this file — MLP roadmap
  public-api.md                    # Editor target API surface
  decisions.md                     # decision log (resolved + open)
  principal-engineering-tenets.md  # PE tenets reference
  <name>/spec.md                    # per-feature specifications
  runs/                            # experiment run data
  archive/                         # superseded docs
src/Terminal.Gui.Editor/             # UI-independent document layer
  Document/  Utils/  Extensions/  Properties/
  (Folding/, Search/, Indentation/, Highlighting/ pending)
src/Terminal.Gui.Editor/           # the View
  Editor.cs / .Drawing / .Keyboard / .Mouse / .Selection / .Commands
  Rendering/                       # rendering pipeline types
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

The diagram shows **must-finish-before** edges between user-visible features. Features not shown are independent. AvaloniaEdit lifts are not standalone nodes — they ride inside the feature that ships them.

```
   ██ rendering-pipeline DONE ██ ─┬─ ██ drawing-overhaul DONE ██ ─┬─ syntax-colorizer ── textmate-grammars
                                  │                               │
                                  └─ word-wrap                    └─ folding-ui

   ██ caret-anchors DONE ██ ── multi-caret

   ██ tab-handling DONE ██
   ██ read-only DONE ██

   clipboard          ── independent
   find-and-replace   ── independent (search lift already landed)
   auto-indent        ── independent
```

### Ready start state

All of these are end-to-end (model + Editor + ted) and can be picked up immediately: **find-and-replace, auto-indent, folding-ui, syntax-colorizer, word-wrap, multi-caret, clipboard**.

`textmate-grammars` ships post-alpha and follows `syntax-colorizer`.

## MLP Definition of Done

Each criterion is testable. This is the merge-to-`main` gate.

- [ ] All features merged: drawing-overhaul, caret-anchors, read-only, find-and-replace, auto-indent, folding-ui, syntax-colorizer, word-wrap, multi-caret, clipboard.
- [ ] `dotnet build Terminal.Gui.Editor.slnx` clean on Linux/macOS/Windows on net10.0.
- [ ] All test projects pass. Coverage: `Editor.Tests` ≥ 90%. `PerformanceTests` smoke tests + the `*VisualLineBuild*` BenchmarkDotNet gate stay within 3× of `benchmarks/baseline.json`.
- [ ] `Editor.OnDrawingContent` does not iterate `text` by `char`. R1, R2, R4, R5 hold.
- [ ] `Editor.TabWidth`, `Editor.SyntaxHighlighter`, `Editor.SyntaxLanguage` all removed.
- [ ] No file under `src/Terminal.Gui.Editor/` references `Terminal.Gui`.
- [ ] ted exercises: typing, selection, multi-caret, undo/redo, find/replace, folding, word wrap, line numbers, mouse, large-file (10 MB < 200 ms initial render).
- [ ] `specs/public-api.md` and `specs/decisions.md` populated; every open decision resolved.
- [ ] README documents MIT licensing, AvaloniaEdit attribution, targets, install, usage example.

## Risks

| Risk | Mitigation |
|------|------------|
| Cell-grid math under variable-width graphemes | rendering-pipeline landed — risk partially paid down. drawing-overhaul completes it. |
| AvaloniaEdit threading assumptions | Grep for `VerifyAccess` after each AvaloniaEdit lift. |
| Performance regression vs `TextView` | Populate `EditorBenchmarks` during drawing-overhaul. |
| Fork maintenance | `UPSTREAM.md` well-maintained; each AvaloniaEdit lift must append rows. |

## How to Use This Plan (Codex-only autonomous lane)

1. Read `specs/constitution.md`. Internalize rules R1–R10. Reject any implementation path that violates them.
2. Use `specs/codex-autonomous-sprint.md` as the runbook.
3. Pick one ready feature from the status table above, preferring dependency-unblocking work.
4. Read that feature's `specs/<name>/spec.md` verbatim before editing.
5. Integrate completed work into `experiment/codex/develop`; use feature branches under `experiment/codex/<feature>`.
6. Track each PR against the Definition of Done in its spec, not the agent's self-report.
7. When a dependency feature merges, update the second-wave feature it unblocks.
8. Update the status table in this file every time an item lands.
9. When all DoD boxes are checked, propose the cut from `develop` to `main` and a `v*` tag.
