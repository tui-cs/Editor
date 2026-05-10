# gui-cs/Text — MLP Plan

**Updated**: 2026-05-10 | **Target**: Alpha (MLP — Minimum Lovable Product)

---

## MLP Definition

The alpha release of `gui-cs/Text` ships when `Editor` reaches MLP:

- **Most of what people expect from a TUI code editor is in place and works.** Typing, selection, multi-caret, find/replace, syntax highlighting, folding, soft wrap, line numbers, indentation, clipboard, mouse, undo with sane granularity, large-file responsiveness.
- **`examples/ted` is a TUI editor someone would actually want to use.** Open, edit, save, close. Find. Replace. Toggle wrap. Pick a theme.
- **`gui-cs/clet` can ship a `clet edit` subcommand and not be embarrassed.** This is the concrete external-consumer test.

The textmate-grammars feature ships in the release **after** alpha.

## Status Snapshot (2026-05-10)

### Done

- **Repo + CI**: solution (`Terminal.Gui.Text.slnx`), two src csprojs, three test csprojs, `examples/ted`, `examples/EditorBenchmarks` placeholder, GitHub Actions. net10.0, xUnit.v3 exe-style tests.
- **AvaloniaEdit fork**: pinned at `d7a6b63`; `Document/` and `Utils/` lifted; `UPSTREAM.md` tracks modifications.
- **Editor partials**: `Editor.cs`, `Editor.Commands.cs`, `Editor.Keyboard.cs`, `Editor.Mouse.cs`, `Editor.Drawing.cs`, `Editor.Selection.cs`, `Editor.FindReplace.cs`. Caret, sticky virtual column, navigation, editing, undo/redo, selection, mouse, line numbers, find/replace (bespoke, pre-`ISearchStrategy`).
- **rendering-pipeline — Rendering pipeline** ✅: `VisualLineBuilder` → `CellVisualLine` → `CellVisualLineElement` (`TextRunElement`, `TabElement`). `IVisualLineTransformer`, `IBackgroundRenderer` interfaces. Grapheme-aware via `GraphemeHelper`. `Editor.LineTransformers` and `BackgroundRenderers` exposed. (Codex branch, merged with tweaks.)
- **tab-handling — Tab handling** ✅: `IndentationSize`, `ConvertTabsToSpaces`, `ShowTabs` properties. Tab/Shift+Tab insert/indent/unindent. `TabElement` in pipeline. Mouse midpoint snap. Indentation-aware Backspace. (Codex branch, merged with tweaks.)
- **ted demo**: file menu, `FindReplaceDialog`, theme dropdown, tab controls, status bar, line-numbers toggle.

### Remaining (per-feature specs in `specs/<name>/spec.md`)

| Feature | Status | Blocked by |
|---------|--------|------------|
| [folding](folding/spec.md) | Ready | — |
| [search](search/spec.md) | Ready | — |
| [indentation](indentation/spec.md) | Ready | — |
| [syntax-highlighting](syntax-highlighting/spec.md) | Ready | — |
| [drawing-overhaul](drawing-overhaul/spec.md) | Ready | — |
| [word-wrap](word-wrap/spec.md) | Ready | — |
| [caret-anchors](caret-anchors/spec.md) | Ready | — |
| [multi-caret](multi-caret/spec.md) | Blocked | caret-anchors |
| [read-only](read-only/spec.md) | Ready | — |
| [clipboard](clipboard/spec.md) | Ready | — |
| [find-and-replace](find-and-replace/spec.md) | Blocked | search |
| [word-wrap-toggle](word-wrap-toggle/spec.md) | Blocked | word-wrap |
| [folding-ui](folding-ui/spec.md) | Blocked | folding |
| [auto-indent](auto-indent/spec.md) | Blocked | indentation |
| [syntax-colorizer](syntax-colorizer/spec.md) | Blocked | syntax-highlighting, drawing-overhaul |
| [textmate-grammars](textmate-grammars/spec.md) | Blocked | syntax-colorizer |

## Repository Layout

