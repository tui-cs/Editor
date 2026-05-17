// CoPilot - gpt-4.1

using Terminal.Gui.Document;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Editor.IntegrationTests.Testing;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Xunit;

namespace Terminal.Gui.Editor.IntegrationTests;

/// <summary>
///     Integration tests for in-editor completion. Uses <see cref="AppFixture{T}" /> to exercise
///     the full key-dispatch pipeline, including <see cref="App.Popover{TView,TResult}" /> interaction.
/// </summary>
public class EditorCompletionIntegrationTests
{
    private static readonly InputInjectionOptions Direct = new () { Mode = InputInjectionMode.Direct };

    /// <summary>
    ///     Typing a character while the completion popup is open must insert the character
    ///     into the document (not be swallowed by the Popover) and update the completion list.
    /// </summary>
    [Fact]
    public async Task Typing_Char_While_Completion_Active_Inserts_Into_Document ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret at the end (after trailing space).
        editor.CaretOffset = editor.Document!.TextLength;

        // Set up a completion provider that matches words from the document.
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "u" to open completion.
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive, "Completion should be active after typing 'u'");

        // Type "s" — this must go to the Editor, not be captured by the Popover.
        fx.Injector.InjectKey (Key.S, Direct);

        // The document should now end with "us".
        Assert.EndsWith ("us", editor.Document!.Text);

        // Completion should still be active with filtered results.
        Assert.True (editor.IsCompletionActive, "Completion should remain active with 'us' prefix");
    }

    /// <summary>
    ///     Typing a character that eliminates all matches must dismiss the completion popup.
    /// </summary>
    [Fact]
    public async Task Typing_NonMatching_Char_Dismisses_Completion ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret at the end (after trailing space).
        editor.CaretOffset = editor.Document!.TextLength;

        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "u" to open completion.
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive);

        // Type "z" — no words in the document start with "uz", so completion should dismiss.
        fx.Injector.InjectKey (Key.Z, Direct);

        Assert.EndsWith ("uz", editor.Document!.Text);
        Assert.False (editor.IsCompletionActive, "Completion should dismiss when no items match");
    }

    /// <summary>
    ///     Pressing Enter while completion is active should accept the completion (replace prefix
    ///     with the selected item's text), not insert a newline.
    /// </summary>
    [Fact]
    public async Task Enter_While_Completion_Active_Accepts_Item ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Place caret at the end (after trailing space).
        editor.CaretOffset = editor.Document!.TextLength;

        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "us" to open completion with "using" and "unsafe" as matches.
        fx.Injector.InjectKey (Key.U, Direct);
        fx.Injector.InjectKey (Key.S, Direct);

        Assert.True (editor.IsCompletionActive, "Completion should be active after 'us'");

        // Press Enter — should accept the first completion item, not insert a newline.
        fx.Injector.InjectKey (Key.Enter, Direct);

        // The text should NOT contain a newline near the end; it should have the accepted completion.
        var text = editor.Document!.Text;
        var lastChunk = text[^Math.Min (15, text.Length)..];
        Assert.DoesNotContain ("\n", lastChunk);
        Assert.False (editor.IsCompletionActive, "Completion should be dismissed after accept");
    }

    /// <summary>
    ///     Arrow keys while completion is active should navigate the list, not move the caret.
    ///     Down arrow then Enter should accept the second item.
    /// </summary>
    [Fact]
    public async Task ArrowDown_Then_Enter_Accepts_Second_Item ()
    {
        // "hello help world" — typing "he" at the end matches "hello" and "help".
        await using AppFixture<EditorTestHost> fx = new (() => new ("hello help world "));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        editor.CaretOffset = editor.Document!.TextLength;
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "he" to open completion.
        fx.Injector.InjectKey (Key.H, Direct);
        fx.Injector.InjectKey (Key.E, Direct);

        Assert.True (editor.IsCompletionActive, "Completion should be active after 'he'");

        // Down arrow to select the second item.
        fx.Injector.InjectKey (Key.CursorDown, Direct);

        // Enter to accept.
        fx.Injector.InjectKey (Key.Enter, Direct);

        // The accepted text should be the second match (alphabetically: "help" comes after "hello").
        Assert.EndsWith ("help", editor.Document!.Text.TrimEnd ());
        Assert.False (editor.IsCompletionActive, "Completion should be dismissed after accept");
    }

    /// <summary>
    ///     Minimal word-completion provider for integration tests. Returns all word tokens
    ///     from the document that start with the prefix.
    /// </summary>
    private sealed class TestWordCompletionProvider : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            if (string.IsNullOrEmpty (prefix))
            {
                return [];
            }

            var text = document.Text;
            HashSet<string> seen = new (StringComparer.OrdinalIgnoreCase);
            List<CompletionItem> results = [];

            var i = 0;

            while (i < text.Length)
            {
                if (!char.IsLetterOrDigit (text[i]) && text[i] != '_')
                {
                    i++;

                    continue;
                }

                var start = i;

                while (i < text.Length && (char.IsLetterOrDigit (text[i]) || text[i] == '_'))
                {
                    i++;
                }

                var word = text.Substring (start, i - start);

                if (word.Length > prefix.Length
                    && word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
                    && seen.Add (word))
                {
                    results.Add (new CompletionItem { Label = word });
                }
            }

            return results;
        }

        public bool ShouldTrigger (Key key)
        {
            return key == Key.Space.WithCtrl;
        }
    }
}
