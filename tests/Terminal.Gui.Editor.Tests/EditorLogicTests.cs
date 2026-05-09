// Claude - claude-opus-4-7

using System.Reflection;
using Terminal.Gui.Text.Document;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> behaviors that don't need <c>Application.Init</c> — caret math,
///     document rewiring, edit-tracking arithmetic. UI-side tests live in IntegrationTests.
/// </summary>
public class EditorLogicTests
{
    [Fact]
    public void Default_DocumentSays_HelloWorld ()
    {
        Views.Editor editor = new ();

        Assert.NotNull (editor.Document);
        Assert.Equal ("Hello world", editor.Document.Text);
    }

    [Fact]
    public void CaretOffset_Clamps_To_DocumentLength ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abc") };

        editor.CaretOffset = 99;
        Assert.Equal (3, editor.CaretOffset);

        editor.CaretOffset = -10;
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CaretOffset_Set_Raises_CaretChanged ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("abcdef") };
        var fires = 0;
        editor.CaretChanged += (_, _) => fires++;

        editor.CaretOffset = 3;
        Assert.Equal (1, fires);

        editor.CaretOffset = 3;
        Assert.Equal (1, fires);
    }

    [Fact]
    public void Document_Setter_Rewires_ChangeHandler ()
    {
        Views.Editor editor = new ();
        TextDocument? original = editor.Document;

        TextDocument replacement = new ("first\nsecond");
        editor.Document = replacement;

        Assert.Same (replacement, editor.Document);

        // Mutating the original after the swap should NOT raise DocumentChanged on the editor.
        var fired = false;
        editor.DocumentChanged += (_, _) => fired = true;
        original?.Insert (0, "x");
        Assert.False (fired);

        // Mutating the replacement does raise it.
        replacement.Insert (0, "y");
        Assert.True (fired);
    }

    [Fact]
    public void Caret_Tracks_Insertion_Before_It ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5;

        editor.Document.Insert (0, ">>>");

        Assert.Equal (8, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Stays_Put_For_Insertion_After_It ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;

        editor.Document.Insert (4, "X");

        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Snaps_To_Removal_Start_When_Inside_Removed_Range ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 4;

        editor.Document.Remove (2, 5); // removes "llo w" → "heorld"

        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void TabWidth_Defaults_To_4 ()
    {
        Views.Editor editor = new ();

        Assert.Equal (4, editor.TabWidth);
    }

    [Fact]
    public void TabWidth_Rejects_Values_Less_Than_1 ()
    {
        Views.Editor editor = new ();

        Assert.Throws<ArgumentOutOfRangeException> (() => editor.TabWidth = 0);
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
    public void Changing_TabWidth_Recomputes_Caret_Visibility ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("\t"), Width = 4, Height = 1 };
        editor.Viewport = new (0, 0, 4, 1);
        editor.CaretOffset = 1;
        Assert.Equal (1, editor.Viewport.X);

        editor.TabWidth = 8;

        Assert.Equal (5, editor.Viewport.X);
    }

    // The Editor.SyntaxHighlighter / Editor.SyntaxLanguage surface reuses Terminal.Gui's
    // Markdown ISyntaxHighlighter as a stopgap until specs/00-plan.md Phase 6 lifts the
    // AvaloniaEdit Highlighting/ pipeline (HighlightingColorizer : IVisualLineTransformer,
    // tracked by issue #28). Mark the property [Obsolete] so external code knows not to
    // take a hard dependency on the temporary contract. See issue #32.
    [Fact]
    public void SyntaxHighlighter_Is_Obsolete ()
    {
        PropertyInfo prop = typeof (Views.Editor).GetProperty (nameof (Views.Editor.SyntaxHighlighter))!;
        ObsoleteAttribute? attr = prop.GetCustomAttribute<ObsoleteAttribute> ();

        Assert.NotNull (attr);
        Assert.Contains ("28", attr!.Message ?? string.Empty);
    }

    [Fact]
    public void SyntaxLanguage_Is_Obsolete ()
    {
        PropertyInfo prop = typeof (Views.Editor).GetProperty (nameof (Views.Editor.SyntaxLanguage))!;
        ObsoleteAttribute? attr = prop.GetCustomAttribute<ObsoleteAttribute> ();

        Assert.NotNull (attr);
        Assert.Contains ("28", attr!.Message ?? string.Empty);
    }
}
