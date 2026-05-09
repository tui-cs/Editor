using System.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (_document is null)
        {
            return false;
        }

        if (mouse.IsWheel)
        {
            return ScrollMouseWheel (mouse.Flags);
        }

        if (mouse.Position is not { } pos)
        {
            return false;
        }

        bool shift = mouse.Flags.HasFlag (MouseFlags.Shift);

        // Drag: left button held while position changes — extend selection from the press point.
        // Tested first because PositionReport+LeftButtonPressed also satisfies the plain-press check.
        if (mouse.Flags.FastHasFlags (MouseFlags.LeftButtonPressed | MouseFlags.PositionReport))
        {
            int offset = MousePositionToOffset (pos);
            EnsureSelectionAnchor ();
            CaretOffset = offset;
            SelectionChanged?.Invoke (this, EventArgs.Empty);
            SetNeedsDraw ();

            return true;
        }

        // Press: focus, place caret, optionally start a selection (shift) or clear it (plain).
        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed))
        {
            if (CanFocus && !HasFocus)
            {
                SetFocus ();
            }

            int offset = MousePositionToOffset (pos);

            if (shift)
            {
                EnsureSelectionAnchor ();
                CaretOffset = offset;
                SelectionChanged?.Invoke (this, EventArgs.Empty);
                SetNeedsDraw ();
            }
            else
            {
                ClearSelection ();
                CaretOffset = offset;
            }

            // Grab the mouse so subsequent drag/release events route here even if the cursor leaves
            // this view's bounds — TG's default routing only delivers events to the view under the
            // pointer, which would break the drag-to-select gesture mid-stroke.
            App?.Mouse.GrabMouse (this);

            return true;
        }

        // Release: end the drag-grab so other views start receiving events again.
        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonReleased))
        {
            App?.Mouse.UngrabMouse ();

            return true;
        }

        return false;
    }

    private bool ScrollMouseWheel (MouseFlags flags)
    {
        bool? scrolled = flags switch
        {
            _ when flags.HasFlag (MouseFlags.WheeledUp) => ScrollVertical (-1),
            _ when flags.HasFlag (MouseFlags.WheeledDown) => ScrollVertical (1),
            _ when flags.HasFlag (MouseFlags.WheeledLeft) => ScrollHorizontal (-1),
            _ when flags.HasFlag (MouseFlags.WheeledRight) => ScrollHorizontal (1),
            _ => false
        };

        if (scrolled == true)
        {
            SetNeedsDraw ();
        }

        return scrolled == true;
    }

    /// <summary>
    ///     Converts a viewport-relative <see cref="Point"/> into a clamped document offset. Mouse events
    ///     past the end of a line snap to that line's end; clicks below the last line snap to the last line.
    /// </summary>
    private int MousePositionToOffset (Point viewPos)
    {
        if (_document is null || _document.LineCount == 0)
        {
            return 0;
        }

        int lineIndex = Math.Clamp (Viewport.Y + viewPos.Y, 0, _document.LineCount - 1);
        DocumentLine line = _document.GetLineByNumber (lineIndex + 1);
        int col = Math.Max (0, Viewport.X + viewPos.X);
        int colInLine = Math.Min (col, line.Length);

        return line.Offset + colInLine;
    }
}
