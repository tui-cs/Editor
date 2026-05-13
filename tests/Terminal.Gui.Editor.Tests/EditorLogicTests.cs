// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Highlighting;
using Terminal.Gui.Views;
using Terminal.Gui.Views.Rendering;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

/// <summary>
///     Tests for <see cref="Editor" /> behaviors that don't need an <see cref="App.IApplication" /> —
///     caret tracking, document rewiring, and edit reactions. UI-side tests (full layout/draw,
///     input injection) live in IntegrationTests, which also runs in parallel.
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

        // Mutating the replacement still drives the editor's caret anchor.
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
    public void Caret_Tracks_Insertion_At_It_With_AfterInsertion_Semantics ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;

        editor.Document.Insert (2, "XYZ");

        Assert.Equal (5, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Tracks_External_Edit_On_Shared_Document ()
    {
        TextDocument document = new ("0123456789");
        Views.Editor editor = new () { Document = document };
        Views.Editor secondConsumer = new () { Document = document };
        editor.CaretOffset = 8;

        secondConsumer.Document!.Insert (3, "abc");

        Assert.Equal (11, editor.CaretOffset);
    }

    [Fact]
    public void CaretChanged_Fires_When_Document_Insertion_Shifts_Caret ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5;
        var fires = 0;
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
        var fires = 0;
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
        var fires = 0;
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
    public void ReadOnly_Defaults_To_False ()
    {
        Views.Editor editor = new ();

        Assert.False (editor.ReadOnly);
    }

    [Fact]
    public void Caret_After_Tab_Uses_Visual_Columns_For_Viewport_Scrolling ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("a\tb"), Width = 3, Height = 1 };
        editor.Viewport = new Rectangle (0, 0, 3, 1);
        editor.CaretOffset = 2;

        Assert.Equal (2, editor.Viewport.X);
    }

    [Fact]
    public void Dispose_Unsubscribes_From_Document_Changed ()
    {
        TextDocument doc = new ("hello");
        Views.Editor editor = new () { Document = doc };
        editor.CaretOffset = 3;

        var caretFires = 0;
        editor.CaretChanged += (_, _) => caretFires++;

        editor.Dispose ();

        // After dispose, mutating the still-reachable document must not affect the disposed editor's state.
        var caretBefore = editor.CaretOffset;
        doc.Insert (0, ">>>");

        Assert.Equal (caretBefore, editor.CaretOffset);
        Assert.Equal (0, caretFires);
    }

    [Fact]
    public void Changing_IndentationSize_Recomputes_Caret_Visibility ()
    {
        Views.Editor editor = new () { Document = new TextDocument ("\t"), Width = 4, Height = 1 };
        editor.Viewport = new Rectangle (0, 0, 4, 1);
        editor.CaretOffset = 1;
        Assert.Equal (1, editor.Viewport.X);

        editor.IndentationSize = 8;

        Assert.Equal (5, editor.Viewport.X);
    }

    // The Editor.HighlightingDefinition property replaced the obsolete SyntaxHighlighter /
    // SyntaxLanguage stopgap (issues #28, #32). It drives a HighlightingColorizer transformer
    // through the visual-line pipeline.
    [Fact]
    public void HighlightingDefinition_Is_Null_By_Default ()
    {
        Views.Editor editor = new ();

        Assert.Null (editor.HighlightingDefinition);
    }

    [Fact]
    public void HighlightingDefinition_Adds_Colorizer_To_LineTransformers ()
    {
        Views.Editor editor = new ();
        editor.Document = new TextDocument ("public class Foo { }");

        IHighlightingDefinition? csharp = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.NotNull (csharp);

        editor.HighlightingDefinition = csharp;

        Assert.Single (editor.LineTransformers);
        Assert.IsType<HighlightingColorizer> (editor.LineTransformers[0]);
    }

    [Fact]
    public void HighlightingDefinition_Set_Null_Removes_Colorizer ()
    {
        Views.Editor editor = new ();
        editor.Document = new TextDocument ("public class Foo { }");

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.Single (editor.LineTransformers);

        editor.HighlightingDefinition = null;
        Assert.Empty (editor.LineTransformers);
    }
}
