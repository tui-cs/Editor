// Claude - claude-opus-4-7

using Terminal.Gui.Document;
using Terminal.Gui.Document.Search;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> find/replace surface. Pure logic — no
///     <see cref="App.IApplication" />, no driver. Covers both the legacy string-based
///     convenience overloads (which build a <see cref="SearchMode.Normal" /> strategy under
///     the hood) and the <see cref="Editor.SearchStrategy" /> property-driven path that
///     unlocks regex, whole-word, and wildcard search through ted.
/// </summary>
public class EditorFindReplaceTests
{
    [Fact]
    public void ReplaceAll_Replaces_Every_Match_And_Returns_Count ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo baz foo") };

        var n = editor.ReplaceAll ("foo", "qux");

        Assert.Equal (3, n);
        Assert.Equal ("qux bar qux baz qux", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_Collapses_To_Single_Undo_Step ()
    {
        // Regression for the R5 violation called out in spec §3 / §8 D4: ReplaceAll used to
        // run N un-grouped Replace calls, so undo took N presses. RunUpdate () groups them.
        TextDocument doc = new ("foo foo foo foo foo");
        Editor editor = new () { Document = doc };

        var n = editor.ReplaceAll ("foo", "qux");
        Assert.Equal (5, n);
        Assert.Equal ("qux qux qux qux qux", doc.Text);

        Assert.True (doc.UndoStack.CanUndo);
        doc.UndoStack.Undo ();

        Assert.Equal ("foo foo foo foo foo", doc.Text);
    }

    [Fact]
    public void ReplaceAll_With_Zero_Matches_Returns_Zero_And_Does_Not_Modify_Document ()
    {
        TextDocument doc = new ("hello world");
        Editor editor = new () { Document = doc };

        var n = editor.ReplaceAll ("xyz", "abc");

        Assert.Equal (0, n);
        Assert.Equal ("hello world", doc.Text);
    }

    [Fact]
    public void ReplaceNext_ReadOnly_Does_Not_Modify_Document ()
    {
        Editor editor = new () { Document = new TextDocument ("one two one"), ReadOnly = true };

        Assert.False (editor.ReplaceNext ("one", "1"));

        Assert.Equal ("one two one", editor.Document!.Text);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void ReplaceAll_ReadOnly_Does_Not_Modify_Document ()
    {
        Editor editor = new () { Document = new TextDocument ("one two one"), ReadOnly = true };

        Assert.Equal (0, editor.ReplaceAll ("one", "1"));

        Assert.Equal ("one two one", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_Throws_On_Null_Replacement ()
    {
        Editor editor = new () { Document = new TextDocument ("foo") };

        Assert.Throws<ArgumentNullException> (() => editor.ReplaceAll ("foo", null!));
    }

    [Fact]
    public void FindNext_Selects_Next_Match_From_Caret ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 0;

        var found = editor.FindNext ("foo");

        Assert.True (found);
        Assert.Equal (0, editor.SelectionStart);
        Assert.Equal (3, editor.SelectionEnd);
    }

    [Fact]
    public void FindNext_Wraps_Around_When_Past_Last_Match ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = editor.Document!.TextLength;

        var found = editor.FindNext ("foo");

        Assert.True (found);
        Assert.Equal (0, editor.SelectionStart);
    }

    // ─── SearchStrategy property-driven path ──────────────────────────────────────

    [Fact]
    public void SearchStrategy_Property_Round_Trips ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        ISearchStrategy strategy = SearchStrategyFactory.Create ("hello", false, false, SearchMode.Normal);

        editor.SearchStrategy = strategy;

        Assert.Same (strategy, editor.SearchStrategy);
    }

    [Fact]
    public void FindNext_NoArg_Returns_False_When_Strategy_Not_Set ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };

