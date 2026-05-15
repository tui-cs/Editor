# Feature Specification: Syntax-Highlighting Theme/Palette Layer

**Status**: Proposed — issues open; implementation pending
**Created**: 2026-05-15
**Last updated**: 2026-05-15
**Depends on**: syntax-highlighting ✅, syntax-colorizer ✅, [gui-cs/Terminal.Gui#5310](https://github.com/gui-cs/Terminal.Gui/issues/5310) (config benchmarks, Phase 0), [gui-cs/Terminal.Gui#5311](https://github.com/gui-cs/Terminal.Gui/issues/5311) (code-token VisualRoles + Code view + markdown unification, Phase 1)
**Blocked by**: TG #5310 must land first (establishes baseline). TG #5311 lands second and must not regress the baseline. The Editor PR (this repo, Phase 2, tracked in [#132](https://github.com/gui-cs/Editor/issues/132)) bumps `<TerminalGuiVersion>` once #5311 ships.

**Tracking issues**:
- TG Phase 0: [gui-cs/Terminal.Gui#5310](https://github.com/gui-cs/Terminal.Gui/issues/5310) — ConfigurationManager / Scheme / Theme benchmark baseline
- TG Phase 1: [gui-cs/Terminal.Gui#5311](https://github.com/gui-cs/Terminal.Gui/issues/5311) — code-token VisualRoles + Code view + markdown unification
- Editor Phase 2: [gui-cs/Editor#132](https://github.com/gui-cs/Editor/issues/132) — xshd colorizer through TG Scheme

**Closes** (on completion of Phase 2): #99, #128

## Overview

`.xshd` language definitions hardcode foreground colors. Every color hits the screen through one
chokepoint — `HighlightingColorizer.ToAttribute` at
`src/Terminal.Gui.Editor/Rendering/HighlightingColorizer.cs:132` — which converts
`HighlightingColor.Foreground.Color` directly to a `Terminal.Gui.Drawing.Attribute`. There is no
theme indirection, so two problems persist:

- **#128** — JSON's hardcoded colors are unreadable in dark terminals (the literal motivating bug).
- **#99** — The DarkPlus/LightPlus/Monokai theme story from the retired `TextMateSyntaxHighlighter`
  is gone.

The Editor is part of the Terminal.Gui ecosystem. TG already has the right model: `Theme` bundles
named `Scheme`s; each `Scheme` is a map from `VisualRole` → `Attribute`; themes are JSON-driven
through `ConfigurationManager`. TG already includes `VisualRole.Code` (numeric value 10) for
"preformatted or source code content" — a single role today, but the obvious place to grow a
syntax-token vocabulary.

This spec **expands TG's `VisualRole`** with a token-level code vocabulary and routes the xshd
colorizer through TG's existing scheme/theme machinery. It deliberately benefits TG itself, not
just Editor: any TG view rendering code-shaped text (Markdown blocks, log/diff viewers, embedded
REPLs) inherits the palette.

Per DEC-002, xshd is the pre-alpha tokenizer; TextMate grammars are the post-alpha story. This
design is **grammar-agnostic** — a TextMate scope maps to a `VisualRole` the same way an xshd name
does. Only the bridge table differs.

## User Scenarios

### Scenario 1 — Dark terminal renders JSON readably (closes #128)

**Given** a dark terminal and `ted foo.json`, **When** the file renders, **Then** comment, string,
number, bool, and null tokens are visible against the dark background with sufficient contrast,
using the Dark theme's `CodeXxx` Scheme entries.

### Scenario 2 — Theme switch updates highlighting live

**Given** an open document with syntax highlighting active, **When** the user cycles the `Theme`
Shortcut in ted's status bar (which sets `ThemeManager.Theme`), **Then** all visible lines
re-render with the new theme's `CodeXxx` Attributes within one draw cycle.

### Scenario 3 — Theme without code-role overrides still works

**Given** a TG theme (e.g. `TurboPascal 5`) that defines no `CodeXxx` entries, **When** the editor
renders, **Then** all code-token attributes derive through `Code` → `Editable` → `Normal` per
TG's existing derivation chain. No xshd or editor change required for stylized themes to look
acceptable.

### Scenario 4 — xshd name not in the role table falls back to xshd color

**Given** an xshd entry like `<Color name="JavaDocTags" foreground="Blue"/>` with no role mapping
in `XshdRoleMap` and no `category=` attribute, **When** the colorizer renders that token, **Then**
the xshd-declared `Blue` foreground is used (preserves today's behavior for language-specific
names).

### Scenario 5 — Per-xshd category override wins over the default table

**Given** an xshd entry `<Color name="StringInterpolation" category="CodeKeyword" foreground="..."/>`,
**When** the colorizer resolves the role, **Then** `CodeKeyword`'s Attribute is used regardless
of what `XshdRoleMap` says for `StringInterpolation`.

### Scenario 6 — Custom theme via user `.tui` config

**Given** a user's `~/.tui/config.json` overrides the `Dark` theme's `Base.CodeKeyword` Attribute,
**When** the Editor renders with `ThemeManager.Theme = "Dark"`, **Then** the user's color appears
in keywords. No Editor-specific JSON file exists — TG's config system is the single source of
truth.

## Requirements

### Phase 0 — Terminal.Gui ConfigurationManager benchmark baseline PR

This phase is a standalone, prerequisite TG PR. Its sole purpose is to establish baseline
performance numbers for `ConfigurationManager` and `Scheme` so Phase 1's added fields can be
proven non-regressing. TG today has no Configuration benchmarks
(see `Tests/Benchmarks/`); this fills the gap.

#### Add `ConfigurationManager` load benchmark
`Tests/Benchmarks/Configuration/ConfigurationManagerLoadBenchmark.cs` — measure
`ConfigurationManager.Apply()` from cold (embedded `config.json` only, no user `.tui`).
Captures the full load + deserialize + apply path.

#### Add theme-switch benchmark
`Tests/Benchmarks/Configuration/ThemeSwitchBenchmark.cs` — measure
`ThemeManager.Theme = "Dark"; ConfigurationManager.Apply()` against the embedded config.
Run with each built-in theme name as a parameter. This is the user-facing hot path when
cycling a Theme `Shortcut`.

#### Add Scheme attribute-resolution benchmark
`Tests/Benchmarks/Configuration/SchemeAttributeBenchmark.cs` — measure
`Scheme.GetAttributeForRole(VisualRole)` for: an explicitly-set role, a derived role
(unset, traverses the chain), and `Normal`. Expected per-call cost: low-nanosecond field
read for explicit, slightly more for derived. Locks in O(1) lookup.

#### Add Scheme JSON round-trip benchmark
`Tests/Benchmarks/Configuration/SchemeSerializationBenchmark.cs` — measure
serialize-then-deserialize of a representative `Base` Scheme. Catches any regression in
JSON code paths when Phase 1 adds fields.

#### Capture baseline numbers in repo
Run the suite once, commit a `Tests/Benchmarks/Configuration/baseline.json` (or whatever
format `perf-gate.yml` already consumes — match the existing convention). Document the
machine/runtime used in `Tests/Benchmarks/README.md`. The Editor repo's
`benchmarks/baseline.json` + `compare-baseline.sh` pattern is a reasonable reference if
TG doesn't yet have one for any benchmark.

#### Wire baseline comparison into `perf-gate.yml`
`.github/workflows/perf-gate.yml` gains a job (or filter) that runs the Configuration
benchmarks with `--job short` and compares against the baseline. Thresholds match
whatever convention `perf-gate.yml` already enforces for the existing benchmarks; if
none exist, use ">2× wall-time regression fails the gate" as the starting bar.

### Phase 1 — Terminal.Gui code-token VisualRole PR 

**This can be implemented while Phase 0 is being implemented; but it can't be ACCEPTED unitl after**

#### Add code-token VisualRoles
Append 12 entries to `Terminal.Gui/Drawing/VisualRole.cs`, after `Code`:
`CodeComment`, `CodeKeyword`, `CodeString`, `CodeNumber`, `CodeOperator`, `CodeType`,
`CodePreprocessor`, `CodeIdentifier`, `CodeConstant`, `CodePunctuation`, `CodeFunctionName`,
`CodeAttribute`. Preserve numeric values 0–10 for existing roles.

#### Store the new roles on Scheme
Add 12 inline `Attribute?` fields on `Scheme` (one per new role), with paired init-only
properties mirroring the existing `Code` property pattern. Storage cost: ~160 bytes per Scheme;
app-wide ~3–5 KB. No conditional branch in `GetAttributeForRole`.

#### Wire up derivation fallback
Extend `GetAttributeForRoleCore`'s derivation table so unset `CodeXxx` roles resolve to `Code`.
`Code` already chains through `Editable` → `Normal`, so the chain completes for free. Themes that
set zero code roles still render legibly.

#### Populate Dark and Light themes with code colors
Add `CodeXxx` entries to the `Base` scheme of the `Dark` and `Light` themes in
`Terminal.Gui/Resources/config.json`. Other themes (`TurboPascal 5`, `Anders`, `Green Phosphor`,
`Amber Phosphor`) gain no entries — they derive through `Code`.

#### Accept new role names in the config schema
Update `tui-config-schema.json` so new role names are valid. Additive only — existing `.tui`
configs without code-role entries must continue to load without warnings.

#### Verify with tests
Round-trip JSON serialization for the new role names; derivation returns the chain end for
unset roles; explicit overrides return the exact Attribute that was set.

#### No user-perceptible regression vs. Phase 0 baseline
Re-run the four Configuration benchmarks added in Phase 0 and submit results in the PR
description alongside Phase 0's baseline numbers. Acceptance bar: each benchmark must be
**within the perf-gate's regression threshold** (matching whatever Phase 0 wired up, e.g.
">2× wall-time fails"). Specifically:

- `ConfigurationManager.Apply` cold load: regression is acceptable up to the gate
  threshold; user-perceptible perception kicks in around +50ms on app startup, so any
  regression past +20ms requires PR-description justification.
- `ThemeSwitchBenchmark`: theme cycling must remain visually instant — bar is +5ms over
  baseline, hard fail at gate threshold.
- `SchemeAttributeBenchmark`: per-call lookup must stay O(1); regression past +50% on the
  explicit-role case is suspect and requires investigation (a regression here suggests
  the storage choice introduced unexpected indirection).
- `SchemeSerializationBenchmark`: scales with field count by definition; expect a
  proportional ~25–50% increase (12 new fields on a Scheme with ~11 existing). The
  perf-gate threshold absorbs this; flag if the increase is super-linear.

If any benchmark fails, the PR must either fix the regression or document a deliberate
trade-off and update the baseline (with reviewer sign-off).

#### Add a public `Code` view
A new first-class view in `Terminal.Gui/Views/Code.cs` (note: no `View` suffix — Tig's
convention for new view classes; parallels `Markdown`). Public API:

- `string Text { get; set; }` — source text to render.
- `string? Language { get; set; }` — language hint (e.g. `"cs"`, `"json"`).
- `ISyntaxHighlighter? SyntaxHighlighter { get; set; }` — defaults to a
  `TextMateSyntaxHighlighter` instance.
- Renders read-only, single-style highlighted code. Pulls per-token attributes via
  `GetAttributeForRole(VisualRole.CodeXxx)` after the highlighter's scope-to-role mapping
  (see "Unify markdown code rendering" below).

Reusable in dialogs, status messages, and the UI Catalog scenarios that follow. Not
required to support editing — for that, callers use the Editor package's `Editor`.

#### Unify markdown code rendering onto the new roles
`Terminal.Gui/Drawing/Markdown/TextMateSyntaxHighlighter.cs`: add a `TmScopeRoleMap` that
maps common TextMate scopes (`comment`, `keyword.*`, `string.*`, `constant.numeric`,
`entity.name.function.*`, etc.) to `VisualRole.Code*` entries. The highlighter still
tokenizes via TextMateSharp, but each `StyledSegment`'s attribute is now resolved through
`GetAttributeForRole(role)` on the consuming view (Markdown viewer, the new `Code` view,
or any other consumer), not through tmTheme colors.

`Terminal.Gui/Views/Markdown/MarkdownCodeBlock.cs`: read per-token attributes from the
active scheme via `GetAttributeForRole(segment.Role)` instead of consuming the segment's
pre-baked attribute. Falls back to `VisualRole.Code` when a segment lacks a role mapping.

Deprecate `TextMateSyntaxHighlighter.SetTheme`/`ThemeName`/`GetThemeForBackground` as
configuration knobs — they remain compilable and behave as no-ops or return the active
TG theme name, with `[Obsolete]` attributes pointing at `ThemeManager.Theme`. (Pre-1.0 TG,
but production code paths depend on these — softer deprecation than the Editor side.)

#### Expand the existing UI Catalog "Themes" scenario
Find TG's existing Themes scenario and add a "Code roles" panel that lists every
`VisualRole.Code*` entry with a swatch and label, plus a `Markdown` showing a code block
that updates live as `ThemeManager.Theme` cycles. Visible derivation: an unset
`CodeKeyword` inherits `Code` → `Editable` → `Normal`.

#### Add a UI Catalog "Code View Demo" scenario
A new scenario in TG's UI Catalog that hosts a `Code` view rendering a sample snippet,
with controls to switch language (cs, json, xml, md, etc.) and theme. Demonstrates the
end-to-end flow inside TG without touching the Editor package.

#### Ship a user-config override example
`Terminal.Gui/examples/themes/code-dark.config.json` (or wherever TG examples live): a tiny
JSON `config.json` file that overrides one or two `CodeXxx` entries on the `Dark` theme. Proves
users can customize syntax colors without writing C#. Linked from TG's docfx config docs.

### Phase 2 — Editor PR (this repo, after TG release)

#### Bump the Terminal.Gui dependency
Update `<TerminalGuiVersion>` in `Directory.Build.props` to the TG release containing Phase 1.

#### Bridge xshd named colors to VisualRoles
Add `src/Terminal.Gui.Editor/Highlighting/XshdRoleMap.cs` — a built-in
`Dictionary<string, VisualRole>` keyed by xshd `<Color name="...">` values, covering the common
cross-language names listed in §6.2. Names absent from the table fall through to the
xshd-declared color (today's behavior).

#### Let xshd entries override the bridge per-color
Add an optional `category="..."` attribute to xshd's `<Color>` element and a `VisualRole? Role`
property on the runtime `HighlightingColor`. At load time, populate `Role` from `category=` if
present; else from `XshdRoleMap.TryGetRole(name)`; else null.

#### Route the colorizer through the active scheme
Rewrite `HighlightingColorizer.ToAttribute`. Resolution order:
1. If `color.Role` is non-null AND the editor's current scheme has an *explicit* (non-derived)
   `Attribute` for that role, return it.
2. Else fall back to xshd's declared `Foreground` paired with the editor's scheme background.
3. Else return the editor's default attribute.

The constructor gains a `Func<VisualRole, Attribute>? getRoleAttribute` parameter, supplied by
`Editor` as `role => GetAttributeForRole(role)`.

#### Retire the UseThemeBackground knob
Remove `Editor.UseThemeBackground` (the property at `Editor.cs:231-245`), the
`_useThemeBackground` field on `HighlightingColorizer`, and the `useThemeBackground` parameter on
`VisualLineBuildContext`. Theme decides backgrounds now; pre-alpha — no compat shim.

#### Repaint on theme changes
Editor hooks `ThemeManager.ThemeChanged` (and `SchemeChanged` if exposed). On either event,
invalidate visual-line caches and redraw.

#### Add a Theme switcher to the ted demo
`examples/ted/TedApp.cs` gains a `Theme` `Shortcut` in the status bar next to the existing
`Language` shortcut. Click cycles `ThemeManager.Theme` across the TG-shipped theme names; title
updates to the current theme.

#### Document the xshd fork addition
Log the new xshd `category=` attribute as an intentional fork addition in
`third_party/AvaloniaEdit/UPSTREAM.md`.

### Cross-cutting

#### Single source of truth for themes
No new JSON config file in Editor. TG's `ConfigurationManager` owns themes; the Editor reads
from the active scheme.

#### Don't touch the 14 xshd files in this PR
The role-map table covers them. Per-language `category=` overrides are a follow-up only if a
specific case proves wrong; each such edit is logged in `UPSTREAM.md` when made.

#### Stay grammar-agnostic
The xshd-name → VisualRole bridge is `XshdRoleMap`. A future TextMate-scope bridge will be
`TextMateRoleMap`. Both produce a `VisualRole`; the colorizer's resolution logic does not branch
on grammar.

## Files in Scope

### Terminal.Gui repo (Phase 0 — benchmark baseline)

- `Tests/Benchmarks/Configuration/ConfigurationManagerLoadBenchmark.cs` — **NEW**
- `Tests/Benchmarks/Configuration/ThemeSwitchBenchmark.cs` — **NEW**
- `Tests/Benchmarks/Configuration/SchemeAttributeBenchmark.cs` — **NEW**
- `Tests/Benchmarks/Configuration/SchemeSerializationBenchmark.cs` — **NEW**
- `Tests/Benchmarks/Configuration/baseline.json` (or matching `perf-gate.yml` format) — **NEW**
- `Tests/Benchmarks/README.md` — document the machine/runtime used for baselines
- `.github/workflows/perf-gate.yml` — wire Configuration benchmarks into the gate

### Terminal.Gui repo (Phase 1 — code-token VisualRoles)

- `Terminal.Gui/Drawing/VisualRole.cs` — append 12 enum values
- `Terminal.Gui/Drawing/Scheme.cs` — 12 inline `Attribute?` fields + properties; extend
  derivation table
- `Terminal.Gui/Resources/config.json` — populate Dark + Light `Base.CodeXxx` entries
- `Terminal.Gui/Resources/tui-config-schema.json` — accept new role names
- `Terminal.Gui/Views/Code.cs` — **NEW** public read-only highlighted code view (no `View` suffix)
- `Terminal.Gui/Drawing/Markdown/TextMateSyntaxHighlighter.cs` — add `TmScopeRoleMap`;
  emit role-tagged segments; obsolete the local theme switch
- `Terminal.Gui/Views/Markdown/MarkdownCodeBlock.cs` — resolve segment attributes via
  `GetAttributeForRole`
- UI Catalog "Themes" scenario — add Code roles panel + live-updating Markdown block
- UI Catalog "Code View Demo" scenario — **NEW**
- `examples/themes/code-dark.config.json` (or equivalent path) — **NEW** sample user-config override
- TG test project — round-trip, derivation, `TmScopeRoleMap` coverage, `Code` view smoke test

### Editor repo (Phase 2)

- `Directory.Build.props` — bump `<TerminalGuiVersion>`
- `src/Terminal.Gui.Editor/Highlighting/XshdRoleMap.cs` — **NEW**
- `src/Terminal.Gui.Editor/Highlighting/HighlightingColor.cs` — add `VisualRole? Role`
- `src/Terminal.Gui.Editor/Highlighting/Xshd/XshdColor.cs` — add `Category` property
- `src/Terminal.Gui.Editor/Highlighting/Xshd/V2Loader.cs` (or equivalent) — read `category=`
- `src/Terminal.Gui.Editor/Rendering/HighlightingColorizer.cs` — rewrite `ToAttribute`; ctor
  takes role-lookup func
- `src/Terminal.Gui.Editor/Rendering/VisualLineBuildContext.cs` — drop `useThemeBackground` param
- `src/Terminal.Gui.Editor/Editor.cs` — remove `UseThemeBackground`; wire theme/scheme-changed
  handlers; pass role lookup to colorizer
- `examples/ted/TedApp.cs` — add `Theme` `Shortcut` to status bar
- `tests/Terminal.Gui.Editor.Tests/HighlightingTests.cs` — add scheme/derivation/override tests;
  delete `UseThemeBackground` tests
- `tests/Terminal.Gui.Editor.IntegrationTests/` — theme-swap smoke test using `AppFixture<T>`
- `third_party/AvaloniaEdit/UPSTREAM.md` — log `category=` addition

## Definition of Done

### Phase 0 (Terminal.Gui benchmarks)

- [ ] Four Configuration benchmarks added under `Tests/Benchmarks/Configuration/`
- [ ] Baseline JSON committed and `Tests/Benchmarks/README.md` documents the capture environment
- [ ] `.github/workflows/perf-gate.yml` runs the new benchmarks and gates on the threshold
- [ ] Phase 0 TG PR merged and released (or available as a pre-release for Phase 1 to compare against)

### Phase 1 (Terminal.Gui code-token VisualRoles)

- [ ] All 12 new `VisualRole` entries land with paired `Scheme` fields and derivation
- [ ] `Dark` and `Light` themes populate `CodeXxx` entries; other themes derive
- [ ] `tui-config-schema.json` accepts the new names; existing configs load without warnings
- [ ] `Code` view ships with a public API surface and a basic render test
- [ ] `TextMateSyntaxHighlighter` emits role-tagged segments; `MarkdownCodeBlock` reads
      attributes via `GetAttributeForRole`
- [ ] UI Catalog "Themes" scenario shows the Code roles panel + live theme cycling
- [ ] UI Catalog "Code View Demo" scenario renders snippets with language + theme switching
- [ ] `examples/themes/code-dark.config.json` (or equivalent) loads via `ConfigurationManager` and
      overrides `CodeKeyword`
- [ ] **Phase 0 benchmarks re-run and compared against baseline; no regression past gate
      threshold; results posted in PR description**
- [ ] TG PR merged and released

### Phase 2 (Editor)

- [ ] `<TerminalGuiVersion>` bumped to the Phase 1 release
- [ ] `dotnet run --project tests/Terminal.Gui.Editor.Tests` passes incl. new tests:
  - [ ] Colorizer uses scheme code-role when the theme defines it
  - [ ] Colorizer falls back to xshd-declared color when the theme does not override
  - [ ] `XshdRoleMap` covers the common cross-language names from §6.2
  - [ ] `category=` attribute on an xshd `<Color>` wins over the default table
- [ ] `dotnet run --project tests/Terminal.Gui.Editor.IntegrationTests` passes incl. theme-swap
      smoke test
- [ ] `ted foo.cs` and `ted foo.json` are readable on both dark and light terminals (manual)
- [ ] Theme Shortcut in ted cycles `ThemeManager.Theme` and lines re-render live
- [ ] `Editor.UseThemeBackground` removed; no callers remain
- [ ] `dotnet format Terminal.Gui.Editor.slnx --exclude third_party/ --verify-no-changes` clean
- [ ] `dotnet jb cleanupcode Terminal.Gui.Editor.slnx --profile="TG.Editor Full Cleanup"` clean
- [ ] Issues #99 and #128 closeable

## Out of Scope

- **Auto dark/light terminal-bg theme selection.** TG already detects terminal bg via OSC 10/11.
  Whether the *default* theme auto-flips on dark terminals is a separate TG design conversation
  tied to discussion [#4056](https://github.com/gui-cs/Terminal.Gui/discussions/4056) and issue
  [#457](https://github.com/gui-cs/Terminal.Gui/issues/457). This spec ensures the Dark theme has
  legible code colors; once TG auto-selects Dark on dark terminals, #128 is closed at the TG
  level. Until then, the user picks the theme.
- **Editing the 14 xshd resource files.** The role table covers them. Per-language `category=`
  overrides are deferred to follow-up PRs only if specific cases need them.
- **TextMate grammar palette bridge for Editor.** Same VisualRole design applies for the
  Editor's xshd→TextMate migration (per DEC-002), but `TextMateRoleMap` for the Editor ships
  with the textmate-grammars work, not here. (Note: the TG-side `TmScopeRoleMap` for the
  Markdown viewer IS in scope — see Phase 1.)
- **Themable Markdown structural tokens (`Markup*` role family).** Headings, Emphasis, Link,
  BlockQuote, etc. A reasonable future addition patterned on this spec (see §6.4). Out of
  scope until a concrete consumer arises.
- **Per-Editor `HighlightingTheme` property.** TG's `ThemeManager.Theme` is the single switch;
  no per-Editor override in v1.
- **Bold/italic/underline overrides via theme.** `HighlightingColor.Style` (from xshd) continues
  to provide `TextStyle` flags. The theme's `Attribute.Style` is a future enhancement, not in
  this PR.
- **Larger #457/#4056 attribute-system redesign.** This PR is intentionally minimal and additive.

## Notes

### §6.1 Default code-role colors

Dark (One Dark / VS Code Dark+ inspired):

| Role | Foreground (hex) |
|---|---|
| `CodeComment`      | `#6a9955` |
| `CodeKeyword`      | `#569cd6` |
| `CodeString`       | `#ce9178` |
| `CodeNumber`       | `#b5cea8` |
| `CodeOperator`     | `#d4d4d4` |
| `CodeType`         | `#4ec9b0` |
| `CodePreprocessor` | `#c586c0` |
| `CodeIdentifier`   | `#9cdcfe` |
| `CodeConstant`     | `#569cd6` |
| `CodePunctuation`  | `#d4d4d4` |
| `CodeFunctionName` | `#dcdcaa` |
| `CodeAttribute`    | `#9cdcfe` |

Light: analogous, tuned for white bg (TBD during Phase 1 — propose VS Code Light+).

All entries use `on default` for background, so the editor's scheme background shows through.

### §6.2 xshd-name → VisualRole table (draft coverage)

Cross-language common (high coverage):

- **CodeComment**: `Comment`, `DocComment`, `CommentTags`, `XmlDoc`
- **CodeKeyword**: `Keyword`, `Keywords`, `ControlFlow`, `AccessKeywords`, `ContextKeywords`,
  `ExceptionKeywords`, `GotoKeywords`, `OperatorKeywords`, `ParameterModifiers`, `Modifiers`,
  `AccessModifiers`, `Visibility`, `NamespaceKeywords`, `JumpStatements`, `JumpKeywords`,
  `LoopKeywords`, `IterationStatements`, `SelectionStatements`, `ExceptionHandling`,
  `ExceptionHandlingStatements`, `SemanticKeywords`, `CheckedKeyword`, `UnsafeKeywords`,
  `CompoundKeywords`, `FunctionKeywords`, `Package`, `Friend`, `This`, `ThisOrBaseReference`,
  `Void`, `JavaScriptKeyWords`, `JavaScriptIntrinsics`
- **CodeString**: `String`, `Char`, `Character`, `StringInterpolation`, `Regex`
- **CodeNumber**: `Number`, `NumberLiteral`, `Digits`, `DateLiteral`, `Literals`
- **CodeConstant**: `Bool`, `Null`, `NullOrValueKeywords`, `TrueFalse`, `BooleanConstants`,
  `Constants`
- **CodeOperator**: `Operators`
- **CodeType**: `ValueTypeKeywords`, `ReferenceTypeKeywords`, `DataTypes`, `ValueTypes`,
  `ReferenceTypes`, `TypeKeywords`
- **CodePreprocessor**: `Preprocessor`
- **CodePunctuation**: `Punctuation`, `CurlyBraces`, `Colon`, `Slash`, `Assignment`,
  `XmlPunctuation`
- **CodeFunctionName**: `MethodCall`, `MethodName`, `Command`
- **CodeIdentifier**: `FieldName`, `Variable`, `Property`, `Value`, `Selector`, `Class`,
  `Namespace`
- **CodeAttribute**: `JavaDocTags`, `KnownDocTags`, `Attributes`, `EntityReference`, `Entities`,
  `Tags`, `HtmlTag`, `ScriptTag`, `JavaScriptTag`, `JScriptTag`, `VBScriptTag`,
  `UnknownScriptTag`, `UnknownAttribute`

Markdown / one-offs (unmapped, fall through to xshd-declared color):
`Heading`, `Emphasis`, `StrongEmphasis`, `Code`, `BlockQuote`, `Link`, `Image`, `LineBreak`

The table is editable; this PR ships the version above. Future PRs refine as new xshd
definitions are added (per DEC-002, the post-alpha grammar story is TextMate, with its own
bridge).

### §6.3 Impact on TG's Markdown rendering

**TG's markdown path today.** `Terminal.Gui/Drawing/Markdown/ISyntaxHighlighter.cs` defines a
highlighter abstraction (`Highlight(code, language) → IReadOnlyList<StyledSegment>`,
`ResetState()`, `ThemeName`, `DefaultBackground`, `GetAttributeForScope(MarkdownStyleRole)`).
The default implementation, `TextMateSyntaxHighlighter`, uses **TextMateSharp** with VS Code
tmThemes (`DarkPlus`, `LightPlus`, etc.) and includes a `GetThemeForBackground(Color)` static
helper that auto-picks Dark vs Light from terminal-bg luminance. `MarkdownCodeBlock.OnDrawingContent`
uses `VisualRole.Code` as the base attribute (background) and overlays each `StyledSegment`'s
pre-baked Attribute per grapheme. **Fenced code blocks already render with syntax highlighting in
TG today** — by a parallel theming system independent of `ThemeManager`/`Scheme`/`VisualRole`.

**This PR's impact, by component:**

1. **`VisualRole.Code` (existing base role)** — unchanged. `MarkdownCodeBlock` continues to read
   it for code-block backgrounds. After this PR, `Code` sits at the head of the new derivation
   chain (`CodeXxx` → `Code` → `Editable` → `Normal`); behavior is transparent to existing callers.
2. **`TextMateSyntaxHighlighter`** — gains a `TmScopeRoleMap` mapping TextMate scopes to
   `VisualRole.Code*` (e.g. `comment.*` → `CodeComment`, `keyword.*` → `CodeKeyword`,
   `string.*` → `CodeString`, `entity.name.function.*` → `CodeFunctionName`). It still
   tokenizes via TextMateSharp, but each `StyledSegment` carries a `VisualRole?` instead of a
   pre-baked Attribute. `SetTheme`/`ThemeName`/`GetThemeForBackground` are `[Obsolete]`
   with messages pointing at `ThemeManager.Theme` — they keep compiling but no longer drive
   colors.
3. **`MarkdownCodeBlock`** — resolves per-token attributes via
   `GetAttributeForRole(segment.Role ?? VisualRole.Code)` instead of consuming the segment's
   pre-baked attribute. A single `ThemeManager.Theme = "Dark"` switch now themes Markdown
   code blocks alongside the Editor and the new `Code` view.
4. **Markdown-display tokens edited in Editor (`.md` files via `MarkDown-Mode.xshd`)** — still
   fall through to xshd-declared colors per §6.2. Themable structural tokens for `.md` editing
   remains a deferred concern (see §6.4).

### §6.4 Future "Markup*" role family (not in this spec)

Markdown structural tokens (`Heading`, `Emphasis`, `StrongEmphasis`, `Link`, `BlockQuote`,
`Image`, `LineBreak`) are not code — they are markup display. They live in
`MarkDown-Mode.xshd` (for Editor's `.md` editing) and in TG's `MarkdownStyleRole` enum (for TG's
markdown viewer). Unifying them through a `Markup*` `VisualRole` family (`MarkupHeading`,
`MarkupEmphasis`, `MarkupStrong`, `MarkupLink`, `MarkupQuote`) is a reasonable next step but is
intentionally deferred:

- No concrete consumer in this PR demands them.
- TG's markdown viewer renders structural tokens fine today (via `MarkdownStyleRole` +
  hardcoded styling).
- Editor's `.md` editing currently falls through to xshd colors — acceptable for v1.

When the next consumer appears (themable markdown headings, or Editor `.md` editing wanting to
respect TG themes), open a new spec patterned on this one: add `Markup*` to `VisualRole`, populate
in `Dark`/`Light`, add the same kind of fallback bridges. The architecture is reusable.

### §6.5 Risks

- **Stylized themes legibility**: `TurboPascal 5` and the Phosphor themes derive code roles
  through `Code` → `Editable`. Worth a manual eyeball during Phase 1 TG review; if any look
  broken, add explicit `Code` entries to that theme rather than expanding the derivation
  algorithm.
- **`tui-config-schema.json` validation**: must be additive and optional; users with existing
  `.tui` configs that omit code-role entries must continue to load without warnings.
- **Fork hygiene**: the xshd `category=` attribute is an addition over upstream AvaloniaEdit
  xshd. Log it in `third_party/AvaloniaEdit/UPSTREAM.md` so a future re-sync knows it is
  intentional drift, not a parsing bug.
- **Coordination with #457 / #4056**: stay minimal and additive; this is not the venue for the
  larger Attribute-system redesign.
