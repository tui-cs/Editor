using System.Drawing;
using Terminal.Gui.Document;

namespace Terminal.Gui.Editor;

/// <summary>
///     Multi-caret state and operations. Additional carets are backed by <see cref="TextAnchor" />
///     instances (same as the primary caret) so they track document edits automatically. Each
///     additional caret carries its own selection anchor. Editing commands iterate all carets in
///     descending offset order inside a single <see cref="TextDocument.RunUpdate" /> scope so that
///     undo collapses the whole operation into one step.
/// </summary>
public partial class Editor
{
    private readonly List<CaretInfo> _additionalCarets = [];

    /// <summary>Gets the offsets of all additional carets (excludes the primary).</summary>
    public IReadOnlyList<int> AdditionalCaretOffsets => _additionalCarets
        .Where (c => c.CaretAnchor is { IsDeleted: false })
        .Select (c => c.CaretAnchor!.Offset)
        .ToList ();

    /// <summary>True when there are additional carets beyond the primary.</summary>
    public bool HasMultipleCarets => _additionalCarets.Count > 0;

    /// <summary>
    ///     Adds an additional caret at the given <paramref name="offset" />, or removes the one
    ///     already there (toggle behavior for Ctrl+Click). The add path goes through
    ///     <see cref="AddAdditionalCaretAt" />; the toggle-off is an explicit, user-driven single
    ///     removal. The primary caret is never removed.
    /// </summary>
    public void ToggleCaretAt (int offset)
    {
        if (_document is null)
        {
            return;
        }

        offset = Math.Clamp (offset, 0, _document.TextLength);

        if (offset == CaretOffset)
        {
            return;
        }

        for (var i = _additionalCarets.Count - 1; i >= 0; i--)
        {
            if (_additionalCarets[i].CaretAnchor is not { IsDeleted: false } anchor || anchor.Offset != offset)
            {
                continue;
            }

            _additionalCarets.RemoveAt (i);
            SetNeedsDraw ();

            return;
        }

        AddAdditionalCaretAt (offset);
    }

    /// <summary>
    ///     Adds one additional caret at <paramref name="offset" />. The single add path that
    ///     mutates <see cref="_additionalCarets" />: it never duplicates the primary and never
    ///     stacks two additional carets on one offset, so the caret set is deduped by construction
    ///     rather than normalized after the fact.
    /// </summary>
    private void AddAdditionalCaretAt (int offset)
    {
        if (_document is null)
        {
            return;
        }

        offset = Math.Clamp (offset, 0, _document.TextLength);

        if (offset == CaretOffset)
        {
            return;
        }

        foreach (CaretInfo caret in _additionalCarets)
        {
            if (caret.CaretAnchor is { IsDeleted: false } anchor && anchor.Offset == offset)
            {
                return;
            }
        }

        _additionalCarets.Add (new CaretInfo { CaretAnchor = CreateCaretAnchor (offset) });
        SetNeedsDraw ();
    }

