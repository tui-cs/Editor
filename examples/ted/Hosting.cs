using Microsoft.Extensions.Logging;
using Serilog;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Tracing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Ted;

/// <summary>
///     Process-wide hosting concerns for the <c>ted</c> demo: Serilog → MEL → Terminal.Gui's
///     <see cref="Logging.Logger"/>, plus <see cref="Trace.EnabledCategories"/>.
///     Extracted from <c>Program.cs</c> so tests can call it and assert side effects.
/// </summary>
public static class Hosting
{
    /// <summary>Default trace categories for ted. Useful enough to debug menu/key/mouse flow without flooding the log.</summary>
    public const TraceCategory DEFAULT_TRACE_CATEGORIES = TraceCategory.Command | TraceCategory.Keyboard | TraceCategory.Mouse;

    /// <summary>Default log file path. Daily rolling.</summary>
    public const string DEFAULT_LOG_PATH = "logs/ted.log";

    /// <summary>
    ///     Configures Serilog → Microsoft.Extensions.Logging → <see cref="Logging.Logger"/>. Returns the
    ///     <see cref="ILogger"/> assigned to <see cref="Logging.Logger"/> so callers can also use it directly.
    /// </summary>
    public static ILogger ConfigureLogging (string? logPath = null)
    {
        Log.Logger = new LoggerConfiguration ()
                     .MinimumLevel.Verbose ()
                     .Enrich.FromLogContext ()
                     .WriteTo.File (logPath ?? DEFAULT_LOG_PATH,
                                    rollingInterval: RollingInterval.Day,
                                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                     .CreateLogger ();

        ILoggerFactory factory = LoggerFactory.Create (b => b.AddSerilog (dispose: true).SetMinimumLevel (LogLevel.Trace));
        ILogger logger = factory.CreateLogger ("ted");
        Logging.Logger = logger;

        return logger;
    }

    /// <summary>Sets <see cref="Trace.EnabledCategories"/>. Defaults to <see cref="DEFAULT_TRACE_CATEGORIES"/>.</summary>
    public static void EnableTracing (TraceCategory categories = DEFAULT_TRACE_CATEGORIES) { Trace.EnabledCategories = categories; }
}
