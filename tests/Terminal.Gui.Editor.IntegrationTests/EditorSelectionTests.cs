// Claude - claude-opus-4-7
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     End-to-end selection behavior driven through TG's keybinding system. Each Shift+key path
///     should turn into the right <c>*Extend</c> command and then into anchor + caret state on
///     the editor.
/// </summary>
public class EditorSelectionTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task ShiftRight_Begins_And_Extends_Selection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (2, fx.Top.Editor.SelectionEnd);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftLeft_Extends_Backwards ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Injector.InjectKey (Key.CursorLeft.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorLeft.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (3, fx.Top.Editor.SelectionStart);
        Assert.Equal (5, fx.Top.Editor.SelectionEnd);
        Assert.Equal (3, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task PlainArrow_Collapses_Selection_To_End ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);

        fx.Injector.InjectKey (Key.CursorRight, Direct);

        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task PlainArrow_Left_Collapses_Selection_To_Start ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);

        fx.Injector.InjectKey (Key.CursorLeft, Direct);

        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Typing_With_Selection_Replaces_It ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("x", fx.Top.Editor.Document!.Text);
        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Backspace_With_Selection_Deletes_Range ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal (string.Empty, fx.Top.Editor.Document!.Text);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task Delete_With_Selection_Deletes_Range ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Delete, Direct);

        Assert.Equal (string.Empty, fx.Top.Editor.Document!.Text);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task Enter_With_Selection_Replaces_With_Newline ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Enter, Direct);

        Assert.Equal ("\n", fx.Top.Editor.Document!.Text);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task CtrlA_Selects_All ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.Injector.InjectKey (Key.A.WithCtrl, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (5, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task ShiftHome_Extends_To_Line_Start ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("first\nsecond line"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = "first\nsecond line".Length;

        fx.Injector.InjectKey (Key.Home.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal ("first\n".Length, fx.Top.Editor.SelectionStart);
    }

    [Fact]
    public async Task ShiftCtrlEnd_Extends_To_DocEnd ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("first\nsecond"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.End.WithCtrl.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (fx.Top.Editor.Document!.TextLength, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task ShiftDown_Extends_Across_Lines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2; // line 1, column 2

        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.SelectionStart);
        // sticky col 2 on line 2 → "alpha\n".Length + 2
        Assert.Equal ("alpha\n".Length + 2, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task ReplaceSelection_Spans_Multiple_Lines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Z, Direct);

        Assert.Equal ("z", fx.Top.Editor.Document!.Text);
    }
}
