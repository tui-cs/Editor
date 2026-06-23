# tui-cs/Editor — Beta Plan

**Updated**: 2026-05-17 | **Target**: Beta | **Bar**: full MLP feature set + Terminal.Gui's `TextView` marked `[Obsolete]`

> **Alpha shipped 2026-05-12** off `develop` (rolling pre-release stream). This plan supersedes the original MLP/Alpha plan and tracks the work remaining for the **beta** cut.
>
> **All four beta features merged 2026-05-13/14** (find-and-replace tail #104, clipboard #107, word-wrap #106, multi-caret #105). The remaining beta gate is now external/verification work plus the `develop`→`main` cut — see the rescoped Status Snapshot and Definition of Done below.

---

## Beta Definition

The beta of `tui-cs/Editor` ships when:

- **Full MLP feature set is in place and works.** Typing, selection, multi-caret, find/replace (with hit-highlight + standard keybindings), syntax highlighting, folding, soft wrap, line numbers, indentation, clipboard, mouse, undo with sane granularity, large-file responsiveness.
- **`examples/ted` is a TUI editor someone would actually want to use.** Open, edit, save, close. Find. Replace. Toggle wrap. Pick a theme. Multi-caret. Cut/copy/paste from any consumer (not just ted).
- **`tui-cs/clet edit` ships against the beta package and is not embarrassing.** Concrete external-consumer test.
- **Terminal.Gui marks `TextView` as `[Obsolete]`** pointing at `tui-cs/Editor`. Tracked in [tui-cs/Terminal.Gui#5303](https://github.com/tui-cs/Terminal.Gui/issues/5303). The deprecation lands in the TG release that coincides with our beta — the warning needs a real artifact to point at.

The `textmate-grammars` feature ships in the release **after** beta.

## Status Snapshot (2026-05-17)

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
- **ted demo**: file menu, find/replace dialog, theme dropdown, tab controls, status bar, gutter toggles, ted-side clipboard wiring (since lifted into `Editor` — clipboard #107, see Done (beta)).

### Done (beta — landed since the alpha snapshot)

All four beta features merged. Plus follow-on UX/quality work the beta bar implies.

| Feature | Status | PR / Issue | Notes |
|---------|--------|------------|-------|
| [find-and-replace tail](find-and-replace/spec.md) | ✅ Done | PR #104 · issue #100 closed | `SearchHitRenderer : IBackgroundRenderer` + F3 / Shift+F3 / Ctrl+F / Ctrl+H + edit-driven highlight invalidation. |
| [clipboard](clipboard/spec.md) | ✅ Done | PR #107 · issue #101 closed | Cut/Copy/Paste lifted into `Editor` as first-class commands w/ default keybindings + single-step undo. DEC-005: no-selection Cut/Copy is a no-op. |
| [word-wrap](word-wrap/spec.md) | ✅ Done | PR #106 (+#114) · issue #102 closed | `WordWrapStrategy` + `VisualLineBuilder` + `Editor.WordWrap` + ted toggle; mouse/gutter-under-wrap fixes in #114. DEC-005: continuation lines flush at col 0. |
| [multi-caret](multi-caret/spec.md) | ✅ Done | PR #105 (+#121) · issue #103 closed | `AdditionalCaretOffsets`, Ctrl+Click add/remove, per-caret selection, single-step undo across all carets. |
| [vertical-multi-caret](vertical-multi-caret/spec.md) | ✅ Done | PR #133 · issues #124/#125 closed | Ctrl+Alt+Up/Down + Alt+Drag column of carets; re-implemented per spec, superseding throwaway PR #125. Carets-only. |
| [syntax-theme](syntax-theme/spec.md) Phase 2 | ✅ Done | PR #134 · issues #99/#128/#132 closed | xshd colorizer routed through TG `Scheme` code-token `VisualRole`s + ted Theme switcher. Phases 0–1 are TG-repo work. |
| Word navigation | ✅ Done | PR #138 · issue #137 closed | Ctrl+Left/Right + Ctrl+Shift+Left/Right word-wise move/select. |
| Configurable keybindings | ✅ Done | PRs #118/#120 · issue #119 closed | Hardcoded keys migrated to `[ConfigurationProperty] Editor.DefaultKeyBindings`; config tests. |
| ted settings UX | ✅ Done | PR #123 · issue #122 closed | View/Options menus, immediate persistence to `~/.tui`, ported from `clet edit`. |
| ted Markdown preview | ✅ Done | PR #112 · issue #111 closed | Side-by-side highlighted Markdown preview for `.md`, bidirectional scroll sync. |
| End-user docs | ✅ Done | PR #116 · issue #115 closed | `Docs/Help` markdown; README rewrite (PR #110). |

### Remaining for beta

**Composition rule** (constitution R9): every feature is end-to-end — `Terminal.Gui.Editor` model layer + `Editor : View` consumer + `examples/ted` UI wiring, in a single PR. Lift-only PRs are not accepted.

No feature work is left. The remaining beta gate is external coordination, decision-log closure, verification, and the release cut:

| Item | Status | Tracking | Notes |
|------|--------|----------|-------|
| Terminal.Gui `[Obsolete]` TextView |  ✅ Done | | [Terminal.Gui#5303](https://github.com/tui-cs/Terminal.Gui/issues/5303) | TG-side. Lands in the TG release alongside our beta; don't block our cut on theirs. |
| `tui-cs/clet edit` ships against beta |  ✅ Done | | — | External-consumer smoke test (Beta DoD). |
| Open decisions OPEN-001…005 |  ✅ Done | | `decisions.md` | OPEN-005 (`HighlightingColor` mapping) settled by syntax-theme  |
| Verification pass | Pending | Beta DoD | Cross-platform build, all test projects green, `Editor.Tests` ≥90%, perf gate within 3× baseline. |
| `develop` → `main` + `v*` tag cut |  ✅ Done | | release.yml | The merge-to-main + tag is the beta. |
| [textmate-grammars](textmate-grammars/spec.md) | Post-beta | — | Ships in the release **after** beta. Builds on `syntax-colorizer`. |

**Open follow-ups (post-beta candidates, not beta blockers):**

- **Multi-select PR** — `Alt+Drag` producing a *selection per row* (column/box select), not just carets; render additional-caret selections. The natural successor to vertical-multi-caret. Tracked by issue #139 and open PR #142. Per commit `494261d`, this PR **must** restore multi-caret `Tab`/`Shift+Tab` selection-preservation parity with the single-caret `IndentSelectedLines` path.
- Open bugs/PRs awaiting review: #144/#143 (Editor cursor-style preservation on `UpdateCursor`), #141/#140 (swallowed Tab after raw ANSI Shift+Tab in ted).
- **TextView parity gaps** — `Editor` will functionally replace `Terminal.Gui.TextView` (not API/UI-compatible). Survey + per-gap dispositions in [`textview-parity-gap/spec.md`](textview-parity-gap/spec.md); all seven gaps filed as issues #145–#151: autocomplete (#145), overwrite mode (#146), single-line/input mode (#147, decided — DEC-008), kill-ring (#148), context menu (#149), large-file streaming (#150), `IDesignable` (#151). All post-beta.

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

All beta feature work is merged; the dependency graph below is historical (every `tui-cs/Editor` edge resolved). Only the external edge remains.

```
   find-and-replace tail (#100)  ✅ merged (PR #104)
   clipboard             (#101)  ✅ merged (PR #107)
   word-wrap             (#102)  ✅ merged (PR #106)
   multi-caret           (#103)  ✅ merged (PR #105)
   vertical-multi-caret  (#124)  ✅ merged (PR #133) — needed multi-caret + word-wrap + caret-anchors
   syntax-theme Phase 2  (#132)  ✅ merged (PR #134) — consumes TG #5311 via <TerminalGuiVersion>

   TG #5303 (TextView [Obsolete])  — external, on TG release schedule (not a cut blocker)
```

## Beta Definition of Done

Each criterion is testable. This is the merge-to-`main` + `v*` tag gate.

- [x] All beta features merged: find-and-replace tail (#104), clipboard (#107), word-wrap (#106), multi-caret (#105). *(2026-05-14)*
- [ ] `dotnet build Terminal.Gui.Editor.slnx` clean on Linux/macOS/Windows on net10.0. *(verify at cut)*
- [ ] All test projects pass. Coverage: `Editor.Tests` ≥ 90%. `PerformanceTests` smoke tests + the `*VisualLineBuild*` BenchmarkDotNet gate stay within 3× of `benchmarks/baseline.json`. *(verify at cut)*
- [ ] `Editor.OnDrawingContent` does not iterate `text` by `char`. R1, R2, R4, R5 hold. *(carried from MLP; held at alpha — re-verify at cut)*
- [ ] No file under `src/Terminal.Gui.Editor/` references `Terminal.Gui`. *(carried from MLP — re-verify at cut)*
- [ ] ted exercises: typing, selection, multi-caret, undo/redo, find/replace (with highlights + keybindings), folding, word wrap, line numbers, mouse, clipboard, large-file (10 MB < 200 ms initial render). *(all wired; needs an explicit end-to-end pass)*
- [ ] `specs/public-api.md` and `specs/decisions.md` populated; every open decision resolved. **Partial**: DEC-005 (#101 no-selection Cut/Copy) ✅ and DEC-005 word-wrap (#102 continuation-line indent) ✅ logged; DEC-006/007 logged. Still open: OPEN-001…005 (OPEN-005 effectively settled by syntax-theme — confirm + move to Resolved).
- [ ] README documents MIT licensing, AvaloniaEdit attribution, targets, install, and a usage example. *(README rewritten PR #110 — confirm licensing/attribution section present)*
- [ ] `tui-cs/clet edit` builds and ships against the beta package.
- [ ] Terminal.Gui#5303 lands or is committed for the next TG release with the warning message pointing at `tui-cs/Editor`.

## Risks

| Risk | Mitigation |
|------|------------|
| Multi-caret + word-wrap interaction (caret positioning under wrap with secondary carets) | Land word-wrap first, then multi-caret. Add an integration test covering both enabled together. |
| TG `[Obsolete]` release timing skew | Beta release notes call out the TG version expected to carry the warning. Don't block our cut on theirs — TG#5303 can land in a TG release that follows ours. |
| Performance regression from per-caret rendering | `*VisualLineBuild*` gate covers builder; add a multi-caret draw benchmark before merging #103. |
| Fork maintenance creep | `UPSTREAM.md` well-maintained; each AvaloniaEdit lift appended rows during alpha and that habit continues. |

## How to Use This Plan

1. Read `specs/constitution.md`. Internalize rules R1–R10. Reject any implementation path that violates them.
2. All beta features are merged. Remaining work is the external/verification/cut checklist in **Remaining for beta** and the **Beta Definition of Done** — work those, not new feature specs.
3. For post-beta or follow-up work (e.g. the multi-select PR, textmate-grammars), read that feature's `specs/<name>/spec.md` and its tracking issue verbatim before editing.
4. Work on a feature branch off `develop`; merge back to `develop`; route releases through the existing workflow.
5. Track each PR against the Definition of Done in its spec, not the agent's self-report.
6. Update the status table in this file every time an item lands.
7. When all DoD boxes are checked, cut from `develop` to `main` and push a `v*` tag for the beta.
