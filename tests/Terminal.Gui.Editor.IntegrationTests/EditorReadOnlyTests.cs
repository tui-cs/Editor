// Codex - GPT-5

using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

public class EditorReadOnlyTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task Typing_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Backspace_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 2);

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Delete_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);

        fx.Injector.InjectKey (Key.Delete, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Enter_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);

        fx.Injector.InjectKey (Key.Enter, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Tab_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);

        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Paste_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);
        fx.Driver.Clipboard = new FakeClipboard ();
        Assert.True (fx.App.Clipboard!.TrySetClipboardData ("XYZ"));

        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Cut_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 0);
        fx.Top.Editor.SelectRange (0, 1);

        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
        Assert.True (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task ShiftTab_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("    abc", 4);

        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.Equal ("    abc", fx.Top.Editor.Document!.Text);
        Assert.Equal (4, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Undo_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 3);
        fx.Top.Editor.ReadOnly = false;
        fx.Top.Editor.Document!.Insert (3, "DEF");
        fx.Top.Editor.ReadOnly = true;

        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("abcDEF", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task Redo_Is_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 3);
        fx.Top.Editor.ReadOnly = false;
        fx.Top.Editor.Document!.Insert (3, "DEF");
        fx.Top.Editor.Document.UndoStack.Undo ();
        fx.Top.Editor.ReadOnly = true;

        fx.Injector.InjectKey (Key.Y.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task Navigation_Still_Works ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 1);

        fx.Injector.InjectKey (Key.CursorRight, Direct);

        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Selection_Still_Works ()
    {
        await using AppFixture<EditorTestHost> fx = CreateReadOnlyHost ("abc", 0);

        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.A.WithCtrl, Direct);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (0, fx.Top.Editor.SelectionStart);
        Assert.Equal (3, fx.Top.Editor.SelectionEnd);
    }

    private static AppFixture<EditorTestHost> CreateReadOnlyHost (string text, int caretOffset)
    {
        AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));
        fx.Top.Editor.ReadOnly = true;
        fx.Top.Editor.CaretOffset = caretOffset;
        fx.Top.Editor.SetFocus ();

        return fx;
    }
}
