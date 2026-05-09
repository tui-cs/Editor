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
    public void Default_DocumentIsEmpty ()
    {
        Views.Editor editor = new ();

        Assert.NotNull (editor.Document);
        Assert.Equal (string.Empty, editor.Document!.Text);
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

        // Mutating the original after the swap should NOT move the editor's caret — the editor
        // must have unsubscribed from the original document's Changed event.
        editor.CaretOffset = 0;
        original?.Insert (0, "xxx");
        Assert.Equal (0, editor.CaretOffset);

        // Mutating the replacement still drives the editor's caret arithmetic.
        replacement.Insert (0, "y");
        Assert.Equal (1, editor.CaretOffset);
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
    public void CaretChanged_Fires_When_Document_Insertion_Shifts_Caret ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5;
        int fires = 0;
        editor.CaretChanged += (_, _) => fires++;

        editor.Document.Insert (0, ">>>");

        Assert.Equal (8, editor.CaretOffset);
        Assert.Equal (1, fires);
    }

    [Fact]
    public void CaretChanged_Does_Not_Fire_When_Document_Insertion_Leaves_Caret_Alone ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;
        int fires = 0;
        editor.CaretChanged += (_, _) => fires++;

        // Insert strictly after the caret — caret offset should not change.
        editor.Document.Insert (4, "X");

        Assert.Equal (2, editor.CaretOffset);
        Assert.Equal (0, fires);
    }

    [Fact]
    public void CaretChanged_Fires_When_Document_Removal_Snaps_Caret ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 4;
        int fires = 0;
        editor.CaretChanged += (_, _) => fires++;

        editor.Document.Remove (2, 5); // straddles caret → snaps to 2

        Assert.Equal (2, editor.CaretOffset);
        Assert.Equal (1, fires);
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
    public void ShowLineNumbers_Toggles_LeftPadding ()
    {
        Views.Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };

        Assert.Equal (0, editor.Padding.Thickness.Left);

        editor.ShowLineNumbers = true;
        Assert.Equal (2, editor.Padding.Thickness.Left);

        editor.ShowLineNumbers = false;
        Assert.Equal (0, editor.Padding.Thickness.Left);
    }

    [Fact]
    public void ShowLineNumbers_Updates_Padding_When_LineCount_DigitWidth_Changes ()
    {
        Views.Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };
        editor.ShowLineNumbers = true;

        Assert.Equal (2, editor.Padding.Thickness.Left);

        editor.Document.Insert (editor.Document.TextLength, "\n10");

        Assert.Equal (3, editor.Padding.Thickness.Left);
    }

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
    public void Caret_After_Tab_Uses_Visual_Columns_For_Viewport_Scrolling ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\tb"), Width = 3, Height = 1 };
        editor.Viewport = new (0, 0, 3, 1);
        editor.CaretOffset = 2;

        Assert.Equal (2, editor.Viewport.X);
    }

    [Fact]
    public void Dispose_Unsubscribes_From_Document_Changed ()
    {
        TextDocument doc = new ("hello");
        Views.Editor editor = new () { Document = doc };
        editor.CaretOffset = 3;

        int caretFires = 0;
        editor.CaretChanged += (_, _) => caretFires++;

        editor.Dispose ();

        // After dispose, mutating the still-reachable document must not affect the disposed editor's state.
        int caretBefore = editor.CaretOffset;
        doc.Insert (0, ">>>");

        Assert.Equal (caretBefore, editor.CaretOffset);
        Assert.Equal (0, caretFires);
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

    // The Editor.SyntaxHighlighter / Editor.SyntaxLanguage surface reuses Terminal.Gui's
    // Markdown ISyntaxHighlighter as a stopgap until specs/00-plan.md Phase 6 lifts the
    // AvaloniaEdit Highlighting/ pipeline (HighlightingColorizer : IVisualLineTransformer,
    // tracked by issue #28). Mark the property [Obsolete] so external code knows not to
    // take a hard dependency on the temporary contract. See issue #32.
    //
    // The CS0618 suppressions here exist so the compiler doesn't fight the test's purpose,
    // which is to *verify* the obsoletion is wired up.
    [Fact]
    public void SyntaxHighlighter_Is_Obsolete ()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        PropertyInfo prop = typeof (Views.Editor).GetProperty (nameof (Views.Editor.SyntaxHighlighter))!;
#pragma warning restore CS0618 // Type or member is obsolete
        ObsoleteAttribute? attr = prop.GetCustomAttribute<ObsoleteAttribute> ();

        Assert.NotNull (attr);
        Assert.Contains ("28", attr!.Message ?? string.Empty);
    }

    [Fact]
    public void SyntaxLanguage_Is_Obsolete ()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        PropertyInfo prop = typeof (Views.Editor).GetProperty (nameof (Views.Editor.SyntaxLanguage))!;
#pragma warning restore CS0618 // Type or member is obsolete
        ObsoleteAttribute? attr = prop.GetCustomAttribute<ObsoleteAttribute> ();

        Assert.NotNull (attr);
        Assert.Contains ("28", attr!.Message ?? string.Empty);
    }
}
