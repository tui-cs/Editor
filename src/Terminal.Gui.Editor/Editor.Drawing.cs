using System.Drawing;
using Terminal.Gui.Drivers;
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
            return false;
        }
        Rectangle viewport = Viewport;

        for (var row = 0; row < viewport.Height; row++)
        {
            var lineIndex = viewport.Y + row;

            if (lineIndex < 0 || lineIndex >= _document.LineCount)
            {
                break;
            }

            DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
            var text = _document.GetText (line);

            if (viewport.X >= text.Length)
            {
                continue;
            }

            var visible = text[viewport.X..];

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
        var caretLine = GetCaretLineIndex ();
        var caretCol = GetCaretColumn ();
        var row = caretLine - viewport.Y;
        var col = caretCol - viewport.X;

        if (row < 0 || row >= viewport.Height || col < 0 || col >= viewport.Width)
        {
            Cursor = new ();

            return;
        }

        Point screen = ViewportToScreen (new Point (col, row));
        Cursor = new () { Position = screen, Style = CursorStyle.BlinkingBar };
    }
}
