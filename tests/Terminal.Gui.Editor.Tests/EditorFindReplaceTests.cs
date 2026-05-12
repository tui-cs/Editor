// Claude - claude-opus-4-7

using Terminal.Gui.Text.Document;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> find/replace surface. Pure logic — no
///     <see cref="App.IApplication" />, no driver. The bigger migration onto
///     <c>ISearchStrategy</c> is tracked by spec §8 D4; these tests pin the current
///     contract so the migration is a refactor, not a rewrite.
/// </summary>
public class EditorFindReplaceTests
{
    [Fact]
    public void ReplaceAll_Replaces_Every_Match_And_Returns_Count ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("foo bar foo baz foo") };

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
        Views.Editor editor = new () { Document = doc };

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
        Views.Editor editor = new () { Document = doc };

        var n = editor.ReplaceAll ("xyz", "abc");

        Assert.Equal (0, n);
        Assert.Equal ("hello world", doc.Text);
    }

    [Fact]
    public void ReplaceNext_ReadOnly_Does_Not_Modify_Document ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("one two one"), ReadOnly = true };

        Assert.False (editor.ReplaceNext ("one", "1"));

        Assert.Equal ("one two one", editor.Document!.Text);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void ReplaceAll_ReadOnly_Does_Not_Modify_Document ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("one two one"), ReadOnly = true };

        Assert.Equal (0, editor.ReplaceAll ("one", "1"));

        Assert.Equal ("one two one", editor.Document!.Text);
    }

    [Fact]
    public void ReplaceAll_Throws_On_Null_Replacement ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("foo") };

        Assert.Throws<ArgumentNullException> (() => editor.ReplaceAll ("foo", null!));
    }

    [Fact]
    public void FindNext_Selects_Next_Match_From_Caret ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = 0;

        var found = editor.FindNext ("foo");

        Assert.True (found);
        Assert.Equal (0, editor.SelectionStart);
        Assert.Equal (3, editor.SelectionEnd);
    }

    [Fact]
    public void FindNext_Wraps_Around_When_Past_Last_Match ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("foo bar foo") };
        editor.CaretOffset = editor.Document!.TextLength;

        var found = editor.FindNext ("foo");

        Assert.True (found);
        Assert.Equal (0, editor.SelectionStart);
    }
}
