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

        // Editor first so menu/status-bar shortcuts can pull their hotkeys directly from
        // Editor's KeyBindings (any commands the editor doesn't claim fall back to Application).
        Editor = new ();

        MenuBar menu = new ();

        StatusBar statusBar =
            new ([
                new Shortcut (KeyFor (Command.Quit), "Quit", Quit),

                // TODO: Add a themes dropdown shortcut
                new Shortcut (Key.Empty, "x, y", null, "Loc") { MouseHighlightStates = MouseState.None },
                new Shortcut (Key.Empty, "<filename>", Open) { MouseHighlightStates = MouseState.None }
            ])
            {
                AlignmentModes = AlignmentModes.IgnoreFirstOrLast
            };

        menu.Add (new MenuBarItem (Strings.menuFile,
                [
                    new MenuItem { Command = Command.New, Action = New, Key = KeyFor (Command.New) },
                    new MenuItem { Command = Command.Open, Action = Open, Key = KeyFor (Command.Open) },
                    new MenuItem { Command = Command.Save, Action = Save, Key = KeyFor (Command.Save) },
                    new MenuItem { Command = Command.SaveAs, Action = SaveAs, Key = KeyFor (Command.SaveAs) },
                    new MenuItem { Command = Command.Quit, Action = Quit, Key = KeyFor (Command.Quit) }
                ]),
            new MenuBarItem (Strings.menuEdit,
                [
                    new MenuItem { Command = Command.Undo, Action = Undo, Key = KeyFor (Command.Undo) },
                    new MenuItem { Command = Command.Redo, Action = Redo, Key = KeyFor (Command.Redo) },
                    new Line (),
                    new MenuItem { Command = Command.Cut, Action = Cut, Key = KeyFor (Command.Cut) },
                    new MenuItem { Command = Command.Copy, Action = Copy, Key = KeyFor (Command.Copy) },
                    new MenuItem { Command = Command.Paste, Action = Paste, Key = KeyFor (Command.Paste) },
                    new MenuItem { Command = Command.SelectAll, Action = SelectAll, Key = KeyFor (Command.SelectAll) }
                ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "Show About dialog", Action)])
        );

        Editor.Y = Pos.Bottom (menu);
        Editor.Width = Dim.Fill ();
        Editor.Height = Dim.Fill (statusBar);

        Add (menu, Editor, statusBar);
    }


    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    /// <summary>
    ///     Resolves the key shortcut for <paramref name="command" /> by asking the <see cref="Editor" />'s
    ///     <see cref="View.KeyBindings" /> first; falls back to <see cref="Application.GetDefaultKey" /> for
    ///     commands the editor doesn't claim (Quit, Open/Save, clipboard, …).
    /// </summary>
    private Key KeyFor (Command command) =>
        Editor.KeyBindings.GetAllFromCommands (command).FirstOrDefault () ?? Application.GetDefaultKey (command);

    private void Action () { }

    private void SelectAll () { }

    private void Paste () { }

    private void Copy () { }

    private void Cut () { }

    private void Redo () { Editor.Document?.UndoStack.Redo (); }

    private void Undo () { Editor.Document?.UndoStack.Undo (); }

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

    private void Save () { /* placeholder — save comes when Editor can hold a document */ }

    private void SaveAs () { /* placeholder — save comes when Editor can hold a document */ }

    private void Quit ()
    {
        // TODO: add logic for unsaved changes, confirm quit, etc.
        RequestStop ();
    }
}
