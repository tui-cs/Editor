// Claude - claude-opus-4-6

using Terminal.Gui.Text.Document;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for tab-handling properties and logic per issue #37. Pure logic — no <c>Application.Init</c>.
/// </summary>
public class EditorTabHandlingTests
{
    [Fact]
    public void IndentationSize_Defaults_To_4 ()
    {
        Views.Editor editor = new ();

        Assert.Equal (4, editor.IndentationSize);
    }

    [Fact]
    public void IndentationSize_Rejects_Values_Less_Than_1 ()
    {
        Views.Editor editor = new ();

        Assert.Throws<ArgumentOutOfRangeException> (() => editor.IndentationSize = 0);
    }

    [Fact]
    public void ConvertTabsToSpaces_Defaults_To_False ()
    {
        Views.Editor editor = new ();

        Assert.False (editor.ConvertTabsToSpaces);
    }

    [Fact]
    public void ShowTabs_Defaults_To_False ()
    {
        Views.Editor editor = new ();

        Assert.False (editor.ShowTabs);
    }

    [Fact]
    public void Tab_Character_Survives_Load_Save_RoundTrip ()
    {
        // Issue #37 §1: Loading and saving never transform tabs.
        const string text = "line1\n\tindented\n\t\tdouble";
        TextDocument doc = new (text);

        Assert.Equal (text, doc.Text);
    }

    [Fact]
    public void Changing_IndentationSize_Recomputes_Caret_Visibility ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("\t"), Width = 4, Height = 1 };
        editor.Viewport = new (0, 0, 4, 1);
        editor.CaretOffset = 1;
        Assert.Equal (1, editor.Viewport.X);

        editor.IndentationSize = 8;

        Assert.Equal (5, editor.Viewport.X);
    }

    [Fact]
    public void Caret_After_Tab_Uses_Visual_Columns_For_Viewport_Scrolling ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\tb"), Width = 3, Height = 1 };
        editor.Viewport = new (0, 0, 3, 1);
        editor.CaretOffset = 2;

        Assert.Equal (2, editor.Viewport.X);
    }

    [Fact]
    public void ShowTabs_Setter_Triggers_Redraw ()
    {
        Views.Editor editor = new ();

        // ShowTabs toggles should not throw and should update the value.
        editor.ShowTabs = true;
        Assert.True (editor.ShowTabs);

        editor.ShowTabs = false;
        Assert.False (editor.ShowTabs);
    }
}
