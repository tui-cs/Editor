// Claude - claude-opus-4-7

using System.Drawing;
using Ted;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Editor;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
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
        Assert.Equal (string.Empty, app.Editor.Document!.Text);
    }

    [Fact]
    public void OpenFile_LoadsSelectedFile_FromDisk ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-open-{Guid.NewGuid ():N}.txt");
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
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-save-{Guid.NewGuid ():N}.txt");
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
    public void SaveFile_MarksDocumentUnmodified ()
    {
        TedApp app = new ();
        app.ShowOpenDialog = () => "/tmp/ted-save.txt";
        app.ReadAllText = _ => "before";
        Assert.True (app.OpenFile ());
        app.Editor.Document!.Text = "after";
        app.WriteAllText = (_, _) => { };

        Assert.True (app.IsDocumentModified);

        Assert.True (app.SaveFile ());

        Assert.False (app.IsDocumentModified);
    }

    [Fact]
    public void Open_Save_RoundTrip_Preserves_Tab_Characters ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-tabs-{Guid.NewGuid ():N}.txt");
        File.WriteAllText (filePath, "a\tb");

        try
        {
            TedApp app = new ();
            app.ShowOpenDialog = () => filePath;

            Assert.True (app.OpenFile ());
            Assert.True (app.SaveFile ());

            Assert.Equal ("a\tb", File.ReadAllText (filePath));
        }
        finally
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public void SaveFileAs_Canceled_DoesNotWrite ()
    {
        var wrote = false;
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
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-save-as-{Guid.NewGuid ():N}.txt");

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
    public void QuitFile_ModifiedDocument_CancelChoice_DoesNotQuit ()
    {
        var prompted = false;
        TedApp app = new ();
        app.Editor.Document!.Text = "dirty";
        app.ShowSaveChangesDialog = () =>
        {
            prompted = true;

            return SaveChangesChoice.Cancel;
        };

        Assert.False (app.QuitFile ());

        Assert.True (prompted);
        Assert.True (app.IsDocumentModified);
    }

    [Fact]
    public async Task QuitFile_ModifiedDocument_SaveChoice_SavesBeforeQuitting ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());
        string? savedPath = null;
        string? savedText = null;
        fx.Top.ShowOpenDialog = () => "/tmp/ted-save-on-quit.txt";
        fx.Top.ReadAllText = _ => "before";
        Assert.True (fx.Top.OpenFile ());
        fx.Top.Editor.Document!.Text = "after";
        fx.Top.ShowSaveChangesDialog = () => SaveChangesChoice.Save;
        fx.Top.WriteAllText = (path, text) =>
        {
            savedPath = path;
            savedText = text;
        };

        Assert.True (fx.Top.QuitFile ());

        Assert.Equal ("/tmp/ted-save-on-quit.txt", savedPath);
        Assert.Equal ("after", savedText);
        Assert.False (fx.Top.IsDocumentModified);
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
    public async Task Renders_Indent_Size_StatusBar_Item ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsContains (fx.Driver, "Indent");
    }

    [Fact]
    public async Task Highlighting_Auto_Detects_From_File_Extension ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        // Default is C# (set in constructor).
        Assert.NotNull (fx.Top.Editor.HighlightingDefinition);
        Assert.Equal ("C#", fx.Top.Editor.HighlightingDefinition!.Name);

        // Switching to XML highlighting works.
        fx.Top.Editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinitionByExtension (".xml");
        Assert.NotNull (fx.Top.Editor.HighlightingDefinition);
        Assert.Equal ("XML", fx.Top.Editor.HighlightingDefinition!.Name);
    }

    [Fact]
    public async Task Highlighting_Json_Punctuation_Uses_Theme_Foreground ()
    {
        await using AppFixture<TedApp> fx = new (() =>
        {
            TedApp app = new ();
            app.ShowOpenDialog = () => "/tmp/settings.json";
            app.ReadAllText = _ => "{}";
            Assert.True (app.OpenFile ());

            return app;
        });

        fx.Render ();

        var normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell[,] contents = fx.Driver.Contents!;
        Cell? braceCell = null;

        for (var row = 0; row < contents.GetLength (0) && braceCell is null; row++)
        {
            for (var col = 0; col < contents.GetLength (1); col++)
            {
                if (contents[row, col].Grapheme == "{")
                {
                    braceCell = contents[row, col];

                    break;
                }
            }
        }

        Assert.True (braceCell.HasValue);
        Assert.Equal (normal.Foreground, braceCell.Value.Attribute!.Value.Foreground);
    }

    [Fact]
    public async Task IndentationSize_StatusBar_NumericUpDown_Changes_Editor_IndentationSize ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        fx.Top.IndentationSizeUpDown.Value = 8;

        Assert.Equal (8, fx.Top.Editor.IndentationSize);
    }

    [Fact]
    public async Task ShowTabs_StatusBar_CheckBox_Changes_Editor_ShowTabs ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        fx.Top.ShowTabsCheckBox.Value = CheckState.Checked;

        Assert.True (fx.Top.Editor.ShowTabs);
    }

    [Fact]
    public void Constructor_ReadOnly_Sets_Editor_ReadOnly ()
    {
        TedApp app = new (true);

        Assert.True (app.Editor.ReadOnly);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Initially_Shows_Line_1_Column_1 ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        Assert.Equal ("Ln: 1, Ch: 1", fx.Top.LocShortcut.Title);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Tracks_Caret_Movement ()
    {
        await using AppFixture<TedApp> fx = new (() =>
        {
            TedApp app = new ();
            app.Editor.Document!.Text = "alpha\nbeta\ngamma";

            return app;
        });

        // Caret at offset 8 → "beta": line 2, column 3 ('t').
        fx.Top.Editor.CaretOffset = 8;

        Assert.Equal ("Ln: 2, Ch: 3", fx.Top.LocShortcut.Title);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Updates_When_Document_Edit_Shifts_Caret ()
    {
        await using AppFixture<TedApp> fx = new (() =>
        {
            TedApp app = new ();
            app.Editor.Document!.Text = "abc";

            return app;
        });

        fx.Top.Editor.CaretOffset = 1;

        // Inserting before the caret shifts it to the right; the loc shortcut must follow.
        fx.Top.Editor.Document!.Insert (0, ">>>");

        Assert.Equal ("Ln: 1, Ch: 5", fx.Top.LocShortcut.Title);
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
        Assert.True (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.O.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Line Numbers");
        DriverAssert.ContentsContains (fx.Driver, "☒ Line Numbers");
        DriverAssert.ContentsContains (fx.Driver, "Convert Tabs To Spaces");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.False (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));

        fx.Injector.InjectKey (Key.O.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "☐ Line Numbers");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.True (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));
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
            new Mouse { ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonPressed, Timestamp = baseTime },
            options);
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = clickPos, Flags = MouseFlags.LeftButtonReleased,
                Timestamp = baseTime.AddMilliseconds (50)
            }, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }

    [Fact]
    public async Task EditMenu_OpensViaKeyboard_AltE_Contains_Find_And_Replace ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Find...");
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Replace...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.E.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Find...");
        DriverAssert.ContentsContains (fx.Driver, "Replace...");
    }

    [Fact]
    public async Task Editor_RightClick_Opens_Edit_Context_Menu ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp ());

        DriverAssert.ContentsDoesNotContain (fx.Driver, "Find...");
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Replace...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };

        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (4, 2),
                Flags = MouseFlags.RightButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            },
            options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Find...");
        DriverAssert.ContentsContains (fx.Driver, "Replace...");
        DriverAssert.ContentsContains (fx.Driver, "Select all");
    }
}
