// Claude - claude-opus-4-6

using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for Tab key, Shift+Tab, block indent/unindent, and tab rendering per issue #37.
/// </summary>
public class EditorTabKeyTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task Tab_Inserts_Tab_Character_When_ConvertTabsToSpaces_Is_False ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("a\tbc", fx.Top.Editor.Document?.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_Inserts_Spaces_When_ConvertTabsToSpaces_Is_True ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ConvertTabsToSpaces = true;
        fx.Top.Editor.IndentationSize = 4;
        fx.Top.Editor.CaretOffset = 1; // visual column 1 → next tab stop at 4 → 3 spaces

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("a   bc", fx.Top.Editor.Document?.Text);
        Assert.Equal (4, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_Inserts_Full_IndentationSize_Spaces_At_TabStop ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ConvertTabsToSpaces = true;
        fx.Top.Editor.IndentationSize = 4;
        fx.Top.Editor.CaretOffset = 0; // visual column 0 → at tab stop → full 4 spaces

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("    abc", fx.Top.Editor.Document?.Text);
        Assert.Equal (4, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftTab_Unindents_Tab_Character ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("\tline"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1; // after the tab

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("line", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task ShiftTab_Unindents_Spaces ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("    line"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.IndentationSize = 4;
        fx.Top.Editor.CaretOffset = 4;

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("line", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task ShiftTab_NoOp_On_Unindented_Line ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("line"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("line", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Tab_With_MultiLine_Selection_Indents_Block ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("line1\nline2\nline3"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // Select the first two lines: shift+down, then shift+down
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        var text = fx.Top.Editor.Document?.Text ?? "";

        // All three lines should be indented by one tab
        Assert.StartsWith ("\tline1\n\tline2\n\tline3", text);
    }

    [Fact]
    public async Task ShiftTab_With_MultiLine_Selection_Unindents_Block ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("\tline1\n\tline2\n\tline3"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // Select all three lines
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        fx.Injector.InjectKey (Key.End.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("line1\nline2\nline3", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Tab_Block_Indent_Uses_Spaces_When_ConvertTabsToSpaces ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\nb\nc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ConvertTabsToSpaces = true;
        fx.Top.Editor.IndentationSize = 2;
        fx.Top.Editor.CaretOffset = 0;

        // Select two lines
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        var text = fx.Top.Editor.Document?.Text ?? "";
        Assert.StartsWith ("  a\n  b", text);
    }

    [Fact]
    public async Task Block_Indent_Collapses_To_One_Undo_Step ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\nb\nc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // Select all lines
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithShift, Direct);
        fx.Injector.InjectKey (Key.End.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        // All three lines indented
        Assert.Contains ("\ta", fx.Top.Editor.Document?.Text ?? "");

        // Single undo should revert the whole block indent
        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("a\nb\nc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Tab_Replaces_Selection_When_SingleLine ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcdef"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        // Select "bc" (2 chars)
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);

        fx.Injector.InjectKey (Key.Tab, Direct);

        // Selection should be deleted, then tab inserted at resulting caret
        Assert.Equal ("a\tdef", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Tabs_Render_As_Spaces_Using_Default_IndentationSize ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
        fx.Render ();

        Assert.Equal ("a", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);
        Assert.Equal ("b", fx.Driver.Contents![0, 4].Grapheme);
    }

    [Fact]
    public async Task ShowTabs_Renders_Tab_Glyph ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("\tx"));
        fx.Top.Editor.ShowTabs = true;
        fx.Render ();

        // ShowTabs renders '→' in the first cell of the tab expansion
        Assert.Equal ("→", fx.Driver.Contents![0, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 1].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 2].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents![0, 3].Grapheme);
        Assert.Equal ("x", fx.Driver.Contents![0, 4].Grapheme);
    }
}
