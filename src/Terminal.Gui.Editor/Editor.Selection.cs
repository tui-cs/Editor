using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

/// <summary>
///     Selection state and operations. Internal model is anchor + caret: <c>_selectionAnchor</c> is
///     where Shift was first held; the <c>CaretOffset</c> is the live end. Selection is the segment
///     between the two; when they coincide (or the anchor is null), there is no selection.
/// </summary>
public partial class Editor
{
    private int? _selectionAnchor;

    /// <summary>True when there is a non-empty selection.</summary>
    public bool HasSelection => _selectionAnchor is { } a && a != _caretOffset;

    /// <summary>Start offset of the selection (inclusive). Equals <see cref="CaretOffset" /> when no selection.</summary>
    public int SelectionStart => HasSelection ? Math.Min (_selectionAnchor!.Value, _caretOffset) : _caretOffset;

    /// <summary>End offset of the selection (exclusive). Equals <see cref="CaretOffset" /> when no selection.</summary>
    public int SelectionEnd => HasSelection ? Math.Max (_selectionAnchor!.Value, _caretOffset) : _caretOffset;

    /// <summary>Length of the selection in characters; 0 when no selection.</summary>
    public int SelectionLength => SelectionEnd - SelectionStart;

    /// <summary>The selection as a <see cref="TextSegment" />, or <see langword="null" /> if no selection.</summary>
    public TextSegment? Selection =>
        !HasSelection ? null : new TextSegment { StartOffset = SelectionStart, Length = SelectionLength };

    /// <summary>The selected document text, or an empty string when there is no selection.</summary>
    public string SelectedText => HasSelection ? _document!.GetText (SelectionStart, SelectionLength) : string.Empty;

    /// <summary>Raised whenever the selection range changes (created, extended, cleared).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Clears the selection without moving the caret.</summary>
    public void ClearSelection ()
    {
        if (_selectionAnchor is null)
        {
            return;
        }

        (int start, int end) before = SelectionTuple ();
        _selectionAnchor = null;
        RaiseSelectionChangedIfMoved (before);
        SetNeedsDraw ();
    }

    /// <summary>Selects the entire document. Caret moves to <c>TextLength</c>.</summary>
    public void SelectAll ()
    {
        SelectRange (0, _document!.TextLength);
    }

    /// <summary>Selects a document range and moves the caret to the range end.</summary>
    public void SelectRange (int startOffset, int length)
    {
        if (_document is null)
        {
            return;
        }

        var start = Math.Clamp (startOffset, 0, _document.TextLength);
        var end = (int)Math.Clamp ((long)start + Math.Max (0L, length), 0L, _document.TextLength);

        (int start, int end) before = SelectionTuple ();
        _selectionAnchor = start == end ? null : start;
        CaretOffset = end;
        RaiseSelectionChangedIfMoved (before);
        SetNeedsDraw ();
    }

    /// <summary>
    ///     Replaces the current selection with <paramref name="replacement" />. If there is no selection, this is a no-op.
    ///     The caret lands at <c>SelectionStart + replacement.Length</c> via the document's edit-tracking arithmetic.
    /// </summary>
    public void ReplaceSelection (string replacement)
    {
        if (!HasSelection)
        {
            return;
        }

        var start = SelectionStart;
        var len = SelectionLength;

        (int start, int end) before = SelectionTuple ();
        _selectionAnchor = null;
        _document!.Replace (start, len, replacement);
        RaiseSelectionChangedIfMoved (before);
    }

    /// <summary>
    ///     Begins (if needed) or extends the selection by setting the anchor to the current caret and then moving the
    ///     caret <paramref name="delta" /> characters horizontally.
    /// </summary>
    private void ExtendCaretBy (int delta)
    {
        ExtendCaretTo (_caretOffset + delta);
    }

    private void ExtendCaretVertically (int delta)
    {
        (int start, int end) before = SelectionTupleWithEnsuredAnchor ();
        MoveCaretVertically (delta);
        RaiseSelectionChangedIfMoved (before);
        SetNeedsDraw ();
    }

    private void ExtendCaretTo (int newCaret)
    {
        (int start, int end) before = SelectionTupleWithEnsuredAnchor ();
        CaretOffset = newCaret;
        RaiseSelectionChangedIfMoved (before);
        SetNeedsDraw ();
    }

    private void EnsureSelectionAnchor ()
    {
        _selectionAnchor ??= _caretOffset;
    }

