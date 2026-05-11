// Codex - GPT-5

using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

public class EditorClipboardTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task CtrlC_Copies_Selected_Text ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("hello world");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectRange (6, 5);

        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        Assert.Equal ("world", GetClipboardText (fx));
        Assert.Equal ("hello world", fx.Top.Editor.Document!.Text);
        Assert.True (fx.Top.Editor.HasSelection);
    }

    [Fact]
    public async Task CtrlC_With_No_Selection_Copies_Current_Line ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("one\ntwo");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        Assert.Equal ("one\n", GetClipboardText (fx));
        Assert.Equal ("one\ntwo", fx.Top.Editor.Document!.Text);
    }

    [Fact]
    public async Task CtrlX_Cuts_Selected_Text_And_Undo_Restores_In_One_Step ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("hello world");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectRange (0, 5);

        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal ("hello", GetClipboardText (fx));
        Assert.Equal (" world", fx.Top.Editor.Document!.Text);

        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("hello world", fx.Top.Editor.Document!.Text);
        Assert.False (fx.Top.Editor.Document.UndoStack.CanUndo);
    }

    [Fact]
    public async Task CtrlX_With_No_Selection_Cuts_Current_Line ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("one\ntwo");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.X.WithCtrl, Direct);

        Assert.Equal ("one\n", GetClipboardText (fx));
        Assert.Equal ("two", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("one\ntwo", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task CtrlC_Then_CtrlV_Replaces_Selection_With_Copied_Text ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("hello world");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectRange (0, 5);

        fx.Injector.InjectKey (Key.C.WithCtrl, Direct);

        fx.Top.Editor.SelectRange (6, 5);

        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("hello hello", fx.Top.Editor.Document!.Text);
        Assert.Equal ("hello", GetClipboardText (fx));
        Assert.False (fx.Top.Editor.HasSelection);
        Assert.Equal ("hello hello".Length, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task CtrlV_Inserts_MultiLine_Clipboard_Text ()
    {
        await using AppFixture<EditorTestHost> fx = CreateClipboardHost ("ab");
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;
        SetClipboardText (fx, "one\ntwo");

        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal ("aone\ntwob", fx.Top.Editor.Document!.Text);
        Assert.Equal (2, fx.Top.Editor.Document.LineCount);
        Assert.Equal ("aone\ntwo".Length, fx.Top.Editor.CaretOffset);
    }

    private static AppFixture<EditorTestHost> CreateClipboardHost (string text)
    {
        AppFixture<EditorTestHost> fx = new (() => new EditorTestHost (text));
        fx.Driver.Clipboard = new FakeClipboard ();

        return fx;
    }

    private static string GetClipboardText (AppFixture<EditorTestHost> fx)
    {
        Assert.NotNull (fx.App.Clipboard);
        Assert.True (fx.App.Clipboard.TryGetClipboardData (out string text));

        return text;
    }

    private static void SetClipboardText (AppFixture<EditorTestHost> fx, string text)
    {
        Assert.NotNull (fx.App.Clipboard);
        Assert.True (fx.App.Clipboard.TrySetClipboardData (text));
    }
}
