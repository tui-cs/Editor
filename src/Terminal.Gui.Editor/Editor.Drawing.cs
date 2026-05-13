using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Highlighting;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        if (_document is null)
        {
            return true;
        }

        Rectangle viewport = Viewport;
        Attribute normal = GetAttributeForRole (VisualRole.Normal);
        Attribute selected = GetAttributeForRole (VisualRole.Active);

        // Ensure the colorizer sees the current attribute (scheme may have changed since install).
        EnsureColorizerAttribute (normal);

        FillViewportBackground (viewport, normal);
        DrawVisibleLines (viewport, normal, selected);
        SetAttribute (normal);
        UpdateCursor ();

        return true;
    }

    /// <summary>
    ///     When <see cref="UseThemeBackground" /> is <see langword="true" /> and a highlighting
    ///     definition has a default background color, fills the viewport with that background
    ///     so empty cells match per-token backgrounds.
    /// </summary>
    private void FillViewportBackground (Rectangle viewport, Attribute normal)
    {
        if (!UseThemeBackground)
        {
            return;
        }

        Color? themeBg = _highlighter?.DefaultTextColor?.Background?.Color;

        if (themeBg is not { } bg)
        {
            return;
        }

        Attribute fillAttr = new (normal.Foreground, bg);
        SetAttribute (fillAttr);

        var spaces = new string (' ', viewport.Width);

        for (var row = 0; row < viewport.Height; row++)
        {
            AddStr (0, row, spaces);
        }
    }

    private void DrawVisibleLines (Rectangle viewport, Attribute normal, Attribute selected)
    {
        var hasSelection = HasSelection;
        var selStart = hasSelection ? SelectionStart : 0;
        var selEnd = hasSelection ? SelectionEnd : 0;
        var visibleStart = viewport.X;
        var visibleEnd = viewport.X + viewport.Width;

        for (var row = 0; row < viewport.Height; row++)
        {
            var lineIndex = viewport.Y + row;

            if (lineIndex < 0 || lineIndex >= _document!.LineCount)
            {
                break;
            }

            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);

            DrawVisualLine (row, line, visibleStart, visibleEnd, null, normal, selected, selStart, selEnd);
        }
    }

    /// <summary>
    ///     Rebuilds the <see cref="HighlightingColorizer" /> if the editor's normal attribute
    ///     has changed (e.g. after a scheme swap or focus change). Keeps the same underlying
    ///     <see cref="DocumentHighlighter" />.
    /// </summary>
    private void EnsureColorizerAttribute (Attribute normal)
    {
        if (_highlightingColorizer is null)
        {
            return;
        }

        HighlightingColorizer replacement = _highlightingColorizer.WithDefaultAttribute (normal, UseThemeBackground);

        if (ReferenceEquals (replacement, _highlightingColorizer))
        {
            return;
        }

        var index = LineTransformers.IndexOf (_highlightingColorizer);

        if (index >= 0)
        {
            LineTransformers[index] = replacement;
        }
        else
        {
            LineTransformers.Insert (0, replacement);
        }

        _highlightingColorizer = replacement;
    }

    private void DrawVisualLine (
        int row,
        DocumentLine line,
        int visibleStart,
        int visibleEnd,
        IReadOnlyList<StyledSegment>? segments,
        Attribute normal,
        Attribute selected,
        int selStart,
        int selEnd)
    {
        // GetOrBuildDrawVisualLine caches when segments == null && no selection && no transformers,
        // i.e. plain-text scrolling without a highlighter. The caret-path cache is separate so the
        // two don't thrash each other's entries (they use different attribute sets).
        CellVisualLine visualLine = GetOrBuildDrawVisualLine (line, segments, normal, selected, selStart, selEnd);

        foreach (IBackgroundRenderer renderer in BackgroundRenderers)
        {
            renderer.Draw (this, visualLine, row, Viewport);
        }

        foreach (CellVisualLineElement element in visualLine.Elements)
        {
            // Elements are ordered by visual column. Once we pass the visible end,
            // all remaining elements are off-screen — skip them entirely.
            if (element.VisualColumn >= visibleEnd)
            {
                break;
            }

            if (element.VisualEndColumn <= visibleStart)
            {
                continue;
            }

            element.Draw (this, 0, row, visibleStart, visibleEnd);
        }
    }

    private void UpdateCursor ()
    {
        if (!HasFocus || _document is null)
        {
            Cursor = new Cursor ();

            return;
        }

        Rectangle viewport = Viewport;
        var caretLine = GetCaretLineIndex ();
        var caretCol = GetCaretColumn ();
        var row = caretLine - viewport.Y;
        var col = caretCol - viewport.X;

        if (row < 0 || row >= viewport.Height || col < 0 || col >= viewport.Width)
        {
            Cursor = new Cursor ();

            return;
        }

        Point screen = ViewportToScreen (new Point (col, row));
        Cursor = new Cursor { Position = screen, Style = CursorStyle.BlinkingBar };
    }
}
