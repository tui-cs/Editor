// Claude - claude-opus-4-7
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     End-to-end tests that boot an <see cref="EditorTestHost"/> on the ANSI driver and exercise
///     the <see cref="Views.Editor"/> via synthetic keyboard input. Asserts on <c>Driver.Contents</c>
///     and the document state.
/// </summary>
public class EditorTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    [Fact]
    public async Task Renders_InitialDocumentText ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("Hello world"));

        DriverAssert.ContentsContains (fx.Driver, "Hello world");
    }

    [Fact]
    public async Task Typing_ASCII_Inserts_Characters ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new (""));
        fx.Top.Editor.SetFocus ();

        fx.Injector.InjectKey (Key.H, Direct);
        fx.Injector.InjectKey (Key.I, Direct);

        // Bare letter keys produce lowercase; Shift would uppercase.
        Assert.Equal ("hi", fx.Top.Editor.Document.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task CursorLeft_Right_MovesCaret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 3;

        fx.Injector.InjectKey (Key.CursorLeft, Direct);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.CursorLeft, Direct);
        fx.Injector.InjectKey (Key.CursorLeft, Direct);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        // Past start should clamp.
        fx.Injector.InjectKey (Key.CursorLeft, Direct);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.CursorRight, Direct);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task CursorUp_Down_PreservesVirtualColumn_AcrossShortLines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("longer line\nshort\nanother long line"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 8; // column 8 of "longer line"

        fx.Injector.InjectKey (Key.CursorDown, Direct);

        // "short" only has 5 chars — caret snaps to its end.
        int afterFirstDown = fx.Top.Editor.CaretOffset;
        Assert.Equal ("longer line\nshort".Length, afterFirstDown);

        fx.Injector.InjectKey (Key.CursorDown, Direct);

        // On the long line below, the sticky column 8 should be restored.
        int afterSecondDown = fx.Top.Editor.CaretOffset;
        int line3Start = "longer line\nshort\n".Length;
        Assert.Equal (line3Start + 8, afterSecondDown);
    }

    [Fact]
    public async Task Home_End_Move_WithinLine ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("first\nsecond"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = "first\n".Length + 2; // line 2, col 2

        fx.Injector.InjectKey (Key.Home, Direct);
        Assert.Equal ("first\n".Length, fx.Top.Editor.CaretOffset);

        fx.Injector.InjectKey (Key.End, Direct);
        Assert.Equal ("first\nsecond".Length, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Backspace_Removes_CharBefore_Caret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 3;

        fx.Injector.InjectKey (Key.Backspace, Direct);

        Assert.Equal ("ab", fx.Top.Editor.Document.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Delete_Removes_CharAt_Caret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.Delete, Direct);

        Assert.Equal ("ac", fx.Top.Editor.Document.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Enter_Inserts_Newline ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("ab"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1;

        fx.Injector.InjectKey (Key.Enter, Direct);

        Assert.Equal ("a\nb", fx.Top.Editor.Document.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);
        Assert.Equal (2, fx.Top.Editor.Document.LineCount);
    }

    [Fact]
    public async Task CtrlZ_Undoes_LastEdit ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.Document.Insert (3, "DEF");

        Assert.Equal ("abcDEF", fx.Top.Editor.Document.Text);

        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task CtrlY_Redoes_LastUndo ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.Document.Insert (3, "DEF");
        fx.Injector.InjectKey (Key.Z.WithCtrl, Direct);

        Assert.Equal ("abc", fx.Top.Editor.Document.Text);

        fx.Injector.InjectKey (Key.Y.WithCtrl, Direct);

        Assert.Equal ("abcDEF", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task MultiLine_Document_Renders_AllLines ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("alpha\nbeta\ngamma"));
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "alpha");
        DriverAssert.ContentsContains (fx.Driver, "beta");
        DriverAssert.ContentsContains (fx.Driver, "gamma");
    }

    [Fact]
    public async Task LongDocument_Scrolls_To_Keep_Caret_Visible ()
    {
        // 50 lines, viewport is 24 rows. Moving caret to line 40 should scroll.
        string[] lines = new string [50];
        for (int i = 0; i < 50; i++)
        {
            lines [i] = $"line-{i:00}";
        }

        await using AppFixture<EditorTestHost> fx = new (() => new (string.Join ("\n", lines)));
        fx.Top.Editor.SetFocus ();

        // Place caret on line index 40 (0-based).
        int offset = 0;
        for (int i = 0; i < 40; i++)
        {
            offset += lines [i].Length + 1;
        }
        fx.Top.Editor.CaretOffset = offset;
        fx.Render ();

        DriverAssert.ContentsContains (fx.Driver, "line-40");
        DriverAssert.ContentsDoesNotContain (fx.Driver, "line-00"); // scrolled out
    }
}