    /// <summary>
    ///     Re-establishes the multi-caret invariant: drops any additional caret whose anchor was
    ///     deleted, any that coincides with the primary, and collapses duplicates at the same
    ///     offset. Called after every primary-caret move and every document change (before the
    ///     next edit applies) — the single invariant-trim path that mutates
    ///     <see cref="_additionalCarets" />.
    /// </summary>
    private void NormalizeAdditionalCarets ()
    {
        if (_additionalCarets.Count == 0)
        {
            return;
        }

        HashSet<int> seen = [CaretOffset];
        var removed = false;

        for (var i = _additionalCarets.Count - 1; i >= 0; i--)
        {
            if (_additionalCarets[i].CaretAnchor is { IsDeleted: false } anchor && seen.Add (anchor.Offset))
            {
                continue;
            }

            _additionalCarets.RemoveAt (i);
            removed = true;
        }

        if (removed)
        {
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Extends the vertical caret block one line above (<paramref name="delta" /> &lt; 0) the
    ///     topmost caret or below (&gt; 0) the bottommost, landing on the sticky visual column.
    ///     Crossing the top/bottom document bound is a no-op. Bound to
    ///     <c>Ctrl+Alt+CursorUp</c> / <c>Ctrl+Alt+CursorDown</c> via the configurable
    ///     <see cref="DefaultKeyBindings" />.
    /// </summary>
    private bool? AddCaretVertically (int delta)
    {
        if (_document is null)
        {
            return true;
        }

        // The sticky column is captured once, when the block is first created, from the primary
        // caret's column — then preserved across extensions (single-caret virtual-column
        // behavior, reused rather than re-derived).
        if (!HasMultipleCarets)
        {
            _virtualCaretColumn = GetVisualColumnForOffset (CaretOffset);
        }

        // Reference = the extreme caret in the requested direction: topmost for up, bottommost
        // for down. The block grows past that edge.
        var reference = CaretOffset;

        foreach (var offset in AdditionalCaretOffsets)
        {
            reference = delta < 0 ? Math.Min (reference, offset) : Math.Max (reference, offset);
        }

        if (TryGetVerticalOffset (reference, delta, _virtualCaretColumn, out var target))
        {
            AddAdditionalCaretAt (target);
        }

        return true;
    }

    /// <summary>
    ///     Builds a column of carets from the <paramref name="anchorViewRow" /> (which hosts the
    ///     primary) through the <paramref name="activeViewRow" />, all at
    ///     <paramref name="viewColumn" />. Used by the <c>Shift+Alt</c> column-drag. Rebuilt from
    ///     scratch on every drag event so the end state is identical to a single press at the
    ///     final position.
    /// </summary>
    private void SetVerticalCaretsFromViewRows (int anchorViewRow, int activeViewRow, int viewColumn)
    {
        if (_document is null)
        {
            return;
        }

        var primaryOffset = MousePositionToOffset (new Point (viewColumn, anchorViewRow));

        ClearSelection ();
        ClearAdditionalCarets ();

        // The CaretOffset setter resets the sticky column from the anchor row's primary.
        CaretOffset = primaryOffset;

        var top = Math.Min (anchorViewRow, activeViewRow);
        var bottom = Math.Max (anchorViewRow, activeViewRow);

        for (var row = top; row <= bottom; row++)
        {
            if (row == anchorViewRow)
            {
                continue;
            }

            AddAdditionalCaretAt (MousePositionToOffset (new Point (viewColumn, row)));
        }

        SetNeedsDraw ();
    }

    /// <summary>Removes all additional carets, leaving only the primary.</summary>
    public void ClearAdditionalCarets ()
    {
        var had = _additionalCarets.Count > 0;

        if (had)
        {
            _additionalCarets.Clear ();
        }

        // Refresh the sticky virtual column to the primary's current column so vertical
        // navigation resumes freely from where the primary actually is — not wherever the
        // dismissed block left it. (specs/vertical-multi-caret: "Esc clears multi-caret and
        // refreshes sticky column".)
        if (_document is not null)
        {
            _virtualCaretColumn = GetCaretColumn ();
        }

        if (had)
        {
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Returns all caret offsets (primary + additional) sorted in descending order.
    ///     Used by editing commands to process from high to low offset so earlier edits don't
    ///     invalidate later positions.
    /// </summary>
    private List<CaretEditInfo> GetAllCaretsDescending ()
    {
        List<CaretEditInfo> result = [new CaretEditInfo { Offset = CaretOffset, IsPrimary = true }];

        foreach (CaretInfo caret in _additionalCarets)
        {
            if (caret.CaretAnchor is { IsDeleted: false } anchor)
            {
                result.Add (new CaretEditInfo
                {
                    Offset = anchor.Offset,
                    IsPrimary = false,
                    SelectionAnchor = caret.SelectionAnchor
                });
            }
        }

        result.Sort ((a, b) => b.Offset.CompareTo (a.Offset));

        return result;
    }

    /// <summary>
    ///     Executes a multi-caret insert. Each caret has <paramref name="text" /> inserted at its
    ///     position. Wrapped in a single undo scope.
    /// </summary>
    private bool? MultiCaretInsert (string text)
    {
        if (ReadOnly || _document is null)
        {
            return true;
        }

        if (!HasMultipleCarets)
        {
            return InsertOrReplace (text);
        }

        using (_document.RunUpdate ())
        {
            List<CaretEditInfo> carets = GetAllCaretsDescending ();

            foreach (CaretEditInfo caret in carets)
            {
                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        ReplaceSelection (text);
                    }
                    else
                    {
                        _document.Insert (CaretOffset, text);
                    }
                }
                else
                {
                    if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                    {
                        var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                        var selEnd = Math.Max (selAnchor.Offset, caret.Offset);

                        if (selEnd > selStart)
                        {
                            _document.Replace (selStart, selEnd - selStart, text);

                            continue;
                        }
                    }

                    _document.Insert (caret.Offset, text);
                }
            }
        }

        // Clear per-caret selections after edit.
        // Editing collapses per-caret selections, matching single-caret behavior.
        ClearAdditionalCaretSelections ();

        return true;
    }

    /// <summary>
    ///     Executes a multi-caret delete-left (backspace). Each caret deletes one indentation unit
    ///     (when at a leading-whitespace boundary) or one character to its left, matching single-caret
    ///     smart-backspace behavior. Wrapped in a single undo scope.
    /// </summary>
    private bool? MultiCaretDeleteLeft ()
    {
        if (ReadOnly || _document is null)
        {
            return true;
        }

        if (!HasMultipleCarets)
        {
            return DeleteLeft ();
        }

        using (_document.RunUpdate ())
        {
            List<CaretEditInfo> carets = GetAllCaretsDescending ();

            foreach (CaretEditInfo caret in carets)
            {
                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        ReplaceSelection (string.Empty);
                    }
                    else if (!TryDeleteIndentationLeftAt (CaretOffset) && CaretOffset > 0)
                    {
                        _document.Remove (CaretOffset - 1, 1);
                    }
                }
                else
                {
                    if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                    {
                        var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                        var selEnd = Math.Max (selAnchor.Offset, caret.Offset);

                        if (selEnd > selStart)
                        {
                            _document.Replace (selStart, selEnd - selStart, string.Empty);

                            continue;
                        }
                    }

                    if (!TryDeleteIndentationLeftAt (caret.Offset) && caret.Offset > 0)
                    {
                        _document.Remove (caret.Offset - 1, 1);
                    }
                }
            }
        }

        ClearAdditionalCaretSelections ();

        return true;
    }

