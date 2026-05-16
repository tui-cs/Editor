# Editor Public API Target

**Updated**: 2026-05-10

The MLP shape, AvaloniaEdit-aligned. This is the target surface for the alpha release. Where current properties differ, the notes column says what to rename/add. New properties added to `Editor` require updating this document before merge (rule R8).

```csharp
namespace Terminal.Gui.Editor;

public class Editor : View
{
    // --- Document ---
    public TextDocument Document { get; set; }                    // exists
    public event EventHandler<DocumentChangeEventArgs>? DocumentChanged; // exists

    // --- Caret ---
    public int CaretOffset { get; set; }                          // exists; backed by TextAnchor (caret-anchors ✅)
    public event EventHandler? CaretChanged;                      // exists

    // --- Selection ---
    public TextSegment? Selection { get; }                        // exists; backed by anchor pair (caret-anchors ✅)
    public event EventHandler? SelectionChanged;                  // exists

    // --- Multi-caret ---
    public IReadOnlyList<int> AdditionalCaretOffsets { get; }     // multi-caret
    public bool HasMultipleCarets { get; }                        // multi-caret
    public void ToggleCaretAt (int offset);                       // multi-caret (Ctrl+Click toggle)
    public void ClearAdditionalCarets ();                         // multi-caret (Esc collapse)
    // vertical-multi-caret adds NO new public API: Ctrl+Alt+CursorUp / Ctrl+Alt+CursorDown
    //   create a vertically-aligned column of carets at the sticky visual column, and
    //   Shift+Alt + LeftButton drag builds a column of carets (carets only). Both reuse the
    //   existing AdditionalCaretOffsets / HasMultipleCarets / ClearAdditionalCarets surface and
    //   are bound through the configurable Editor.DefaultKeyBindings ([ConfigurationProperty]).

    // --- Display ---
    public bool ShowLineNumbers { get; set; }                     // exists
    public bool WordWrap { get; set; }                            // word-wrap-toggle (needs word-wrap)
    public bool ReadOnly { get; set; }                            // exists (read-only ✅)

    // --- Indentation (tab-handling ✅ + auto-indent) ---
    public int IndentationSize { get; set; } = 4;                 // exists (codex merge)
    public bool ConvertTabsToSpaces { get; set; }                 // exists (codex merge)
    public bool ShowTabs { get; set; }                            // exists (codex merge)
    public IIndentationStrategy IndentationStrategy { get; set; } // auto-indent (needs indentation)

    // --- Rendering pipeline (rendering-pipeline ✅) ---
    public IList<IVisualLineTransformer> LineTransformers { get; }  // exists (codex merge)
    public IList<IBackgroundRenderer> BackgroundRenderers { get; }  // exists (codex merge)
    public IList<IOverlayRenderer> OverlayRenderers { get; }       // multi-caret (drawn after elements)

    // --- Syntax highlighting (syntax-colorizer ✅) ---
    public IHighlightingDefinition? HighlightingDefinition { get; set; } // exists (syntax-colorizer)

    // --- Folding ---
    public FoldingManager? FoldingManager { get; set; }           // folding-ui (needs folding + rendering-pipeline ✅)

    // --- Search ---
    public ISearchStrategy? SearchStrategy { get; set; }          // find-and-replace (needs search + rendering-pipeline ✅)

    // --- Completion (post-MLP) ---
    public IEditorCompletionProvider? CompletionProvider { get; set; } // post-MLP
}
```

## Pipeline Types (rendering-pipeline — landed)

```csharp
namespace Terminal.Gui.Editor.Rendering;

public sealed class CellVisualLine
{
    public DocumentLine DocumentLine { get; }
    public IReadOnlyList<CellVisualLineElement> Elements { get; }
    public int VisualLength { get; }      // total cells
}

public abstract class CellVisualLineElement
{
    public int RelativeOffset { get; }    // doc-offset relative to DocumentLine.Offset
    public int LogicalLength { get; }     // # of doc chars this element represents
    public int VisualLength { get; }      // # of cells this element occupies
    public Drawing.Attribute Attribute { get; set; }
    public abstract void Draw (View host, int x, int y, int visibleStart, int visibleEnd);
}

public sealed class TextRunElement : CellVisualLineElement { }
public sealed class TabElement : CellVisualLineElement { }
public sealed class FoldingMarkerElement : CellVisualLineElement { }

public interface IVisualLineTransformer
{
    void Transform (CellVisualLine line);
}

public interface IBackgroundRenderer
{
    void Draw (View host, CellVisualLine line, int row, Rectangle viewport);
}

public interface IOverlayRenderer
{
    void Draw (View host, CellVisualLine line, int row, Rectangle viewport);
}
```

## Change Log

| Date | Change | Feature |
|------|--------|---------|
| 2026-05-10 | Initial API target extracted from plan | — |
| 2026-05-10 | rendering-pipeline pipeline types landed (codex merge) | rendering-pipeline |
| 2026-05-10 | tab-handling tab properties landed (codex merge) | tab-handling |
| 2026-05-11 | Caret and selection storage migrated to TextAnchor-backed tracking | caret-anchors |
| 2026-05-11 | ReadOnly property landed on Editor | read-only |
| 2026-05-12 | `ISearchStrategy?` `SearchStrategy { get; set; }` landed on Editor; string-based FindNext/FindPrevious/ReplaceNext/ReplaceAll overloads retained as convenience wrappers | find-and-replace |
| 2026-05-16 | Vertical multi-caret keybindings (`Ctrl+Alt+CursorUp/Down`, `Shift+Alt+Drag`) added via `Editor.DefaultKeyBindings`; no new public Editor API (R8) | vertical-multi-caret |
