// CoPilot - gpt-4.1

using System.Drawing;
using Terminal.Gui.App;
using Terminal.Gui.Document;
using Terminal.Gui.Drivers;
using Terminal.Gui.Editor.Completion;
using Terminal.Gui.Input;
using Terminal.Gui.Testing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
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

    [Fact]
    public void HandleCompletionKey_Handles_Regular_Chars_While_Active ()
    {
        // When the popup is active, regular character keys ARE consumed by
        // HandleCompletionKey — the character is inserted directly into the document
        // and the completion list is refreshed. This avoids the Popover intercepting
        // the key when it is visible.
        Editor editor = new ()
        {
            Document = new TextDocument ("us"),
            CompletionProvider = new MultiWordCompletionProvider ("using", "unsafe", "uint")
        };
        editor.CaretOffset = 2; // after "us"

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // A regular character key should be consumed and the character inserted.
        Assert.True (editor.HandleCompletionKey (new Key ('i')));
        Assert.Equal ("usi", editor.Document!.Text);
        Assert.True (editor.IsCompletionActive, "Completion should still be active after 'usi' (matches 'using')");
    }

    // Up/Down navigation and end-of-list cycling are covered below. Still untested:
    // PageUp/PageDown/Home/End within the list, and mouse selection.

    [Fact]
    public void ArrowKeys_Navigate_Completion_Selection ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" matches "hello" and "help" — two items.
        Editor editor = new ()
        {
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help", "world")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);
        Assert.Equal (0, editor.CompletionSelectedIndex);

        // Down arrow should move to index 1.
        app.InjectKey (Key.CursorDown);
        Assert.Equal (1, editor.CompletionSelectedIndex);

        // Accept the completion — should insert the second item ("help").
        editor.AcceptCompletion ();
        Assert.Equal ("help", editor.Document!.Text);
    }

    [Fact]
    public void ArrowUp_At_First_Item_Wraps_To_Last_Item ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" matches "hello" and "help" — two items.
        Editor editor = new ()
        {
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Up from index 0 should wrap to last item (index 1 = "help").
        app.InjectKey (Key.CursorUp);
        Assert.Equal (1, editor.CompletionSelectedIndex);

        editor.AcceptCompletion ();
        Assert.Equal ("help", editor.Document!.Text);
    }

    [Fact]
    public void ArrowDown_At_Last_Item_Cycles_To_Top ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" matches "hello" and "help" — two items.
        Editor editor = new ()
        {
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.NotNull (app.Popovers?.GetActivePopover ());
        Assert.True (editor.IsCompletionActive);
        Assert.Equal (0, editor.CompletionSelectedIndex);

        // Down to index 1, then down again cycles to 0.
        app.InjectKey (Key.CursorDown);
        Assert.Equal (1, editor.CompletionSelectedIndex);

        app.InjectKey (Key.CursorDown);
        Assert.Equal (0, editor.CompletionSelectedIndex);

        Assert.True (editor.IsCompletionActive);
    }

    // Horizontal caret movement dismisses the popup — unlike Up/Down, which the focused
    // popup ListView consumes to move the selection. The key still falls through, so the
    // caret moves too (HandleCompletionKey returns false after dismissing).
    [Theory]
    [InlineData (KeyCode.CursorLeft, 1)]
    [InlineData (KeyCode.CursorRight, 3)]
    public void Arrow_Left_Or_Right_While_Completion_Active_Dismisses_And_Moves_Caret (KeyCode navKey, int expectedCaret)
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // Caret after "he"; the trailing 'x' gives CursorRight room to move.
        Editor editor = new ()
        {
            Document = new TextDocument ("hex"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        app.InjectKey (navKey);

        Assert.False (editor.IsCompletionActive);
        Assert.Equal (expectedCaret, editor.CaretOffset);
    }

    // Regression (user-reported): type "te", Esc, Enter produced "Ted" instead of "te\n".
    // Terminal.Gui auto-hides the popover on Esc but never tells the Editor, so
    // IsCompletionActive stayed true and the next Enter accepted. The popover's
    // VisibleChanged handler must tear the session down on auto-hide.
    [Fact]
    public void Esc_AutoHides_Popover_So_Following_Enter_Newlines_Not_Accepts ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        Editor editor = new ()
        {
            Document = new TextDocument ("te"),
            CompletionProvider = new MultiWordCompletionProvider ("Ted", "TedApp")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Esc: TG auto-hides the popover → VisibleChanged → session torn down.
        app.InjectKey (Key.Esc);
        Assert.False (editor.IsCompletionActive);

        // Enter must newline, not resurrect the dead completion.
        app.InjectKey (Key.Enter);

        Assert.StartsWith ("te", editor.Document!.Text);
        Assert.Contains ("\n", editor.Document!.Text);
        Assert.DoesNotContain ("Ted", editor.Document!.Text);
        Assert.False (editor.IsCompletionActive);
    }

    // Enter (the key bound to Command.NewLine) and Tab (Command.InsertTab) are the default
    // accept keys. SPACE is deliberately NOT one — see
    // Space_While_Completion_Active_Inserts_Space_And_Dismisses.
    [Theory]
    [InlineData (KeyCode.Enter)]
    [InlineData (KeyCode.Tab)]
    public void ValidAcceptKeys_Accept_Completion (KeyCode acceptKey)
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" matches "hello" and "help" — two items.
        Editor editor = new ()
        {
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);
        Assert.Equal (0, editor.CompletionSelectedIndex);

        // Down to index 1
        app.InjectKey (Key.CursorDown);
        Assert.Equal (1, editor.CompletionSelectedIndex);

        app.InjectKey (acceptKey);
        Assert.Equal (1, editor.CompletionSelectedIndex);

        Assert.False (editor.IsCompletionActive);

        Assert.Equal ("help", editor.Document!.Text);
    }

    // Regression guard for the "this is a test." → "this IsDefaulta test." bug: SPACE must
    // not accept the selected item. It is an ordinary printable — it inserts a space, the
    // now-empty prefix dismisses the popup, and the typed text is left intact.
    [Fact]
    public void Space_While_Completion_Active_Inserts_Space_And_Dismisses ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" matches "hello" and "help" — two items, "hello" preselected.
        Editor editor = new ()
        {
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        app.InjectKey (Key.Space);

        // SPACE inserted literally, popup gone, no completion text applied.
        Assert.False (editor.IsCompletionActive);
        Assert.Equal ("he ", editor.Document!.Text);
    }

    [Fact]
    public void Setting_CompletionProvider_To_Null_Dismisses_Active_Session ()
    {
        Editor editor = new ()
        {
            Document = new TextDocument ("hel"),
            CompletionProvider = new StubCompletionProvider ("hello")
        };
        editor.CaretOffset = 3;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Setting provider to null should dismiss the active session.
        editor.CompletionProvider = null;
        Assert.False (editor.IsCompletionActive);
    }

    [Fact]
    public void Typing_Characters_While_Completion_Active_Filters_List ()
    {
        // Simulates: document has "u", popup shows [using, unsafe, uint].
        // User types "s" → document becomes "us", popup re-filters to [using, unsafe].
        Editor editor = new ()
        {
            Document = new TextDocument ("u"),
            CompletionProvider = new MultiWordCompletionProvider ("using", "unsafe", "uint")
        };
        editor.CaretOffset = 1;

        // Open completion.
        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Simulate the Editor inserting "s" (as OnKeyDownNotHandled would).
        editor.Document!.Insert (editor.CaretOffset, "s");
        editor.CaretOffset = 2;

        // Re-filter via NotifyCompletionAfterInsert.
        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // "us" prefix should match "using" and "unsafe" but not "uint".
        // Verify by accepting — the accepted text should be one of the "us" matches.
        editor.AcceptCompletion ();
        Assert.Contains (editor.Document!.Text, new[] { "using", "unsafe" });
    }

    [Fact]
    public void Typing_NonMatching_Char_While_Completion_Active_Dismisses ()
    {
        // Simulates: document has "x", popup shows items for "x".
        // User types "z" → document becomes "xz", no matches → popup dismissed.
        Editor editor = new ()
        {
            Document = new TextDocument ("us"),
            CompletionProvider = new MultiWordCompletionProvider ("using", "unsafe")
        };
        editor.CaretOffset = 2;

        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Simulate inserting a character that breaks all matches.
        editor.Document!.Insert (editor.CaretOffset, "z");
        editor.CaretOffset = 3;

        editor.NotifyCompletionAfterInsert ();
        Assert.False (editor.IsCompletionActive);
    }

    // Missing-coverage regression: clicking an item in the popover must select+accept that
    // item. Nothing exercised HandleCompletionMouse before, so its hit-test could (and did)
    // break silently.
    [Fact]
    public void SingleClick_On_Popover_Item_Accepts_That_Item ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        // "he" → [hello, help, helm]. We click index 1 ("help").
        Editor editor = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help", "helm")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;
        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        // Lay the popover out so its screen Frame is valid before we hit-test against it.
        app.LayoutAndDraw (true);
        var popover = (View)app.Popovers!.GetActivePopover ()!;
        Rectangle frame = popover.Frame;

        // HandleCompletionMouse maps clickedIdx = ScreenPosition.Y - Frame.Y, so Frame.Y + 1
        // is the second item.
        app.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (frame.X, frame.Y + 1),
                Flags = MouseFlags.LeftButtonClicked,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            });

        Assert.False (editor.IsCompletionActive);
        Assert.Equal ("help", editor.Document!.Text);
    }

    // Bug-2 regression (user-reported: clicking elsewhere inserted "TedApp" at the click).
    // Terminal.Gui hides an active popover on a mouse PRESS outside it (the press →
    // release → click cycle), so this injects a real LeftButtonPressed clear of the
    // popover frame and asserts the popup is gone with nothing accepted/inserted.
    [Fact]
    public void Click_Outside_Popover_Dismisses_And_Inserts_Nothing ()
    {
        using IApplication app = Application.Create ();
        app.Init (DriverRegistry.Names.ANSI);
        Runnable top = new ();

        Editor editor = new ()
        {
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            Document = new TextDocument ("he"),
            CompletionProvider = new MultiWordCompletionProvider ("hello", "help", "helm")
        };
        top.Add (editor);
        app.Begin (top);

        editor.CaretOffset = 2;
        editor.NotifyCompletionAfterInsert ();
        Assert.True (editor.IsCompletionActive);

        app.LayoutAndDraw (true);
        var popover = (View)app.Popovers!.GetActivePopover ()!;
        Rectangle frame = popover.Frame;

        var before = editor.Document!.Text;

        // Press well clear of the popover (column 0 is left of it; a few rows below).
        app.InjectMouse (
            new Mouse
            {
                ScreenPosition = new Point (0, frame.Bottom + 3),
                Flags = MouseFlags.LeftButtonPressed,
                Timestamp = new DateTime (2025, 1, 1, 12, 0, 0)
            });

        Assert.False (editor.IsCompletionActive);
        Assert.Equal (before, editor.Document!.Text);
    }

    /// <summary>Stub provider that always returns a single hard-coded item.</summary>
    private sealed class StubCompletionProvider (string word) : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            if (string.IsNullOrEmpty (prefix))
            {
                return [];
            }

            return word.StartsWith (prefix, StringComparison.OrdinalIgnoreCase)
                ? [new CompletionItem { Label = word }]
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

    /// <summary>Provider that returns all words starting with the given prefix.</summary>
    private sealed class MultiWordCompletionProvider (params string[] words) : IEditorCompletionProvider
    {
        public IReadOnlyList<CompletionItem> GetCompletions (TextDocument document, int caretOffset, string prefix)
        {
            if (string.IsNullOrEmpty (prefix))
            {
                return [];
            }

            return words
                .Where (w => w.StartsWith (prefix, StringComparison.OrdinalIgnoreCase))
                .Select (w => new CompletionItem { Label = w })
                .ToList ();
        }

        public bool ShouldTrigger (Key key)
        {
            return key == Key.Space.WithCtrl;
        }
    }
}
