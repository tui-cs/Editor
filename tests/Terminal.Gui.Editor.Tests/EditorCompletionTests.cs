// CoPilot - gpt-4.1

using Terminal.Gui.Document;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> completion logic — prefix extraction, provider querying,
///     accept/dismiss, and single-undo-step guarantee. No <see cref="App.IApplication" /> needed.
/// </summary>
public class EditorCompletionTests
{
    [Fact]
    public void GetCompletionPrefix_Returns_Empty_On_Empty_Document ()
    {
        Editor editor = new ();

        var prefix = editor.GetCompletionPrefix ();

        Assert.Equal (string.Empty, prefix);
    }

    [Fact]
    public void GetCompletionPrefix_Extracts_WordBefore_Caret ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5; // after "hello"

        var prefix = editor.GetCompletionPrefix (out var start);

        Assert.Equal ("hello", prefix);
        Assert.Equal (0, start);
    }

    [Fact]
    public void GetCompletionPrefix_Stops_At_Punctuation ()
    {
        Editor editor = new () { Document = new TextDocument ("foo.bar") };
        editor.CaretOffset = 7; // after "bar"

        var prefix = editor.GetCompletionPrefix (out var start);

        Assert.Equal ("bar", prefix);
        Assert.Equal (4, start);
    }

    [Fact]
    public void GetCompletionPrefix_Returns_Empty_AfterWhitespace ()
    {
        Editor editor = new () { Document = new TextDocument ("hello ") };
        editor.CaretOffset = 6; // after space

        var prefix = editor.GetCompletionPrefix ();

        Assert.Equal (string.Empty, prefix);
    }

    [Fact]
    public void GetCompletionPrefix_Includes_Underscores_And_Digits ()
    {
        Editor editor = new () { Document = new TextDocument ("my_var2 ") };
        editor.CaretOffset = 7; // after "my_var2"

        var prefix = editor.GetCompletionPrefix ();

        Assert.Equal ("my_var2", prefix);
    }

    [Fact]
    public void CompletionProvider_Default_Is_Null ()
    {
        Editor editor = new ();

        Assert.Null (editor.CompletionProvider);
        Assert.False (editor.IsCompletionActive);
    }

    [Fact]
    public void AcceptCompletion_Replaces_Prefix_In_SingleUndoStep ()
    {
        Editor editor = new ()
        {
            Document = new TextDocument ("hel"),
            CompletionProvider = new StubCompletionProvider ("hello_world")
        };
        editor.CaretOffset = 3; // after "hel"

        // NotifyCompletionAfterInsert sets up the items and prefix state.
        editor.NotifyCompletionAfterInsert ();

        // Accept the completion — uses the prefix state set by Notify.
        editor.AcceptCompletion ();
        Assert.Equal ("hello_world", editor.Document!.Text);

        // Single undo step should restore the original text.
        editor.Document!.UndoStack.Undo ();
        Assert.Equal ("hel", editor.Document!.Text);
    }

    [Fact]
    public void DismissCompletion_Clears_State ()
    {
        Editor editor = new ()
        {
            Document = new TextDocument ("hel"),
            CompletionProvider = new StubCompletionProvider ("hello")
        };
        editor.CaretOffset = 3;

        editor.NotifyCompletionAfterInsert ();
        editor.DismissCompletion ();

        // After dismiss, AcceptCompletion should be a no-op.
        editor.AcceptCompletion ();
        Assert.Equal ("hel", editor.Document!.Text);
    }

    [Fact]
    public void ShowCompletion_NoOp_Without_Provider ()
    {
        Editor editor = new () { Document = new TextDocument ("hel") };
        editor.CaretOffset = 3;

        editor.ShowCompletion ();
        Assert.False (editor.IsCompletionActive);
    }

    [Fact]
    public void ShowCompletion_NoOp_When_Provider_Returns_Empty ()
    {
        Editor editor = new ()
        {
            Document = new TextDocument ("hel"),
            CompletionProvider = new EmptyCompletionProvider ()
        };
        editor.CaretOffset = 3;

        editor.ShowCompletion ();
        Assert.False (editor.IsCompletionActive);
    }

    [Fact]
    public void HandleCompletionKey_Returns_False_Without_Provider ()
    {
        Editor editor = new ();

        Assert.False (editor.HandleCompletionKey (Key.Esc));
    }

    /// <summary>Stub provider that always returns a single hard-coded item.</summary>
    private sealed class StubCompletionProvider : IEditorCompletionProvider
    {
        private readonly string _word;

        public StubCompletionProvider (string word)
        {
            _word = word;
        }

        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            if (string.IsNullOrEmpty (prefix))
            {
                return [];
            }

            return _word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
                ? [new CompletionItem { Label = _word }]
                : [];
        }

        public bool ShouldTrigger (Key key)
        {
            return key == Key.Space.WithCtrl;
        }
    }

    /// <summary>Provider that always returns an empty list.</summary>
    private sealed class EmptyCompletionProvider : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            return [];
        }

        public bool ShouldTrigger (Key key)
        {
            return false;
        }
    }
}