```
specs/                              # spec-kit structure
  constitution.md                   # architectural rules & principles
  plan.md                          # this file — MLP roadmap
  public-api.md                    # Editor target API surface
  decisions.md                     # decision log (resolved + open)
  principal-engineering-tenets.md  # PE tenets reference
  <name>/spec.md                    # per-feature specifications
  runs/                            # experiment run data
  archive/                         # superseded docs
src/Terminal.Gui.Text/             # UI-independent document layer
  Document/  Utils/  Extensions/  Properties/
  (Folding/, Search/, Indentation/, Highlighting/ pending)
src/Terminal.Gui.Editor/           # the View
  Editor.cs / .Drawing / .Keyboard / .Mouse / .Selection / .Commands
  Rendering/                       # rendering pipeline types
tests/
  Terminal.Gui.Text.Tests/         (parallel, pure)
  Terminal.Gui.Editor.Tests/       (parallel logic)
  Terminal.Gui.Editor.IntegrationTests/  (Application.Init)
examples/
  ted/                             (TG demo app)
  EditorBenchmarks/                (placeholder)
third_party/AvaloniaEdit/
  LICENSE  UPSTREAM.md             (commit d7a6b63 pinned)
```

## Dependencies

The diagram shows **must-finish-before** edges. Features not shown are independent.

```
   ┌── folding ─────────┐
   │                    │
   ├── search ──────────┼── ██ rendering-pipeline DONE ██ ─┬─ drawing-overhaul ─┬─ syntax-colorizer ── textmate-grammars
   │                    │                                  │                    │
   ├── indentation ─────┘                                  └─ word-wrap ── word-wrap-toggle
   │
   └── syntax-highlighting ─────────────────────────────────────────────────────┘

   caret-anchors ── multi-caret

   ██ tab-handling DONE ██
   read-only            ── independent
   clipboard            ── independent
   find-and-replace     ── needs search
   folding-ui           ── needs folding
   auto-indent          ── needs indentation
```

### Maximally-parallel start state (post codex merge)

All of these can be picked up immediately: **folding, search, indentation, syntax-highlighting, drawing-overhaul, word-wrap, caret-anchors, read-only, clipboard**.

drawing-overhaul is the new long pole — it migrates the draw loop onto the rendering-pipeline pipeline and must land before syntax-colorizer.

Second wave (after their dependencies): **multi-caret** (after caret-anchors), **find-and-replace** (after search), **word-wrap-toggle** (after word-wrap), **folding-ui** (after folding), **auto-indent** (after indentation), **syntax-colorizer** (after syntax-highlighting + drawing-overhaul).

## MLP Definition of Done

Each criterion is testable. This is the merge-to-`main` gate.

- [ ] All features merged: folding, search, indentation, syntax-highlighting, drawing-overhaul, word-wrap, caret-anchors, multi-caret, read-only, clipboard, find-and-replace, word-wrap-toggle, folding-ui, auto-indent, syntax-colorizer.
- [ ] `dotnet build Terminal.Gui.Text.slnx` clean on Linux/macOS/Windows on net10.0.
- [ ] All three test projects pass. Coverage: `Text.Tests` ≥ 90%, `Editor.Tests` ≥ 75%.
- [ ] `Editor.OnDrawingContent` does not iterate `text` by `char`. R1, R2, R4, R5 hold.
- [ ] `Editor.TabWidth`, `Editor.SyntaxHighlighter`, `Editor.SyntaxLanguage` all removed.
- [ ] No file under `src/Terminal.Gui.Text/` references `Terminal.Gui`.
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

## How to Use This Plan (for a dispatching agent)

1. Read `specs/constitution.md`. Internalize rules R1–R10. Reject sub-agent output that violates them.
2. Pick the maximally-parallel start set from the status table above.
3. For each item, give the sub-agent the `specs/<name>/spec.md` verbatim. Append: the constitution rules and a pointer to `CLAUDE.md`.
4. When drawing-overhaul merges, the second wave (find-and-replace, word-wrap-toggle, folding-ui, auto-indent, syntax-colorizer) becomes parallel-eligible.
5. Track each item's PR against the Definition of Done in its spec, not the agent's self-report.
6. Update the status table in this file every time an item lands.
7. When all DoD boxes are checked, propose the cut from `develop` to `main` and a `v*` tag.
