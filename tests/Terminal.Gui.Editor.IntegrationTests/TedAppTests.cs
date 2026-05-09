// Claude - claude-opus-4-7

using System.Drawing;
using Ted;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using TextMateSharp.Grammars;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     End-to-end tests that boot the <see cref="TedApp" /> demo on the ANSI driver and assert against
///     <see cref="Terminal.Gui.Drivers.IDriver.Contents" /> after synthetic keyboard / mouse input.
/// </summary>
public class TedAppTests
{
    [Fact]
    public void NewFile_ClearsEditor_AndCurrentFilePath ()
    {
        TedApp app = new ();
        app.ShowOpenDialog = () => "/tmp/ted-open.txt";
        app.ReadAllText = _ => "opened";

        Assert.True (app.OpenFile ());
        app.Editor.SelectAll ();

        app.NewFile ();

        Assert.Null (app.CurrentFilePath);
        Assert.Equal (string.Empty, app.Editor.Document!.Text);
        Assert.Equal (0, app.Editor.CaretOffset);
        Assert.False (app.Editor.HasSelection);
    }

    [Fact]
    public void OpenFile_Canceled_DoesNotChangeEditor ()
    {
        TedApp app = new ();
        app.ShowOpenDialog = () => null;
        app.ReadAllText = _ => throw new InvalidOperationException ("Canceled open should not read.");

        Assert.False (app.OpenFile ());

        Assert.Null (app.CurrentFilePath);
        Assert.Equal ("Hello world", app.Editor.Document!.Text);
    }

    [Fact]
    public void OpenFile_LoadsSelectedFile_FromDisk ()
    {
        string filePath = Path.Combine (Path.GetTempPath (), $"ted-open-{Guid.NewGuid ():N}.txt");
        File.WriteAllText (filePath, "from disk");

        try
        {
            TedApp app = new ();
            app.ShowOpenDialog = () => filePath;

            Assert.True (app.OpenFile ());

            Assert.Equal (filePath, app.CurrentFilePath);
            Assert.Equal ("from disk", app.Editor.Document!.Text);
            Assert.Equal (0, app.Editor.CaretOffset);
        }
        finally
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public void SaveFile_WritesCurrentEditorText_ToCurrentPath ()
    {
        string filePath = Path.Combine (Path.GetTempPath (), $"ted-save-{Guid.NewGuid ():N}.txt");
        File.WriteAllText (filePath, "before");

        try
        {
            TedApp app = new ();
            app.ShowOpenDialog = () => filePath;
            Assert.True (app.OpenFile ());
            app.Editor.Document!.Text = "after";

            Assert.True (app.SaveFile ());

            Assert.Equal ("after", File.ReadAllText (filePath));
            Assert.Equal (filePath, app.CurrentFilePath);
        }
        finally
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public void SaveFileAs_Canceled_DoesNotWrite ()
    {
        bool wrote = false;
        TedApp app = new ();
        app.ShowSaveDialog = () => " ";
        app.WriteAllText = (_, _) => wrote = true;

        Assert.False (app.SaveFileAs ());

        Assert.False (wrote);
        Assert.Null (app.CurrentFilePath);
    }

    [Fact]
    public void SaveFileAs_WritesEditorText_ToSelectedPath ()
    {
        string filePath = Path.Combine (Path.GetTempPath (), $"ted-save-as-{Guid.NewGuid ():N}.txt");

        try
        {
            TedApp app = new ();
            app.ShowSaveDialog = () => filePath;
            app.Editor.Document!.Text = "save as";

            Assert.True (app.SaveFileAs ());

            Assert.Equal ("save as", File.ReadAllText (filePath));
            Assert.Equal (filePath, app.CurrentFilePath);
        }
        finally
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public async Task Renders_HelloWorld_InEditorArea ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsContains (fx.Driver, "Hello world");
    }

    [Fact]
    public async Task Renders_FileMenu_Header ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsContains (fx.Driver, "File");
    }

    [Fact]
    public async Task Renders_OptionsMenu_Header ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsContains (fx.Driver, "Options");
    }

    [Fact]
    public async Task Renders_Themes_StatusBar_Item ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsContains (fx.Driver, "Themes");
    }

    [Fact]
    public async Task Theme_StatusBar_DropDown_Changes_Editor_Syntax_Theme ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        fx.Top.ThemeDropDown.Value = ThemeName.LightPlus;

        TextMateSyntaxHighlighter highlighter = Assert.IsType<TextMateSyntaxHighlighter> (fx.Top.Editor.SyntaxHighlighter);
        Assert.Equal (ThemeName.LightPlus, highlighter.ThemeName);
    }

    [Fact]
    public async Task FileMenu_OpensViaKeyboard_AltF ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        // The "Open..." menu item is unique to the dropdown — the StatusBar shortcut is just "Open".
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Open...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.F.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }

    [Fact]
    public async Task OptionsMenu_TogglesLineNumbers_ViaKeyboard ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        Assert.False (fx.Top.Editor.ShowLineNumbers);

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.O.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Line Numbers");
        DriverAssert.ContentsContains (fx.Driver, "☐ Line Numbers");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.True (fx.Top.Editor.ShowLineNumbers);

        fx.Injector.InjectKey (Key.O.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "☒ Line Numbers");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.False (fx.Top.Editor.ShowLineNumbers);
    }

    [Fact]
    public async Task FileMenu_OpensViaMouse_ClickOnHeader ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Open...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        DateTime baseTime = new (2025, 1, 1, 12, 0, 0);
        Point clickPos = new (2, 0); // somewhere on the "File" header at row 0

        fx.Injector.InjectMouse (
            new () { ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonPressed, Timestamp = baseTime }, options);
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonReleased,
                Timestamp = baseTime.AddMilliseconds (50)
            }, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }
}
