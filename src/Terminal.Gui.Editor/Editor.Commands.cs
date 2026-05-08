using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

public partial class Editor
{
    /// <summary>
    ///     Editor-specific default key bindings layered on top of <see cref="View.DefaultKeyBindings"/>.
    ///     The base layer already maps cursor / Home / End / PageUp / PageDown to the corresponding
    ///     movement <see cref="Command"/>s; this dictionary covers what's specific to a text editor
    ///     (Ctrl+Home/End for whole-document navigation, Enter for newline, Backspace/Delete, undo/redo).
    /// </summary>
    /// <remarks>
    ///     Process-wide static. Do not mutate from parallel tests — see Terminal.Gui's same convention
    ///     on <see cref="Terminal.Gui.Views.TextField.DefaultKeyBindings"/>.
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


        // Movement
        AddCommand (Command.Left, () => MoveCaretBy (-1));
        AddCommand (Command.Right, () => MoveCaretBy (1));
        AddCommand (Command.Up, () => MoveCaretVerticallyCommand (-1));
        AddCommand (Command.Down, () => MoveCaretVerticallyCommand (1));
        AddCommand (Command.LeftStart, MoveCaretToLineStart);
        AddCommand (Command.RightEnd, MoveCaretToLineEnd);
        AddCommand (Command.Start, () => SetCaretAndReturnTrue (0));
        AddCommand (Command.End, () => SetCaretAndReturnTrue (_document!.TextLength));
        AddCommand (Command.PageUp, () => MoveCaretVerticallyCommand (-Math.Max (1, Viewport.Height)));
        AddCommand (Command.PageDown, () => MoveCaretVerticallyCommand (Math.Max (1, Viewport.Height)));

        // Editing
        AddCommand (Command.NewLine, () =>
        {
            _document!.Insert (_caretOffset, "\n");

            return true;
        });

        AddCommand (Command.DeleteCharLeft, () =>
        {
            if (_caretOffset > 0)
            {
                _document!.Remove (_caretOffset - 1, 1);
            }

            return true;
        });

        AddCommand (Command.DeleteCharRight, () =>
        {
            if (_caretOffset < _document!.TextLength)
            {
                _document!.Remove (_caretOffset, 1);
            }

            return true;
        });

        // History
        AddCommand (Command.Undo, () =>
        {
            if (_document!.UndoStack.CanUndo)
            {
                _document!.UndoStack.Undo ();
            }

            return true;
        });

        AddCommand (Command.Redo, () =>
        {
            if (_document!.UndoStack.CanRedo)
            {
                _document!.UndoStack.Redo ();
            }

            return true;
        });

        ApplyKeyBindings (View.DefaultKeyBindings, DefaultKeyBindings);
    }

    private bool? SetCaretAndReturnTrue (int offset)
    {
        CaretOffset = offset;

        return true;
    }

    private bool? MoveCaretBy (int delta)
    {
        CaretOffset = _caretOffset + delta;

        return true;
    }

    private bool? MoveCaretVerticallyCommand (int delta)
    {
        MoveCaretVertically (delta);

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
}
