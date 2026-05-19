// prompt — single-line Editor example. Captures command-line text, lets the user edit it,
// outputs to stdout on Enter, exits silently on Esc.

using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Editor;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

var initialText = string.Join (' ', args);
string? result = null;

using IApplication app = Application.Create ();
app.AppModel = AppModel.Inline;
app.Init ();

Window window = new ()
{
    Title = "prompt",
    Width = Dim.Fill (),
    Height = Dim.Fill ()
};

Editor editor = new ()
{
    Multiline = false,
    X = 0,
    Y = 0,
    Width = Dim.Fill (),
    Document = new TextDocument (initialText),
    CaretOffset = initialText.Length
};

editor.Accepting += (_, e) =>
{
    result = editor.Document.Text;
    window.RequestStop ();
    e.Handled = true;
};

editor.KeyDown += (_, key) =>
{
    if (key != Key.Esc)
    {
        return;
    }

    window.RequestStop ();
    key.Handled = true;
};

window.Add (editor);

app.Run (window);

if (result is not null)
{
    Console.WriteLine (result);
}
