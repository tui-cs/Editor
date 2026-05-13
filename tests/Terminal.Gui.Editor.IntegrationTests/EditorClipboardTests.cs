// Claude - claude-sonnet-4

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

    [Fact]
    public async Task Copy_Paste_RoundTrip ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
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
    public async Task Copy_NoSelection_IsNoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        // Copy with no selection — should be a no-op (nothing on clipboard).
        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        // Paste should have nothing to paste.
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Cut_RemovesSelection_And_SingleUndo ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        // Cut with no selection — no-op.
        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Paste_ReplacesSelection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello world"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("secret"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("original"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("secret"));
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
        await using AppFixture<EditorTestHost> fx = new (() => new ("AB"));
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
}
