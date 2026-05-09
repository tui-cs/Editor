using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
using Terminal.Gui.Text;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

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
        Drawing.Attribute normal = GetAttributeForRole (VisualRole.Normal);
        Drawing.Attribute selected = GetAttributeForRole (VisualRole.Active);

        // The CS0618 here is the API's purpose: SyntaxHighlighter is [Obsolete] to warn
        // external callers that this is a stopgap (issue #32). The editor itself still has to
        // honor the property until Phase 6 lifts AvaloniaEdit's Highlighting/ pipeline (#28).
#pragma warning disable CS0618 // Type or member is obsolete
        ISyntaxHighlighter? syntaxHighlighter = SyntaxHighlighter;
#pragma warning restore CS0618 // Type or member is obsolete

        bool hasSelection = HasSelection;
        int selStart = hasSelection ? SelectionStart : 0;
        int selEnd = hasSelection ? SelectionEnd : 0;

        PrepareSyntaxHighlighter (syntaxHighlighter, viewport.Y);

        for (int row = 0; row < viewport.Height; row++)
        {
            int lineIndex = viewport.Y + row;

            if (lineIndex < 0 || lineIndex >= _document.LineCount)
            {
                break;
            }

            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
            string text = _document.GetText (line);
#pragma warning disable CS0618 // Type or member is obsolete — see note at top of OnDrawingContent.
            IReadOnlyList<StyledSegment>? segments = syntaxHighlighter?.Highlight (text, SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete
            int visibleStart = viewport.X;
            int visibleEnd = viewport.X + viewport.Width;

            DrawLineContent (
                row,
                text,
                visibleStart,
                visibleEnd,
                segments,
                normal,
                selected,
                line.Offset,
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

        for (int lineIndex = 0; lineIndex < firstVisibleLineIndex && lineIndex < _document.LineCount; lineIndex++)
        {
            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
#pragma warning disable CS0618 // Type or member is obsolete — see note in OnDrawingContent.
            syntaxHighlighter.Highlight (_document.GetText (line), SyntaxLanguage);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    private void DrawLineContent (
        int row,
        string text,
        int visibleStart,
        int visibleEnd,
        IReadOnlyList<StyledSegment>? segments,
        Drawing.Attribute normal,
        Drawing.Attribute selected,
        int lineOffset,
        bool hasSelection,
        int selStart,
        int selEnd)
    {
        int visualColumn = 0;
        int segmentIndex = 0;
        bool hasSegments = segments is { Count: > 0 };
        int segmentEnd = hasSegments ? segments![0].Text.Length : int.MaxValue;

        foreach ((int i, string grapheme) in EnumerateGraphemes (text))
        {
            while (hasSegments && i >= segmentEnd && segmentIndex + 1 < segments!.Count)
            {
                segmentIndex++;
                segmentEnd += segments[segmentIndex].Text.Length;
            }

            Drawing.Attribute attribute = hasSegments
                ? segments![segmentIndex].Attribute ?? normal
                : normal;

            if (hasSelection && lineOffset + i < selEnd && lineOffset + i + grapheme.Length > selStart)
            {
                attribute = selected;
            }

            int width = GetVisualWidthForGrapheme (grapheme, visualColumn, IndentationSize);
            int graphemeVisualStart = visualColumn;
            int graphemeVisualEnd = visualColumn + width;

            if (graphemeVisualEnd <= visibleStart)
            {
                visualColumn = graphemeVisualEnd;

                continue;
            }

            if (graphemeVisualStart >= visibleEnd)
            {
                break;
            }

            int drawStart = Math.Max (graphemeVisualStart, visibleStart);
            int drawEnd = Math.Min (graphemeVisualEnd, visibleEnd);

            if (drawEnd > drawStart)
            {
                SetAttribute (attribute);

                if (grapheme == "\t")
                {
                    string textToDraw = ShowTabs
                                            ? "→" + new string (' ', width - 1)
                                            : new string (' ', width);
                    AddStr (
                        drawStart - visibleStart,
                        row,
                        textToDraw.Substring (drawStart - graphemeVisualStart, drawEnd - drawStart));
                }
                else if (graphemeVisualStart >= visibleStart && graphemeVisualEnd <= visibleEnd)
                {
                    AddStr (graphemeVisualStart - visibleStart, row, grapheme);
                }
            }

            visualColumn = graphemeVisualEnd;
        }
    }

    private void UpdateCursor ()
    {
        if (!HasFocus || _document is null)
        {
            Cursor = new ();

            return;
        }

        Rectangle viewport = Viewport;
        int caretLine = GetCaretLineIndex ();
        int caretCol = GetCaretColumn ();
        int row = caretLine - viewport.Y;
        int col = caretCol - viewport.X;

        if (row < 0 || row >= viewport.Height || col < 0 || col >= viewport.Width)
        {
            Cursor = new ();

            return;
        }

        Point screen = ViewportToScreen (new Point (col, row));
        Cursor = new () { Position = screen, Style = CursorStyle.BlinkingBar };
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

        int width = Padding.Thickness.Left;

        if (width <= 0)
        {
            return;
        }

        Rectangle viewport = Viewport;
        Rectangle screen = ViewportToScreen ();
        Region? clip = GetClip ();
        Drawing.Attribute previous = driver.SetAttribute (GetAttributeForRole (VisualRole.Normal));

        SetClipToScreen ();

        try
        {
            for (int row = 0; row < viewport.Height; row++)
            {
                int lineIndex = viewport.Y + row;
                string text = lineIndex < _document.LineCount
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
