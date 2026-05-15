using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Editor.Rendering;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor;

public partial class Editor
{
    /// <summary>
    ///     Set to <see langword="true" /> when a Ctrl+Click press is handled so subsequent drag
    ///     (PositionReport) events don't hijack the primary caret via <see cref="ExtendCaretTo" />.
    ///     Cleared on mouse release.
    /// </summary>
    private bool _suppressDragUntilRelease;

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
        // Suppress when the press was a Ctrl+Click (multi-caret add) so the drag handler doesn't
        // move the primary caret via ExtendCaretTo.
        if (mouse.Flags.FastHasFlags (MouseFlags.LeftButtonPressed | MouseFlags.PositionReport))
        {
            if (_suppressDragUntilRelease)
            {
                return true;
            }

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
            var ctrl = mouse.Flags.HasFlag (MouseFlags.Ctrl);

            if (ctrl)
            {
                ToggleCaretAt (offset);
                _suppressDragUntilRelease = true;
            }
            else if (shift)
            {
                _suppressDragUntilRelease = false;
                ExtendCaretTo (offset);
            }
            else
            {
                _suppressDragUntilRelease = false;
                ClearAdditionalCarets ();
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

        _suppressDragUntilRelease = false;
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

        var col = Math.Max (0, Viewport.X + viewPos.X);

        if (WordWrap)
        {
            return MousePositionToOffsetWrapped (viewPos, col);
        }

        // Map viewport row to document line via visible-line list (respects folding).
        List<int> visibleLines = GetVisibleLineNumbers ();
        var visibleIndex = Math.Clamp (Viewport.Y + viewPos.Y, 0, visibleLines.Count - 1);
        var lineNumber = visibleLines[visibleIndex];
        DocumentLine line = _document.GetLineByNumber (lineNumber);
        var colInLine = GetOrBuildDefaultVisualLine (line).GetRelativeOffset (col);

        return line.Offset + colInLine;
    }

    /// <summary>
    ///     Word-wrap-aware variant of <see cref="MousePositionToOffset" />. Uses the wrap map to
    ///     resolve the visual row to a specific segment within a document line, then computes the
    ///     offset within that segment.
    /// </summary>
    private int MousePositionToOffsetWrapped (Point viewPos, int col)
    {
        List<WrapMapEntry> map = GetWrapMap ();

        if (map.Count == 0)
        {
            return 0;
        }

        var visibleIndex = Math.Clamp (Viewport.Y + viewPos.Y, 0, map.Count - 1);
        WrapMapEntry entry = map[visibleIndex];
        DocumentLine line = _document!.GetLineByNumber (entry.LineNumber);
        var text = _document.GetText (line);
        IReadOnlyList<WrapSegment> segments =
            WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);
        WrapSegment seg = segments[entry.SegmentIndex];
        var segText = text.Substring (seg.StartOffset, seg.Length);

        // Build a visual line for just this segment so GetRelativeOffset works on segment-local columns.
        // GetRelativeOffset returns an offset relative to DocumentLine.Offset (already includes
        // seg.StartOffset), and falls back to DocumentLine.Length for past-end clicks — clamp to
        // the segment boundary instead.
        CellVisualLine visualLine = BuildVisualLineForSegment (line, seg.StartOffset, segText);
        var offsetInLine = Math.Min (visualLine.GetRelativeOffset (col), seg.StartOffset + seg.Length);

        return line.Offset + offsetInLine;
    }

    /// <summary>
    ///     Builds a <see cref="CellVisualLine" /> for a single word-wrap segment of a document line.
    ///     Used by the mouse-hit-testing path — only element geometry matters, not attributes.
    /// </summary>
    private CellVisualLine BuildVisualLineForSegment (DocumentLine documentLine, int segmentStartOffset, string segmentText)
    {
        CellVisualLine visualLine = new (documentLine);
        var visualColumn = 0;
        Attribute attr = default;

        for (var i = 0; i < segmentText.Length; i++)
        {
            var documentOffset = documentLine.Offset + segmentStartOffset + i;

            if (segmentText[i] == '\t')
            {
                var width = VisualLineBuilder.GetTabExpansionWidth (visualColumn, IndentationSize);
                visualLine.AddElement (new TabElement (documentOffset, visualColumn, width, false, attr));
                visualColumn += width;
            }
            else if (segmentText[i] <= 127)
            {
                TextRunElement element = new (documentOffset, 1, visualColumn, segmentText.Substring (i, 1), attr);
                visualLine.AddElement (element);
                visualColumn += 1;
            }
            else
            {
                var remaining = segmentText[i..];

                foreach (var grapheme in GraphemeHelper.GetGraphemes (remaining))
                {
                    var gDocOffset = documentLine.Offset + segmentStartOffset + i;
                    TextRunElement gElement = new (gDocOffset, grapheme.Length, visualColumn, grapheme, attr);
                    visualLine.AddElement (gElement);
                    visualColumn += gElement.VisualLength;
                    i += grapheme.Length;
                }

                i--;

                break;
            }
        }

        return visualLine;
    }
}
