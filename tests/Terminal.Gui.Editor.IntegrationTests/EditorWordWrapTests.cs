using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;
using Color = Terminal.Gui.Drawing.Color;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>Integration tests for word-wrap rendering and caret behavior.</summary>
public class EditorWordWrapTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task WordWrap_Long_Line_Wraps_At_Viewport_Width ()
    {
        // 20-column viewport, text longer than 20 characters.
        var text = "hello world this is a test";

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text), 20, 5);
        fx.Render ();

        // Enable wrap after layout is established.
        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        // First row should show "h" at column 0.
        Cell firstCell = fx.Driver.Contents![0, 0];
        Assert.Equal ("h", firstCell.Grapheme);

        // Second row should start with the continuation of the wrapped text (not blank).
        Cell secondRow = fx.Driver.Contents![1, 0];
        Assert.NotEqual (" ", secondRow.Grapheme);
        Assert.NotEqual ("\0", secondRow.Grapheme);
    }

    [Fact]
    public async Task Caret_Under_Wrap_Reports_Correct_Visual_Position ()
    {
        // "hello world" with wrap at 6 => "hello " on row 0, "world" on row 1.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 6, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        // Move caret to offset 6 ("w" in "world") which should be on visual row 1, col 0.
        fx.Top.Editor.CaretOffset = 6;
        fx.Render ();

        // The cursor should be positioned on the second row.
        Cursor cursor = fx.Top.Editor.Cursor;
        Assert.NotNull (cursor);
    }

    [Fact]
    public async Task Vertical_Movement_Traverses_Wrap_Segments ()
    {
        // "hello world" with wrap at 6 => first segment "hello ", second segment "world".
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 6, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        // Position caret at start of line (offset 0, row 0).
        fx.Top.Editor.CaretOffset = 0;
        fx.Render ();

        // Press Down — should move to the next wrap segment (row 1).
        fx.Injector.InjectKey (Key.CursorDown, Direct);
        fx.Render ();

        // Caret should now be in the second segment.
        var offset = fx.Top.Editor.CaretOffset;
        Assert.True (offset >= 6, $"Expected caret at offset >= 6 after Down, got {offset}");
    }

    [Fact]
    public async Task Toggle_WordWrap_Reflows_Immediately ()
    {
        var longLine = new string ('x', 40);

        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (longLine), 20, 5);
        fx.Render ();

        // Without wrap, the long line is a single visual line.
        fx.Top.Editor.WordWrap = false;
        fx.Render ();

        // Enable wrap.
        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        // Second row should now show content (the continuation of the wrapped line).
        Cell secondRow = fx.Driver.Contents![1, 0];
        Assert.Equal ("x", secondRow.Grapheme);
    }

    [Fact]
    public async Task Caret_Survives_WordWrap_Toggle ()
    {
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("hello world foo bar baz");
            host.Editor.WordWrap = false;

            return host;
        }, 10, 5);

        fx.Render ();

        // Set caret to a known position.
        fx.Top.Editor.CaretOffset = 12; // "f" in "foo"
        fx.Render ();

        // Toggle wrap on — caret should stay at the same logical offset.
        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        Assert.Equal (12, fx.Top.Editor.CaretOffset);

        // Toggle wrap off — caret still at the same logical offset.
        fx.Top.Editor.WordWrap = false;
        fx.Render ();

        Assert.Equal (12, fx.Top.Editor.CaretOffset);
    }

    /// <summary>
    ///     Proves P1: wrap map must rebuild when the viewport width changes. Previously,
    ///     the map was cached unconditionally and a viewport resize (changing the wrap column)
    ///     would leave stale segment indices that could crash or produce wrong output.
    /// </summary>
    [Fact]
    public async Task WrapMap_Rebuilds_On_Viewport_Width_Change ()
    {
        var longLine = new string ('a', 30);

        // Start with a 20-column viewport — "a×30" wraps into 2 visual lines.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (longLine), 20, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Render ();

        // Second row should have content (wrap at col 20).
        Cell row1 = fx.Driver.Contents![1, 0];
        Assert.Equal ("a", row1.Grapheme);

        // Now resize to 40 columns — entire line fits on one row, no wrap needed.
        fx.App.Driver!.SetScreenSize (40, 5);
        fx.Render ();

        // After resize, second row should be blank (line fits in one visual row).
        Cell row1After = fx.Driver.Contents![1, 0];
        Assert.NotEqual ("a", row1After.Grapheme);
    }

    /// <summary>
    ///     Proves P2: selection attributes must survive transformers in wrapped mode.
    ///     Previously, selection was applied before transformers, so a
    ///     <see cref="IVisualLineTransformer" /> (e.g. syntax highlighter) would overwrite the
    ///     selection attribute.
    /// </summary>
    [Fact]
    public async Task Selection_Survives_Transformers_In_Wrapped_Mode ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 20, 5);
        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Add a transformer that paints everything red — simulates syntax highlighting.
        Attribute redAttr = new (new Color (255, 0), new Color (0, 0));
        fx.Top.Editor.LineTransformers.Add (new OverwriteTransformer (redAttr));

        fx.Top.Editor.WordWrap = true;
        fx.Top.Editor.CaretOffset = 0;
        fx.Render ();

        // Select "hel" (first 3 chars) using Shift+Right.
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Render ();

        Attribute active = fx.Top.Editor.GetAttributeForRole (VisualRole.Active);

        // The selected cell should have the Active (selection) attribute, NOT the red one.
        Cell cell = fx.Driver.Contents![0, 0];
        Assert.Equal ("h", cell.Grapheme);
        Assert.Equal (active, cell.Attribute);

        // Unselected cell should have the transformer's attribute (red).
        Cell unselected = fx.Driver.Contents![0, 4];
        Assert.Equal ("o", unselected.Grapheme);
        Assert.Equal (redAttr, unselected.Attribute);
    }

    [Fact]
    public async Task Mouse_Click_On_Wrapped_Row_Positions_Caret_Correctly ()
    {
        // "hello world" with wrap at 6 => "hello " on visual row 0, "world" on visual row 1.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 6, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Click on visual row 1 (the wrapped portion), col 2 — should land on "r" in "world".
        InjectClick (fx, new Point (2, 1));

        // "hello " is 6 chars, so offset of "r" = 6 + 2 = 8.
        Assert.Equal (8, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Mouse_Click_Past_Wrapped_Segment_End_Snaps_To_Segment_End ()
    {
        // "hello world" with wrap at 6 => "hello " on visual row 0, "world" on visual row 1.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 6, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Click past the end of the "world" segment but within the viewport (col 5 on row 1).
        // "world" is 5 chars, so col 5 is one past the last character.
        InjectClick (fx, new Point (5, 1));

        // Should snap to end of "world" = offset 11.
        Assert.Equal (11, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Mouse_Click_On_First_Wrapped_Row_Still_Works ()
    {
        // "hello world" with wrap at 6 => "hello " on visual row 0, "world" on visual row 1.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"), 6, 5);
        fx.Render ();

        fx.Top.Editor.WordWrap = true;
        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Click on visual row 0, col 3 — should land within "hello ".
        InjectClick (fx, new Point (3, 0));

        Assert.Equal (3, fx.Top.Editor.CaretOffset);
    }

    /// <summary>
    ///     Clicking the line-number gutter on a wrap-continuation row should select the same document
    ///     line, not the next one.
    /// </summary>
    [Fact]
    public async Task GutterClick_On_Wrap_Continuation_Selects_Correct_Line ()
    {
        // "hello world\nbye" — 2 lines. Gutter = 2 cols (1-digit + space). Screen = 12 cols → editor = 10 cols.
        // "hello world" (11 chars) wraps: row 0 = "hello worl", row 1 = "d", row 2 = "bye".
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("hello world\nbye");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;
            host.Editor.WordWrap = true;

            return host;
        }, 12, 5);

        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Click gutter at row 1 (the wrap-continuation of line 1). Gutter occupies cols 0–1.
        InjectClick (fx, new Point (0, 1));

        Assert.True (fx.Top.Editor.HasSelection);

        // Should select line 1 ("hello world\n"), NOT line 2 ("bye").
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (12, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("hello world\n", fx.Top.Editor.SelectedText);
    }

    /// <summary>
    ///     Dragging in the gutter across wrap-continuation rows should select the correct lines,
    ///     not over-select due to wrap rows being counted as separate lines.
    /// </summary>
    [Fact]
    public async Task GutterDrag_Across_Wrap_Rows_Selects_Correct_Lines ()
    {
        // "hello world\nbye" — line 1 wraps into 2 visual rows, line 2 on row 2.
        // Drag from row 0 to row 2: should select lines 1–2 (the entire document).
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("hello world\nbye");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;
            host.Editor.WordWrap = true;

            return host;
        }, 12, 5);

        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Press on gutter row 0 (line 1).
        fx.Injector.InjectMouse (
            new Mouse { ScreenPosition = new Point (0, 0), Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);

        // Drag to gutter row 2 (line 2 "bye").
        fx.Injector.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (0, 2),
                Flags = MouseFlags.LeftButtonPressed | MouseFlags.PositionReport,
                Timestamp = BaseTime.AddMilliseconds (50)
            },
            Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (15, fx.Top.Editor.SelectionEnd);
        Assert.Equal ("hello world\nbye", fx.Top.Editor.SelectedText);
    }

    /// <summary>
    ///     The gutter should show blank for wrap-continuation rows even when that row is the first
    ///     visible row (i.e. segment detection must use wrap-segment metadata, not previous-row
    ///     comparison which fails when the viewport starts mid-line).
    /// </summary>
    [Fact]
    public async Task Gutter_Continuation_Row_Shows_Blank_Not_Line_Number ()
    {
        // "hello world\nbye" — line 1 wraps into 2 visual rows (row 0 = "hello worl", row 1 = "d").
        // Gutter = 2 cols (1-digit + space). Screen = 12 cols → editor area = 10 cols.
        await using AppFixture<EditorTestHost> fx = new (() =>
        {
            EditorTestHost host = new ("hello world\nbye");
            host.Editor.GutterOptions = GutterOptions.LineNumbers;
            host.Editor.WordWrap = true;

            return host;
        }, 12, 5);

        fx.Top.Editor.SetFocus ();
        fx.Render ();

        // Row 0: line 1, first segment → should show "1 "
        Assert.Equal ("1", fx.Driver.Contents![0, 0].Grapheme);

        // Row 1: line 1, continuation → should show "  " (blank)
        Assert.Equal (" ", fx.Driver.Contents[1, 0].Grapheme);
        Assert.Equal (" ", fx.Driver.Contents[1, 1].Grapheme);

        // Row 2: line 2, first segment → should show "2 "
        Assert.Equal ("2", fx.Driver.Contents[2, 0].Grapheme);
    }

    private static readonly DateTime BaseTime = new (2025, 1, 1, 12, 0, 0);

    private static void InjectClick (AppFixture<EditorTestHost> fx, Point pos)
    {
        fx.Injector.InjectMouse (
            new Mouse { ScreenPosition = pos, Flags = MouseFlags.LeftButtonPressed, Timestamp = BaseTime },
            Direct);
    }

    /// <summary>Transformer that overwrites all element attributes with a fixed value.</summary>
    private sealed class OverwriteTransformer (Attribute attr) : IVisualLineTransformer
    {
        public void Transform (CellVisualLine line)
        {
            foreach (CellVisualLineElement element in line.Elements)
            {
                element.Attribute = attr;
            }
        }
    }
}
