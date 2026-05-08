// Claude - claude-opus-4-7

using Microsoft.Extensions.Logging;
using Ted;
using Terminal.Gui.App;
using Terminal.Gui.Tracing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Tests that ted's <see cref="Hosting" /> static helpers actually wire up <see cref="Logging.Logger" />
///     and <see cref="Trace.EnabledCategories" />. These prove the wiring exists; downstream Serilog tests
///     belong elsewhere.
/// </summary>
public class HostingTests
{
    [Fact]
    public void ConfigureLogging_SetsLoggingLogger ()
    {
        ILogger? before = Logging.Logger;

        try
        {
            ILogger logger =
                Hosting.ConfigureLogging (Path.Combine (Path.GetTempPath (), $"ted-test-{Guid.NewGuid ():N}.log"));

            Assert.NotNull (logger);
            Assert.Same (logger, Logging.Logger);
        }
        finally
        {
            Logging.Logger = before;
        }
    }

    [Fact]
    public void EnableTracing_SetsCategories ()
    {
        TraceCategory before = Trace.EnabledCategories;

        try
        {
            Hosting.EnableTracing ();

            Assert.Equal (Hosting.DefaultTraceCategories, Trace.EnabledCategories);
            Assert.NotEqual (TraceCategory.None, Trace.EnabledCategories);
        }
        finally
        {
            Trace.EnabledCategories = before;
        }
    }

    [Fact]
    public void EnableTracing_RespectsExplicitCategories ()
    {
        TraceCategory before = Trace.EnabledCategories;

        try
        {
            Hosting.EnableTracing (TraceCategory.Lifecycle | TraceCategory.Draw);

            Assert.Equal (TraceCategory.Lifecycle | TraceCategory.Draw, Trace.EnabledCategories);
        }
        finally
        {
            Trace.EnabledCategories = before;
        }
    }
}
