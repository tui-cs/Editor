// Claude - claude-opus-4-7

using System.Collections.Immutable;
using System.Drawing;
using System.Text;
using Ted;
using Terminal.Gui.Configuration;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Text.Indentation;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     End-to-end tests that boot the <see cref="TedApp" /> demo on the ANSI driver and assert against
///     <see cref="Terminal.Gui.Drivers.IDriver.Contents" /> after synthetic keyboard / mouse input.
/// </summary>
public class TedAppTests
{
    private static void DeleteIfExists (string filePath)
    {
        if (File.Exists (filePath))
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public void NewFile_ClearsEditor_AndCurrentFilePath ()
    {
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        app.ShowOpenDialog = () => "/tmp/ted-open.txt";
        app.OpenRead = _ => new MemoryStream (Encoding.UTF8.GetBytes ("opened"));

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
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        app.ShowOpenDialog = () => null;
        app.OpenRead = _ => throw new InvalidOperationException ("Canceled open should not read.");

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
            TedApp app = new (configPath: TedTestConfig.NewPath ());
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
    public void OpenMissingFile_SetsPath_AndMarksDocumentModified ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-missing-{Guid.NewGuid ():N}.txt");
        DeleteIfExists (filePath);

        try
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());
            app.OpenMissingFile (filePath);

            Assert.Equal (filePath, app.CurrentFilePath);
            Assert.Equal (string.Empty, app.Editor.Document!.Text);
            Assert.True (app.IsDocumentModified);
        }
        finally
        {
            DeleteIfExists (filePath);
        }
    }

    [Fact]
    public async Task OpenFileAsync_Updates_LoadStatusShortcut ()
    {
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        app.ShowOpenDialog = () => "/tmp/ted-progress.txt";
        app.OpenRead = _ => new MemoryStream (Encoding.UTF8.GetBytes (new string ('x', 100_000)));

        Assert.Equal (string.Empty, app.LoadSpinnerShortcut.Title);

        Assert.True (await app.OpenFileAsync (TestContext.Current.CancellationToken));

        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.Title);
        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.HelpText);
        Assert.Same (app.LoadStatusSpinner, app.LoadSpinnerShortcut.CommandView);
        Assert.False (app.LoadStatusSpinner.Visible);
        Assert.False (app.LoadStatusSpinner.AutoSpin);
        Assert.Equal (100_000, app.Editor.Document!.TextLength);
    }

    [Fact]
    public async Task OpenFileAsync_Loads_Stream_On_Background_Thread ()
    {
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        GatedReadStream stream = new (Encoding.UTF8.GetBytes (new string ('x', 100_000)));
        app.ShowOpenDialog = () => "/tmp/ted-progress.txt";
        app.OpenRead = _ => stream;

        Task<bool> openTask = app.OpenFileAsync (TestContext.Current.CancellationToken);

        await stream.ReadStarted.Task.WaitAsync (TestContext.Current.CancellationToken);

        Assert.False (openTask.IsCompleted);
        Assert.True (app.LoadStatusSpinner.Visible);
        Assert.True (app.LoadStatusSpinner.AutoSpin);
        Assert.Equal ("Loading 0 B of 97.7 KiB", app.LoadSpinnerShortcut.Title);
        Assert.Equal ("Loading 0 B of 97.7 KiB", app.LoadSpinnerShortcut.HelpText);

        stream.AllowRead.SetResult ();

        Assert.True (await openTask);
        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.Title);
        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.HelpText);
    }

    [Fact]
    public async Task OpenFileAsync_ByPath_Updates_LoadStatusShortcut ()
    {
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        GatedReadStream stream = new (Encoding.UTF8.GetBytes (new string ('x', 100_000)));
        app.OpenRead = _ => stream;

        Task<bool> openTask = app.OpenFileAsync ("/tmp/ted-progress.cs", TestContext.Current.CancellationToken);

        await stream.ReadStarted.Task.WaitAsync (TestContext.Current.CancellationToken);

        Assert.Equal ("Loading 0 B of 97.7 KiB", app.LoadSpinnerShortcut.Title);
        Assert.Equal ("Loading 0 B of 97.7 KiB", app.LoadSpinnerShortcut.HelpText);

        stream.AllowRead.SetResult ();

        Assert.True (await openTask);
        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.Title);
        Assert.Equal ("Loaded 97.7 KiB", app.LoadSpinnerShortcut.HelpText);
        Assert.False (app.LoadStatusSpinner.Visible);
        Assert.False (app.LoadStatusSpinner.AutoSpin);
    }

    [Fact]
    public async Task StatusBar_Shows_Loaded_FileSize_After_StartupOpen ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-startup-{Guid.NewGuid ():N}.cs");
        await File.WriteAllTextAsync (filePath, new string ('x', 100_000), TestContext.Current.CancellationToken);

        try
        {
            await using AppFixture<TedApp> fx = new (() =>
            {
                TedApp app = new (configPath: TedTestConfig.NewPath ());
                app.OpenFileAsync (filePath).GetAwaiter ().GetResult ();

                return app;
            });

            fx.Render ();

            DriverAssert.ContentsContains (fx.Driver, "Loaded 97.7 KiB");
        }
        finally
        {
            File.Delete (filePath);
        }
    }

    [Fact]
    public async Task OpenFileAsync_LargeFile_DisablesAutomaticFolding ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-large-{Guid.NewGuid ():N}.cs");
        await File.WriteAllTextAsync (filePath, new string ('x', 1_000_001), TestContext.Current.CancellationToken);

        try
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());

            Assert.True (await app.OpenFileAsync (filePath, TestContext.Current.CancellationToken));

            Assert.Null (app.Editor.FoldingManager);
            Assert.Equal ("Loaded 976.6 KiB", app.LoadSpinnerShortcut.Title);
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
            TedApp app = new (configPath: TedTestConfig.NewPath ());
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
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        app.ShowOpenDialog = () => "/tmp/ted-save.txt";
        app.OpenRead = _ => new MemoryStream (Encoding.UTF8.GetBytes ("before"));
        Assert.True (app.OpenFile ());
        app.Editor.Document!.Text = "after";
        app.CreateWrite = _ => new MemoryStream ();

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
            TedApp app = new (configPath: TedTestConfig.NewPath ());
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
        TedApp app = new (configPath: TedTestConfig.NewPath ());
        app.ShowSaveDialog = () => " ";
        app.CreateWrite = _ =>
        {
            wrote = true;

            return new MemoryStream ();
        };

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
            TedApp app = new (configPath: TedTestConfig.NewPath ());
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
        TedApp app = new (configPath: TedTestConfig.NewPath ());
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
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));
        string? savedPath = null;
        string? savedText = null;
        fx.Top.ShowOpenDialog = () => "/tmp/ted-save-on-quit.txt";
        fx.Top.OpenRead = _ => new MemoryStream (Encoding.UTF8.GetBytes ("before"));
        Assert.True (fx.Top.OpenFile ());
        fx.Top.Editor.Document!.Text = "after";
        fx.Top.ShowSaveChangesDialog = () => SaveChangesChoice.Save;
        fx.Top.CreateWrite = path =>
        {
            savedPath = path;

            return new CapturingWriteStream (text => savedText = text);
        };

        Assert.True (fx.Top.QuitFile ());

        Assert.Equal ("/tmp/ted-save-on-quit.txt", savedPath);
        Assert.Equal ("after", savedText);
        Assert.False (fx.Top.IsDocumentModified);
    }

    [Fact]
    public void QuitFile_MissingFile_DiscardChoice_DoesNotCreateFile ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-missing-discard-{Guid.NewGuid ():N}.txt");
        DeleteIfExists (filePath);

        try
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());
            app.OpenMissingFile (filePath);
            app.ShowSaveChangesDialog = () => SaveChangesChoice.Discard;

            Assert.True (app.QuitFile ());
            Assert.False (File.Exists (filePath));
        }
        finally
        {
            DeleteIfExists (filePath);
        }
    }

    [Fact]
    public void QuitFile_MissingFile_SaveChoice_CreatesEmptyFile ()
    {
        var filePath = Path.Combine (Path.GetTempPath (), $"ted-missing-save-{Guid.NewGuid ():N}.txt");
        DeleteIfExists (filePath);

        try
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());
            app.OpenMissingFile (filePath);
            app.ShowSaveChangesDialog = () => SaveChangesChoice.Save;

            Assert.True (app.QuitFile ());

            Assert.True (File.Exists (filePath));
            Assert.Equal (string.Empty, File.ReadAllText (filePath));
            Assert.False (app.IsDocumentModified);
        }
        finally
        {
            DeleteIfExists (filePath);
        }
    }

    [Fact]
    public async Task Renders_FileMenu_Header ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        DriverAssert.ContentsContains (fx.Driver, "File");
    }

    [Fact]
    public async Task Renders_OptionsMenu_Header ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        DriverAssert.ContentsContains (fx.Driver, "Options");
    }

    [Fact]
    public async Task Renders_ViewMenu_Header ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        DriverAssert.ContentsContains (fx.Driver, "View");
    }

    [Fact]
    public async Task Constructor_Defaults_To_Plain_Text_Highlighting ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        Assert.Null (fx.Top.Editor.HighlightingDefinition);
        Assert.Equal ("Plain Text", fx.Top.LanguageShortcut.Title);
    }

    [Fact]
    public async Task Highlighting_Auto_Detects_From_File_Extension ()
    {
        var tempXmlFilePath = Path.Combine (Path.GetTempPath (), $"ted-highlight-{Guid.NewGuid ():N}.xml");

        try
        {
            await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));
            fx.Top.OpenMissingFile (tempXmlFilePath);

            Assert.NotNull (fx.Top.Editor.HighlightingDefinition);
            Assert.Equal ("XML", fx.Top.Editor.HighlightingDefinition!.Name);
            Assert.Equal ("XML", fx.Top.LanguageShortcut.Title);
        }
        finally
        {
            DeleteIfExists (tempXmlFilePath);
        }
    }

    [Fact]
    public async Task OptionsMenu_Contains_Settings_Item ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.O.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Settings...");
    }

    [Fact]
    public void Constructor_ReadOnly_Sets_Editor_ReadOnly ()
    {
        TedApp app = new (true);

        Assert.True (app.Editor.ReadOnly);
    }

    [Fact]
    public void Constructor_Defaults_AutoIndent_To_Enabled ()
    {
        TedApp app = new (configPath: TedTestConfig.NewPath ());

        Assert.IsType<DefaultIndentationStrategy> (app.Editor.IndentationStrategy);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Initially_Shows_Line_1_Column_1 ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        Assert.Equal ("Ln 1, Col 1", fx.Top.LocShortcut.Title);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Tracks_Caret_Movement ()
    {
        await using AppFixture<TedApp> fx = new (() =>
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());
            app.Editor.Document!.Text = "alpha\nbeta\ngamma";

            return app;
        });

        // Caret at offset 8 → "beta": line 2, column 3 ('t').
        fx.Top.Editor.CaretOffset = 8;

        Assert.Equal ("Ln 2, Col 3", fx.Top.LocShortcut.Title);
    }

    [Fact]
    public async Task Loc_StatusBar_Shortcut_Updates_When_Document_Edit_Shifts_Caret ()
    {
        await using AppFixture<TedApp> fx = new (() =>
        {
            TedApp app = new (configPath: TedTestConfig.NewPath ());
            app.Editor.Document!.Text = "abc";

            return app;
        });

        fx.Top.Editor.CaretOffset = 1;

        // Inserting before the caret shifts it to the right; the loc shortcut must follow.
        fx.Top.Editor.Document!.Insert (0, ">>>");

        Assert.Equal ("Ln 1, Col 5", fx.Top.LocShortcut.Title);
    }

    [Fact]
    public async Task FileMenu_OpensViaKeyboard_AltF ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        // The "Open..." menu item is unique to the dropdown — the StatusBar shortcut is just "Open".
        DriverAssert.ContentsDoesNotContain (fx.Driver, "Open...");

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.F.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Open...");
    }

    [Fact]
    public async Task ViewMenu_TogglesLineNumbers_ViaKeyboard ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));
        Assert.True (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));

        InputInjectionOptions options = new () { Mode = InputInjectionMode.Direct };
        fx.Injector.InjectKey (Key.V.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "Line Numbers");
        DriverAssert.ContentsContains (fx.Driver, "☒ Line Numbers");
        DriverAssert.ContentsContains (fx.Driver, "Show Tabs");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.False (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));

        fx.Injector.InjectKey (Key.V.WithAlt, options);
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "☐ Line Numbers");

        fx.Injector.InjectKey (Key.Enter, options);
        fx.Render ();

        Assert.True (fx.Top.Editor.GutterOptions.HasFlag (GutterOptions.LineNumbers));
    }

    [Fact]
    public async Task FileMenu_OpensViaMouse_ClickOnHeader ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

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
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

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
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

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

    [Fact]
    public async Task ThemeDropDown_Initially_Shows_Current_Theme ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        Assert.Equal (ThemeManager.Theme, fx.Top.ThemeDropDown.Text);
    }

    [Fact]
    public async Task ThemeDropDown_Source_Contains_All_Available_Themes ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        ImmutableList<string> expected = ThemeManager.GetThemeNames ();
        Assert.True (expected.Count > 0, "ThemeManager should expose at least one theme.");

        List<string> actual = fx.Top.ThemeDropDown.Source!.ToList ()
            .Cast<string> ()
            .ToList ();

        Assert.Equal (expected, actual);
    }

    [Fact]
    public async Task ThemeDropDown_Selection_Changes_Active_Theme ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        ImmutableList<string> names = ThemeManager.GetThemeNames ();

        if (names.Count < 2)
        {
            return;
        }

        // Pick a theme that differs from the current one.
        var original = ThemeManager.Theme;
        var target = names.First (n => n != original);

        fx.Top.ThemeDropDown.Text = target;

        Assert.Equal (target, ThemeManager.Theme);
    }

    private sealed class CapturingWriteStream : MemoryStream
    {
        private readonly Action<string> _capture;

        public CapturingWriteStream (Action<string> capture)
        {
            _capture = capture;
        }

        protected override void Dispose (bool disposing)
        {
            if (disposing)
            {
                _capture (Encoding.UTF8.GetString (ToArray ()));
            }

            base.Dispose (disposing);
        }

        public override async ValueTask DisposeAsync ()
        {
            _capture (Encoding.UTF8.GetString (ToArray ()));
            await base.DisposeAsync ();
        }
    }

    /// <summary>Gates async reads and captures the reading thread ID for background-load tests.</summary>
    private sealed class GatedReadStream : MemoryStream
    {
        public GatedReadStream (byte[] buffer)
            : base (buffer)
        {
        }

        public TaskCompletionSource AllowRead { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReadStarted { get; } = new (TaskCreationOptions.RunContinuationsAsynchronously);

        public int ReadThreadId { get; private set; }

        public override ValueTask<int> ReadAsync (Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadThreadId = Environment.CurrentManagedThreadId;
            ReadStarted.TrySetResult ();

            return new ValueTask<int> (ReadAfterGateAsync (buffer, cancellationToken));
        }

        private async Task<int> ReadAfterGateAsync (Memory<byte> buffer, CancellationToken cancellationToken)
        {
            await AllowRead.Task.WaitAsync (cancellationToken);

            return await base.ReadAsync (buffer, cancellationToken);
        }
    }
}
