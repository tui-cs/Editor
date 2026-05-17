# Feature Survey: Terminal.Gui `TextView` capabilities `Editor` lacks

**Status**: Proposed — survey + dispositions; no implementation scheduled
**Created**: 2026-05-17
**Last updated**: 2026-05-17
**Source**: `gui-cs/Terminal.Gui` `develop` (TextView split across `Terminal.Gui/Views/TextInput/TextView/TextView.*.cs`), compared against `Editor` at `develop` post-beta-feature-merge.

## Purpose

A control-by-control read of the current `TextView` against `Editor`, capturing **only** the
user-facing capabilities `TextView` ships that `Editor` does not — and that a consumer **cannot**
trivially reproduce with `Editor`'s existing public API. Pure `TextView` helper conveniences that a
developer can already build on `Editor.Document` / commands / the rendering pipeline are
deliberately excluded (see § Excluded).

This is the input to deciding what (if anything) gets its own `specs/<name>/spec.md`. It does not
itself schedule work. Per the project non-goal *"Source/API compatibility with `TextView` —
`Editor` ships beside it, not as a replacement"* (CLAUDE.md), some of these are genuine product
gaps and some are intentional divergences; each row says which.

## Already at parity (verified — not gaps)

To prevent re-litigating settled ground: `Editor` already has, end-to-end, the `TextView`
behaviors for selection + region highlight, cut/copy/paste, undo/redo, word-wise navigation
(`Ctrl+Left/Right` + extend), sticky column on vertical move, read-only mode, configurable
key bindings, soft word-wrap, syntax highlighting + theming, find/replace with hit-highlight,
wide-grapheme rendering, **double-click word selection and triple-click line selection**
(`Editor.Mouse.cs` handles `LeftButtonDoubleClicked` / `LeftButtonTripleClicked`), mouse-wheel
scrolling, and (beyond `TextView`) folding, multi-caret, and vertical multi-caret.

## Gaps (significant; not trivially replicable)

Ordered roughly by product value.

### 1. In-editor completion / autocomplete popup

**TextView**: `Autocomplete` (`IAutocomplete` / `PopupAutocomplete`, `TextViewAutocomplete`) —
a suggestion dropdown anchored at the caret, with insert/delete-back/set-cursor hooks and
first-priority key handling.

**Editor**: none. `specs/public-api.md` reserves `IEditorCompletionProvider? CompletionProvider`
as **post-MLP**; `decisions.md` **OPEN-002** parks the completion-item shape (reuse TG
`IAutocomplete` vs. a fresh LSP-flavored provider). No code, no spec.

**Replicable by a consumer?** No. A completion UI needs caret-anchored popover placement,
key-event interception ahead of the editor, and edit-coordination with undo grouping — none
exposed today.

**Disposition**: **Spec it post-beta.** Resolve OPEN-002 first. The natural TG-native vehicle is
`PopoverMenu` (already used elsewhere in the codebase) rather than lifting AvaloniaEdit's
`CodeCompletion/` (an explicit non-goal). Becomes `specs/completion/spec.md`.

### 2. Overwrite / insert-replace mode

**TextView**: `Used` flag + `Command.ToggleOverwrite` (Insert key), `Command.EnableOverwrite`,
`Command.DisableOverwrite`; a distinct caret rendering for overwrite; typing replaces the rune
under the caret instead of inserting.

**Editor**: insert-only. No overwrite state, command, or caret style.

**Replicable by a consumer?** No — requires intercepting text input before the document edit and
a mode-aware caret; not expressible through existing commands.

**Disposition**: **Spec it (beta-adjacent, small).** A widely-expected editor mode. Add
`OverwriteMode` state + `Command.ToggleOverwrite/EnableOverwrite/DisableOverwrite` (TG already
defines these `Command` members), Insert-key default binding via the existing
`[ConfigurationProperty] Editor.DefaultKeyBindings`, a block/underline caret variant, and ted
status-bar indicator. Becomes `specs/overwrite-mode/spec.md`.

### 3. Single-line / embeddable-input mode

**TextView**: `Multiline` (false ⇒ single-line field; disables word-wrap, constrains
navigation), `EnterKeyAddsLine` (false ⇒ Enter raises `Accepting` instead of inserting a
newline — form submit), `TabKeyAddsTab` (false ⇒ Tab traverses focus instead of inserting).
Together these let `TextView` be reused as a one-line (or fixed-height) text/code input inside
dialogs and forms.

**Editor**: multi-line only; Enter always inserts a line; Tab always indents/inserts. Cannot be
dropped into a dialog as a single-line code field.

**Replicable by a consumer?** No — these change core key semantics and the layout/scroll
contract; not reachable via current API.

**Disposition**: **Decision required before spec.** This is the sharpest tension with the
*"`Editor` ships beside `TextView`, not as a replacement"* non-goal: a single-line mode is exactly
the `TextView` use case `Editor` deliberately did not target. But a *code-aware single-line/few-line
input* (syntax-highlighted expression field, REPL line) is a real product want that `TextView`
serves poorly. **Decision required** — `decisions.md` **OPEN-006** (→ `DEC-008` on resolution):
does `Editor` add `Multiline` / `EnterKeyAddsLine` (`Accepting`) / `TabKeyAddsTab`, or is
single-line input explicitly ceded to `TextView`? Spec only follows a "yes."

### 4. Emacs kill-ring (kill-to-EOL / kill-to-BOL with append)

