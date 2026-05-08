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

        bool hasSelection = HasSelection;
        int selStart = hasSelection ? SelectionStart : 0;
        int selEnd = hasSelection ? SelectionEnd : 0;

        for (int row = 0; row < viewport.Height; row++)
        {
            int lineIndex = viewport.Y + row;

            if (lineIndex < 0 || lineIndex >= _document.LineCount)
            {
                break;
            }

            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
            string text = _document.GetText (line);

            if (viewport.X >= text.Length)
            {
                continue;
            }

            int visibleStart = viewport.X;
            int visibleEnd = Math.Min (text.Length, viewport.X + viewport.Width);

            if (!hasSelection || selEnd <= line.Offset + visibleStart || selStart >= line.Offset + visibleEnd)
            {
                // Whole visible segment is outside the selection.
                SetAttribute (normal);
                AddStr (0, row, text[visibleStart..visibleEnd]);

                continue;
            }

            // Selection overlaps this line's visible window. Split into up to three runs.
            int lineSelStart = Math.Max (0, selStart - line.Offset);
            int lineSelEnd = Math.Min (text.Length, selEnd - line.Offset);
            int runStart = visibleStart;

            if (runStart < lineSelStart)
            {
                int runEnd = Math.Min (lineSelStart, visibleEnd);
                SetAttribute (normal);
                AddStr (runStart - visibleStart, row, text[runStart..runEnd]);
                runStart = runEnd;
            }

            if (runStart < lineSelEnd)
            {
                int runEnd = Math.Min (lineSelEnd, visibleEnd);
                SetAttribute (selected);
                AddStr (runStart - visibleStart, row, text[runStart..runEnd]);
                runStart = runEnd;
            }

            if (runStart < visibleEnd)
            {
                SetAttribute (normal);
                AddStr (runStart - visibleStart, row, text[runStart..visibleEnd]);
            }
        }

        SetAttribute (normal);
        UpdateCursor ();

        return true;
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
