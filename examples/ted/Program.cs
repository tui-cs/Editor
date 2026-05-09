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

using TedApp ted = new ();

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

app.Run (ted);

return;

static string? FindRepoRoot (string? directory)
{
    while (directory is not null)
    {
        if (File.Exists (Path.Combine (directory, "Terminal.Gui.Text.slnx")))
        {
            return directory;
        }

        directory = Path.GetDirectoryName (directory);
    }

    return null;
}
