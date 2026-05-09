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
            int visibleStart = viewport.X;
            int visibleEnd = viewport.X + viewport.Width;

            DrawLine (
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

    private void DrawLine (
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
        int segmentStart = 0;
        int segmentEnd = segments is { Count: > 0 } ? segments[0].Text.Length : int.MaxValue;

        for (int i = 0; i < text.Length; i++)
        {
            while (segments is not null && i >= segmentEnd && segmentIndex + 1 < segments.Count)
            {
                segmentIndex++;
                segmentStart = segmentEnd;
                segmentEnd = segmentStart + segments[segmentIndex].Text.Length;
            }

            Drawing.Attribute attribute = segments is null
                ? normal
                : segments[segmentIndex].Attribute ?? normal;

            if (hasSelection && lineOffset + i >= selStart && lineOffset + i < selEnd)
            {
                attribute = selected;
            }

            char c = text[i];
            int width = GetVisualWidthForCharacter (c, visualColumn, TabWidth);
            int charVisualStart = visualColumn;
            int charVisualEnd = visualColumn + width;

            if (charVisualEnd <= visibleStart)
            {
                visualColumn = charVisualEnd;

                continue;
            }

            if (charVisualStart >= visibleEnd)
            {
                break;
            }

            int drawStart = Math.Max (charVisualStart, visibleStart);
            int drawEnd = Math.Min (charVisualEnd, visibleEnd);

            if (drawEnd > drawStart)
            {
                SetAttribute (attribute);
                AddStr (
                    drawStart - visibleStart,
                    row,
                    c == '\t' ? new (' ', drawEnd - drawStart) : c.ToString ());
            }

            visualColumn = charVisualEnd;
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
}
