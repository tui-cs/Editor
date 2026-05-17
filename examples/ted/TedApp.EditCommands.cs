using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

public sealed partial class TedApp
{
    internal View[] CreateEditMenuItems ()
    {
        return
        [
            new MenuItem ("_Find...", "Find text in the current document", Find),
            new MenuItem ("_Replace...", "Find and replace text in the current document", Replace),
            new Line (),
            new MenuItem { Command = Command.Undo, Action = Undo, Key = KeyFor (Command.Undo) },
            new MenuItem { Command = Command.Redo, Action = Redo, Key = KeyFor (Command.Redo) },
            new Line (),
            new MenuItem { Command = Command.Cut, Key = KeyFor (Command.Cut) },
            new MenuItem { Command = Command.Copy, Key = KeyFor (Command.Copy) },
            new MenuItem { Command = Command.Paste, Key = KeyFor (Command.Paste) },
            new MenuItem { Command = Command.SelectAll, Action = SelectAll, Key = KeyFor (Command.SelectAll) }
        ];
    }

    private void Find () { ShowFindReplaceDialog (false); }

    private void Replace () { ShowFindReplaceDialog (true); }

    private void SelectAll () { Editor.SelectAll (); }

    private void Undo ()
    {
        if (!Editor.ReadOnly)
        {
            Editor.Document?.UndoStack.Undo ();
        }
    }

    private void Redo ()
    {
        if (!Editor.ReadOnly)
        {
            Editor.Document?.UndoStack.Redo ();
        }
    }

    private void ShowFindReplaceDialog (bool selectReplaceTab)
    {
        if (App is null)
        {
            throw new InvalidOperationException ("Cannot show find/replace when Application is not running.");
        }

        using FindReplaceDialog dialog = new (Editor, selectReplaceTab);
        App.Run (dialog);
    }
}
