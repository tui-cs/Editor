// Claude - claude-opus-4-7
using Terminal.Gui.App;
using Terminal.Gui.Drivers;
using Terminal.Gui.Testing;

namespace Terminal.Gui.Editor.IntegrationTests.Testing;

/// <summary>
///     Generic test fixture that boots an <see cref="IApplication"/> on the ANSI driver, instantiates a
///     <typeparamref name="TRunnable"/> via the supplied factory, and starts a non-blocking session via
///     <see cref="IApplication.Begin"/>. Tests then drive the app synchronously via <see cref="Injector"/>
///     and assert against <see cref="Driver"/>.
/// </summary>
/// <remarks>
///     Modeled loosely on Terminal.Gui's <c>AppTestHelper</c> but slimmer and only what the gui-cs/Text
///     integration tests need today. Uses <c>app.Begin</c> (non-blocking) instead of <c>app.Run</c>
///     (blocks) so each test can inject + assert + dispose deterministically without a worker thread.
/// </remarks>
public sealed class AppFixture<TRunnable> : IAsyncDisposable
    where TRunnable : class, IRunnable
{
    /// <summary>Default test viewport size — wide enough for menus and status bars.</summary>
    public const int DEFAULT_WIDTH = 80;

    /// <summary>Default test viewport size — tall enough for a menu, content rows, and a status bar.</summary>
    public const int DEFAULT_HEIGHT = 24;

    private readonly SessionToken? _session;

    /// <summary>Boots the app and begins <paramref name="factory"/>'s runnable.</summary>
    /// <param name="factory">Factory for the runnable under test.</param>
    /// <param name="width">Test viewport width in cells.</param>
    /// <param name="height">Test viewport height in cells.</param>
    public AppFixture (Func<TRunnable> factory, int width = DEFAULT_WIDTH, int height = DEFAULT_HEIGHT)
    {
        ArgumentNullException.ThrowIfNull (factory);

        App = Application.Create ();
        App.Init (DriverRegistry.Names.ANSI);
        App.Screen = new (0, 0, width, height);

        Top = factory ()!;
        _session = App.Begin (Top) ?? throw new InvalidOperationException ("Application.Begin returned null — session was cancelled.");
    }

    /// <summary>The application instance under test.</summary>
    public IApplication App { get; }

    /// <summary>The runnable under test.</summary>
    public TRunnable Top { get; }

    /// <summary>The driver, for asserting on rendered contents.</summary>
    public IDriver Driver => App.Driver!;

    /// <summary>Synchronous input injection entry point.</summary>
    public IInputInjector Injector => App.GetInputInjector ();

    /// <summary>
    ///     Forces a full layout and draw cycle. Call after input injection so subsequent
    ///     <see cref="DriverAssert"/> calls see the post-input rendered state.
    /// </summary>
    public void Render () { App.LayoutAndDraw (forceRedraw: true); }

    /// <inheritdoc />
    public ValueTask DisposeAsync ()
    {
        if (_session is not null)
        {
            App.End (_session);
        }

        if (Top is IDisposable d)
        {
            d.Dispose ();
        }

        App.Dispose ();

        return ValueTask.CompletedTask;
    }
}
