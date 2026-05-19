// Claude - claude-sonnet-4-5
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
app.Init ();

var window = new Window
{
    Title = "prompt",
    Width = Dim.Fill (),
    Height = Dim.Fill ()
};

var editor = new Editor
{
    Multiline = false,
    X = 0,
    Y = 0,
    Width = Dim.Fill (),
    Height = 1,
    Document = new TextDocument (initialText),
    CaretOffset = initialText.Length
};

editor.KeyDown += (_, key) =>
{
    if (key == Key.Enter)
    {
        result = editor.Document.Text;
        window.RequestStop ();
        key.Handled = true;
    }
    else if (key == Key.Esc)
    {
        window.RequestStop ();
        key.Handled = true;
    }
};

window.Add (editor);

app.Run (window);

if (result is not null)
{
    Console.WriteLine (result);
}