        Assert.False (editor.FindNext ());
    }

    [Fact]
    public void FindNext_Via_Strategy_Finds_Regex_Match ()
    {
        // The bespoke IndexOf engine cannot express this — regex is the whole point of the lift.
        Editor editor = new () { Document = new TextDocument ("abc 123 def 456") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"\d+", false, false, SearchMode.RegEx);

        Assert.True (editor.FindNext ());

        Assert.Equal (4, editor.SelectionStart);
        Assert.Equal (7, editor.SelectionEnd);
    }

    [Fact]
    public void FindNext_Via_Strategy_Respects_Whole_Word ()
    {
        Editor editor = new () { Document = new TextDocument ("cat catalog scatter cat") };
        editor.SearchStrategy = SearchStrategyFactory.Create ("cat", false, true, SearchMode.Normal);

        Assert.True (editor.FindNext ());
        Assert.Equal (0, editor.SelectionStart);

        Assert.True (editor.FindNext ());
        Assert.Equal (20, editor.SelectionStart);
        Assert.Equal (23, editor.SelectionEnd);
    }

    [Fact]
    public void FindPrevious_Finds_Rightmost_Match_Before_Caret ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo baz foo") };
        editor.CaretOffset = 15; // between "foo" #2 (offset 8-11) and "foo" #3 (offset 16-19)

        Assert.True (editor.FindPrevious ("foo"));

        Assert.Equal (8, editor.SelectionStart);
        Assert.Equal (11, editor.SelectionEnd);
    }

    [Fact]
    public void FindPrevious_Wraps_Around_When_Before_First_Match ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 0;

        Assert.True (editor.FindPrevious ("foo"));

        Assert.Equal (8, editor.SelectionStart);
    }

    [Fact]
    public void ReplaceNext_Substitutes_Regex_Backreferences ()
    {
        Editor editor = new () { Document = new TextDocument ("count=42 size=7") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"(\w+)=(\d+)", false, false, SearchMode.RegEx);

        Assert.True (editor.ReplaceNext ("$2:$1"));

        Assert.Equal ("42:count size=7", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_Via_Regex_Strategy_Replaces_All_Matches ()
    {
        Editor editor = new () { Document = new TextDocument ("abc 123 def 456 ghi 789") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"\d+", false, false, SearchMode.RegEx);

        var n = editor.ReplaceAll ("#");

        Assert.Equal (3, n);
        Assert.Equal ("abc # def # ghi #", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_Via_Regex_Strategy_Collapses_To_Single_Undo_Step ()
    {
        // Reverse-iteration replace under a single RunUpdate () scope is the new path; verify the
        // R5 invariant (one user-visible undo step) holds even when the engine is regex.
        TextDocument doc = new ("a1 b2 c3 d4 e5");
        Editor editor = new () { Document = doc };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"\d", false, false, SearchMode.RegEx);

        var n = editor.ReplaceAll ("X");
        Assert.Equal (5, n);
        Assert.Equal ("aX bX cX dX eX", doc.Text);

        doc.UndoStack.Undo ();

        Assert.Equal ("a1 b2 c3 d4 e5", doc.Text);
    }

    [Fact]
    public void ReplaceAll_Substitutes_Backreferences_In_Reverse_Without_Drift ()
    {
        // Reverse iteration is required because replacing forward would invalidate later offsets
        // as soon as the substitution differs in length from the match. This test would fail under
        // a naive forward loop because "$1:$2" expands the match.
        Editor editor = new () { Document = new TextDocument ("a=1 b=2 c=3") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"(\w)=(\d)", false, false, SearchMode.RegEx);

        var n = editor.ReplaceAll ("$1->$2");

        Assert.Equal (3, n);
        Assert.Equal ("a->1 b->2 c->3", editor.Document!.Text);
    }

    // ─── Zero-length regex match tests ────────────────────────────────────────────

    [Fact]
    public void FindNext_ZeroLength_Caret_Regex_Cycles_Through_Line_Starts ()
    {
        // ^ matches at the start of each line: offsets 0, 6, 12 for "line1\nline2\nline3"
        Editor editor = new () { Document = new TextDocument ("line1\nline2\nline3") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"^", false, false, SearchMode.RegEx);
        editor.CaretOffset = 0;

        Assert.True (editor.FindNext ());
        Assert.Equal (6, editor.CaretOffset);
        Assert.False (editor.HasSelection);

        Assert.True (editor.FindNext ());
        Assert.Equal (12, editor.CaretOffset);
        Assert.False (editor.HasSelection);

        // Wrap around back to 0
        Assert.True (editor.FindNext ());
        Assert.Equal (0, editor.CaretOffset);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void FindNext_ZeroLength_WordBoundary_Cycles_Through_Positions ()
    {
        // \b on "ab cd" matches at: 0, 2, 3, 5
        Editor editor = new () { Document = new TextDocument ("ab cd") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"\b", false, false, SearchMode.RegEx);
        editor.CaretOffset = 0;

        Assert.True (editor.FindNext ());
        Assert.Equal (2, editor.CaretOffset);

        Assert.True (editor.FindNext ());
        Assert.Equal (3, editor.CaretOffset);

        Assert.True (editor.FindNext ());
        Assert.Equal (5, editor.CaretOffset);

        // Wrap around back to 0
        Assert.True (editor.FindNext ());
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void FindPrevious_ZeroLength_Caret_Regex_Cycles_Backward ()
    {
        Editor editor = new () { Document = new TextDocument ("line1\nline2\nline3") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"^", false, false, SearchMode.RegEx);
        editor.CaretOffset = 12;

        Assert.True (editor.FindPrevious ());
        Assert.Equal (6, editor.CaretOffset);
        Assert.False (editor.HasSelection);

        Assert.True (editor.FindPrevious ());
        Assert.Equal (0, editor.CaretOffset);
        Assert.False (editor.HasSelection);

        // Wrap around to last match
        Assert.True (editor.FindPrevious ());
        Assert.Equal (12, editor.CaretOffset);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void ReplaceNext_ZeroLength_Match_Inserts_At_Position ()
    {
        // Replace ^ with "// " — prepend comment marker to each line
        Editor editor = new () { Document = new TextDocument ("a\nb\nc") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"^", false, false, SearchMode.RegEx);
        editor.CaretOffset = 0;

        Assert.True (editor.ReplaceNext ("// "));
        Assert.Equal ("// a\nb\nc", editor.Document!.Text);

        Assert.True (editor.ReplaceNext ("// "));
        Assert.Equal ("// a\n// b\nc", editor.Document!.Text);

        Assert.True (editor.ReplaceNext ("// "));
        Assert.Equal ("// a\n// b\n// c", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_ZeroLength_Match_Inserts_At_All_Positions ()
    {
        // ReplaceAll with ^ — prepend comment marker to every line
        Editor editor = new () { Document = new TextDocument ("a\nb\nc") };
        editor.SearchStrategy = SearchStrategyFactory.Create (@"^", false, false, SearchMode.RegEx);

        var n = editor.ReplaceAll ("// ");

        Assert.Equal (3, n);
        Assert.Equal ("// a\n// b\n// c", editor.Document!.Text);
    }

    // ─── FindPrevious in-progress match tests ─────────────────────────────────────

    [Fact]
    public void FindPrevious_Selects_InProgress_Match_When_Caret_Is_Inside ()
    {
        // Caret at offset 9 is inside the second "foo" (offsets 8..10).
        // FindPrevious should select [8, 11) — the in-progress match.
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 9;

        Assert.True (editor.FindPrevious ("foo"));

        Assert.Equal (8, editor.SelectionStart);
        Assert.Equal (11, editor.SelectionEnd);
    }

    [Fact]
    public void FindPrevious_Twice_From_InProgress_Moves_To_Prior_Hit ()
    {
        // First call lands on the in-progress match, second call lands on the earlier match.
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 9;

        Assert.True (editor.FindPrevious ("foo"));
        Assert.Equal (8, editor.SelectionStart);
        Assert.Equal (11, editor.SelectionEnd);

        // After first FindPrevious, selection is [8,11), so SelectionStart=8.
        // Second call should find the match before offset 8 → offset 0.
        Assert.True (editor.FindPrevious ("foo"));
        Assert.Equal (0, editor.SelectionStart);
        Assert.Equal (3, editor.SelectionEnd);
    }

    [Fact]
    public void FindPrevious_WrapAround_False_Returns_False_When_No_Match_Before_Caret ()
    {
        Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 0;

        Assert.False (editor.FindPrevious ("foo", false, false));
    }
}
