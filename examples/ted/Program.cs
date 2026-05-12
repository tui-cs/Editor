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

var readOnly = args.Any (static arg => arg is "--read-only" or "-r");
string? requestedPath = args.FirstOrDefault (static arg => arg is not ("--read-only" or "-r"));

using TedApp ted = new (readOnly);

if (!string.IsNullOrWhiteSpace (requestedPath) && File.Exists (requestedPath))
{
    ted.SetDocument (File.ReadAllText (requestedPath), requestedPath);
}
else
{
    // If running from within the repo, open TedApp.cs as the default file.
    var repoRoot = FindRepoRoot (AppContext.BaseDirectory);

    if (repoRoot is not null)
    {
        var tedAppPath = Path.Combine (repoRoot, "examples", "ted", "TedApp.cs");

        if (File.Exists (tedAppPath))
        {
            ted.SetDocument (File.ReadAllText (tedAppPath), tedAppPath);
        }
    }
}

app.Run (ted);

return;

static string? FindRepoRoot (string? directory)
{
    while (directory is not null)
    {
        if (File.Exists (Path.Combine (directory, "Terminal.Gui.Editor.slnx")))
        {
            return directory;
        }

        directory = Path.GetDirectoryName (directory);
    }

    return null;
}
