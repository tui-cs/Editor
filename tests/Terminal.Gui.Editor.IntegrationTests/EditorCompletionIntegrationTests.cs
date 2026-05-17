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
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint"));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        // Set up a completion provider that matches words from the document.
        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "u" to open completion.
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.Equal ("u", editor.Document!.GetText (0, 1));
        Assert.True (editor.IsCompletionActive, "Completion should be active after typing 'u'");

        // Type "s" — this must go to the Editor, not be captured by the Popover.
        fx.Injector.InjectKey (Key.S, Direct);

        // The document should now contain "us" at the start.
        Assert.StartsWith ("us", editor.Document!.Text);
        Assert.Equal (2, editor.CaretOffset);

        // Completion should still be active with filtered results.
        Assert.True (editor.IsCompletionActive, "Completion should remain active with 'us' prefix");
    }

    /// <summary>
    ///     Typing a character that eliminates all matches must dismiss the completion popup.
    /// </summary>
    [Fact]
    public async Task Typing_NonMatching_Char_Dismisses_Completion ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint"));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "u" to open completion.
        fx.Injector.InjectKey (Key.U, Direct);
        Assert.True (editor.IsCompletionActive);

        // Type "z" — no words start with "uz", so completion should dismiss.
        fx.Injector.InjectKey (Key.Z, Direct);

        Assert.StartsWith ("uz", editor.Document!.Text);
        Assert.False (editor.IsCompletionActive, "Completion should dismiss when no items match");
    }

    /// <summary>
    ///     Pressing Enter while completion is active should accept the completion (replace prefix
    ///     with the selected item's text), not insert a newline.
    /// </summary>
    [Fact]
    public async Task Enter_While_Completion_Active_Accepts_Item ()
    {
        await using AppFixture<EditorTestHost> fx = new (() => new ("using unsafe uint"));
        Editor editor = fx.Top.Editor;
        editor.SetFocus ();

        editor.CompletionProvider = new TestWordCompletionProvider ();

        // Type "us" to open completion with "using" and "unsafe" as matches.
        fx.Injector.InjectKey (Key.U, Direct);
        fx.Injector.InjectKey (Key.S, Direct);

        Assert.True (editor.IsCompletionActive, "Completion should be active after 'us'");

        // Press Enter — should accept the first completion item, not insert a newline.
        fx.Injector.InjectKey (Key.Enter, Direct);

        // The text should NOT start with "us\n" (newline), it should have the accepted completion.
        Assert.DoesNotContain ("\n", editor.Document!.Text.Substring (0, Math.Min (10, editor.Document!.Text.Length)));
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