    /// <summary>
    ///     Returns the current effective selection range as a (start, end) tuple. Callers compare a
    ///     before-snapshot to the post-mutation tuple via <see cref="RaiseSelectionChangedIfMoved" /> to
    ///     suppress no-op <see cref="SelectionChanged" /> firings.
    /// </summary>
    private (int start, int end) SelectionTuple ()
    {
        return (SelectionStart, SelectionEnd);
    }

    /// <summary>
    ///     Snapshot variant that primes the selection anchor first — used by extend-style operations
    ///     where setting the anchor is part of the operation but should not by itself count as a
    ///     selection-range change.
    /// </summary>
    private (int start, int end) SelectionTupleWithEnsuredAnchor ()
    {
        EnsureSelectionAnchor ();

        return SelectionTuple ();
    }

    private void RaiseSelectionChangedIfMoved ((int start, int end) before)
    {
        if (before == SelectionTuple ())
        {
            return;
        }

        SelectionChanged?.Invoke (this, EventArgs.Empty);
    }

    /// <summary>
    ///     Movement helper that respects an existing selection: plain (non-extending) cursor keys clear the selection
    ///     and snap to the appropriate end; otherwise the caret moves by <paramref name="delta" />.
    /// </summary>
    private void MoveCaretByCollapsingSelection (int delta)
    {
        if (HasSelection)
        {
            var target = delta < 0 ? SelectionStart : SelectionEnd;
            ClearSelection ();
            CaretOffset = target;

            return;
        }

        CaretOffset = _caretOffset + delta;
    }

    /// <summary>
    ///     Vertical movement helper: clears any selection then runs the standard line-up/line-down with sticky-column.
    /// </summary>
    private void MoveCaretVerticallyCollapsingSelection (int delta)
    {
        ClearSelection ();
        MoveCaretVertically (delta);
    }

    private void SelectWordAtOffset (int offset)
    {
        if (_document is null)
        {
            return;
        }

        var originalOffset = Math.Clamp (offset, 0, _document.TextLength);
        var wordOffset = originalOffset;

        if (wordOffset == _document.TextLength || !IsIdentifierWordCharAt (wordOffset))
        {
            if (wordOffset == 0 || !IsIdentifierWordCharAt (wordOffset - 1))
            {
                ClearSelection ();
                CaretOffset = originalOffset;

                return;
            }

            wordOffset--;
        }

        var start = wordOffset;

        while (start > 0 && IsIdentifierWordCharAt (start - 1))
        {
            start--;
        }

        var end = wordOffset + 1;

        while (end < _document.TextLength && IsIdentifierWordCharAt (end))
        {
            end++;
        }

        SelectRange (start, end - start);
    }

    private void SelectLineAtOffset (int offset)
    {
        if (_document is null)
        {
            return;
        }

        DocumentLine line = _document.GetLineByOffset (Math.Clamp (offset, 0, _document.TextLength));
        SelectRange (line.Offset, line.TotalLength);
    }

    internal void SelectLineAtViewRow (int row)
    {
        if (_document is null || _document.LineCount == 0)
        {
            return;
        }

        var lineNumber = Math.Clamp (Viewport.Y + row + 1, 1, _document.LineCount);
        SelectLines (lineNumber, lineNumber);
    }

    internal void SelectLines (int anchorLineNumber, int activeLineNumber)
    {
        if (_document is null || _document.LineCount == 0)
        {
            return;
        }

        var startLineNumber = Math.Clamp (Math.Min (anchorLineNumber, activeLineNumber), 1, _document.LineCount);
        var endLineNumber = Math.Clamp (Math.Max (anchorLineNumber, activeLineNumber), 1, _document.LineCount);
        DocumentLine startLine = _document.GetLineByNumber (startLineNumber);
        DocumentLine endLine = _document.GetLineByNumber (endLineNumber);
        SelectRange (startLine.Offset, endLine.Offset + endLine.TotalLength - startLine.Offset);
    }

    internal int ViewRowToLineNumber (int row)
    {
        if (_document is null || _document.LineCount == 0)
        {
            return 1;
        }

        return Math.Clamp (Viewport.Y + row + 1, 1, _document.LineCount);
    }

    private bool IsIdentifierWordCharAt (int offset)
    {
        char ch = _document!.GetCharAt (offset);

        return char.IsLetterOrDigit (ch) || ch == '_';
    }
}
