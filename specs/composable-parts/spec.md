# Feature Specification: Composable Editor Parts

**Status**: Proposed
**Created**: 2026-05-20
**Relates to**: Issue #178 (automatic folding orchestration)
**Depends on**: folding-ui (landed), completion (landed), file-io (landed)

## Overview

The `Terminal.Gui.Editor` NuGet package ships **composable building blocks** that
consumers assemble freely. The consumer provides the `Toplevel` / `Window` (the
"Runnable"). The library provides:

1. `Editor` — the View (exists today)
2. `EditorMenuBar` — a pre-wired `MenuBar` with File/Edit/View menus
3. `EditorStatusBar` — a pre-wired `StatusBar` with language, theme, Ln/Col, INS/OVR
4. Reusable orchestration APIs on `Editor` itself (automatic folding, etc.)

The consumer calls `Add (menu, editor, statusBar)` after configuring each piece, and
can choose not to use the canned versions at all.

## Motivation

Ted currently contains ~350 lines of orchestration that any consumer wanting a
full-featured editor experience must duplicate:

- Folding setup/teardown, change-filtering, large-document disabling (~250 lines)
- Menu construction with checkbox toggles for View options (~120 lines)
- Status bar with language/theme/loc indicators (~50 lines)
- File-command wiring (New/Open/Save/SaveAs) (~60 lines)

A second consumer (`clet edit *.cs`) would need all of this. The library should
provide it as optional, composable pieces.

## Design Principles

| Principle | Implication |
|-----------|-------------|
| **Consumer owns the Runnable** | No `EditorWindow` base class. Just a `Window`/`Toplevel`. |
| **Canned parts are optional** | `EditorMenuBar` and `EditorStatusBar` are convenience types — skip them freely. |
| **Parts take an `Editor` reference** | Wire themselves to Editor's commands/events/properties. No global state. |
| **Consumer calls `Add()`** | Full control over layout, z-order, additional views. |
| **Extra menus via composition** | `EditorMenuBar.ExtraMenuItems` — append without subclassing. |
| **No forced settings persistence** | Canned parts don't save/load settings — that's the consumer's job. |
| **No static state on View** | Per CLAUDE.md: `Editor` must not declare `static` members. |

## Phase 1: Automatic Folding Extraction (Issue #178)

The first deliverable is extracting Ted's folding orchestration into `Editor` itself.
This is a prerequisite for the canned `EditorMenuBar` (which needs toggle-able folding)
and validates the composable pattern.

### New API on `Editor`

```csharp
namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Gets or sets the folding strategy. When non-null and <see cref="AutomaticFolding"/>
    ///     is <see langword="true"/>, the editor automatically creates a <see cref="FoldingManager"/>,
    ///     subscribes to document changes, and refreshes foldings when structural characters change.
    /// </summary>
    public IFoldingStrategy? FoldingStrategy { get; set; }

    /// <summary>
    ///     Gets or sets whether the editor automatically runs the <see cref="FoldingStrategy"/>
    ///     in response to document changes. Defaults to <see langword="true"/> when
    ///     <see cref="FoldingStrategy"/> is assigned.
    /// </summary>
    public bool AutomaticFolding { get; set; }

    /// <summary>
    ///     Gets or sets the maximum document length (in characters) for which automatic
    ///     folding is active. Documents exceeding this threshold skip fold re-scanning
    ///     to avoid UI-thread hangs. Defaults to 1,000,000.
    /// </summary>
    public int MaximumAutomaticFoldingDocumentLength { get; set; } = 1_000_000;
}
```

### New Interface

```csharp
namespace Terminal.Gui.Document.Folding;

/// <summary>
///     Strategy for computing folding regions and determining whether a document
///     change may affect folding structure.
/// </summary>
public interface IFoldingStrategy
{
    /// <summary>Recompute all foldings for the given document.</summary>
    void UpdateFoldings (FoldingManager manager, TextDocument document);

    /// <summary>
    ///     Returns <see langword="true"/> if the given change may have introduced or
    ///     removed folding-structural characters (braces, newlines, etc.).
    ///     The orchestrator uses this to skip expensive re-scans on plain text edits.
    /// </summary>
    bool ChangeMayAffectFoldings (DocumentChangeEventArgs e);
}
```

`BraceFoldingStrategy` implements `IFoldingStrategy`, absorbing the
`OffsetChangeMap`-aware structural-change detection currently in Ted.

### What Ted becomes after extraction

```csharp
// TedApp constructor — the entire folding setup:
Editor.FoldingStrategy = new BraceFoldingStrategy ();
Editor.AutomaticFolding = true;
Editor.MaximumAutomaticFoldingDocumentLength = MaximumAutomaticFoldingDocumentLength;
Editor.GutterOptions |= GutterOptions.Folding;
```

