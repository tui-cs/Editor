using Terminal.Gui.Text.Document;
using Terminal.Gui.Views.Rendering;

namespace Terminal.Gui.Views;

public partial class Editor
{
    private bool InsertTab ()
    {
        if (_document is null)
        {
            return false;
        }

        if (HasSelection && SelectionSpansMultipleLines ())
        {
            IndentSelectedLines ();

            return true;
        }

        string text = GetTabInsertionText (HasSelection ? SelectionStart : _caretOffset);

        if (HasSelection)
        {
            ReplaceSelection (text);
        }
        else
        {
            _document.Insert (_caretOffset, text);
        }

        return true;
    }

    private bool Unindent ()
    {
        if (_document is null)
        {
            return false;
        }

        List<DocumentLine> lines = HasSelection && SelectionSpansMultipleLines ()
            ? GetSelectedLines ()
            : [_document.GetLineByOffset (_caretOffset)];

        List<(int offset, int length)> removals = [];

        foreach (DocumentLine line in lines)
        {
            ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, line.Offset, IndentationSize);

            if (segment.Length > 0)
            {
                removals.Add ((segment.Offset, segment.Length));
            }
        }

        if (removals.Count == 0)
        {
            return true;
        }

        var hadSelection = HasSelection;
        var selectionWasForward = _selectionAnchor <= _caretOffset;
        var selectionStart = SelectionStart;
        var selectionEnd = SelectionEnd;

        using (_document.RunUpdate ())
        {
            foreach ((int offset, int length) removal in removals.OrderByDescending (static r => r.offset))
            {
                _document.Remove (removal.offset, removal.length);
            }
        }

        if (hadSelection)
        {
            SetSelectionRangePreservingDirection (
                selectionWasForward,
                AdjustOffsetAfterRemovals (selectionStart, removals),
                AdjustOffsetAfterRemovals (selectionEnd, removals));
        }

        return true;
    }

    private void IndentSelectedLines ()
    {
        List<DocumentLine> lines = GetSelectedLines ();

        if (lines.Count == 0)
        {
            return;
        }

        string indentText = GetIndentText ();
        var selectionWasForward = _selectionAnchor <= _caretOffset;
        var selectionStart = SelectionStart;
        var selectionEnd = SelectionEnd;

        using (_document!.RunUpdate ())
        {
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                _document.Insert (lines[i].Offset, indentText);
            }
        }

        SetSelectionRangePreservingDirection (
            selectionWasForward,
            selectionStart + indentText.Length,
            selectionEnd + indentText.Length * lines.Count);
    }

    private bool TryDeleteIndentationLeft ()
    {
        if (_document is null || _caretOffset == 0)
        {
            return false;
        }

        DocumentLine line = _document.GetLineByOffset (_caretOffset);
        ISegment leadingWhitespace = TextUtilities.GetLeadingWhitespace (_document, line);

        if (leadingWhitespace.Length == 0 || _caretOffset != leadingWhitespace.EndOffset)
        {
            return false;
        }

        var scanOffset = line.Offset;
        (int offset, int length) lastSegment = (0, 0);

        while (scanOffset < leadingWhitespace.EndOffset)
        {
            ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, scanOffset, IndentationSize);

            if (segment.Length == 0 || scanOffset + segment.Length > _caretOffset)
            {
                break;
            }

            lastSegment = (segment.Offset, segment.Length);
            scanOffset += segment.Length;
        }

        if (scanOffset != _caretOffset || lastSegment.length == 0)
        {
            return false;
        }

        _document.Remove (lastSegment.offset, lastSegment.length);

        return true;
    }

    private bool SelectionSpansMultipleLines ()
    {
        return HasSelection && GetSelectedLines ().Count > 1;
    }

    private List<DocumentLine> GetSelectedLines ()
    {
        List<DocumentLine> lines = [];

        if (_document is null || !HasSelection)
        {
            return lines;
        }

        DocumentLine firstLine = _document.GetLineByOffset (SelectionStart);
        var endOffset = Math.Max (SelectionStart, SelectionEnd - 1);
        DocumentLine lastLine = _document.GetLineByOffset (endOffset);

        for (var lineNumber = firstLine.LineNumber; lineNumber <= lastLine.LineNumber; lineNumber++)
        {
            lines.Add (_document.GetLineByNumber (lineNumber));
        }

        return lines;
    }

    private string GetTabInsertionText (int offset)
    {
        if (!ConvertTabsToSpaces)
        {
            return "\t";
        }

        DocumentLine line = _document!.GetLineByOffset (offset);
        var visualColumn = GetVisualColumnFromLogicalColumn (line, offset - line.Offset);
        var spaces = VisualLineBuilder.GetTabExpansionWidth (visualColumn, IndentationSize);

        return new string (' ', spaces);
    }

    private string GetIndentText ()
    {
        return ConvertTabsToSpaces ? new string (' ', IndentationSize) : "\t";
    }

    private void SetSelectionRangePreservingDirection (bool forward, int start, int end)
    {
        _selectionAnchor = forward ? start : end;
        SetCaretOffset (forward ? end : start, true);
        SelectionChanged?.Invoke (this, EventArgs.Empty);
        SetNeedsDraw ();
    }

    private static int AdjustOffsetAfterRemovals (int offset, IReadOnlyList<(int offset, int length)> removals)
    {
        var adjusted = offset;

        foreach ((int removeOffset, int removeLength) in removals.OrderBy (static r => r.offset))
        {
            if (removeOffset + removeLength <= adjusted)
            {
                adjusted -= removeLength;

                continue;
            }

            if (removeOffset < adjusted)
            {
                adjusted = removeOffset;
            }
        }

        return adjusted;
    }
}
