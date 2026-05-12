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
            () => ExtendCommand (() => ExtendCaretTo (_document!.GetLineByOffset (CaretOffset).Offset)));

        AddCommand (Command.RightEndExtend, () => ExtendCommand (() =>
        {
            DocumentLine line = _document!.GetLineByOffset (CaretOffset);
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
        AddCommand (Command.NewLine, () => InsertOrReplace ("\n"));
        AddCommand (Command.DeleteCharLeft, DeleteLeft);
        AddCommand (Command.DeleteCharRight, DeleteRight);

        // History
        AddCommand (Command.Undo, () =>
        {
            if (ReadOnly || !_document!.UndoStack.CanUndo)
            {
                return true;
            }

            ClearSelection ();
            _document!.UndoStack.Undo ();

            return true;
        });

        AddCommand (Command.Redo, () =>
        {
            if (ReadOnly || !_document!.UndoStack.CanRedo)
            {
                return true;
            }

            ClearSelection ();
            _document!.UndoStack.Redo ();

            return true;
        });

        ApplyKeyBindings (View.DefaultKeyBindings, DefaultKeyBindings);

        // Reclaim Tab before the framework consumes it; the editor handles Tab / Shift+Tab
        // in OnKeyDownNotHandled so indentation still works without a command binding.
        KeyBindings.Remove (Key.Tab);
        KeyBindings.Remove (Key.Tab.WithShift);

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
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (text);
        }
        else
        {
            _document!.Insert (CaretOffset, text);
        }

        return true;
    }

    private bool? DeleteLeft ()
    {
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (TryDeleteIndentationLeft ())
        {
            return true;
        }
        else if (CaretOffset > 0)
        {
            _document!.Remove (CaretOffset - 1, 1);
        }

        return true;
    }

    private bool? DeleteRight ()
    {
        if (ReadOnly)
        {
            return true;
        }

        if (HasSelection)
        {
            ReplaceSelection (string.Empty);
        }
        else if (CaretOffset < _document!.TextLength)
        {
            _document!.Remove (CaretOffset, 1);
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
        DocumentLine line = _document!.GetLineByOffset (CaretOffset);
        CaretOffset = line.Offset;

        return true;
    }

    private bool? MoveCaretToLineEnd ()
    {
        DocumentLine line = _document!.GetLineByOffset (CaretOffset);
        CaretOffset = line.Offset + line.Length;

        return true;
    }
}
