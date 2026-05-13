using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Input;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (_document is null)
        {
            return false;
        }

        if (mouse.Position is not { } pos)
        {
            return false;
        }

        var shift = mouse.Flags.HasFlag (MouseFlags.Shift);

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonTripleClicked))
        {
            SelectLineAtOffset (MousePositionToOffset (pos));

            return true;
        }

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonDoubleClicked))
        {
            SelectWordAtOffset (MousePositionToOffset (pos));

            return true;
        }

        // Drag: left button held while position changes — extend selection from the press point.
        // Tested first because PositionReport+LeftButtonPressed also satisfies the plain-press check.
        if (mouse.Flags.FastHasFlags (MouseFlags.LeftButtonPressed | MouseFlags.PositionReport))
        {
            var offset = MousePositionToOffset (pos);

            // Route through the selection helper so SelectionChanged fires only on real changes.
            ExtendCaretTo (offset);

            return true;
        }

        // Press: focus, place caret, optionally start a selection (shift) or clear it (plain).
        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed))
        {
            if (CanFocus && !HasFocus)
            {
                SetFocus ();
            }

            var offset = MousePositionToOffset (pos);

            if (shift)
            {
                ExtendCaretTo (offset);
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
        if (!mouse.Flags.HasFlag (MouseFlags.LeftButtonReleased))
        {
            return false;
        }

        App?.Mouse.UngrabMouse ();

        return true;
    }

    /// <summary>
    ///     Converts a viewport-relative <see cref="Point" /> into a clamped document offset. Mouse events
    ///     past the end of a line snap to that line's end; clicks below the last line snap to the last line.
    /// </summary>
    private int MousePositionToOffset (Point viewPos)
    {
        if (_document is null || _document.LineCount == 0)
        {
            return 0;
        }

        // Map viewport row to document line via visible-line list (respects folding).
        List<int> visibleLines = GetVisibleLineNumbers ();
        var visibleIndex = Math.Clamp (Viewport.Y + viewPos.Y, 0, visibleLines.Count - 1);
        var lineNumber = visibleLines[visibleIndex];
        DocumentLine line = _document.GetLineByNumber (lineNumber);
        var col = Math.Max (0, Viewport.X + viewPos.X);
        var colInLine = GetOrBuildDefaultVisualLine (line).GetRelativeOffset (col);

        return line.Offset + colInLine;
    }
}
