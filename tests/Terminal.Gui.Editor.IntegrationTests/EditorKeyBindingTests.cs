// Copilot - gpt-4.1

using Terminal.Gui.Drawing;
using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Document.Search;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Tests that verify the six migrated key bindings (Tab, Shift+Tab, F3, Shift+F3, Ctrl+F, Ctrl+H)
///     are registered as proper <see cref="Command" /> bindings (not hardcoded in OnKeyDownNotHandled)
///     and respond correctly to both key injection and direct <see cref="Command" /> invocation.
/// </summary>
public class EditorKeyBindingTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    // ───────────────────── DefaultKeyBindings dictionary ─────────────────────

    [Theory]
    [InlineData (Command.InsertTab)]
    [InlineData (Command.Unindent)]
    [InlineData (Command.FindNext)]
    [InlineData (Command.FindPrevious)]
    [InlineData (Command.Find)]
    [InlineData (Command.Replace)]
    public void DefaultKeyBindings_Contains_Command (Command command)
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Assert.True (Editor.DefaultKeyBindings!.ContainsKey (command), $"DefaultKeyBindings missing {command}");
    }

    // ───────────────────── Command.InsertTab ─────────────────────

    [Fact]
    public async Task InsertTab_Command_Inserts_Tab ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        fx.Top.Editor.InvokeCommand (Command.InsertTab);

        Assert.Equal ("\t", fx.Top.Editor.Document!.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task InsertTab_Command_ReadOnly_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;

        fx.Top.Editor.InvokeCommand (Command.InsertTab);

        Assert.Equal ("abc", fx.Top.Editor.Document!.Text);
    }

    // ───────────────────── Command.Unindent ─────────────────────

    [Fact]
    public async Task Unindent_Command_Removes_Leading_Whitespace ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("    alpha"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4;

        fx.Top.Editor.InvokeCommand (Command.Unindent);

        Assert.Equal ("alpha", fx.Top.Editor.Document!.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Unindent_Command_ReadOnly_NoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("    abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;
        fx.Top.Editor.CaretOffset = 4;

        fx.Top.Editor.InvokeCommand (Command.Unindent);

        Assert.Equal ("    abc", fx.Top.Editor.Document!.Text);
    }

    // ───────────────────── Command.FindNext ─────────────────────

    [Fact]
    public async Task FindNext_Command_Selects_Next_Match ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Top.Editor.CaretOffset = 0;

        fx.Top.Editor.InvokeCommand (Command.FindNext);

        Assert.Equal (5, fx.Top.Editor.CaretOffset);
        Assert.True (fx.Top.Editor.HasSelection);
    }

    // ───────────────────── Command.FindPrevious ─────────────────────

    [Fact]
    public async Task FindPrevious_Command_Selects_Previous_Match ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world hello"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SearchStrategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);
        fx.Top.Editor.CaretOffset = 17;

        fx.Top.Editor.InvokeCommand (Command.FindPrevious);

        Assert.True (fx.Top.Editor.HasSelection);
        Assert.Equal (12, fx.Top.Editor.SelectionStart);
    }

    // ───────────────────── Command.Find ─────────────────────

    [Fact]
    public async Task Find_Command_Raises_FindRequested ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        fx.Top.Editor.SetFocus ();
        var fired = false;
        fx.Top.Editor.FindRequested += (_, _) => fired = true;

        fx.Top.Editor.InvokeCommand (Command.Find);

        Assert.True (fired);
    }

    // ───────────────────── Command.Replace ─────────────────────

    [Fact]
    public async Task Replace_Command_Raises_ReplaceRequested ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("test"));
        fx.Top.Editor.SetFocus ();
        var fired = false;
        fx.Top.Editor.ReplaceRequested += (_, _) => fired = true;

        fx.Top.Editor.InvokeCommand (Command.Replace);

        Assert.True (fired);
    }

    // ───────────────────── Key binding wire-up via InjectKey ─────────────────────

    [Fact]
    public async Task Tab_Key_Bound_To_InsertTab_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.InsertTab, fx.Top.Editor.KeyBindings.GetCommands (Key.Tab));
    }

    [Fact]
    public async Task ShiftTab_Key_Bound_To_Unindent_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.Unindent, fx.Top.Editor.KeyBindings.GetCommands (Key.Tab.WithShift));
    }

    [Fact]
    public async Task F3_Key_Bound_To_FindNext_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.FindNext, fx.Top.Editor.KeyBindings.GetCommands (Key.F3));
    }

    [Fact]
    public async Task ShiftF3_Key_Bound_To_FindPrevious_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.FindPrevious, fx.Top.Editor.KeyBindings.GetCommands (Key.F3.WithShift));
    }

    [Fact]
    public async Task CtrlF_Key_Bound_To_Find_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.Find, fx.Top.Editor.KeyBindings.GetCommands (Key.F.WithCtrl));
    }

    [Fact]
    public async Task CtrlH_Key_Bound_To_Replace_Command ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ());
        fx.Top.Editor.SetFocus ();

        Assert.Contains (Command.Replace, fx.Top.Editor.KeyBindings.GetCommands (Key.H.WithCtrl));
    }

    [Fact]
    public async Task Removing_Tab_KeyBinding_Restores_Default_View_Navigation ()
    {
        await using AppFixture<FocusNavigationHost> fx = new (() => new FocusNavigationHost ());
        fx.Top.Editor.SetFocus ();

        fx.Top.Editor.KeyBindings.Remove (Key.Tab);
        fx.Injector.InjectKey (Key.Tab, Direct);

        Assert.True (fx.Top.Next.HasFocus);
    }

    [Fact]
    public async Task Removing_ShiftTab_KeyBinding_Restores_Default_View_Navigation ()
    {
        await using AppFixture<FocusNavigationHost> fx = new (() => new FocusNavigationHost ());
        fx.Top.Editor.SetFocus ();

        fx.Top.Editor.KeyBindings.Remove (Key.Tab.WithShift);
        fx.Injector.InjectKey (Key.Tab.WithShift, Direct);

        Assert.True (fx.Top.Previous.HasFocus);
    }

    private sealed class FocusNavigationHost : Window
    {
        public FocusNavigationHost ()
        {
            BorderStyle = LineStyle.None;
            Previous = new TextField
            {
                Text = "prev",
                X = 0,
                Y = 0,
                Width = 10
            };
            Editor = new Editor
            {
                Document = new TextDocument (),
                X = 0,
                Y = 1,
                Width = Dim.Fill (),
                Height = 1
            };
            Next = new TextField
            {
                Text = "next",
                X = 0,
                Y = 2,
                Width = 10
            };
            Add (Previous, Editor, Next);
        }

        public Editor Editor { get; }
        public TextField Next { get; }
        public TextField Previous { get; }
    }
}