All `InstallFolding`, `SetFoldingDocument`, `OnFoldingDocumentChanged`,
`OnFoldingDocumentUpdateFinished`, `FoldingChangeMayAffectStructure`,
`TryGetMappedStructuralChange`, `MappedInsertionsContainFoldingStructuralCharacter`,
`MappedRemovalsContainFoldingStructuralCharacter`,
`ContainsFoldingStructuralCharacter`, and related fields are deleted from Ted.

### Acceptance Criteria (from #178, preserved)

- Ted no longer owns automatic folding orchestration; it only configures the editor.
- Another app can enable Ted-equivalent brace folding with 3–4 lines of code.
- Plain multi-caret typing, Backspace, and Undo remain fast after extraction.
- Existing folding behavior still works (indicators render, collapse/expand works).
- Large documents still skip automatic brace folding.
- Tests cover: setup on document assignment, no refresh for non-structural edits,
  refresh for braces/newlines, document replacement/unsubscribe, Ted integration.

## Phase 2: `EditorMenuBar`

A `MenuBar`-derived type that wires itself to an `Editor` instance.

```csharp
namespace Terminal.Gui.Editor;

public class EditorMenuBar : MenuBar
{
    public EditorMenuBar (Editor editor);
    // For multi-file: active editor changes (e.g. tab switch)
    public EditorMenuBar (Func<Editor> activeEditorProvider);

    /// <summary>Additional menu items appended after the built-in menus.</summary>
    public IList<MenuBarItem> ExtraMenuItems { get; }

    /// <summary>Override to customize the file-open dialog.</summary>
    public Func<string?>? ShowOpenDialog { get; set; }

    /// <summary>Override to customize the file-save dialog.</summary>
    public Func<string?>? ShowSaveDialog { get; set; }
}
```

Built-in menus:
- **File**: New, Open, Save, Save As, Quit
- **Edit**: Undo, Redo, Cut, Copy, Paste, Select All, Find, Replace
- **View**: Line Numbers (checkbox), Fold Indicators (checkbox), Word Wrap (checkbox),
  Show Tabs (checkbox), Scrollbars (checkbox), Preview Markdown (if applicable)

All toggle states bind to the `Editor`'s properties. No settings persistence —
that's the consumer's responsibility.

## Phase 3: `EditorStatusBar`

A `StatusBar`-derived type that wires itself to an `Editor` instance.

```csharp
namespace Terminal.Gui.Editor;

public class EditorStatusBar : StatusBar
{
    public EditorStatusBar (Editor editor);
    public EditorStatusBar (Func<Editor> activeEditorProvider);

    /// <summary>Additional shortcuts appended to the status bar.</summary>
    public IList<Shortcut> ExtraShortcuts { get; }
}
```

Built-in indicators:
- Language name (from `HighlightingDefinition`)
- Theme dropdown (from `ThemeManager`)
- Load progress spinner (from streaming I/O)
- OVR/INS mode
- Ln/Col position

## Multi-file: `clet edit *.cs`

The consumer owns multi-file UX — a `TabView` with one `Editor` per tab, a split
view, or any other arrangement. The canned parts accept a `Func<Editor>` to track
the active editor:

```csharp
TabView tabs = new ();
foreach (string file in files)
{
    Editor editor = new ();
    editor.LoadFile (file);
    editor.FoldingStrategy = new BraceFoldingStrategy ();
    editor.AutomaticFolding = true;
    tabs.AddTab (new Tab (Path.GetFileName (file), editor));
}

Func<Editor> activeEditor = () => (Editor)tabs.SelectedTab!.View!;
EditorMenuBar menu = new (activeEditor);
EditorStatusBar statusBar = new (activeEditor);

Window win = new () { Title = "clet edit" };
win.Add (menu, tabs, statusBar);
```

## What stays in `examples/ted`

Ted becomes the **reference consumer** demonstrating:
- How to use `EditorMenuBar` / `EditorStatusBar` with its own settings persistence
- Custom About dialog, markdown preview panel
- `WordCompletionProvider` as a completion example
- `EditorSettings` POCO for configuration (not promoted to library — app-specific)

Ted's constructor shrinks from ~350 lines to ~50.

## Non-goals

- No `EditorWindow` base class — composition over inheritance.
- No forced tab/split UX in the library — consumer owns multi-file layout.
- No settings persistence in the library — consumer handles config.
- No RTL/bidi/rich-text beyond grapheme width.

## Implementation Order

1. **Phase 1** (Issue #178): `IFoldingStrategy` + automatic folding on `Editor` + Ted simplification
2. **Phase 2**: `EditorMenuBar` (new type, Ted refactored to use it)
3. **Phase 3**: `EditorStatusBar` (new type, Ted refactored to use it)

Phase 1 can ship independently. Phases 2–3 depend on Phase 1 being landed (the
menu needs folding toggles that work via `Editor.AutomaticFolding`).
