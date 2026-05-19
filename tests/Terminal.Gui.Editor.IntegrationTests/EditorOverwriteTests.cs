// Copilot - gpt-4.1

using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for overwrite (insert-replace) mode in <see cref="Editor" />.
/// </summary>
public class EditorOverwriteTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    // ───────────────────── Property / Command ─────────────────────

    [Fact]
    public void OverwriteMode_DefaultsToFalse ()
    {
        Editor editor = new ();
        Assert.False (editor.OverwriteMode);
    }

    [Fact]
    public void OverwriteMode_RaisesEvent ()
    {
        Editor editor = new ();
        var raised = false;
        editor.OverwriteModeChanged += (_, _) => raised = true;
        editor.OverwriteMode = true;
        Assert.True (raised);
    }

    [Fact]
    public void DefaultKeyBindings_Contains_ToggleOverwrite ()
    {
        Assert.True (Editor.DefaultKeyBindings!.ContainsKey (Command.ToggleOverwrite));
    }

    [Fact]
    public async Task InsertKey_Toggles_OverwriteMode ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();

        Assert.False (fx.Top.Editor.OverwriteMode);

        fx.Injector.InjectKey (Key.InsertChar, Direct);
        Assert.True (fx.Top.Editor.OverwriteMode);

        fx.Injector.InjectKey (Key.InsertChar, Direct);
        Assert.False (fx.Top.Editor.OverwriteMode);
    }

    // ───────────────────── Overwrite typing behaviour ─────────────────────

    [Fact]
    public async Task Overwrite_Replaces_CharacterAtCaret ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.OverwriteMode = true;

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("xbc", fx.Top.Editor.Document?.Text);
        Assert.Equal (1, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Overwrite_AtLineEnd_Inserts ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 2; // at end of "ab"
        fx.Top.Editor.OverwriteMode = true;

        fx.Injector.InjectKey (Key.X, Direct);

        Assert.Equal ("abx", fx.Top.Editor.Document?.Text);
        Assert.Equal (3, fx.Top.Editor.CaretOffset);
    }

    [Fact]
    public async Task Overwrite_WithSelection_ReplacesSelection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcdef"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.OverwriteMode = true;

        // Select "bcd" (offsets 1..4) via Shift+Right
        fx.Top.Editor.CaretOffset = 1;
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        fx.Injector.InjectKey (Key.CursorRight.WithShift, Direct);
        Assert.True (fx.Top.Editor.HasSelection);

        fx.Injector.InjectKey (Key.X, Direct);

        // Selection should be replaced entirely, not overwrite-style.
        Assert.Equal ("axef", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task Overwrite_SingleUndo_Step ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;
        fx.Top.Editor.OverwriteMode = true;

        fx.Injector.InjectKey (Key.X, Direct);
        Assert.Equal ("xbc", fx.Top.Editor.Document?.Text);

        fx.Top.Editor.Document!.UndoStack.Undo ();
        Assert.Equal ("abc", fx.Top.Editor.Document.Text);
    }

    [Fact]
    public async Task Overwrite_MultiLine_DoesNotConsumeNewline ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("ab\ncd"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 1; // at 'b'
        fx.Top.Editor.OverwriteMode = true;

        // Overwrite 'b', then caret at offset 2 is at line-end (\n), should insert
        fx.Injector.InjectKey (Key.X, Direct);
        Assert.Equal ("ax\ncd", fx.Top.Editor.Document?.Text);
        Assert.Equal (2, fx.Top.Editor.CaretOffset);

        // Now at line-end — type inserts rather than consuming newline
        fx.Injector.InjectKey (Key.Y, Direct);
        Assert.Equal ("axy\ncd", fx.Top.Editor.Document?.Text);
    }

    // ───────────────────── Enable / Disable commands ─────────────────────

    [Fact]
    public async Task EnableOverwrite_Command_SetsMode ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();

        fx.Top.Editor.InvokeCommand (Command.EnableOverwrite);
        Assert.True (fx.Top.Editor.OverwriteMode);
    }

    [Fact]
    public async Task DisableOverwrite_Command_ClearsMode ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.OverwriteMode = true;

        fx.Top.Editor.InvokeCommand (Command.DisableOverwrite);
        Assert.False (fx.Top.Editor.OverwriteMode);
    }
}
