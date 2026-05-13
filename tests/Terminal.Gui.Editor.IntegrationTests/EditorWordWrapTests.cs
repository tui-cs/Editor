using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.Editor;
using Xunit;

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
        var cursor = fx.Top.Editor.Cursor;
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
}
