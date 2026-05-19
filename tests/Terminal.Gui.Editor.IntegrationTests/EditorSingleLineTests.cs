// Copilot - claude-sonnet-4

using Terminal.Gui.Document.Folding;
using Terminal.Gui.Drivers;
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
    public async Task SingleLine_Height_Defaults_To_DimAuto ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Render ();

        // Multiline=false sets Height to Dim.Auto(Content), so no explicit Height=1 needed.
        Assert.Equal (1, fx.Top.Editor.Frame.Height);
    }

    [Fact]
    public async Task SingleLine_Enter_Raises_Accept ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();

        var accepted = false;
        fx.Top.Editor.Accepting += (_, _) => accepted = true;

        fx.Injector.InjectKey (Key.Enter, Direct);

        Assert.True (accepted);
        // Document is still unchanged (no newline inserted).
        Assert.Equal ("hello", fx.Top.Editor.Document!.Text);
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

    [Fact]
    public async Task SingleLine_Paste_Strips_Newlines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab"));
        fx.Driver.Clipboard = new FakeClipboard (false, false);
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2;

        fx.App.Clipboard!.TrySetClipboardData ("cd\nef\r\ngh");
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        // Paste still strips newlines so new content stays on one logical line.
        Assert.Equal ("abcdefgh", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.Document.LineCount);
    }

    [Fact]
    public async Task SingleLine_Transition_Preserves_Newlines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("line1\nline2\nline3"));
        Assert.Equal (3, fx.Top.Editor.Document!.LineCount);

        fx.Top.Editor.Multiline = false;

        // Newlines are preserved — no data loss.
        Assert.Equal ("line1\nline2\nline3", fx.Top.Editor.Document.Text);
        Assert.Equal (3, fx.Top.Editor.Document.LineCount);

        // Content size height is still 1 (single visual row).
        fx.Render ();
        Assert.Equal (1, fx.Top.Editor.GetContentSize ().Height);
    }

    [Fact]
    public async Task SingleLine_Home_End_Navigate_To_Document_Bounds ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab\ncd\nef"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();

        // Place caret in the middle of line 2.
        fx.Top.Editor.CaretOffset = 4; // 'c' on line 2

        fx.Injector.InjectKey (Key.Home, Direct);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        fx.Top.Editor.CaretOffset = 4;
        fx.Injector.InjectKey (Key.End, Direct);
        Assert.Equal (8, fx.Top.Editor.CaretOffset); // end of "ef"
    }

    /// <summary>
    ///     CR1: Vertical scroll command should return true (handled) in single-line mode so the
    ///     event doesn't bubble to parent containers.
    /// </summary>
    [Fact]
    public async Task SingleLine_ScrollVertical_Is_Handled_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();

        // Scroll commands should not change caret position and should be handled (not bubble).
        // Viewport.Y should remain 0 since there's only one row.
        fx.Top.Editor.CaretOffset = 2;
        fx.Injector.InjectKey (Key.CursorUp.WithCtrl, Direct); // typically triggers scroll
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
        Assert.Equal (0, fx.Top.Editor.Viewport.Y);
    }

    /// <summary>
    ///     CR2/CR3/CR5: Folded lines should be excluded from the single-line flat view. When lines
    ///     are hidden by folding, the flat visual width and rendering should skip them.
    /// </summary>
    [Fact]
    public async Task SingleLine_Folding_Hides_Lines_From_Flat_View ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab\ncd\nef"));
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();

        // Install a FoldingManager so we can fold lines.
        fx.Top.Editor.FoldingManager = new FoldingManager (fx.Top.Editor.Document!);
        fx.Render ();

        // Full flat width: "ab" (2) + ⏎ (1) + "cd" (2) + ⏎ (1) + "ef" (2) = 8 plus +1 for caret-past-end = 9
        Assert.Equal (9, fx.Top.Editor.GetContentSize ().Width);

        // CreateFolding spanning from start of "ab" to end of "cd\n" hides line 2.
        FoldingSection fold = fx.Top.Editor.FoldingManager!.CreateFolding (
            0, fx.Top.Editor.Document!.GetLineByNumber (2).EndOffset);
        fold.IsFolded = true;

        // Toggling a property that clears caches to force re-computation
        fx.Top.Editor.SetNeedsDraw ();
        fx.Render ();

        // After folding, UpdateContentSize skips hidden lines, reducing the width.
        int widthAfterFold = fx.Top.Editor.GetContentSize ().Width;
        Assert.True (widthAfterFold < 9,
            $"Content width after fold should decrease from 9, was {widthAfterFold}");
    }
}
