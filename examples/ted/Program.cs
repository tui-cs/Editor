// Claude - claude-opus-4-7
// ted — Terminal.Gui.Editor demo. Menubar + Editor + StatusBar, single-file.
// See Issue #7 and specs/00-plan.md §12 for the planned demo surface.

using Ted;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

Hosting.ConfigureLogging ();
Hosting.EnableTracing ();

ConfigurationManager.Enable (ConfigLocations.All);

using IApplication app = Application.Create ();

app.Init ();

using TedApp ted = new();

app.Run (ted);
