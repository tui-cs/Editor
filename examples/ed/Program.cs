// Claude - claude-opus-4-7
// ed — Terminal.Gui.Editor demo. Pre-alpha stub.
// Will become an interactive Editor scenario as the View ships
// (specs/00-plan.md phases 2–5; MVP demo surface in §12).

using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

_ = typeof (AssemblyMarker);

using IApplication app = Application.Create ();

app.Init ();

Window win = new ()
{
    Title = "Editor Demo",
    X = 0,
    Y = 0,
    Width = Dim.Fill (),
    Height = Dim.Fill ()
};

app.Run (win);
