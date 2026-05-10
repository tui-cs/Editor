// Claude - claude-opus-4-7

using Microsoft.Extensions.Logging;
using Ted;
using Terminal.Gui.App;
using Terminal.Gui.Tracing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Marker collection that serializes <see cref="HostingTests" /> against every other test
///     collection in this assembly. <see cref="HostingTests" /> mutates process-global statics
///     (<see cref="Logging.Logger" /> and <see cref="Trace.EnabledCategories" />) that
///     Terminal.Gui itself reads during draw and lifecycle calls on other threads — running
///     them in parallel with the rest of the suite is unsafe. <see cref="DisableParallelization" />
///     ensures no other collection runs while these tests are in flight.
/// </summary>
[CollectionDefinition (nameof (HostingTestsCollection), DisableParallelization = true)]
public sealed class HostingTestsCollection;

/// <summary>
///     Tests that ted's <see cref="Hosting" /> static helpers actually wire up <see cref="Logging.Logger" />
///     and <see cref="Trace.EnabledCategories" />. These prove the wiring exists; downstream Serilog tests
///     belong elsewhere.
/// </summary>
[Collection (nameof (HostingTestsCollection))]
public class HostingTests
{
    [Fact]
    public void ConfigureLogging_SetsLoggingLogger ()
    {
        ILogger before = Logging.Logger;

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
