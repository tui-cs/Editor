// Copilot - gpt-4.1

using Terminal.Gui.Document;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Kill-ring integration tests — CutToEndOfLine, CutToStartOfLine, and consecutive-kill
///     append behavior. Each test boots an <see cref="EditorTestHost" /> so <c>App.Clipboard</c>
///     is available.
/// </summary>
public class EditorKillRingTests
{
    /// <summary>
    ///     Ensures the fixture's driver has a working in-memory clipboard regardless of platform.
    /// </summary>
    private static void EnsureFakeClipboard (AppFixture<EditorTestHost> fx)
    {
        fx.Driver.Clipboard = new FakeClipboard ();
    }

    // ───────────────────── CutToEndOfLine ─────────────────────

    [Fact]
    public async Task CutToEndOfLine_KillsToLineEnd ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5; // after "hello"

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        Assert.Equal ("hello", fx.Top.Editor.Document?.Text);

        // Clipboard should contain " world"
        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal (" world", data);
    }

    [Fact]
    public async Task CutToEndOfLine_AtEOL_KillsDelimiter ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 3; // at end of "abc", before \n

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        Assert.Equal ("abcdef", fx.Top.Editor.Document?.Text);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("\n", data);
    }

    [Fact]
    public async Task CutToEndOfLine_AtEndOfDocument_IsNoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 3;

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        Assert.Equal ("abc", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task CutToEndOfLine_SingleUndo ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);
        Assert.Equal ("hello", fx.Top.Editor.Document?.Text);

        fx.Top.Editor.InvokeCommand (Command.Undo);
        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task CutToEndOfLine_ReadOnly_IsNoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.ReadOnly = true;
        fx.Top.Editor.CaretOffset = 2;

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        Assert.Equal ("hello", fx.Top.Editor.Document?.Text);
    }

    // ───────────────────── CutToStartOfLine ─────────────────────

    [Fact]
    public async Task CutToStartOfLine_KillsToLineStart ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);

        Assert.Equal (" world", fx.Top.Editor.Document?.Text);
        Assert.Equal (0, fx.Top.Editor.CaretOffset);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("hello", data);
    }

    [Fact]
    public async Task CutToStartOfLine_AtBOL_IsNoOp ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 4; // start of "def"

        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);

        Assert.Equal ("abc\ndef", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task CutToStartOfLine_SingleUndo ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);
        Assert.Equal (" world", fx.Top.Editor.Document?.Text);

        fx.Top.Editor.InvokeCommand (Command.Undo);
        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    // ───────────────────── Consecutive-kill append ─────────────────────

    [Fact]
    public async Task ConsecutiveKillToEOL_Appends ()
    {
        // "abc\ndef\nghi" with caret at 0 — two consecutive CutToEndOfLine should accumulate.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef\nghi"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // First kill: "abc" → clipboard = "abc"
        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);
        Assert.Equal ("\ndef\nghi", fx.Top.Editor.Document?.Text);

        // Second kill (consecutive): "\n" → clipboard = "abc\n"
        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);
        Assert.Equal ("def\nghi", fx.Top.Editor.Document?.Text);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("abc\n", data);
    }

    [Fact]
    public async Task NonKillCommand_BreaksConsecutiveRun ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 0;

        // First kill: "abc"
        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);
        Assert.Equal ("\ndef", fx.Top.Editor.Document?.Text);

        // Intervening non-kill command (move right): caret moves past \n to position 1 (start of "def").
        fx.Injector.InjectKey (Key.CursorRight, new InputInjectionOptions { Mode = InputInjectionMode.Direct });

        // Second kill after non-kill — should replace clipboard, not append.
        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));

        // Clipboard replaced with "def" (not "abc" + "def" which would be consecutive-append).
        Assert.Equal ("def", data);
    }

    [Fact]
    public async Task ConsecutiveKillToBOL_Prepends ()
    {
        // Two consecutive CutToStartOfLine on a single line should prepend so clipboard
        // accumulates in document order.
        // "abcdef" — caret at 6 (end)
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abcdef"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 6; // end of "abcdef"

        // First kill-to-BOL: kills "abcdef" → clipboard = "abcdef", text = ""
        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);
        Assert.Equal ("", fx.Top.Editor.Document?.Text);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("abcdef", data);
    }

    [Fact]
    public async Task ConsecutiveKillToBOL_TwoLines_Prepends ()
    {
        // Consecutive CutToStartOfLine across different lines should prepend in document order.
        // "abc\ndef\nghi" — start at end of line 3 (offset 11, after "ghi")
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef\nghi"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 11; // end of "ghi"

        // First kill-to-BOL: kills "ghi" → clipboard = "ghi", text = "abc\ndef\n"
        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);
        Assert.Equal ("abc\ndef\n", fx.Top.Editor.Document?.Text);

        // Move caret to end of line 2 (offset 7, end of "def")
        fx.Top.Editor.CaretOffset = 7;

        // Second consecutive kill-to-BOL: kills "def" → prepend → clipboard = "def" + "ghi" = "defghi"
        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("defghi", data);
    }

    // ───────────────────── Unbound by default ─────────────────────

    [Fact]
    public void DefaultKeyBindings_DoesNotContain_KillCommands ()
    {
        Assert.NotNull (Editor.DefaultKeyBindings);
        Assert.False (Editor.DefaultKeyBindings!.ContainsKey (Command.CutToEndOfLine));
        Assert.False (Editor.DefaultKeyBindings!.ContainsKey (Command.CutToStartOfLine));
    }

    // ───────────────────── Selection consumed ─────────────────────

    [Fact]
    public async Task CutToEndOfLine_WithSelection_DeletesSelection ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));
        EnsureFakeClipboard (fx);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.SelectRange (0, 5);

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        // Selection replaced with empty — "hello" removed.
        Assert.Equal (" world", fx.Top.Editor.Document?.Text);
    }

    // ───────────────────── Clipboard failure ─────────────────────

    [Fact]
    public async Task CutToEndOfLine_PreservesText_When_Clipboard_Unavailable ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));

        // FakeClipboard(false, true) → IsSupported = false, writes fail.
        fx.Driver.Clipboard = new FakeClipboard (false, true);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Top.Editor.InvokeCommand (Command.CutToEndOfLine);

        // Text must be preserved because the clipboard write failed.
        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    [Fact]
    public async Task CutToStartOfLine_PreservesText_When_Clipboard_Unavailable ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("hello world"));

        fx.Driver.Clipboard = new FakeClipboard (false, true);
        fx.Top.Editor.SetFocus ();
        fx.Top.Editor.CaretOffset = 5;

        fx.Top.Editor.InvokeCommand (Command.CutToStartOfLine);

        Assert.Equal ("hello world", fx.Top.Editor.Document?.Text);
    }

    // ───────────────────── Completion breaks kill run ─────────────────────

    [Fact]
    public async Task CompletionConsumedKey_BreaksConsecutiveKillRun ()
    {
        // When HandleCompletionKey consumes a key (returns true), it must clear _lastCommandWasKill
        // so the next kill command (via InvokeCommand) does NOT append to clipboard.
        await using AppFixture<EditorTestHost> fx = new (() => new EditorTestHost ("abc\ndef"));
        EnsureFakeClipboard (fx);
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();
        editor.CompletionProvider = new KillRingTestCompletionProvider ();
        editor.CaretOffset = 0;

        // First CutToEndOfLine via InvokeCommand: "abc" cut, clipboard = "abc"
        editor.InvokeCommand (Command.CutToEndOfLine);
        Assert.Equal ("\ndef", editor.Document?.Text);

        string? data = null;
        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));
        Assert.Equal ("abc", data);

        // Ctrl+Space goes through OnKeyDown → HandleCompletionKey → returns true (ShouldTrigger).
        // This should break the consecutive-kill run.
        fx.Injector.InjectKey (Key.Space.WithCtrl, new InputInjectionOptions { Mode = InputInjectionMode.Direct });

        // Dismiss completion programmatically (not via keyboard).
        editor.CompletionProvider = null;
        editor.CompletionProvider = new KillRingTestCompletionProvider ();

        // Second CutToEndOfLine — caret at 0, text = "\ndef", cuts "\n".
        editor.InvokeCommand (Command.CutToEndOfLine);

        Assert.True (fx.App.Clipboard?.TryGetClipboardData (out data));

        // Bug: clipboard = "abc\n" (appended because _lastCommandWasKill was never cleared)
        // Fix: clipboard = "\n" (replaced because Ctrl+Space broke the run)
        Assert.Equal ("\n", data);
    }

    /// <summary>Simple completion provider for kill-ring interaction tests.</summary>
    private sealed class KillRingTestCompletionProvider : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            // Always return items so the popup stays open regardless of prefix.
            return [new CompletionItem { Label = "ghi" }, new CompletionItem { Label = "ghijk" }];
        }

        public bool ShouldTrigger (Key key)
        {
            return key == Key.Space.WithCtrl;
        }
    }
}
