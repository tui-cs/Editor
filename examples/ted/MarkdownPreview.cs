using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Ted;

/// <summary>
///     A <see cref="Markdown" /> subclass that highlights the rendered line(s) corresponding to a
///     source line in the editor and raises <see cref="SourceLineClicked" /> when the user clicks
///     in the preview, enabling click-to-navigate back to the editor.
/// </summary>
internal sealed class MarkdownPreview : Markdown
{
    private int _highlightSourceLine = -1;
    private int _totalSourceLines;

    /// <summary>
    ///     Gets or sets the 0-based source line number to highlight in the preview.
    ///     Set to -1 to clear the highlight.
    /// </summary>
    public int HighlightSourceLine
    {
        get => _highlightSourceLine;
        set
        {
            if (_highlightSourceLine == value)
            {
                return;
            }

            _highlightSourceLine = value;
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Gets or sets the total number of source lines in the document.
    ///     Used for proportional mapping between source lines and rendered lines.
    /// </summary>
    public int TotalSourceLines
    {
        get => _totalSourceLines;
        set
        {
            if (_totalSourceLines == value)
            {
                return;
            }

            _totalSourceLines = value;
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Raised when the user clicks in the preview. The event arg carries the estimated 0-based
    ///     source line number corresponding to the click position.
    /// </summary>
    public event EventHandler<SourceLineClickedEventArgs>? SourceLineClicked;

    /// <summary>
    ///     Maps a 0-based source line number to the corresponding rendered line index using
    ///     proportional mapping.
    /// </summary>
    private int MapSourceToRendered (int sourceLine)
    {
        if (_totalSourceLines <= 1 || LineCount <= 0)
        {
            return 0;
        }

        return (int)((long)sourceLine * (LineCount - 1) / (_totalSourceLines - 1));
    }

    /// <summary>
    ///     Maps a rendered line index back to an approximate 0-based source line number.
    /// </summary>
    private int MapRenderedToSource (int renderedLine)
    {
        if (LineCount <= 1 || _totalSourceLines <= 0)
        {
            return 0;
        }

        return (int)((long)renderedLine * (_totalSourceLines - 1) / (LineCount - 1));
    }

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        var result = base.OnDrawingContent (context);

        DrawHighlightBar ();

        return result;
    }

    /// <summary>
    ///     Paints a subtle background highlight on the rendered row corresponding to
    ///     <see cref="HighlightSourceLine" />.
    /// </summary>
    private void DrawHighlightBar ()
    {
        if (_highlightSourceLine < 0 || _totalSourceLines <= 0 || LineCount <= 0)
        {
            return;
        }

        var renderedLine = MapSourceToRendered (_highlightSourceLine);

        // Check if the highlighted line is within the visible viewport.
        var drawRow = renderedLine - Viewport.Y;

        if (drawRow < 0 || drawRow >= Viewport.Height)
        {
            return;
        }

        // Compute a highlight attribute: shift the background slightly for contrast.
        Attribute normalAttr = GetAttributeForRole (VisualRole.Normal);
        Color highlightBg = normalAttr.Background.IsDarkColor ()
            ? normalAttr.Background.GetDimmerColor (0.25, false)
            : normalAttr.Background.GetDimmerColor (0.15, true);

        // Paint the highlight over the full viewport width by reading existing screen content
        // and re-drawing with the highlight background.
        Cell[,]? contents = ScreenContents;

        if (contents is null)
        {
            return;
        }

        // Map the viewport-relative draw row to screen coordinates so we read the correct
        // cells regardless of horizontal scroll position (Viewport.X).
        Point screenOrigin = ViewportToScreen (new Point (0, drawRow));
        var screenRow = screenOrigin.Y;
        var screenStartCol = screenOrigin.X;

        for (var col = 0; col < Viewport.Width; col++)
        {
            var sc = screenStartCol + col;

            if (screenRow < 0 || screenRow >= contents.GetLength (0) || sc < 0 || sc >= contents.GetLength (1))
            {
                continue;
            }

            Cell cell = contents[screenRow, sc];
            var grapheme = string.IsNullOrEmpty (cell.Grapheme) ? " " : cell.Grapheme;

            // Preserve the foreground color from the original cell but apply highlight background.
            Attribute cellAttr = (cell.Attribute ?? normalAttr) with { Background = highlightBg };
            SetAttribute (cellAttr);
            AddStr (col, drawRow, grapheme);
        }
    }

    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonClicked) && mouse.Position is { } pos)
        {
            var contentRow = Viewport.Y + pos.Y;

            if (contentRow >= 0 && contentRow < LineCount)
            {
                var sourceLine = MapRenderedToSource (contentRow);
                SourceLineClicked?.Invoke (this, new SourceLineClickedEventArgs (sourceLine));
            }
        }

        return base.OnMouseEvent (mouse);
    }
}
