// Claude - claude-opus-4-7
// ed — Terminal.Gui.Editor demo. Menubar + Editor + StatusBar, single-file.
// See Issue #7 and specs/00-plan.md §12 for the planned demo surface.

using Ed;
using Terminal.Gui.App;

using IApplication app = Application.Create ();

app.Init ();

using EdApp ed = new ();

app.Run (ed);
