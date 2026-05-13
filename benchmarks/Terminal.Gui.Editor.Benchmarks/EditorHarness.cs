using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Editor;
using Terminal.Gui.Views;

namespace Terminal.Gui.Editor.Benchmarks;

/// <summary>
///     Lightweight harness that boots an <see cref="IApplication" /> on the ANSI driver,
///     hosts an <see cref="Editor" /> filling the viewport, and exposes synchronous
///     input injection and rendering. Mirrors the test fixture but without async disposal
///     so BenchmarkDotNet can manage its lifetime.
/// </summary>
internal sealed class EditorHarness : IDisposable
{
    private readonly EditorHost _host;
    private readonly SessionToken? _session;

    public EditorHarness (string text, int width = 80, int height = 24)
    {
        App = Application.Create ();
        App.Init (DriverRegistry.Names.ANSI);
        App.Driver!.SetScreenSize (width, height);

        _host = new EditorHost (text);
        _session = App.Begin (_host) ??
                   throw new InvalidOperationException ("Application.Begin returned null.");

        Editor.SetFocus ();
        Render ();
    }

    public IApplication App { get; }

    public Editor Editor => _host.Editor;

    public IInputInjector Injector => App.GetInputInjector ();

    public void Dispose ()
    {
        if (_session is not null)
        {
            App.End (_session);
        }

        _host.Dispose ();
        App.Dispose ();
    }

    public void Render ()
    {
        App.LayoutAndDraw (true);
    }

    private sealed class EditorHost : Window
    {
        public EditorHost (string text)
        {
            BorderStyle = LineStyle.None;
            Editor = new Editor
            {
                Document = new TextDocument (text),
                X = 0,
                Y = 0,
                Width = Dim.Fill (),
                Height = Dim.Fill ()
            };
            Add (Editor);
        }

        public Editor Editor { get; }
    }
}
