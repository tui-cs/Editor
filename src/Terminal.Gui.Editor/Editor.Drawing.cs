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
        Rectangle viewport = Viewport;

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

            string visible = text[viewport.X..];

            if (visible.Length > viewport.Width)
            {
                visible = visible[..viewport.Width];
            }

            AddStr (0, row, visible);
        }

        UpdateCursor ();

        return true;
    }

    private void UpdateCursor ()
    {
        if (!HasFocus)
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