**TextView**: `Command.CutToEndOfLine` (Ctrl+K), `Command.CutToStartOfLine`; consecutive kills
**append** to the clipboard (kill-ring semantics), and the Emacs nav defaults (Ctrl+B/F/N/P)
are wired.

**Editor**: clipboard is `Command.Cut/Copy/Paste` only (DEC-005: no-selection cut/copy is a
no-op). No kill-to-line-boundary, no append-on-consecutive-kill.

**Replicable by a consumer?** Partly — plain Emacs *navigation* is just key rebinding (already
possible via configurable bindings, so excluded). The *kill-ring* (line-boundary kill +
append-on-repeat) is **not** replicable: it needs new commands and consecutive-command state.

**Disposition**: **Spec it (small, optional, post-beta).** Add `Command.CutToEndOfLine` /
`CutToStartOfLine` with append-on-consecutive-kill; ship **unbound by default** (Ctrl+K
collides with nothing today, but keep the no-surprise default and let users bind via config).
Becomes `specs/kill-ring/spec.md`. Lower priority — power-user feature.

### 5. Built-in editing context menu

**TextView**: `ContextMenu` (a `PopoverMenu`) on right-click / `Command.Context`, exposing the
standard Cut/Copy/Paste/Select-All/Undo set.

**Editor**: no built-in context menu.

**Replicable by a consumer?** Borderline. A consumer *can* build a `PopoverMenu` and route it to
`Editor`'s commands — but a sensible **default** edit menu is a product affordance users expect
out of the box, and shipping it (and keeping it in sync with read-only / selection state) is more
than a one-liner.

**Disposition**: **Spec it small, or fold into the overwrite/input work.** A default
`ContextMenu` populated from the existing command set, suppressed under `ReadOnly` for mutating
items, opt-out via a property. Could be one section of `specs/overwrite-mode/spec.md` or its own
`specs/context-menu/spec.md`.

### 6. Control-level file Load/Save (incl. stream / large-file)

**TextView**: `Load(string path)`, `Load(Stream)`, `Load(List<Cell>…)`, `CloseFile()` — the
control owns file/stream loading (the `Stream` overload matters for large files).

**Editor**: file I/O lives in `examples/ted`; no Load/Save on the control.

**Replicable by a consumer?** The naive case (`File.ReadAllText` → `Document.Text`) is trivial
helper territory and is **excluded**. The *streaming / large-file* path is not trivial and is
already a recognized open question — `decisions.md` **OPEN-003** ("`LoadAsync(Stream)` /
`SaveAsync` on `Editor` vs. on the document").

**Disposition**: **No new spec — resolve OPEN-003.** Tracked. The trivial path stays a consumer
concern; the async/streaming placement is the only real decision and it already has a slot.

## Excluded (intentional — `TextView` helpers a consumer can already do, or non-goals)

- **`InsertText(string)` / `GetCurrentLine` / `GetLine` / `GetAllLines` / `Lines` /
  `CurrentRow`/`CurrentColumn`/`InsertionPoint`** — all expressible via `Editor.Document`
  (`GetLineByNumber`, offset↔location) + `CaretOffset`. Helper sugar.
- **`IsDirty` / `HasHistoryChanges` / `ClearHistoryChanges`** — derivable from the document
  version / undo stack a consumer already has; ted already tracks dirty this way.
- **`InheritsPreviousAttribute`, `OnDrawNormalColor`/`…SelectionColor`/`…ReadOnlyColor` events,
  per-cell color picker (`PromptForColors`, Ctrl+L)** — `Editor`'s themed rendering pipeline
  (`IVisualLineTransformer` / `IBackgroundRenderer` / syntax-theme) is the deliberate, superior
  replacement; manual per-cell color authoring is a `TextView` quirk, not a target.
- **`ScrollBars`, `ScrollTo`, `UpdateContentSize`, mouse-edge auto-scroll** — scrollbar/scroll
  surface is `Terminal.Gui.View`-level infrastructure `Editor` inherits/extends; not a
  TextView-unique feature.
- **Emacs *navigation* defaults (Ctrl+B/F/N/P), dynamic Enter/Tab when multi-line** — pure key
  rebinding, already possible through the configurable `Editor.DefaultKeyBindings`. (The
  kill-ring *edit* behavior is the non-replicable part — see Gap 4.)
- **`EnableForDesign()` / `IDesignable`** — designer tooling, not an editing capability; out of
  scope for the editor feature set.
- **`UnwrappedCursorPositionChanged` / `WordWrapManager` internals** — `Editor`'s wrap pipeline
  (`WordWrapStrategy` + `WrapMapEntry`) already provides display↔model mapping; the event is a
  thin convenience a consumer can derive from `CaretChanged` + the wrap map.

## Recommended dispositions (summary)

| Gap | Disposition | New artifact |
|-----|-------------|--------------|
| 1. Autocomplete | Post-beta; resolve OPEN-002 first | `specs/completion/spec.md` |
| 2. Overwrite mode | Beta-adjacent, small | `specs/overwrite-mode/spec.md` |
| 3. Single-line/input mode | **Decision first** (OPEN-006 → DEC-008) — tension with non-goal | decision, then maybe spec |
| 4. Kill-ring | Post-beta, optional | `specs/kill-ring/spec.md` |
| 5. Context menu | Small; standalone or folded into #2 | `specs/context-menu/spec.md` (or §) |
| 6. File Load/Save | No new spec — resolve OPEN-003 | `decisions.md` OPEN-003 |

None of these are beta blockers (all four beta features merged — see `specs/plan.md`). They are
post-beta candidates and one open decision. The plan's "Open follow-ups" links here.
