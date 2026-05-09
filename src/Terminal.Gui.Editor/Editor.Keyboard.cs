using System.Text;
using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Catches keystrokes that didn't match any registered <see cref="Command" /> binding (set up in
    ///     <see cref="CreateCommandsAndBindings" />) and inserts the typed rune into the document. Skips
    ///     modified keys and control characters — those are either bound elsewhere or not editor input.
    /// </summary>
    /// <inheritdoc />
    protected override bool OnKeyDownNotHandled (Key key)
    {
        if (key == Key.Tab)
        {
            HandleTabKey ();

            return true;
        }

        if (key == Key.Tab.WithShift)
        {
            HandleShiftTabKey ();

            return true;
        }

        if (key.IsCtrl || key.IsAlt)
        {
            return false;
        }

        // Rune.IsControl already covers U+0000 (default(Rune)), so no explicit NUL guard is needed.
        if (key is not { AsRune: { } rune } || Rune.IsControl (rune))
        {
            return false;
        }

        if (HasSelection)
        {
            ReplaceSelection (rune.ToString ());
        }
        else
        {
            _document!.Insert (_caretOffset, rune.ToString ());
        }

        return true;
    }

    private void HandleTabKey ()
    {
        if (HasSelection && SelectionSpansMultipleLines ())
        {
            IndentSelectionLines ();

            return;
        }

        var insertionText = GetTabInsertionTextAtCaret ();

        if (HasSelection)
        {
            ReplaceSelection (insertionText);
        }
        else
        {
            _document!.Insert (_caretOffset, insertionText);
        }
    }

    private void HandleShiftTabKey ()
    {
        if (HasSelection && SelectionSpansMultipleLines ())
        {
            UnindentSelectionLines ();

            return;
        }

        UnindentCurrentLine ();
    }

    private string GetTabInsertionTextAtCaret ()
    {
        if (!ConvertTabsToSpaces)
        {
            return "\t";
        }

        var caretVisualColumn = GetCaretColumn ();
        var remainder = caretVisualColumn % IndentationSize;
        var count = remainder == 0 ? IndentationSize : IndentationSize - remainder;

        return new (' ', count);
    }

    private bool SelectionSpansMultipleLines ()
    {
        if (!HasSelection || _document is null)
        {
            return false;
        }

        DocumentLine startLine = _document.GetLineByOffset (SelectionStart);
        DocumentLine endLine = _document.GetLineByOffset (GetSelectionEndForLineRange ());

        return startLine.LineNumber != endLine.LineNumber;
    }

    private void IndentSelectionLines ()
    {
        if (_document is null || _selectionAnchor is null)
        {
            return;
        }

        int originalAnchor = _selectionAnchor.Value;
        int originalCaret = _caretOffset;
        List<int> lineOffsets = GetSelectionLineOffsets ();
        string indentation = ConvertTabsToSpaces ? new (' ', IndentationSize) : "\t";

        using IDisposable scope = _document.RunUpdate ();

        for (var index = lineOffsets.Count - 1; index >= 0; index--)
        {
            _document.Insert (lineOffsets[index], indentation);
        }

        _selectionAnchor = AdjustOffsetForInsertions (originalAnchor, lineOffsets, indentation.Length);
        CaretOffset = AdjustOffsetForInsertions (originalCaret, lineOffsets, indentation.Length);
    }

    private void UnindentSelectionLines ()
    {
        if (_document is null || _selectionAnchor is null)
        {
            return;
        }

        int originalAnchor = _selectionAnchor.Value;
        int originalCaret = _caretOffset;
        List<(int Offset, int Length)> removals = GetSelectionIndentationRemovals ();

        if (removals.Count == 0)
        {
            return;
        }

        using IDisposable scope = _document.RunUpdate ();

        for (var index = removals.Count - 1; index >= 0; index--)
        {
            (int offset, int length) removal = removals[index];
            _document.Remove (removal.offset, removal.length);
        }

        _selectionAnchor = AdjustOffsetForRemovals (originalAnchor, removals);
        CaretOffset = AdjustOffsetForRemovals (originalCaret, removals);
    }

    private void UnindentCurrentLine ()
    {
        if (_document is null)
        {
            return;
        }

        DocumentLine line = _document.GetLineByOffset (_caretOffset);
        ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, line.Offset, IndentationSize);

        if (segment.Length == 0)
        {
            return;
        }

        _document.Remove (segment.Offset, segment.Length);
    }

    private List<int> GetSelectionLineOffsets ()
    {
        List<int> offsets = [];
        DocumentLine firstLine = _document!.GetLineByOffset (SelectionStart);
        DocumentLine lastLine = _document.GetLineByOffset (GetSelectionEndForLineRange ());

        for (var lineNumber = firstLine.LineNumber; lineNumber <= lastLine.LineNumber; lineNumber++)
        {
            offsets.Add (_document.GetLineByNumber (lineNumber).Offset);
        }

        return offsets;
    }

    private List<(int Offset, int Length)> GetSelectionIndentationRemovals ()
    {
        List<(int Offset, int Length)> removals = [];

        foreach (int lineOffset in GetSelectionLineOffsets ())
        {
            ISegment segment = TextUtilities.GetSingleIndentationSegment (_document!, lineOffset, IndentationSize);

            if (segment.Length > 0)
            {
                removals.Add ((segment.Offset, segment.Length));
            }
        }

        return removals;
    }

    private int GetSelectionEndForLineRange ()
    {
        if (!HasSelection || _document is null)
        {
            return _caretOffset;
        }

        var end = SelectionEnd;

        if (end <= SelectionStart || end == 0)
        {
            return end;
        }

        DocumentLine endLine = _document.GetLineByOffset (end);

        // If the selection ends exactly at the next line's start offset, keep that empty boundary
        // out of the indent/unindent range by treating the previous line as the selection end.
        return end == endLine.Offset ? end - 1 : end;
    }

    private static int AdjustOffsetForInsertions (int offset, IEnumerable<int> lineOffsets, int insertionLength)
    {
        var adjusted = offset;

        foreach (int lineOffset in lineOffsets)
        {
            if (lineOffset <= adjusted)
            {
                adjusted += insertionLength;
            }
        }

        return adjusted;
    }

    private static int AdjustOffsetForRemovals (int offset, IEnumerable<(int Offset, int Length)> removals)
    {
        var adjusted = offset;

        foreach ((int removalOffset, int removalLength) in removals.OrderBy (x => x.Offset))
        {
            if (adjusted <= removalOffset)
            {
                continue;
            }

            if (adjusted < removalOffset + removalLength)
            {
                adjusted = removalOffset;
            }
            else
            {
                adjusted -= removalLength;
            }
        }

        return adjusted;
    }
}
