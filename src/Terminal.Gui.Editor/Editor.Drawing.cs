using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;

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

        // The CS0618 here is the API's purpose: SyntaxHighlighter is [Obsolete] to warn
        // external callers that this is a stopgap (issue #32). The editor itself still has to
        // honor the property until Phase 6 lifts AvaloniaEdit's Highlighting/ pipeline (#28).
#pragma warning disable CS0618 // Type or member is obsolete
        ISyntaxHighlighter? syntaxHighlighter = SyntaxHighlighter;
#pragma warning restore CS0618 // Type or member is obsolete

        var hasSelection = HasSelection;
        var selStart = hasSelection ? SelectionStart : 0;
        var selEnd = hasSelection ? SelectionEnd : 0;

        PrepareSyntaxHighlighter (syntaxHighlighter, viewport.Y);

        for (var row = 0; row < viewport.Height; row++)
        {
            var lineIndex = viewport.Y + row;

            if (lineIndex < 0 || lineIndex >= _document.LineCount)
            {
                break;
            }

            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
            var text = _document.GetText (line);
#pragma warning disable CS0618 // Type or member is obsolete — see note at top of OnDrawingContent.
            IReadOnlyList<StyledSegment>? segments = syntaxHighlighter?.Highlight (text, SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete
            var visibleStart = viewport.X;
            var visibleEnd = viewport.X + viewport.Width;

            DrawLineContent (
                row,
                line,
                visibleStart,
                visibleEnd,
                segments,
                normal,
                selected,
                hasSelection,
                selStart,
                selEnd);
        }

        SetAttribute (normal);
        UpdateCursor ();

        return true;
    }

    private void PrepareSyntaxHighlighter (ISyntaxHighlighter? syntaxHighlighter, int firstVisibleLineIndex)
    {
        if (syntaxHighlighter is null || _document is null)
        {
            return;
        }

        syntaxHighlighter.ResetState ();

        for (var lineIndex = 0; lineIndex < firstVisibleLineIndex && lineIndex < _document.LineCount; lineIndex++)
        {
            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
#pragma warning disable CS0618 // Type or member is obsolete — see note in OnDrawingContent.
            syntaxHighlighter.Highlight (_document.GetText (line), SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    private void DrawLineContent (
        int row,
        DocumentLine line,
        int visibleStart,
        int visibleEnd,
        IReadOnlyList<StyledSegment>? segments,
        Attribute normal,
        Attribute selected,
        bool hasSelection,
        int selStart,
        int selEnd)
    {
        CellVisualLine visualLine = BuildVisualLine (
            line,
            segments,
            normal,
            selected,
            hasSelection ? selStart : 0,
            hasSelection ? selEnd : 0);

        foreach (IBackgroundRenderer renderer in BackgroundRenderers)
        {
            renderer.Draw (this, visualLine, row, Viewport);
        }

        foreach (CellVisualLineElement element in visualLine.Elements)
        {
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

    /// <inheritdoc />
    protected override void OnDrawComplete (DrawContext? context)
    {
        base.OnDrawComplete (context);

        if (App?.Driver is { } driver)
        {
            DrawLineNumbers (driver);
        }
    }

    private void DrawLineNumbers (IDriver driver)
    {
        if (!_showLineNumbers || _document is null)
        {
            return;
        }

        var width = Padding.Thickness.Left;

        if (width <= 0)
        {
            return;
        }

        Rectangle viewport = Viewport;
        Rectangle screen = ViewportToScreen ();
        Region? clip = GetClip ();
        Attribute previous = driver.SetAttribute (GetAttributeForRole (VisualRole.Normal));

        SetClipToScreen ();

        try
        {
            for (var row = 0; row < viewport.Height; row++)
            {
                var lineIndex = viewport.Y + row;
                var text = lineIndex < _document.LineCount
                    ? (lineIndex + 1).ToString ().PadLeft (width - 1).PadRight (width)
                    : new string (' ', width);

                driver.Move (screen.X - width, screen.Y + row);
                driver.AddStr (text);
            }
        }
        finally
        {
            if (clip is not null)
            {
                SetClip (clip);
            }

            driver.SetAttribute (previous);
        }
    }
}
