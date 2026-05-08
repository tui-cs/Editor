// Claude - claude-opus-4-7
using System.Drawing;
using Ed;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     End-to-end tests that boot the <see cref="EdApp"/> demo on the ANSI driver and assert against
///     <see cref="Terminal.Gui.Drivers.IDriver.Contents"/> after synthetic keyboard / mouse input.
/// </summary>
public class EdAppTests
{
    [Fact]
    public async Task Renders_HelloWorld_InEditorArea ()
    {
        await using AppFixture<EdApp> fx = new (() => new EdApp ());

        DriverAssert.ContentsContains (fx.Driver, "Hello world");
    }

    [Fact]
    public async Task Renders_FileMenu_Header ()
    {
        await using AppFixture<EdApp> fx = new (() => new EdApp ());

        DriverAssert.ContentsContains (fx.Driver, "File");
    }

    [Fact]
    public async Task FileMenu_OpensViaKeyboard_AltF ()
    {
        await using AppFixture<EdApp> fx = new (() => new EdApp ());

        // The "Open..." menu item is unique to the dropdown — the StatusBar shortcut is just "Open".
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Open...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.F.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }

    [Fact]
    public async Task FileMenu_OpensViaMouse_ClickOnHeader ()
    {
        await using AppFixture<EdApp> fx = new (() => new EdApp ());

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Open...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        DateTime baseTime = new (2025, 1, 1, 12, 0, 0);
        Point clickPos = new (2, 0); // somewhere on the "File" header at row 0

        fx.Injector.InjectMouse (new () { ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonPressed, Timestamp = baseTime }, options);
        fx.Injector.InjectMouse (new () { ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonReleased, Timestamp = baseTime.AddMilliseconds (50) }, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }
}
