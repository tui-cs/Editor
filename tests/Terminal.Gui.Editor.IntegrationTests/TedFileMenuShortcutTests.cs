// Copilot - claude-opus-4-6

using System.Globalization;
using Ted;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Resources;
using Terminal.Gui.Testing;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Verifies that the File menu items display their keyboard shortcut indicators
///     (Ctrl+N, Ctrl+O, Ctrl+S, Ctrl+Shift+S) and that the Save As dialog is titled correctly.
///     Uses <see cref="AnsiSnapshot" /> for render verification.
/// </summary>
public class TedFileMenuShortcutTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task FileMenu_Shows_Keyboard_Shortcuts ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()));

        // Open File menu via Alt+F
        fx.Injector.InjectKey (Key.F.WithAlt, Direct);
        fx.Render ();

        // The menu should display shortcut indicators for all file commands
        DriverAssert.ContentsContains (fx.Driver, "Ctrl+N");
        DriverAssert.ContentsContains (fx.Driver, "Ctrl+O");
        DriverAssert.ContentsContains (fx.Driver, "Ctrl+S");
        DriverAssert.ContentsContains (fx.Driver, "Ctrl+Shift+S");
    }

    [Fact]
    public async Task FileMenu_Shortcuts_Snapshot ()
    {
        await using AppFixture<TedApp> fx = new (() => new TedApp (configPath: TedTestConfig.NewPath ()), 60, 12);

        // Open File menu via Alt+F
        fx.Injector.InjectKey (Key.F.WithAlt, Direct);
        fx.Render ();

        AnsiSnapshot.Verify (fx.Driver, nameof (FileMenu_Shortcuts_Snapshot));
    }

    [Fact]
    public void SaveAs_Dialog_Title_Uses_Resource_InPortugueseCulture ()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo ("pt-PT");
            CultureInfo.CurrentUICulture = new CultureInfo ("pt-PT");

            using TedApp ted = new ();
            using SaveDialog dialog = ted.CreateSaveDialog ();

            Assert.Equal (Strings.fdSaveAs, dialog.Title);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void SaveAs_Dialog_Title_Uses_Resource ()
    {
        // Verify that CreateSaveDialog (used by ShowDefaultSaveDialog) produces a dialog
        // with the expected title. This catches regressions if someone removes or changes
        // the Title assignment.
        using TedApp ted = new ();
        using SaveDialog dialog = ted.CreateSaveDialog ();

        Assert.Equal (Strings.fdSaveAs, dialog.Title);
        Assert.False (dialog.AllowsMultipleSelection);
        Assert.Equal (OpenMode.File, dialog.OpenMode);
    }
}
