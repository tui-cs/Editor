using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ted;

/// <summary>
///     Top-level <see cref="Window" /> for the <c>ted</c> demo. MenuBar at top,
///     <see cref="Editor" /> in the middle, StatusBar at the bottom. Single-file —
///     no tabs (compare to Terminal.Gui's Notepad scenario).
/// </summary>
public sealed class TedApp : Window
{
    /// <summary>Initializes a new <see cref="TedApp" />.</summary>
    public TedApp ()
    {
        Title = "ted — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        MenuBar menu = new();

        StatusBar statusBar =
            new([
                new Shortcut (Application.GetDefaultKey (Command.Quit), "Quit", Quit),
                // TODO: Add a themes dropdown shortcut
                new Shortcut (Key.Empty, "x, y", null, "Loc") { MouseHighlightStates = MouseState.None },
                new Shortcut (Key.Empty, "<filename>", Open) { MouseHighlightStates = MouseState.None }
            ])
            {
                AlignmentModes = AlignmentModes.IgnoreFirstOrLast
            };

        menu.Add (new MenuBarItem (Strings.menuFile,
            [
                new MenuItem { Command = Command.New, Action = New, Key = Application.GetDefaultKey (Command.New) },
                new MenuItem { Command = Command.Open, Action = Open, Key = Application.GetDefaultKey (Command.Open) },
                new MenuItem { Command = Command.Save, Action = Save, Key = Application.GetDefaultKey (Command.Save) },
                new MenuItem
                    { Command = Command.SaveAs, Action = SaveAs, Key = Application.GetDefaultKey (Command.SaveAs) },
                new MenuItem { Command = Command.Quit, Action = Quit, Key = Application.GetDefaultKey (Command.Quit) }
            ]),
            new MenuBarItem (Strings.menuEdit,
            [
                new MenuItem { Command = Command.Undo, Action = Undo, Key = Application.GetDefaultKey (Command.Undo) },
                new MenuItem { Command = Command.Redo, Action = Redo, Key = Application.GetDefaultKey (Command.Redo) },
                new Line (),
                new MenuItem { Command = Command.Cut, Action = Cut, Key = Application.GetDefaultKey (Command.Cut) },
                new MenuItem { Command = Command.Copy, Action = Copy, Key = Application.GetDefaultKey (Command.Copy) },
                new MenuItem
                    { Command = Command.Paste, Action = Paste, Key = Application.GetDefaultKey (Command.Paste) },
                new MenuItem
                {
                    Command = Command.SelectAll, Action = SelectAll, Key = Application.GetDefaultKey (Command.SelectAll)
                }
            ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "Show About dialog", Action)])
        );

        Editor = new()
        {
            Y = Pos.Bottom (menu),
            Width = Dim.Fill (),
            Height = Dim.Fill (statusBar)
        };

        Add (menu, Editor, statusBar);
    }


    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    private void Action ()
    {
    }

    private void SelectAll ()
    {
    }

    private void Paste ()
    {
    }

    private void Copy ()
    {
    }

    private void Cut ()
    {
    }

    private void Redo ()
    {
        Editor.Document?.UndoStack.Redo ();
    }

    private void Undo ()
    {
        Editor.Document?.UndoStack.Undo ();
    }

    private void New ()
    {
        // TODO: if unsaved changes, confirm with user before clearing
        Editor.Text = string.Empty;
        Editor.SetNeedsDraw ();
    }

    private void Open ()
    {
        // TODO: if unsaved changes, confirm with user before clearing

        /* placeholder — file picker comes when Editor can hold a document */
    }

    private void Save ()
    {
        /* placeholder — save comes when Editor can hold a document */
    }

    private void SaveAs ()
    {
        /* placeholder — save comes when Editor can hold a document */
    }

    private void Quit ()
    {
        // TODO: add logic for unsaved changes, confirm quit, etc.
        RequestStop ();
    }
}
