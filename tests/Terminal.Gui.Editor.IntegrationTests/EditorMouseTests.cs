// Claude - claude-opus-4-7
using System.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Mouse-driven caret placement and selection. Click moves the caret; click+drag selects a range;
///     Shift+click extends the existing selection (or starts one anchored at the prior caret).
/// </summary>
public class EditorMouseTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };
    private static readonly DateTime BaseTime = new (2025, 1, 1, 12, 0, 0);

    [Fact]
    public async Task LeftClick_Places_Caret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        InjectClick (fx, new (5, 0));

        Assert.Equal (5, fx.Top.Editor.CaretOffset);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task LeftClick_Past_LineEnd_Snaps_To_LineEnd ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();

        InjectClick (fx, new (50, 0));

        Assert.Equal (3, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task LeftClick_Below_LastLine_Snaps_To_LastLine ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta"));
        fx.Top.Editor.SetFocus ();

        // Row 5 is below "beta" (line index 1) but still inside the 24-row viewport. Mouse events
        // outside the view's frame don't get routed to it, so we stay within bounds.
        InjectClick (fx, new (2, 5));

        // Last line is "beta" starting at offset 6; col 2 within it → offset 8.
        Assert.Equal (8, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task LeftClick_Clears_Existing_Selection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectAll ();

        InjectClick (fx, new (2, 0));

        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task LeftDrag_Creates_Selection_From_Press_Point ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();

        // Press at col 2.
        fx.Injector.InjectMouse (
            new () { ScreenPosition = new (2, 0), Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);

        // Drag to col 7 (LeftButtonPressed | PositionReport).
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (7, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.SelectionStart);
        Assert.Equal (7, fx.Top.Editor.SelectionEnd);
        Assert.Equal (7, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task LeftDrag_Backwards_Selects_From_End_To_Start ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new () { ScreenPosition = new (8, 0), Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (3, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (3, fx.Top.Editor.SelectionStart);
        Assert.Equal (8, fx.Top.Editor.SelectionEnd);
        Assert.Equal (3, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftClick_Extends_Selection_From_Caret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (8, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Shift,
                Timestamp = BaseTime
            },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (2, fx.Top.Editor.SelectionStart);
        Assert.Equal (8, fx.Top.Editor.SelectionEnd);
    }

    [Fact]
    public async Task LeftClick_Across_Lines_Picks_Correct_Offset ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Top.Editor.SetFocus ();

        // Click on line 1 (0-indexed), col 2 — within "beta".
        InjectClick (fx, new (2, 1));

        // "alpha\n".Length + 2 = 6 + 2 = 8
        Assert.Equal (8, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task LeftClick_Inside_TabExpansion_Snaps_To_Nearest_Tab_Edge ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("a\tb"));
        fx.Top.Editor.SetFocus ();

        InjectClick (fx, new (2, 0));

        Assert.Equal (1, fx.Top.Editor.CaretOffset);

        InjectClick (fx, new (3, 0));

        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task ShiftClick_At_Current_Caret_Does_Not_Fire_SelectionChanged ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;
        int fires = 0;
        fx.Top.Editor.SelectionChanged += (_, _) => fires++;

        // Shift+click at the same column as the existing caret — neither caret nor selection
        // range actually changes, so SelectionChanged must not fire.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (5, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Shift,
                Timestamp = BaseTime
            },
            Direct);

        Assert.Equal (0, fires);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    private static void InjectClick (AppFixture<EditorTestHost> fx, Point pos)
    {
        fx.Injector.InjectMouse (
            new () { ScreenPosition = pos, Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);
    }
}
