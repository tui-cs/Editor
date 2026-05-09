using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;
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
        ISyntaxHighlighter? syntaxHighlighter = SyntaxHighlighter;

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
            IReadOnlyList<StyledSegment>? segments = syntaxHighlighter?.Highlight (text, SyntaxLanguage);

            if (viewport.X >= text.Length)
            {
                continue;
            }

            int visibleStart = viewport.X;
            int visibleEnd = Math.Min (text.Length, viewport.X + viewport.Width);

            DrawLineRuns (
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
            syntaxHighlighter.Highlight (_document.GetText (line), SyntaxLanguage);
        }
    }

    private void DrawLineRuns (
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
        if (segments is null)
        {
            DrawRun (
                row,
                visibleStart,
                visibleStart,
                visibleEnd,
                text[visibleStart..visibleEnd],
                normal,
                selected,
                lineOffset,
                hasSelection,
                selStart,
                selEnd);

            return;
        }

        int segmentStart = 0;

        foreach (StyledSegment segment in segments)
        {
            int segmentEnd = segmentStart + segment.Text.Length;

            if (segmentEnd <= visibleStart)
            {
                segmentStart = segmentEnd;

                continue;
            }

            if (segmentStart >= visibleEnd)
            {
                break;
            }

            int runStart = Math.Max (segmentStart, visibleStart);
            int runEnd = Math.Min (segmentEnd, visibleEnd);
            Drawing.Attribute attribute = segment.Attribute ?? normal;

            DrawRun (
                row,
                visibleStart,
                runStart,
                runEnd,
                segment.Text[(runStart - segmentStart)..(runEnd - segmentStart)],
                attribute,
                selected,
                lineOffset,
                hasSelection,
                selStart,
                selEnd);
            segmentStart = segmentEnd;
        }
    }

    private void DrawRun (
        int row,
        int visibleStart,
        int runStart,
        int runEnd,
        string text,
        Drawing.Attribute attribute,
        Drawing.Attribute selected,
        int lineOffset,
        bool hasSelection,
        int selStart,
        int selEnd)
    {
        if (!hasSelection || selEnd <= lineOffset + runStart || selStart >= lineOffset + runEnd)
        {
            SetAttribute (attribute);
            AddStr (runStart - visibleStart, row, text);

            return;
        }

        int lineSelStart = Math.Max (runStart, selStart - lineOffset);
        int lineSelEnd = Math.Min (runEnd, selEnd - lineOffset);
        int current = runStart;

        if (current < lineSelStart)
        {
            DrawRunPart (row, visibleStart, runStart, current, lineSelStart, text, attribute);
            current = lineSelStart;
        }

        if (current < lineSelEnd)
        {
            DrawRunPart (row, visibleStart, runStart, current, lineSelEnd, text, selected);
            current = lineSelEnd;
        }

        if (current < runEnd)
        {
            DrawRunPart (row, visibleStart, runStart, current, runEnd, text, attribute);
        }
    }

    private void DrawRunPart (
        int row,
        int visibleStart,
        int runStart,
        int partStart,
        int partEnd,
        string text,
        Drawing.Attribute attribute)
    {
        SetAttribute (attribute);
        AddStr (partStart - visibleStart, row, text[(partStart - runStart)..(partEnd - runStart)]);
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
}
