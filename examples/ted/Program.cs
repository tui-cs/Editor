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
    if (File.Exists (requestedPath))
    {
        await ted.OpenFileAsync (requestedPath);
    }
    else
    {
        ted.OpenMissingFile (requestedPath);
    }
}

app.Run (ted);