    /// <summary>
    ///     Executes a multi-caret delete-right (delete key). Each caret deletes one character to its right.
    ///     Wrapped in a single undo scope.
    /// </summary>
    private bool? MultiCaretDeleteRight ()
    {
        if (ReadOnly || _document is null)
        {
            return true;
        }

        if (!HasMultipleCarets)
        {
            return DeleteRight ();
        }

        using (_document.RunUpdate ())
        {
            List<CaretEditInfo> carets = GetAllCaretsDescending ();

            foreach (CaretEditInfo caret in carets)
            {
                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        ReplaceSelection (string.Empty);
                    }
                    else if (CaretOffset < _document.TextLength)
                    {
                        _document.Remove (CaretOffset, 1);
                    }
                }
                else
                {
                    if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                    {
                        var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                        var selEnd = Math.Max (selAnchor.Offset, caret.Offset);

                        if (selEnd > selStart)
                        {
                            _document.Replace (selStart, selEnd - selStart, string.Empty);

                            continue;
                        }
                    }

                    if (caret.Offset < _document.TextLength)
                    {
                        _document.Remove (caret.Offset, 1);
                    }
                }
            }
        }

        ClearAdditionalCaretSelections ();

        return true;
    }

    /// <summary>
    ///     Multi-caret newline insert. Each caret gets a newline followed by auto-indent
    ///     (when <see cref="IndentationStrategy" /> is set), matching single-caret Enter behavior.
    /// </summary>
    private bool? MultiCaretNewLine ()
    {
        if (ReadOnly || _document is null)
        {
            return true;
        }

        if (!HasMultipleCarets)
        {
            return InsertNewLineWithAutoIndent ();
        }

        using (_document.RunUpdate ())
        {
            List<CaretEditInfo> carets = GetAllCaretsDescending ();

            foreach (CaretEditInfo caret in carets)
            {
                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        ReplaceSelection ("\n");
                    }
                    else
                    {
                        _document.Insert (CaretOffset, "\n");
                    }

                    if (IndentationStrategy is { } strategy)
                    {
                        DocumentLine newLine = _document.GetLineByOffset (CaretOffset);
                        strategy.IndentLine (_document, newLine);
                    }
                }
                else
                {
                    if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                    {
                        var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                        var selEnd = Math.Max (selAnchor.Offset, caret.Offset);

                        if (selEnd > selStart)
                        {
                            _document.Replace (selStart, selEnd - selStart, "\n");

                            // Apply indentation to the new line after selection replacement.
                            if (IndentationStrategy is { } selStrategy)
                            {
                                DocumentLine newLine = _document.GetLineByOffset (selStart + 1);
                                selStrategy.IndentLine (_document, newLine);
                            }

                            continue;
                        }
                    }

                    _document.Insert (caret.Offset, "\n");

                    if (IndentationStrategy is { } caretStrategy)
                    {
                        DocumentLine newLine = _document.GetLineByOffset (caret.Offset + 1);
                        caretStrategy.IndentLine (_document, newLine);
                    }
                }
            }
        }

        ClearAdditionalCaretSelections ();

        return true;
    }

    /// <summary>
    ///     Inserts a tab (or its space expansion) at every caret in one undo scope. Each caret's
    ///     insertion text is computed from <em>that caret's</em> own visual column via
    ///     <see cref="GetTabInsertionText" />, so every caret advances to the same next tab stop
    ///     and the column stays aligned across repeated presses. Carets are processed in
    ///     descending offset order so an earlier (higher) edit doesn't shift a not-yet-processed
    ///     offset. Caller (<see cref="InsertTab" />) guarantees a non-null, writable document and
    ///     <see cref="HasMultipleCarets" />.
    /// </summary>
    private bool MultiCaretInsertTab ()
    {
        using (_document!.RunUpdate ())
        {
            foreach (CaretEditInfo caret in GetAllCaretsDescending ())
            {
                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        ReplaceSelection (GetTabInsertionText (SelectionStart));
                    }
                    else
                    {
                        _document.Insert (CaretOffset, GetTabInsertionText (CaretOffset));
                    }

                    continue;
                }

                if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                {
                    var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                    var selEnd = Math.Max (selAnchor.Offset, caret.Offset);

                    if (selEnd > selStart)
                    {
                        _document.Replace (selStart, selEnd - selStart, GetTabInsertionText (selStart));

                        continue;
                    }
                }

                _document.Insert (caret.Offset, GetTabInsertionText (caret.Offset));
            }
        }

        ClearAdditionalCaretSelections ();

        return true;
    }

    /// <summary>
    ///     Removes one indentation unit from every distinct line that hosts a caret, in a single
    ///     undo scope. Lines are deduped (two carets on one line unindent it once) and removals
    ///     run high-offset-first so earlier removals don't shift later ones. Carets are anchors,
    ///     so they follow the removals automatically. Caller (<see cref="Unindent" />) guarantees
    ///     a non-null, writable document and <see cref="HasMultipleCarets" />.
    /// </summary>
    private bool MultiCaretUnindent ()
    {
        HashSet<int> seenLineOffsets = [];
        List<(int offset, int length)> removals = [];

        foreach (CaretEditInfo caret in GetAllCaretsDescending ())
        {
            DocumentLine line = _document!.GetLineByOffset (caret.Offset);

            if (!seenLineOffsets.Add (line.Offset))
            {
                continue;
            }

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

        using (_document!.RunUpdate ())
        {
            foreach ((int offset, int length) removal in removals.OrderByDescending (static r => r.offset))
            {
                _document.Remove (removal.offset, removal.length);
            }
        }

        ClearAdditionalCaretSelections ();

        return true;
    }

    private void ClearAdditionalCaretSelections ()
    {
        foreach (CaretInfo caret in _additionalCarets)
        {
            caret.SelectionAnchor = null;
        }
    }

    /// <summary>Holds anchor state for one additional caret.</summary>
    private sealed class CaretInfo
    {
        public TextAnchor? CaretAnchor { get; set; }
        public TextAnchor? SelectionAnchor { get; set; }
    }

    /// <summary>Transient struct used during multi-caret edit iteration.</summary>
    private readonly struct CaretEditInfo
    {
        public required int Offset { get; init; }
        public required bool IsPrimary { get; init; }
        public TextAnchor? SelectionAnchor { get; init; }
    }
}
