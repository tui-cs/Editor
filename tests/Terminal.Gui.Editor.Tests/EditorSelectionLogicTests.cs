// Claude - claude-opus-4-7

using Terminal.Gui.Input;
using Terminal.Gui.Text.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Selection state logic — anchor + caret arithmetic, ClearSelection, ReplaceSelection, SelectAll.
///     No <see cref="App.IApplication" /> needed; integration tests cover keyboard wiring.
/// </summary>
public class EditorSelectionLogicTests
{
    [Fact]
    public void Default_NoSelection ()
    {
        Views.Editor editor = new ();

        Assert.False (editor.HasSelection);
        Assert.Null (editor.Selection);
        Assert.Equal (0, editor.SelectionLength);
    }

    [Fact]
    public void SelectAll_Selects_Whole_Document ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("alpha\nbeta") };

        editor.SelectAll ();

        Assert.True (editor.HasSelection);
        Assert.Equal (0, editor.SelectionStart);
        Assert.Equal (editor.Document.TextLength, editor.SelectionEnd);
        Assert.Equal (editor.Document.TextLength, editor.CaretOffset);
    }

    [Fact]
    public void ClearSelection_Removes_Selection ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abc") };
        editor.SelectAll ();

        editor.ClearSelection ();

        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void ReplaceSelection_With_Empty_Removes_Range ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.SelectAll ();

        editor.ReplaceSelection (string.Empty);

        Assert.Equal (string.Empty, editor.Document.Text);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void ReplaceSelection_With_Text_Substitutes ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.SelectAll ();

        editor.ReplaceSelection ("hi");

        Assert.Equal ("hi", editor.Document.Text);
        Assert.False (editor.HasSelection);
        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void ReplaceSelection_ReadOnly_Does_Not_Modify_Document ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world"), ReadOnly = true };
        editor.SelectAll ();

        editor.ReplaceSelection ("hi");

        Assert.Equal ("hello world", editor.Document.Text);
        Assert.True (editor.HasSelection);
    }

    [Fact]
    public void Selection_TextSegment_Reflects_Range ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello") };
        editor.SelectAll ();

        TextSegment? sel = editor.Selection;

        Assert.NotNull (sel);
        Assert.Equal (0, sel.StartOffset);
        Assert.Equal (5, sel.Length);
    }

    [Fact]
    public void Selection_Tracks_Insertion_Before_Range ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abcdef") };
        editor.SelectRange (2, 3);

        editor.Document!.Insert (0, ">>");

        Assert.True (editor.HasSelection);
        Assert.Equal (4, editor.SelectionStart);
        Assert.Equal (7, editor.SelectionEnd);
        Assert.Equal ("cde", editor.SelectedText);
    }

    [Fact]
    public void SelectionChanged_Fires_On_SelectAll_And_Clear ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abc") };
        var fires = 0;
        editor.SelectionChanged += (_, _) => fires++;

        editor.SelectAll ();
        Assert.Equal (1, fires);

        editor.ClearSelection ();
        Assert.Equal (2, fires);

        editor.ClearSelection ();
        Assert.Equal (2, fires); // no-op when already cleared
    }

    [Fact]
    public void SelectionChanged_Does_Not_Fire_For_SelectAll_On_Empty_Document ()
    {
        Views.Editor editor = new () { Document = new TextDocument (string.Empty) };
        var fires = 0;
        editor.SelectionChanged += (_, _) => fires++;

        editor.SelectAll ();

        Assert.False (editor.HasSelection);
        Assert.Equal (0, fires);
    }

    [Fact]
    public void SelectionChanged_Does_Not_Fire_When_ExtendCaret_Is_NoOp ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abc") };
        editor.CaretOffset = 3; // already at end
        var fires = 0;
        editor.SelectionChanged += (_, _) => fires++;

        // Extend right by one character but caret can't move — must not raise SelectionChanged.
        editor.InvokeCommand (Command.RightExtend);

        Assert.Equal (0, fires);
        Assert.False (editor.HasSelection);
    }

    [Fact]
    public void SelectionChanged_Does_Not_Fire_When_ExtendCaretVertically_Is_NoOp ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abc") };
        editor.CaretOffset = 0;
        var fires = 0;
        editor.SelectionChanged += (_, _) => fires++;

        // Single-line doc, so extending up has nowhere to go.
        editor.InvokeCommand (Command.UpExtend);

        Assert.Equal (0, fires);
        Assert.False (editor.HasSelection);
    }
}
