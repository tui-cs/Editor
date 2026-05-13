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
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\t", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_Inserts_Spaces_When_ConvertTabsToSpaces_Is_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("a"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("one\ntwo"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("    alpha"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4;

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("alpha", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftTab_On_Multiline_Selection_Unindents_Block ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("\tone\n    two"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("one\ntwo", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (7, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task Tab_On_Selection_Within_Single_Line_Replaces_With_Tab ()
    {
        // Selection that stays on one line must use single-line tab behavior (replace
        // selection with tab) rather than block-indent. This exercises the optimized
        // SelectionSpansMultipleLines path that avoids allocating a List<DocumentLine>.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("one\ntwo"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // Select "one" (offsets 0–3) via Shift+End
        fx.Injector.InjectKey (Key.End.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\t\ntwo", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task Tab_On_Selection_Spanning_Two_Lines_Indents_Both ()
    {
        // Ensures the multi-line detection works: selection from line 1 into line 2
        // must trigger block-indent, not replacement.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("aaa\nbbb"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // Select across both lines: Shift+Down moves caret to line 2 col 0
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        // Then Shift+End to extend to end of line 2
        fx.Injector.InjectKey (Key.End.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("\taaa\n\tbbb", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task Backspace_At_End_Of_Leading_Whitespace_Removes_One_Indentation_Unit ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("    alpha"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4;

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal ("alpha", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }
}
