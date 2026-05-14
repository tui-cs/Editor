using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Editor;

/// <summary>
///     Renders line numbers and handles line-selection mouse interactions.
/// </summary>
internal sealed class LineNumberGutter : View
{
    private readonly Editor _editor;
    private int? _selectionAnchorLineNumber;

    internal LineNumberGutter (Editor editor)
    {
        _editor = editor;
        CanFocus = false;

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
    }

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        TextDocument? document = _editor.Document;

        if (document is null)
        {
            return true;
        }

        Rectangle viewport = Viewport;

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return true;
        }

        var firstVisibleIndex = _editor.Viewport.Y;
        var visibleHeight = _editor.Viewport.Height;
        var blank = new string (' ', viewport.Width);

        var prevLineNumber = -1;

        for (var row = 0; row < viewport.Height; row++)
        {
            if (row >= visibleHeight)
            {
                Move (0, row);
                AddStr (blank);

                continue;
            }

            var lineNumber = _editor.ViewRowToLineNumber (row);

            // For wrap-continuation rows, show blank instead of repeating the line number.
            var isFirstSegment = lineNumber != prevLineNumber;
            prevLineNumber = lineNumber;

            Move (0, row);

            if (isFirstSegment)
            {
                var lineText = lineNumber.ToString ().PadLeft (viewport.Width - 1) + " ";
                AddStr (lineText);
            }
            else
            {
                AddStr (blank);
            }
        }

        return true;
    }

    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (mouse.Position is not { } pos)
        {
            return false;
        }

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed) && mouse.Flags.HasFlag (MouseFlags.PositionReport))
        {
            if (_selectionAnchorLineNumber is not { } anchor)
            {
                anchor = _editor.ViewRowToLineNumber (pos.Y);
                _selectionAnchorLineNumber = anchor;
            }

            _editor.SelectLines (anchor, _editor.ViewRowToLineNumber (pos.Y));

            return true;
        }

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed))
        {
            _selectionAnchorLineNumber = _editor.ViewRowToLineNumber (pos.Y);
            _editor.SelectLineAtViewRow (pos.Y);
            App?.Mouse.GrabMouse (this);

            return true;
        }

        if (!mouse.Flags.HasFlag (MouseFlags.LeftButtonReleased))
        {
            return false;
        }

        _selectionAnchorLineNumber = null;
        App?.Mouse.UngrabMouse ();

        return true;
    }
}
