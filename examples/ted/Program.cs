// ted — Terminal.Gui.Editor demo.

using Ted;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;

// ReSharper disable AccessToDisposedClosure

Hosting.ConfigureLogging ();
Hosting.EnableTracing ();

// ConfigurationManager is the single authority for reading settings: Enable (All) loads
// ~/.tui/ted.config.json (ConfigLocations.AppHome) and applies the values to the
// EditorSettings [ConfigurationProperty] statics before TedApp is constructed.
ConfigurationManager.Enable (ConfigLocations.All);

using IApplication app = Application.Create ();

app.Init ();

var readOnly = args.Any (static arg => arg is "--read-only" or "-r");
var requestedPath = args.FirstOrDefault (static arg => arg is not ("--read-only" or "-r"));

using TedApp ted = new (readOnly);

if (!string.IsNullOrWhiteSpace (requestedPath))
{
    // Files at or below this size load fully before the first paint so the editor opens with content
    // already on screen. Above it, the progressive path wins (window appears immediately, content
    // fills in top-down) and the brief empty-buffer frame is the acceptable cost of not blocking
    // startup on a multi-megabyte read. 1 MiB matches ted's existing "large document" boundary.
    const long synchronousLoadMaxBytes = 1024 * 1024;

    FileInfo file = new (requestedPath);

    if (!file.Exists)
    {
        app.Invoke (() => ted.OpenMissingFile (requestedPath));
    }
    else if (file.Length <= synchronousLoadMaxBytes)
    {
        // Synchronous (non-marshalled) load completes before app.Run, so the very first paint
        // shows the document — no blank-buffer-then-fill flash for the common small-file case.
        ted.OpenFileAsync (requestedPath).GetAwaiter ().GetResult ();
    }
    else
    {
        // Large file: defer onto the app loop so the window renders first, then the file streams
        // in progressively instead of blocking the UI until the whole file is read.
        app.Invoke (() => ted.BeginOpenFile (requestedPath));
    }
}

app.Run (ted);
