using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Editor-specific default key bindings layered on top of <see cref="View.DefaultKeyBindings" />.
    ///     The base layer already maps cursor / Home / End / PageUp / PageDown (and their Shift variants)
    ///     to the corresponding movement and *Extend <see cref="Command" />s, plus Ctrl+A → SelectAll;
    ///     this dictionary covers what's editor-specific (Enter, Backspace/Delete, Ctrl+Z / Ctrl+Y, the
    ///     Ctrl+Home/End whole-document binds).
    /// </summary>
    /// <remarks>
    ///     Process-wide static. Do not mutate from parallel tests — see Terminal.Gui's same convention
    ///     on <see cref="Terminal.Gui.Views.TextField.DefaultKeyBindings" />.
    /// </remarks>
    public new static Dictionary<Command, PlatformKeyBinding>? DefaultKeyBindings { get; set; } = new ()
    {
        [Command.Start] = Bind.All (Key.Home.WithCtrl),
        [Command.End] = Bind.All (Key.End.WithCtrl),
        [Command.NewLine] = Bind.All (Key.Enter),
        [Command.DeleteCharLeft] = Bind.All (Key.Backspace),
        [Command.DeleteCharRight] = Bind.All (Key.Delete),
        [Command.Undo] = Bind.All (Key.Z.WithCtrl),
        [Command.Redo] = Bind.All (Key.Y.WithCtrl, Key.Z.WithCtrl.WithShift)
    };

    private void CreateCommandsAndBindings ()
    {
        // View's SetupKeyboard pre-binds Enter→Accept and Space→Activate. In a text editor those
        // are the literal characters, so reclaim them before applying layered bindings.
        KeyBindings.Remove (Key.Enter);
        KeyBindings.Remove (Key.Space);

        // Plain movement (collapses any existing selection)
        AddCommand (Command.Left, () => MoveCaretByCollapsing (-1));
        AddCommand (Command.Right, () => MoveCaretByCollapsing (1));
        AddCommand (Command.Up, () => MoveCaretVerticallyCollapsing (-1));
        AddCommand (Command.Down, () => MoveCaretVerticallyCollapsing (1));
        AddCommand (Command.LeftStart, MoveCaretToLineStart);
        AddCommand (Command.RightEnd, MoveCaretToLineEnd);
        AddCommand (Command.Start, () => SetCaretAndReturnTrue (0));
        AddCommand (Command.End, () => SetCaretAndReturnTrue (_document!.TextLength));
        AddCommand (Command.PageUp, () => MoveCaretVerticallyCollapsing (-Math.Max (1, Viewport.Height)));
        AddCommand (Command.PageDown, () => MoveCaretVerticallyCollapsing (Math.Max (1, Viewport.Height)));
        AddCommand (Command.ScrollUp, () => ScrollVerticalCommand (-1));
        AddCommand (Command.ScrollDown, () => ScrollVerticalCommand (1));
        AddCommand (Command.ScrollLeft, () => ScrollHorizontalCommand (-1));
        AddCommand (Command.ScrollRight, () => ScrollHorizontalCommand (1));

        // Selection-extending movement
        AddCommand (Command.LeftExtend, () => ExtendCommand (() => ExtendCaretBy (-1)));
        AddCommand (Command.RightExtend, () => ExtendCommand (() => ExtendCaretBy (1)));
        AddCommand (Command.UpExtend, () => ExtendCommand (() => ExtendCaretVertically (-1)));
        AddCommand (Command.DownExtend, () => ExtendCommand (() => ExtendCaretVertically (1)));
        AddCommand (Command.LeftStartExtend,
            () => ExtendCommand (() => ExtendCaretTo (_document!.GetLineByOffset (_caretOffset).Offset)));

        AddCommand (Command.RightEndExtend, () => ExtendCommand (() =>
        {
            DocumentLine line = _document!.GetLineByOffset (_caretOffset);
            ExtendCaretTo (line.Offset + line.Length);
        }));

        AddCommand (Command.StartExtend, () => ExtendCommand (() => ExtendCaretTo (0)));
        AddCommand (Command.EndExtend, () => ExtendCommand (() => ExtendCaretTo (_document!.TextLength)));
        AddCommand (Command.PageUpExtend,
            () => ExtendCommand (() => ExtendCaretVertically (-Math.Max (1, Viewport.Height))));
        AddCommand (Command.PageDownExtend,
            () => ExtendCommand (() => ExtendCaretVertically (Math.Max (1, Viewport.Height))));

        // Selection ops
        AddCommand (Command.SelectAll, () =>
        {
            SelectAll ();

            return true;
        });

        // Editing — selection-aware
        AddCommand (Command.NewLine, InsertNewLine);
        AddCommand (Command.DeleteCharLeft, DeleteLeft);
        AddCommand (Command.DeleteCharRight, DeleteRight);

        // History
        AddCommand (Command.Undo, () =>
        {
            if (_document!.UndoStack.CanUndo)
            {
                ClearSelection ();
                _document!.UndoStack.Undo ();
            }

            return true;
        });

        AddCommand (Command.Redo, () =>
        {
            if (_document!.UndoStack.CanRedo)
            {
                ClearSelection ();
                _document!.UndoStack.Redo ();
            }

            return true;
        });

        ApplyKeyBindings (View.DefaultKeyBindings, DefaultKeyBindings);

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
        MouseBindings.Add (MouseFlags.WheeledLeft, Command.ScrollLeft);
        MouseBindings.Add (MouseFlags.WheeledRight, Command.ScrollRight);
    }

    private bool? ExtendCommand (Action extend)
    {
        extend ();

        return true;
    }

    private bool? MoveCaretByCollapsing (int delta)
    {
        MoveCaretByCollapsingSelection (delta);

        return true;
    }

    private bool? MoveCaretVerticallyCollapsing (int delta)
    {
        MoveCaretVerticallyCollapsingSelection (delta);

        return true;
    }

    private bool? ScrollVerticalCommand (int delta)
    {
        if (_document is null || ScrollVertical (delta) != true)
        {
            return false;
        }

        SetNeedsDraw ();

        return true;
    }

    private bool? ScrollHorizontalCommand (int delta)
    {
        if (_document is null || ScrollHorizontal (delta) != true)
        {
            return false;
        }

        SetNeedsDraw ();

        return true;
    }

    private bool? InsertOrReplace (string text)
    {
        if (HasSelection)
        {
            ReplaceSelection (text);
        }
        else
        {
            _document!.Insert (_caretOffset, text);
        }

        return true;
    }

    private bool? InsertNewLine ()
    {
        DocumentLine line = _document!.GetLineByOffset (_caretOffset);
        string indentation = IndentationStrategy.GetIndentationForNewLine (_document, line);

        return InsertOrReplace ("\n" + indentation);
    }

    private bool? InsertTab ()
    {
        if (HasSelection && SelectionSpansMultipleLines ())
        {
            IndentSelectedLines ();

            return true;
        }

        return InsertOrReplace (GetTabInsertionText ());
    }

    private bool? Unindent ()
    {
        if (HasSelection && SelectionSpansMultipleLines ())
        {
            UnindentSelectedLines ();

            return true;
        }

        DocumentLine line = _document!.GetLineByOffset (_caretOffset);
        ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, line.Offset, IndentationSize);

        if (segment.Length > 0)
        {
            _document.Remove (segment.Offset, segment.Length);
        }

        return true;
    }

    private bool? DeleteLeft ()
    {
        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (TryDeleteIndentationBeforeCaret ())
        {
            return true;
        }
        else if (_caretOffset > 0)
        {
            _document!.Remove (_caretOffset - 1, 1);
        }

        return true;
    }

    private bool? DeleteRight ()
    {
        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (_caretOffset < _document!.TextLength)
        {
            _document!.Remove (_caretOffset, 1);
        }

        return true;
    }

    private bool? SetCaretAndReturnTrue (int offset)
    {
        CaretOffset = offset;

        return true;
    }

    private bool? MoveCaretToLineStart ()
    {
        DocumentLine line = _document!.GetLineByOffset (_caretOffset);
        CaretOffset = line.Offset;

        return true;
    }

    private bool? MoveCaretToLineEnd ()
    {
        DocumentLine line = _document!.GetLineByOffset (_caretOffset);
        CaretOffset = line.Offset + line.Length;

        return true;
    }

    private string GetTabInsertionText ()
    {
        if (!ConvertTabsToSpaces)
        {
            return "\t";
        }

        int visualColumn = GetCaretColumn ();
        int remainder = visualColumn % IndentationSize;
        int spaces = remainder == 0 ? IndentationSize : IndentationSize - remainder;

        return new (' ', spaces);
    }

    private bool SelectionSpansMultipleLines ()
    {
        return _document!.GetLineByOffset (SelectionStart).LineNumber
               != _document.GetLineByOffset (Math.Max (SelectionStart, SelectionEnd - 1)).LineNumber;
    }

    private void IndentSelectedLines ()
    {
        string indentation = ConvertTabsToSpaces ? new (' ', IndentationSize) : "\t";
        int start = SelectionStart;
        int end = SelectionEnd;
        bool forward = _selectionAnchor <= _caretOffset;
        int firstLine = _document!.GetLineByOffset (start).LineNumber;
        int lastLine = _document.GetLineByOffset (Math.Max (start, end - 1)).LineNumber;

        using (_document.RunUpdate ())
        {
            for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
            {
                _document.Insert (_document.GetLineByNumber (lineNumber).Offset, indentation);
            }
        }

        int newStart = start + indentation.Length;
        int newEnd = end + ((lastLine - firstLine + 1) * indentation.Length);
        RestoreSelection (forward, newStart, newEnd);
    }

    private void UnindentSelectedLines ()
    {
        int start = SelectionStart;
        int end = SelectionEnd;
        bool forward = _selectionAnchor <= _caretOffset;
        int firstLine = _document!.GetLineByOffset (start).LineNumber;
        int lastLine = _document.GetLineByOffset (Math.Max (start, end - 1)).LineNumber;
        int newStart = start;
        int newEnd = end;

        using (_document.RunUpdate ())
        {
            for (int lineNumber = firstLine; lineNumber <= lastLine; lineNumber++)
            {
                DocumentLine line = _document.GetLineByNumber (lineNumber);
                ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, line.Offset, IndentationSize);

                if (segment.Length == 0)
                {
                    continue;
                }

                if (segment.Offset < newStart)
                {
                    newStart -= segment.Length;
                }

                if (segment.Offset < newEnd)
                {
                    newEnd -= segment.Length;
                }

                _document.Remove (segment.Offset, segment.Length);
            }
        }

        RestoreSelection (forward, Math.Max (0, newStart), Math.Max (0, newEnd));
    }

    private void RestoreSelection (bool forward, int start, int end)
    {
        (int start, int end) before = SelectionTuple ();

        if (forward)
        {
            _selectionAnchor = start;
            SetCaretOffset (end, true);
        }
        else
        {
            _selectionAnchor = end;
            SetCaretOffset (start, true);
        }

        RaiseSelectionChangedIfMoved (before);
        SetNeedsDraw ();
    }

    private bool TryDeleteIndentationBeforeCaret ()
    {
        if (_caretOffset == 0)
        {
            return false;
        }

        DocumentLine line = _document!.GetLineByOffset (_caretOffset);
        ISegment leading = TextUtilities.GetLeadingWhitespace (_document, line);

        if (_caretOffset != leading.EndOffset || leading.Length == 0)
        {
            return false;
        }

        int scan = line.Offset;
        ISegment? previous = null;

        while (scan < _caretOffset)
        {
            ISegment segment = TextUtilities.GetSingleIndentationSegment (_document, scan, IndentationSize);

            if (segment.Length == 0 || segment.EndOffset > _caretOffset)
            {
                break;
            }

            previous = segment;
            scan = segment.EndOffset;
        }

        if (previous is null || previous.EndOffset != _caretOffset)
        {
            return false;
        }

        _document.Remove (previous.Offset, previous.Length);

        return true;
    }
}
