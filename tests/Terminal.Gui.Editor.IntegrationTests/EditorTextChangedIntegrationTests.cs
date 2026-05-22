using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

public class EditorTextChangedIntegrationTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task TextChanged_Fires_When_Typing_In_Editor ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.SetFocus ();

        var textChangedCount = 0;
        fx.Top.Editor.Document!.TextChanged += (_, _) => textChangedCount++;

        fx.Injector.InjectKey (Key.A, Direct);
        fx.Injector.InjectKey (Key.B, Direct);

        Assert.Equal (2, textChangedCount);
        Assert.Equal ("abhello", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task TextChanged_Fires_When_Pasting_Text ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        fx.Top.Editor.SetFocus ();

        var textChangedCount = 0;
        fx.Top.Editor.Document!.TextChanged += (_, _) => textChangedCount++;

        fx.App.Clipboard?.TrySetClipboardData (" world");
        fx.Top.Editor.CaretOffset = 5;
        fx.Injector.InjectKey (Key.V.WithCtrl, Direct);

        Assert.Equal (1, textChangedCount);
        Assert.Equal ("hello world", fx.Top.Editor.Document.Text);
    }
}
