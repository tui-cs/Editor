// Claude - claude-opus-4-7
// ted — Terminal.Gui.Editor demo. Menubar + Editor + StatusBar, single-file.
// See Issue #7 and specs/00-plan.md §12 for the planned demo surface.

using Ted;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

Hosting.ConfigureLogging ();
Hosting.EnableTracing ();

ConfigurationManager.Enable (ConfigLocations.All);
EditorSettings.Load ();

using IApplication app = Application.Create ();

app.Init ();

var readOnly = args.Any (static arg => arg is "--read-only" or "-r");
var requestedPath = args.FirstOrDefault (static arg => arg is not ("--read-only" or "-r"));

using TedApp ted = new (readOnly);

if (!string.IsNullOrWhiteSpace (requestedPath))
{
    // Defer onto the app loop so the window renders first, then the file streams in progressively
    // instead of blocking the UI until the whole file is read.
    if (File.Exists (requestedPath))
    {
        app.Invoke (() => ted.BeginOpenFile (requestedPath));
    }
    else
    {
        app.Invoke (() => ted.OpenMissingFile (requestedPath));
    }
}

app.Run (ted);
