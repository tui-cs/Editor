using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Editor.Rendering;

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
    ///     Adds an additional caret at the given <paramref name="offset" />. If a caret already exists
    ///     within tolerance (same offset), it is removed instead (toggle behavior for Ctrl+Click).
    /// </summary>
    public void ToggleCaretAt (int offset)
    {
        if (_document is null)
        {
            return;
        }

        offset = Math.Clamp (offset, 0, _document.TextLength);

        // If clicking on the primary caret, ignore — we never remove the primary.
        if (offset == CaretOffset)
        {
            return;
        }

        // Check if there's already an additional caret at this offset — remove it if so.
        for (var i = _additionalCarets.Count - 1; i >= 0; i--)
        {
            if (_additionalCarets[i].CaretAnchor is { IsDeleted: false } anchor && anchor.Offset == offset)
            {
                _additionalCarets.RemoveAt (i);
                SetNeedsDraw ();

                return;
            }
        }

        AddAdditionalCaretAt (offset);
    }

    /// <summary>Removes all additional carets, leaving only the primary.</summary>
    public void ClearAdditionalCarets ()
    {
        if (_additionalCarets.Count == 0)
        {
            return;
        }

        _additionalCarets.Clear ();

        if (_document is not null)
        {
            _virtualCaretColumn = GetCaretColumn ();
        }

        SetNeedsDraw ();
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

    private bool? MultiCaretInsertTab ()
    {
        if (ReadOnly || _document is null)
        {
            return true;
        }

        if (!HasMultipleCarets)
        {
            return InsertTab ();
        }

        using (_document.RunUpdate ())
        {
            List<CaretEditInfo> carets = GetAllCaretsDescending ();

            foreach (CaretEditInfo caret in carets)
            {
                var insertOffset = caret.Offset;
                var replacementLength = 0;

                if (caret.IsPrimary)
                {
                    if (HasSelection)
                    {
                        var selStart = SelectionStart;
                        var selEnd = SelectionEnd;
                        insertOffset = selStart;
                        replacementLength = selEnd - selStart;
                    }
                }
                else if (caret.SelectionAnchor is { IsDeleted: false } selAnchor)
                {
                    var selStart = Math.Min (selAnchor.Offset, caret.Offset);
                    var selEnd = Math.Max (selAnchor.Offset, caret.Offset);
                    insertOffset = selStart;
                    replacementLength = selEnd - selStart;
                }

                var text = GetTabInsertionText (insertOffset);

                if (caret.IsPrimary)
                {
                    if (replacementLength > 0)
                    {
                        ReplaceSelection (text);
                    }
                    else
                    {
                        _document.Insert (insertOffset, text);
                    }
                }
                else if (replacementLength > 0)
                {
                    _document.Replace (insertOffset, replacementLength, text);
                }
                else
                {
                    _document.Insert (caret.Offset, text);
                }
            }
        }

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

    private void ClearAdditionalCaretSelections ()
    {
        foreach (CaretInfo caret in _additionalCarets)
        {
            caret.SelectionAnchor = null;
        }
    }

    /// <summary>
    ///     Adds one vertically aligned caret above (<paramref name="delta" /> = -1) or below
    ///     (<paramref name="delta" /> = 1) the existing caret block, preserving the sticky column.
    /// </summary>
    private void AddCaretVertically (int delta)
    {
        if (_document is null || delta == 0)
        {
            return;
        }

        var referenceOffset = CaretOffset;

        foreach (CaretInfo caret in _additionalCarets)
        {
            if (caret.CaretAnchor is not { IsDeleted: false } anchor)
            {
                continue;
            }

            if (delta < 0 && anchor.Offset < referenceOffset)
            {
                referenceOffset = anchor.Offset;
            }
            else if (delta > 0 && anchor.Offset > referenceOffset)
            {
                referenceOffset = anchor.Offset;
            }
        }

        var targetVisualColumn = _additionalCarets.Count == 0 ? GetVisualColumnForOffset (CaretOffset) : _virtualCaretColumn;

        if (!TryGetVerticalOffset (referenceOffset, delta, targetVisualColumn, out var targetOffset))
        {
            return;
        }

        AddAdditionalCaretAt (targetOffset);
    }

    private void SetVerticalCaretsFromViewRows (int anchorViewRow, int activeViewRow, int viewColumn)
    {
        if (_document is null)
        {
            return;
        }

        var start = Math.Min (anchorViewRow, activeViewRow);
        var end = Math.Max (anchorViewRow, activeViewRow);
        var anchorOffset = MousePositionToOffset (new Point (viewColumn, anchorViewRow));
        CaretOffset = anchorOffset;
        ClearSelection ();
        ClearAdditionalCarets ();

        for (var row = start; row <= end; row++)
        {
            if (row == anchorViewRow)
            {
                continue;
            }

            var offset = MousePositionToOffset (new Point (viewColumn, row));
            AddAdditionalCaretAt (offset);
        }
    }

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

        for (var i = _additionalCarets.Count - 1; i >= 0; i--)
        {
            if (_additionalCarets[i].CaretAnchor is { IsDeleted: false } anchor && anchor.Offset == offset)
            {
                return;
            }
        }

        TextAnchor caretAnchor = CreateCaretAnchor (offset);
        _additionalCarets.Add (new CaretInfo { CaretAnchor = caretAnchor });
        SetNeedsDraw ();
    }

    private void NormalizeAdditionalCarets ()
    {
        if (_additionalCarets.Count == 0)
        {
            return;
        }

        HashSet<int> seenOffsets = [CaretOffset];
        var changed = false;

        for (var i = _additionalCarets.Count - 1; i >= 0; i--)
        {
            if (_additionalCarets[i].CaretAnchor is not { IsDeleted: false } anchor || !seenOffsets.Add (anchor.Offset))
            {
                _additionalCarets.RemoveAt (i);
                changed = true;
            }
        }

        if (changed)
        {
            SetNeedsDraw ();
        }
    }

    private bool TryGetVerticalOffset (int startOffset, int delta, int targetVisualColumn, out int targetOffset)
    {
        targetOffset = startOffset;

        if (_document is null || delta == 0)
        {
            return false;
        }

        if (WordWrap)
        {
            List<WrapMapEntry> map = GetWrapMap ();
            var currentRow = GetWrapRowForOffset (startOffset, map);
            var nextRow = Math.Clamp (currentRow + delta, 0, map.Count - 1);

            if (nextRow == currentRow)
            {
                return false;
            }

            WrapMapEntry entry = map[nextRow];
            DocumentLine line = _document.GetLineByNumber (entry.LineNumber);
            var text = _document.GetText (line);
            IReadOnlyList<WrapSegment> segments =
                WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);
            WrapSegment seg = segments[entry.SegmentIndex];
            var segText = text.Substring (seg.StartOffset, seg.Length);
            var localOffset = ComputeRelativeOffsetDirect (segText, targetVisualColumn);
            targetOffset = line.Offset + seg.StartOffset + localOffset;

            return true;
        }

        DocumentLine currentLine = _document.GetLineByOffset (startOffset);
        var targetLineNumber = Math.Clamp (currentLine.LineNumber + delta, 1, _document.LineCount);

        if (targetLineNumber == currentLine.LineNumber)
        {
            return false;
        }

        DocumentLine targetLine = _document.GetLineByNumber (targetLineNumber);
        var targetColumnOffset = GetOrBuildDefaultVisualLine (targetLine).GetRelativeOffset (targetVisualColumn);
        targetOffset = targetLine.Offset + targetColumnOffset;

        return true;
    }

    private int GetVisualColumnForOffset (int offset)
    {
        if (_document is null)
        {
            return 0;
        }

        var clamped = Math.Clamp (offset, 0, _document.TextLength);
        DocumentLine line = _document.GetLineByOffset (clamped);
        var relativeOffset = clamped - line.Offset;

        return GetOrBuildDefaultVisualLine (line).GetVisualColumn (relativeOffset);
    }

    private int GetWrapRowForOffset (int offset, List<WrapMapEntry> map)
    {
        if (_document is null || map.Count == 0)
        {
            return 0;
        }

        DocumentLine line = _document.GetLineByOffset (Math.Clamp (offset, 0, _document.TextLength));
        var offsetInLine = offset - line.Offset;
        var text = _document.GetText (line);
        IReadOnlyList<WrapSegment> segments =
            WordWrapStrategy.ComputeSegments (text, GetWrapColumn (), IndentationSize);
        var segmentIndex = 0;

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            if (offsetInLine >= segments[i].StartOffset)
            {
                segmentIndex = i;

                break;
            }
        }

        for (var row = 0; row < map.Count; row++)
        {
            if (map[row].LineNumber == line.LineNumber && map[row].SegmentIndex == segmentIndex)
            {
                return row;
            }
        }

        return 0;
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
