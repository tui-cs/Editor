// Claude - claude-sonnet-4

using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Clipboard integration tests — Cut, Copy, Paste via the <see cref="Editor" /> commands.
///     Each test boots an <see cref="EditorTestHost" /> so <c>App.Clipboard</c> is available.
/// </summary>
public class EditorClipboardTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    /// <summary>
    ///     Ensures the fixture's driver has a working in-memory clipboard regardless of platform.
    ///     On macOS/Windows CI the system clipboard is not available in headless mode.
    /// </summary>
    private static void EnsureFakeClipboard (AppFixture<EditorTestHost> fx)
    {
        fx.Driver.Clipboard = new FakeClipboard ();
    }

    [Fact]
    public async Task Copy_Paste_RoundTrip ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Select "hello"
        fx.Top.Editor.SelectRange (0, 5);

        // Ctrl+C
        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Move caret to end
        fx.Top.Editor.CaretOffset = 11;
        fx.Top.Editor.ClearSelection ();

        // Ctrl+V
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("hello worldhello", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Copy_NoSelection_Copies_Current_Line ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        // Copy with no selection — copies the current line (line-copy behaviour).
        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Clipboard should contain the entire line (single-line doc has no delimiter, so just "abc").
        Assert.True (fx.App.Clipboard!.TryGetClipboardData (out var clip));
        Assert.Equal ("abc", clip);
    }

    [Fact]
    public async Task Cut_RemovesSelection_And_SingleUndo ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Select "hello"
        fx.Top.Editor.SelectRange (0, 5);

        // Ctrl+X
        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal (" world", fx.Top.Editor.Document?.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        // Single Ctrl+Z should restore
        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Cut_NoSelection_IsNoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        // Cut with no selection — no-op.
        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Paste_ReplacesSelection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Copy "hello"
        fx.Top.Editor.SelectRange (0, 5);
        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Select "world"
        fx.Top.Editor.SelectRange (6, 5);

        // Paste — should replace "world" with "hello"
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("hello hello", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Paste_MultiLine_PreservesLineEndings ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Put multi-line text on clipboard
        fx.App.Clipboard?.TrySetClipboardData ("line1\nline2\r\nline3");

        // Paste at start
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("line1\nline2\r\nline3abc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task ReadOnly_Blocks_Cut ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("secret"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;

        fx.Top.Editor.SelectRange (0, 6);
        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        // Document unchanged
        Assert.Equal ("secret", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task ReadOnly_Blocks_Paste ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("original"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;

        fx.App.Clipboard?.TrySetClipboardData ("injected");
        fx.Top.Editor.CaretOffset = 0;
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("original", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task ReadOnly_Allows_Copy ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("secret"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;

        fx.Top.Editor.SelectRange (0, 6);
        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Verify clipboard has the text
        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("secret", data);
    }

    [Fact]
    public async Task Paste_SingleUndoStep ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("AB"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Select "AB" and paste multi-line content
        fx.Top.Editor.SelectRange (0, 2);
        fx.App.Clipboard?.TrySetClipboardData ("XY\nZ");
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("XY\nZ", fx.Top.Editor.Document?.Text);

        // Single undo should restore "AB"
        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("AB", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Cut_NoOp_When_Clipboard_Unavailable ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));

        // Use a FakeClipboard that reports unsupported — simulates clipboard unavailability.
        fx.Driver.Clipboard = new FakeClipboard (false, true);
        fx.Top.Editor.SetFocus ();

        fx.Top.Editor.SelectRange (0, 5);
        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        // Text must be preserved because the clipboard write would have failed.
        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    /// <summary>
    ///     Issue #162: In Multiline=true mode, Copy with no selection should copy the current
    ///     line including its newline delimiter.
    /// </summary>
    [Fact]
    public async Task Copy_NoSelection_Copies_Current_Line_With_Newline ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("alpha\nbeta\ngamma"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();

        // Place caret on line 2 ("beta\n") with no selection.
        fx.Top.Editor.CaretOffset = 7; // inside "beta"
        Assert.False (fx.Top.Editor.HasSelection);

        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Should copy "beta\n" (the whole line including its delimiter).
        Assert.True (fx.App.Clipboard!.TryGetClipboardData (out var clip));
        Assert.Equal ("beta\n", clip);
    }

    /// <summary>
    ///     Issue #162: In Multiline=false mode, selecting the ⏎ glyph with Shift+Right and
    ///     copying should place the newline character(s) on the clipboard.
    /// </summary>
    [Fact]
    public async Task Copy_Selected_Newline_Glyph_In_SingleLine_Mode ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("this\nthat"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.Multiline = false;
        fx.Top.Editor.SetFocus ();

        // Move caret to end of "this" (offset 4), then Shift+Right to select the newline.
        fx.Top.Editor.CaretOffset = 4;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal ("\n", fx.Top.Editor.SelectedText);

        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        Assert.True (fx.App.Clipboard!.TryGetClipboardData (out var clip));
        Assert.Equal ("\n", clip);
    }
}
