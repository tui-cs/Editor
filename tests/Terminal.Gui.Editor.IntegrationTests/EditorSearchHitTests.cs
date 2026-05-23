// Claude - claude-sonnet-4

using Terminal.Gui.Editor.Document.Search;
using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for the search-hit-highlight renderer and find/replace keybindings.
/// </summary>
public class EditorSearchHitTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task SearchHitRenderer_Highlights_Matches_In_Viewport ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Render ();

        Attribute highlight = fx.Top.Editor.GetAttributeForRole (VisualRole.Highlight);

        // First 'hello' at columns 0-4
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("h", cell0.Grapheme);
        Assert.Equal (highlight, cell0.Attribute);

        Cell cell4 = fx.Driver.Contents![0, 4];
        Assert.Equal ("o", cell4.Grapheme);
        Assert.Equal (highlight, cell4.Attribute);

        // 'world' at column 6 should NOT be highlighted
        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cellW = fx.Driver.Contents![0, 6];
        Assert.Equal ("w", cellW.Grapheme);
        Assert.Equal (normal, cellW.Attribute);

        // Second 'hello' at columns 12-16
        Cell cell12 = fx.Driver.Contents![0, 12];
        Assert.Equal ("h", cell12.Grapheme);
        Assert.Equal (highlight, cell12.Attribute);
    }

    [Fact]
    public async Task SearchHitRenderer_Selection_Wins_Over_Highlight ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);

        // Select the first "hello"
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);

        // The selected "hello" should use Active, not Highlight.
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("h", cell0.Grapheme);
        Assert.Equal (active, cell0.Attribute);
    }

    [Fact]
    public async Task SearchHitRenderer_Partial_Selection_Overlap_Still_Uses_Active ()
    {
        // "hello" is a search hit at offset 0-4. Select only "hel" (offset 0-2).
        // The selected portion must still show Active, not Highlight.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);

        // Select "hel" (first 3 chars)
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);
        Attribute highlight = fx.Top.Editor.GetAttributeForRole (VisualRole.Highlight);

        // "h" at column 0 — inside selection → Active
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("h", cell0.Grapheme);
        Assert.Equal (active, cell0.Attribute);

        // "l" at column 2 — inside selection → Active
        Cell cell2 = fx.Driver.Contents![0, 2];
        Assert.Equal ("l", cell2.Grapheme);
        Assert.Equal (active, cell2.Attribute);

        // "l" at column 3 — outside selection but inside search hit → Highlight
        Cell cell3 = fx.Driver.Contents![0, 3];
        Assert.Equal ("l", cell3.Grapheme);
        Assert.Equal (highlight, cell3.Attribute);
    }

    [Fact]
    public async Task SearchHitRenderer_Invalidates_After_Edit ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Render ();

        Attribute highlight = fx.Top.Editor.GetAttributeForRole (VisualRole.Highlight);

        // Verify initial highlight
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal (highlight, cell0.Attribute);

        // Edit: replace 'hello' at start with 'xxxxx' — no longer a match
        fx.Top.Editor.Document!.Replace (0, 5, "xxxxx");
        fx.Render ();

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Cell cellAfterEdit = fx.Driver.Contents![0, 0];
        Assert.Equal ("x", cellAfterEdit.Grapheme);
        Assert.Equal (normal, cellAfterEdit.Attribute);
    }

    [Fact]
    public async Task F3_Triggers_FindNext ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Top.Editor.CaretOffset = 0;

        fx.Injector.InjectKey (Key.F3, Direct);
        fx.Render ();

        // Should select first "hello" (caret at end of match = 5)
        Assert.Equal (5, fx.Top.Editor.CaretOffset);
        Assert.True (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task ShiftF3_Triggers_FindPrevious ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Top.Editor.CaretOffset = 17; // end of text

        fx.Injector.InjectKey (Key.F3.WithShift, Direct);
        fx.Render ();

        // Should select last "hello" (caret at end of second match = 17)
        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (12, fx.Top.Editor.SelectionStart);
    }

    [Fact]
    public async Task CtrlF_Raises_FindRequested ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        fx.Top.Editor.SetFocus ();
        var fired = false;
        fx.Top.Editor.FindRequested += (_, _) => fired = true;

        fx.Injector.InjectKey (Key.F.WithCtrl, Direct);
        fx.Render ();

        Assert.True (fired);
    }

    [Fact]
    public async Task CtrlH_Raises_ReplaceRequested ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        fx.Top.Editor.SetFocus ();
        var fired = false;
        fx.Top.Editor.ReplaceRequested += (_, _) => fired = true;

        fx.Injector.InjectKey (Key.H.WithCtrl, Direct);
        fx.Render ();

        Assert.True (fired);
    }
}
