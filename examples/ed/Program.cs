// Claude - claude-opus-4-7
// ed — Terminal.Gui.Editor demo. Menubar + Editor + StatusBar, single-file.
// See Issue #7 and specs/00-plan.md §12 for the planned demo surface.

using Ed;
using Microsoft.Extensions.Logging;
using Serilog;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Tracing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

Logging.Logger = CreateLogger ();
Trace.EnabledCategories = TraceCategory.Command | TraceCategory.Keyboard | TraceCategory.Mouse;

using IApplication app = Application.Create ();

app.Init ();

using EdApp ed = new ();

app.Run (ed);

static ILogger CreateLogger ()
{
    Log.Logger = new LoggerConfiguration ()
                 .MinimumLevel.Verbose ()
                 .Enrich.FromLogContext ()
                 .WriteTo.File ("logs/ed.log",
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                 .CreateLogger ();

    ILoggerFactory factory = LoggerFactory.Create (b => b.AddSerilog (dispose: true).SetMinimumLevel (LogLevel.Trace));

    return factory.CreateLogger ("ed");
}
