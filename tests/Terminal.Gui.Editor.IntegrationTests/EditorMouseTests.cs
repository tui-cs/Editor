// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Editor;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

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
        var fires = 0;
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

    [Fact]
    public async Task DoubleClick_Selects_Word_Under_Cursor ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new () { ScreenPosition = new (1, 0), Flags = MouseFlags.LeftButtonDoubleClicked, Timestamp = BaseTime },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (5, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("hello", fx.Top.Editor.SelectedText);
    }

    [Fact]
    public async Task TripleClick_Selects_Line_Under_Cursor ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta"));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new () { ScreenPosition = new (2, 0), Flags = MouseFlags.LeftButtonTripleClicked, Timestamp = BaseTime },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (6, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("alpha\n", fx.Top.Editor.SelectedText);
    }

    [Fact]
    public async Task GutterClick_Selects_Associated_Line ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("alpha\nbeta\ngamma");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        fx.Top.Editor.SetFocus ();
        InjectClick (fx, new (0, 1));

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (6, fx.Top.Editor.SelectionStart);
        Assert.Equal (11, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("beta\n", fx.Top.Editor.SelectedText);
    }

    [Fact]
    public async Task GutterDrag_Selects_Line_Range ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("alpha\nbeta\ngamma");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;

            return host;
        });

        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new () { ScreenPosition = new (0, 0), Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (0, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (16, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("alpha\nbeta\ngamma", fx.Top.Editor.SelectedText);
    }

    [Fact]
    public async Task CtrlClick_Above_Primary_Adds_Additional_Caret ()
    {
        // Bug: Ctrl+Click above the primary caret failed to add an additional caret because
        // the drag handler (LeftButtonPressed | PositionReport) fired after the press and called
        // ExtendCaretTo, which moved the primary caret instead of preserving the new additional caret.
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 12; // start of "gamma"

        // Ctrl+Click on line 0, col 2 (within "alpha") — above the primary caret.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (2, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Ctrl,
                Timestamp = BaseTime
            },
            Direct);

        // Simulate a PositionReport (micro-drag) at the same position — common during clicks.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (2, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        // The additional caret should exist and the primary should NOT have moved.
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (12, fx.Top.Editor.CaretOffset);
        Assert.Contains (2, fx.Top.Editor.AdditionalCaretOffsets);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task CtrlClick_Below_Primary_Preserves_Primary_Position ()
    {
        // Even clicking below should preserve the primary caret position — the drag handler
        // was moving the primary on every Ctrl+Click.
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0; // start of "alpha"

        // Ctrl+Click on line 2, col 1 (within "gamma") — below the primary caret.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (1, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Ctrl,
                Timestamp = BaseTime
            },
            Direct);

        // Simulate the drag/position-report that comes with click jitter.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (1, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task CtrlClick_First_Slash_Only_Highlights_One_Cell ()
    {
        // Verify that Ctrl+clicking the first '/' of "//" only applies the caret attribute to
        // that one cell, not to adjacent characters. (The visual "bleed" some users see is a
        // font-ligature artifact in the terminal — see README FAQ — not a Cell buffer bug.)
        await using AppFixture<EditorTestHost> fx = new (() => new ("// comment"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5; // primary on 'o'

        // Ctrl+Click on col 0 (first '/').
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (0, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Ctrl,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (0, 0),
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        fx.Render ();

        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Contains (0, fx.Top.Editor.AdditionalCaretOffsets);
        Assert.Equal (5, fx.Top.Editor.CaretOffset); // primary didn't move

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute caretAttr = new (normal.Foreground, normal.Background, TextStyle.Blink | TextStyle.Reverse);

        // Cell 0 (first '/') should have the caret attribute.
        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("/", cell0.Grapheme);
        Assert.Equal (caretAttr, cell0.Attribute);

        // Cell 1 (second '/') must NOT have the caret attribute.
        Cell cell1 = fx.Driver.Contents![0, 1];
        Assert.Equal ("/", cell1.Grapheme);
        Assert.NotEqual (caretAttr, cell1.Attribute);
    }

    [Fact]
    public async Task CtrlClick_First_Slash_Of_TripleSlash_Only_Highlights_One_Cell ()
    {
        // Same as above but for "///".
        await using AppFixture<EditorTestHost> fx = new (() => new ("/// summary"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5; // primary elsewhere

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (0, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Ctrl,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (0, 0),
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        fx.Render ();

        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Contains (0, fx.Top.Editor.AdditionalCaretOffsets);

        Attribute normal = fx.Top.Editor.GetAttributeForRole (VisualRole.Normal);
        Attribute caretAttr = new (normal.Foreground, normal.Background, TextStyle.Blink | TextStyle.Reverse);

        Cell cell0 = fx.Driver.Contents![0, 0];
        Assert.Equal ("/", cell0.Grapheme);
        Assert.Equal (caretAttr, cell0.Attribute);

        Cell cell1 = fx.Driver.Contents![0, 1];
        Assert.Equal ("/", cell1.Grapheme);
        Assert.NotEqual (caretAttr, cell1.Attribute);

        Cell cell2 = fx.Driver.Contents![0, 2];
        Assert.Equal ("/", cell2.Grapheme);
        Assert.NotEqual (caretAttr, cell2.Attribute);
    }

    // ---------------------------------------------------------------------------------------------
    // Vertical multi-caret mouse gestures (specs/vertical-multi-caret/spec.md). Ported from PR #125.
    // Alt + LeftButton drag builds a column of carets — Alt, not VS Code's Shift+Alt, because
    // Windows Terminal reserves Shift+drag for its own forced/block text selection while an app
    // has mouse mode on (DEC-006; configurable Shift+Alt parity tracked by gui-cs/Terminal.Gui#4888).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task AltDrag_Adds_Vertically_Aligned_Carets ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (1, 0),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Alt,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (1, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Alt,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (1, 2),
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (40)
            },
            Direct);

        Assert.Equal (1, fx.Top.Editor.CaretOffset);
        Assert.True (fx.Top.Editor.HasMultipleCarets);
        Assert.Equal (2, fx.Top.Editor.AdditionalCaretOffsets.Count);
        Assert.Contains (6, fx.Top.Editor.AdditionalCaretOffsets);
        Assert.Contains (11, fx.Top.Editor.AdditionalCaretOffsets);
        Assert.False (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task AltDrag_With_Horizontal_Extent_Replaces_Column_On_Type ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();

        InjectAltDrag (fx, new (1, 0), new (3, 2));

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("axd\naxd\naxd", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task AltDrag_Reversed_Column_Replaces_Leftward_Range_On_Type ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();

        InjectAltDrag (fx, new (3, 0), new (1, 2));

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("axd\naxd\naxd", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task AltDrag_Column_Selection_Clamps_Short_Lines_Without_Padding ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\na\nabcd"));
        fx.Top.Editor.SetFocus ();

        InjectAltDrag (fx, new (1, 0), new (4, 2));
        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("ax\nax\nax", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task CtrlClick_After_VerticalCarets_Uses_Click_Position_When_PositionReport_Arrives_First ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abcd\nabcd\nabcd\nabcd"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.CursorDown.WithCtrl.WithAlt, Direct);
        fx.Injector.InjectKey (Key.CursorDown.WithCtrl.WithAlt, Direct);
        Assert.True (fx.Top.Editor.HasMultipleCarets);

        var primaryBefore = fx.Top.Editor.CaretOffset;

        // Some terminals emit PositionReport before the plain LeftButtonPressed during a Ctrl+Click.
        // The pre-press report must not hijack the primary caret.
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (3, 3),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Ctrl,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = new (3, 3),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Ctrl,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        Assert.Equal (primaryBefore, fx.Top.Editor.CaretOffset);
        Assert.Contains ("abcd\nabcd\nabcd\nabc".Length, fx.Top.Editor.AdditionalCaretOffsets);
    }

    private static void InjectClick (AppFixture<EditorTestHost> fx, Point pos)
    {
        fx.Injector.InjectMouse (
            new () { ScreenPosition = pos, Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);
    }

    private static void InjectAltDrag (AppFixture<EditorTestHost> fx, Point press, Point drag)
    {
        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = press,
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.Alt,
                Timestamp = BaseTime
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = drag,
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport | MouseFlags.Alt,
                Timestamp = BaseTime.AddMilliseconds (20)
            },
            Direct);

        fx.Injector.InjectMouse (
            new ()
            {
                ScreenPosition = drag,
                Flags = MouseFlags.LeftButtonReleased,
                Timestamp = BaseTime.AddMilliseconds (40)
            },
            Direct);
    }
}
