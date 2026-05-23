// Claude - claude-opus-4-7

using System.Drawing;
using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Document.Folding;
using Terminal.Gui.Editor.Rendering;
using Terminal.Gui.Editor.Highlighting;
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
        Editor editor = new ();

        Assert.NotNull (editor.Document);
        Assert.Equal (string.Empty, editor.Document!.Text);
    }

    [Fact]
    public void CaretOffset_Clamps_To_DocumentLength ()
    {
        Editor editor = new () { Document = new TextDocument ("abc") };

        editor.CaretOffset = 99;
        Assert.Equal (3, editor.CaretOffset);

        editor.CaretOffset = -10;
        Assert.Equal (0, editor.CaretOffset);
    }

    [Fact]
    public void CaretOffset_Set_Raises_CaretChanged ()
    {
        Editor editor = new () { Document = new TextDocument ("abcdef") };
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
        Editor editor = new ();
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
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 5;

        editor.Document.Insert (0, ">>>");

        Assert.Equal (8, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Tracks_Insertion_At_It_With_AfterInsertion_Semantics ()
    {
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;

        editor.Document.Insert (2, "XYZ");

        Assert.Equal (5, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Tracks_External_Edit_On_Shared_Document ()
    {
        TextDocument document = new ("0123456789");
        Editor editor = new () { Document = document };
        Editor secondConsumer = new () { Document = document };
        editor.CaretOffset = 8;

        secondConsumer.Document!.Insert (3, "abc");

        Assert.Equal (11, editor.CaretOffset);
    }

    [Fact]
    public void CaretChanged_Fires_When_Document_Insertion_Shifts_Caret ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
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
        Editor editor = new () { Document = new TextDocument ("hello") };
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
        Editor editor = new () { Document = new TextDocument ("hello world") };
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
        Editor editor = new () { Document = new TextDocument ("hello") };
        editor.CaretOffset = 2;

        editor.Document.Insert (4, "X");

        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void Caret_Snaps_To_Removal_Start_When_Inside_Removed_Range ()
    {
        Editor editor = new () { Document = new TextDocument ("hello world") };
        editor.CaretOffset = 4;

        editor.Document.Remove (2, 5); // removes "llo w" → "heorld"

        Assert.Equal (2, editor.CaretOffset);
    }

    [Fact]
    public void ShowLineNumbers_Toggles_LeftPadding ()
    {
        Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };

        Assert.Equal (0, editor.Padding.Thickness.Left);

        editor.GutterOptions = GutterOptions.LineNumbers;
        Assert.Equal (2, editor.Padding.Thickness.Left);

        editor.GutterOptions = GutterOptions.None;
        Assert.Equal (0, editor.Padding.Thickness.Left);
    }

    [Fact]
    public void ShowLineNumbers_Updates_Padding_When_LineCount_DigitWidth_Changes ()
    {
        Editor editor = new () { Document = new TextDocument (string.Join ('\n', Enumerable.Range (1, 9))) };
        editor.GutterOptions = GutterOptions.LineNumbers;

        Assert.Equal (2, editor.Padding.Thickness.Left);

        editor.Document.Insert (editor.Document.TextLength, "\n10");

        Assert.Equal (3, editor.Padding.Thickness.Left);
    }

    [Fact]
    public void IndentationSize_Defaults_To_4 ()
    {
        Editor editor = new ();

        Assert.Equal (4, editor.IndentationSize);
    }

    [Fact]
    public void IndentationSize_Rejects_Values_Less_Than_1 ()
    {
        Editor editor = new ();

        Assert.Throws<ArgumentOutOfRangeException> (() => editor.IndentationSize = 0);
    }

    [Fact]
    public void ConvertTabsToSpaces_Defaults_To_False ()
    {
        Editor editor = new ();

        Assert.False (editor.ConvertTabsToSpaces);
    }

    [Fact]
    public void ShowTabs_Defaults_To_False ()
    {
        Editor editor = new ();

        Assert.False (editor.ShowTabs);
    }

    [Fact]
    public void ReadOnly_Defaults_To_False ()
    {
        Editor editor = new ();

        Assert.False (editor.ReadOnly);
    }

    [Fact]
    public void Caret_After_Tab_Uses_Visual_Columns_For_Viewport_Scrolling ()
    {
        Editor editor = new () { Document = new TextDocument ("a\tb"), Width = 3, Height = 1 };
        editor.Viewport = new Rectangle (0, 0, 3, 1);
        editor.CaretOffset = 2;

        Assert.Equal (2, editor.Viewport.X);
    }

    [Fact]
    public void Dispose_Unsubscribes_From_Document_Changed ()
    {
        TextDocument doc = new ("hello");
        Editor editor = new () { Document = doc };
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
        Editor editor = new () { Document = new TextDocument ("\t"), Width = 4, Height = 1 };
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
        Editor editor = new ();

        Assert.Null (editor.HighlightingDefinition);
    }

    [Fact]
    public void HighlightingDefinition_Adds_Colorizer_To_LineTransformers ()
    {
        Editor editor = new ();
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
        Editor editor = new ();
        editor.Document = new TextDocument ("public class Foo { }");

        editor.HighlightingDefinition = HighlightingManager.Instance.GetDefinition ("C#");
        Assert.Single (editor.LineTransformers);

        editor.HighlightingDefinition = null;
        Assert.Empty (editor.LineTransformers);
    }

    [Fact]
    public void GetVisibleLineNumbers_Skips_Deepest_Fold_When_Multiple_Start_On_Same_Line ()
    {
        // Lines: 1="a{", 2="b", 3="c", 4="d{", 5="e", 6="f}", 7="g}"
        // Two folds start on line 1: short (lines 1-4) and long (lines 1-7).
        // When both are collapsed, only line 1 should be visible.
        var text = "a{\nb\nc\nd{\ne\nf}\ng}";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.GutterOptions = GutterOptions.LineNumbers | GutterOptions.Folding;
        editor.FoldingManager = fm;

        // Create two folded sections starting on line 1 with different end offsets.
        // Short fold: offset 0 (line 1) to offset 8 (line 4 "d{")
        FoldingSection shortFold = fm.CreateFolding (0, 8);
        shortFold.IsFolded = true;

        // Long fold: offset 0 (line 1) to offset 16 (line 7 "g}")
        FoldingSection longFold = fm.CreateFolding (0, text.Length);
        longFold.IsFolded = true;

        List<int> visible = editor.GetVisibleLineNumbers ();

        // Only line 1 should be visible — the long fold hides lines 2-7.
        Assert.Single (visible);
        Assert.Equal (1, visible[0]);
    }

    [Fact]
    public void EnableForDesign_PopulatesNonEmptyDocument ()
    {
        Editor editor = new ();

        var result = editor.EnableForDesign ();

        Assert.True (result);
        Assert.NotNull (editor.Document);
        Assert.True (editor.Document!.TextLength > 0, "EnableForDesign must seed non-empty content.");
        Assert.True (editor.Document.LineCount > 1, "Sample content must span more than one line.");
        Assert.NotNull (editor.HighlightingDefinition);
    }

    [Fact]
    public void Text_Get_Returns_DocumentText ()
    {
        Editor editor = new () { Document = new TextDocument ("hello\nworld") };

        Assert.Equal ("hello\nworld", editor.Text);
    }

    [Fact]
    public void Text_Set_Updates_DocumentText ()
    {
        Editor editor = new ();

        editor.Text = "line1\nline2\nline3";

        Assert.Equal ("line1\nline2\nline3", editor.Document!.Text);
        Assert.Equal (3, editor.Document.LineCount);
    }

    [Fact]
    public void Text_Get_Returns_Empty_When_Default ()
    {
        Editor editor = new ();

        Assert.Equal (string.Empty, editor.Text);
    }

    [Fact]
    public void Text_RoundTrip ()
    {
        Editor editor = new ();
        const string content = "{\n  \"key\": \"value\"\n}";

        editor.Text = content;

        Assert.Equal (content, editor.Text);
    }
}
