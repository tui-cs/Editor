// Copilot - claude-sonnet-4

using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for <see cref="Editor.Multiline" /> = <see langword="false" /> (single-line mode).
///     Verifies that newline insertion is suppressed, vertical navigation is constrained,
///     word-wrap is forced off, multi-caret is disabled, and selection + editing still work.
/// </summary>
public class EditorSingleLineTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task SingleLine_Enter_Does_Not_Insert_Newline ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.Enter, Direct);

        Assert.Equal ("ab", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.Document.LineCount);
    }

    [Fact]
    public async Task SingleLine_Up_Down_Are_NoOps ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.Injector.InjectKey (Key.CursorUp, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.CursorDown, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task SingleLine_WordWrap_Cannot_Be_Enabled ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.Multiline = false;

        fx.Top.Editor.WordWrap = true;

        Assert.False (fx.Top.Editor.WordWrap);
    }

    [Fact]
    public async Task SingleLine_Setting_Multiline_False_Disables_WordWrap ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.WordWrap = true;
        Assert.True (fx.Top.Editor.WordWrap);

        fx.Top.Editor.Multiline = false;

        Assert.False (fx.Top.Editor.WordWrap);
    }

    [Fact]
    public async Task SingleLine_MultiCaret_Toggle_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcdef"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Top.Editor.ToggleCaretAt (3);

        Assert.False (fx.Top.Editor.HasMultipleCarets);
    }

    [Fact]
    public async Task SingleLine_Selection_Still_Works ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.A.WithCtrl, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal ("hello", fx.Top.Editor.SelectedText);
    }

    [Fact]
    public async Task SingleLine_Editing_Insert_Delete_Still_Works ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 3;

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal ("ab", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task SingleLine_ContentSize_Height_Is_One ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Render ();

        Assert.Equal (1, fx.Top.Editor.GetContentSize ().Height);
    }

    [Fact]
    public async Task SingleLine_PageUp_PageDown_Are_NoOps ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.Injector.InjectKey (Key.PageUp, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.PageDown, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task SingleLine_Multiline_Default_Is_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));

        Assert.True (fx.Top.Editor.Multiline);
    }

    [Fact]
    public async Task SingleLine_Existing_Carets_Cleared_When_Setting_Multiline_False ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcdef"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.ToggleCaretAt (3);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        fx.Top.Editor.Multiline = false;

        Assert.False (fx.Top.Editor.HasMultipleCarets);
    }

    [Fact]
    public async Task SingleLine_Home_End_Still_Navigate ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Injector.InjectKey (Key.Home, Direct);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.End, Direct);
        Assert.Equal (11, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task SingleLine_VerticalExtend_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.Injector.InjectKey (Key.CursorUp.WithShift, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
        Assert.False (fx.Top.Editor.HasSelection);

        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
        Assert.False (fx.Top.Editor.HasSelection);
    }
}
