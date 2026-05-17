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
            new MenuItem (Editor, Command.Undo) { Key = KeyFor (Command.Undo) },
            new MenuItem (Editor, Command.Redo) { Key = KeyFor (Command.Redo) },
            new Line (),
            new MenuItem (Editor, Command.Cut) { Key = KeyFor (Command.Cut) },
            new MenuItem (Editor, Command.Copy) { Key = KeyFor (Command.Copy) },
            new MenuItem (Editor, Command.Paste) { Key = KeyFor (Command.Paste) },
            new MenuItem (Editor, Command.SelectAll) { Key = KeyFor (Command.SelectAll) }
        ];
    }

    private void Find () { ShowFindReplaceDialog (false); }

    private void Replace () { ShowFindReplaceDialog (true); }

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
