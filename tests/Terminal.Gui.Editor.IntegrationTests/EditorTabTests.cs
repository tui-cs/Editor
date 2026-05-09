// Codex - GPT-5

using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

public class EditorTabTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task Tab_Inserts_Tab_Character_By_Default ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ());
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\t", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_Inserts_Spaces_When_ConvertTabsToSpaces_Is_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;
        fx.Top.Editor.ConvertTabsToSpaces = true;

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("a   ", fx.Top.Editor.Document!.Text);
        Assert.Equal (4, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_On_Multiline_Selection_Indents_Block_In_One_Undo_Step ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("one\ntwo"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\tone\n\ttwo", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.SelectionStart);
        Assert.Equal (9, fx.Top.Editor.SelectionEnd);

        fx.Top.Editor.Document.UndoStack.Undo ();

        Assert.Equal ("one\ntwo", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task ShiftTab_Unindents_Current_Line ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("    alpha"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4;

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("alpha", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftTab_On_Multiline_Selection_Unindents_Block ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("\tone\n    two"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("one\ntwo", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (7, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task Backspace_At_End_Of_Leading_Whitespace_Removes_One_Indentation_Unit ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("    alpha"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4;

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal ("alpha", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }
}
