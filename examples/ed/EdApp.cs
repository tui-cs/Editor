using Terminal.Gui.App;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Ed;

/// <summary>
///     Top-level <see cref="Window"/> for the <c>ed</c> demo. MenuBar at top,
///     <see cref="Editor"/> in the middle, StatusBar at the bottom. Single-file —
///     no tabs (compare to Terminal.Gui's Notepad scenario).
/// </summary>
public sealed class EdApp : Window
{
    /// <summary>Initializes a new <see cref="EdApp"/>.</summary>
    public EdApp ()
    {
        Title = "ed — Terminal.Gui.Editor demo";
        BorderStyle = LineStyle.None;

        MenuBar menu = new ();

        menu.Add (new MenuBarItem ("_File",
                                   [
                                       new MenuItem { Title = "_New", Key = Key.N.WithCtrl, Action = New },
                                       new MenuItem { Title = "_Open...", Key = Key.O.WithCtrl, Action = Open },
                                       new MenuItem { Title = "_Save", Key = Key.S.WithCtrl, Action = Save },
                                       new MenuItem { Title = "_Quit", Key = Application.GetDefaultKey (Command.Quit), Action = Quit }
                                   ]));

        Editor = new ()
        {
            X = 0,
            Y = Pos.Bottom (menu),
            Width = Dim.Fill (),
            Height = Dim.Fill (1)
        };

        StatusBar statusBar =
            new ([
                     new Shortcut (Application.GetDefaultKey (Command.Quit), "Quit", Quit),
                     new Shortcut (Key.F1, "New", New),
                     new Shortcut (Key.F2, "Open", Open),
                     new Shortcut (Key.F3, "Save", Save)
                 ]);

        Add (menu, Editor, statusBar);
    }

    /// <summary>The editor View at the centre of the app. Exposed for tests and future commands.</summary>
    public Editor Editor { get; }

    private void New () { Editor.Text = string.Empty; Editor.SetNeedsDraw (); }

    private void Open () { /* placeholder — file picker comes when Editor can hold a document */ }

    private void Save () { /* placeholder — save comes when Editor can hold a document */ }

    private void Quit () { RequestStop (); }
}
