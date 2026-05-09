namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Finds the next match for <paramref name="searchText" /> starting at the current caret (or after the current
    ///     selection) and selects it.
    /// </summary>
    /// <returns><see langword="true" /> when a match is found; otherwise <see langword="false" />.</returns>
    public bool FindNext (string searchText, bool matchCase = false, bool wrapAround = true)
    {
        if (string.IsNullOrEmpty (searchText) || _document is null)
        {
            return false;
        }

        var startOffset = HasSelection ? SelectionEnd : _caretOffset;
        var matchOffset = FindForwardOffset (searchText, startOffset, matchCase);

        if (matchOffset < 0 && wrapAround && startOffset > 0)
        {
            matchOffset = FindForwardOffset (searchText, 0, matchCase);
        }

        if (matchOffset < 0)
        {
            return false;
        }

        SelectSearchMatch (matchOffset, searchText.Length);

        return true;
    }

    /// <summary>
    ///     Finds the previous match for <paramref name="searchText" /> before the current caret (or before the current
    ///     selection start) and selects it.
    /// </summary>
    /// <returns><see langword="true" /> when a match is found; otherwise <see langword="false" />.</returns>
    public bool FindPrevious (string searchText, bool matchCase = false, bool wrapAround = true)
    {
        if (string.IsNullOrEmpty (searchText) || _document is null || _document.TextLength == 0)
        {
            return false;
        }

        var startOffset = HasSelection ? SelectionStart - 1 : _caretOffset - 1;
        var matchOffset = FindBackwardOffset (searchText, startOffset, matchCase);

        if (matchOffset < 0 && wrapAround && startOffset < _document.TextLength - 1)
        {
            matchOffset = FindBackwardOffset (searchText, _document.TextLength - 1, matchCase);
        }

        if (matchOffset < 0)
        {
            return false;
        }

        SelectSearchMatch (matchOffset, searchText.Length);

        return true;
    }

    /// <summary>
    ///     Replaces the current match (if selected) or finds the next match and replaces it.
    /// </summary>
    /// <returns><see langword="true" /> when a replacement is made; otherwise <see langword="false" />.</returns>
    public bool ReplaceNext (string searchText, string replacement, bool matchCase = false, bool wrapAround = true)
    {
        ArgumentNullException.ThrowIfNull (replacement);

        if (string.IsNullOrEmpty (searchText) || _document is null)
        {
            return false;
        }

        if (!SelectionMatches (searchText, matchCase))
        {
            if (!FindNext (searchText, matchCase, wrapAround))
            {
                return false;
            }
        }

        ReplaceSelection (replacement);

        return true;
    }

    /// <summary>
    ///     Replaces all matches of <paramref name="searchText" /> in the document.
    /// </summary>
    /// <returns>The number of replacements performed.</returns>
    public int ReplaceAll (string searchText, string replacement, bool matchCase = false)
    {
        ArgumentNullException.ThrowIfNull (replacement);

        if (string.IsNullOrEmpty (searchText) || _document is null)
        {
            return 0;
        }

        var replacements = 0;
        var searchOffset = 0;

        while (searchOffset < _document.TextLength)
        {
            var matchOffset = FindForwardOffset (searchText, searchOffset, matchCase);

            if (matchOffset < 0)
            {
                break;
            }

            SelectSearchMatch (matchOffset, searchText.Length);
            ReplaceSelection (replacement);

            searchOffset = matchOffset + replacement.Length;
            replacements++;
        }

        return replacements;
    }

    private int FindForwardOffset (string searchText, int startOffset, bool matchCase)
    {
        if (_document is null || startOffset < 0 || startOffset > _document.TextLength)
        {
            return -1;
        }

        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return _document.Text.IndexOf (searchText, startOffset, comparison);
    }

    private int FindBackwardOffset (string searchText, int startOffset, bool matchCase)
    {
        if (_document is null || _document.TextLength == 0 || startOffset < 0)
        {
            return -1;
        }

        var clampedStart = Math.Clamp (startOffset, 0, _document.TextLength - 1);
        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return _document.Text.LastIndexOf (searchText, clampedStart, comparison);
    }

    private bool SelectionMatches (string searchText, bool matchCase)
    {
        if (!HasSelection || _document is null || SelectionLength != searchText.Length)
        {
            return false;
        }

        if (SelectionStart < 0 || SelectionLength < 0 || SelectionEnd > _document.TextLength)
        {
            return false;
        }

        StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        return string.Compare (_document.Text, SelectionStart, searchText, 0, searchText.Length, comparison) == 0;
    }

    private void SelectSearchMatch (int startOffset, int length)
    {
        if (_document is null || length <= 0)
        {
            return;
        }

        var start = Math.Clamp (startOffset, 0, _document.TextLength);
        var end = Math.Clamp (start + length, start, _document.TextLength);

        _selectionAnchor = start;
        CaretOffset = end;
        SelectionChanged?.Invoke (this, EventArgs.Empty);
        SetNeedsDraw ();
    }
}
