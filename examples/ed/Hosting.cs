using Microsoft.Extensions.Logging;
using Serilog;
using Terminal.Gui;
using Terminal.Gui.App;
using Terminal.Gui.Tracing;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Ed;

/// <summary>
///     Process-wide hosting concerns for the <c>ed</c> demo: Serilog → MEL → Terminal.Gui's
///     <see cref="Logging.Logger"/>, plus <see cref="Trace.EnabledCategories"/>.
///     Extracted from <c>Program.cs</c> so tests can call it and assert side effects.
/// </summary>
public static class Hosting
{
    /// <summary>Default trace categories for ed. Useful enough to debug menu/key/mouse flow without flooding the log.</summary>
    public const TraceCategory DefaultTraceCategories = TraceCategory.Command | TraceCategory.Keyboard | TraceCategory.Mouse;

    /// <summary>Default log file path. Daily rolling.</summary>
    public const string DefaultLogPath = "logs/ed.log";

    /// <summary>
    ///     Configures Serilog → Microsoft.Extensions.Logging → <see cref="Logging.Logger"/>. Returns the
    ///     <see cref="ILogger"/> assigned to <see cref="Logging.Logger"/> so callers can also use it directly.
    /// </summary>
    public static ILogger ConfigureLogging (string? logPath = null)
    {
        Log.Logger = new LoggerConfiguration ()
                     .MinimumLevel.Verbose ()
                     .Enrich.FromLogContext ()
                     .WriteTo.File (logPath ?? DefaultLogPath,
                                    rollingInterval: RollingInterval.Day,
                                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                     .CreateLogger ();

        ILoggerFactory factory = LoggerFactory.Create (b => b.AddSerilog (dispose: true).SetMinimumLevel (LogLevel.Trace));
        ILogger logger = factory.CreateLogger ("ed");
        Logging.Logger = logger;

        return logger;
    }

    /// <summary>Sets <see cref="Trace.EnabledCategories"/>. Defaults to <see cref="DefaultTraceCategories"/>.</summary>
    public static void EnableTracing (TraceCategory categories = DefaultTraceCategories) { Trace.EnabledCategories = categories; }
}
