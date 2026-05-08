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

        menu.Add (new MenuBarItem (Strings.menuFile,
            [
                new MenuItem { Command = Command.New, Action = New },
                new MenuItem { Command = Command.Open, Action = Open },
                new MenuItem { Command = Command.Save, Action = Save },
                new MenuItem { Command = Command.Quit, Action = Quit }
            ]),
            new MenuBarItem (Strings.menuEdit,
            [
                new MenuItem { Command = Command.Undo, Action = Undo },
                new MenuItem { Command = Command.Redo, Action = Redo },
                new Line (),
                new MenuItem { Command = Command.Cut, Action = Cut },
                new MenuItem { Command = Command.Copy, Action = Copy },
                new MenuItem { Command = Command.Paste, Action = Paste },
                new MenuItem { Command = Command.SelectAll, Action = SelectAll }
            ]),
            new MenuBarItem (Strings.menuHelp,
                [new MenuItem ("_About", "Show About dialog", Action)])
        );

        Editor = new()
        {
            X = 0,
            Y = Pos.Bottom (menu),
            Width = Dim.Fill (),
            Height = Dim.Fill (1)
        };

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
    }

    private void Undo ()
    {
    }

    private void New ()
    {
        Editor.Text = string.Empty;
        Editor.SetNeedsDraw ();
    }

    private void Open ()
    {
        /* placeholder — file picker comes when Editor can hold a document */
    }

    private void Save ()
    {
        /* placeholder — save comes when Editor can hold a document */
    }

    private void Quit () { RequestStop (); }
}
