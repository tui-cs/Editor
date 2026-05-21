// Copilot - claude-opus-4-6

using Terminal.Gui.App;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     ANSI snapshot tests for <see cref="FindReplaceDialog" /> layout and usability.
///     Verifies the dialog's visual appearance — tabs, checkboxes, hotkeys, sizing.
/// </summary>
public sealed class FindReplaceDialogTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task FindDialog_Shows_Tabs_And_Checkboxes_Below ()
    {
        await using AppFixture<EditorTestHost> fx = new (
            () => new EditorTestHost ("hello world\nfoo bar"),
            60, 20);

        // Open Find dialog via the Editor's FindRequested event path — but since we can't
        // run a nested modal in tests, construct the dialog directly and Begin it.
        using FindReplaceDialog dialog = new (fx.Top.Editor, false);
        SessionToken? session = fx.App.Begin (dialog);

        try
        {
            fx.Render ();
            AnsiSnapshot.Verify (fx.Driver, nameof (FindDialog_Shows_Tabs_And_Checkboxes_Below));
        }
        finally
        {
            fx.App.End (session!);
        }
    }

    [Fact]
    public async Task ReplaceDialog_Shows_Tabs_And_Checkboxes_Below ()
    {
        await using AppFixture<EditorTestHost> fx = new (
            () => new EditorTestHost ("hello world\nfoo bar"),
            60, 20);

        using FindReplaceDialog dialog = new (fx.Top.Editor, true);
        SessionToken? session = fx.App.Begin (dialog);

        try
        {
            fx.Render ();
            AnsiSnapshot.Verify (fx.Driver, nameof (ReplaceDialog_Shows_Tabs_And_Checkboxes_Below));
        }
        finally
        {
            fx.App.End (session!);
        }
    }

    [Fact]
    public async Task FindDialog_Enter_Triggers_FindNext ()
    {
        await using AppFixture<EditorTestHost> fx = new (
            () => new EditorTestHost ("hello world hello"),
            60, 20);

        fx.Top.Editor.SetFocus ();
        fx.Render ();

        using FindReplaceDialog dialog = new (fx.Top.Editor, false);
        SessionToken? session = fx.App.Begin (dialog);

        try
        {
            // Type search text and press Enter
            fx.Injector.InjectKey (Key.H, Direct);
            fx.Injector.InjectKey (Key.E, Direct);
            fx.Injector.InjectKey (Key.L, Direct);
            fx.Injector.InjectKey (Key.L, Direct);
            fx.Injector.InjectKey (Key.O, Direct);
            fx.Injector.InjectKey (Key.Enter, Direct);
            fx.Render ();

            // The Editor should have found and selected "hello"
            Assert.True (fx.Top.Editor.HasSelection);
            Assert.Equal (5, fx.Top.Editor.SelectionLength);
        }
        finally
        {
            fx.App.End (session!);
        }
    }

    [Fact]
    public async Task FindDialog_Title_Has_No_Hotkey ()
    {
        await using AppFixture<EditorTestHost> fx = new (
            () => new EditorTestHost ("test"),
            60, 20);

        using FindReplaceDialog dialog = new (fx.Top.Editor, false);
        SessionToken? session = fx.App.Begin (dialog);

        try
        {
            fx.Render ();

            // Title should render as "Find / Replace" without underscore-prefixed hotkey
            DriverAssert.ContentsContains (fx.Driver, "Find / Replace");
        }
        finally
        {
            fx.App.End (session!);
        }
    }
}
