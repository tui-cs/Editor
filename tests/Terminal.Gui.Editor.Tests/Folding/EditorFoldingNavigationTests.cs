// Copilot - Claude Opus 4.6

using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Folding;

/// <summary>
///     Tests for caret navigation across folded regions (issue #190).
/// </summary>
public class EditorFoldingNavigationTests
{
    /// <summary>
    ///     CursorDown on the line immediately above a folded region should move to the fold's
    ///     start line (which remains visible), and a second Down should skip past the fold.
    ///     The fold must NOT be expanded.
    /// </summary>
    [Fact]
    public void CursorDown_Skips_Folded_Region ()
    {
        // Lines: 1="line1", 2="line2", 3="line3", 4="line4", 5="line5"
        var text = "line1\nline2\nline3\nline4\nline5";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.FoldingManager = fm;

        // Fold lines 2-4 (offsets for "line2\nline3\nline4")
        // line2 starts at offset 6, line4 ends at offset 23
        DocumentLine line2 = editor.Document!.GetLineByNumber (2);
        DocumentLine line4 = editor.Document!.GetLineByNumber (4);
        FoldingSection fold = fm.CreateFolding (line2.Offset, line4.EndOffset);
        fold.IsFolded = true;

        // Place caret on line 1 (offset 0)
        editor.CaretOffset = 0;

        // Press Down — should land on fold start line (line 2, which is visible)
        editor.InvokeCommand (Command.Down);
        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (2, caretLine.LineNumber);

        // Press Down again — should skip past the fold to line 5
        editor.InvokeCommand (Command.Down);
        caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (5, caretLine.LineNumber);

        // The fold must remain folded — it should NOT have been expanded
        Assert.True (fold.IsFolded);
    }

    /// <summary>
    ///     CursorUp on the line immediately below a folded region should skip the fold
    ///     and land on the fold's start line (the visible header).
    /// </summary>
    [Fact]
    public void CursorUp_Skips_Folded_Region ()
    {
        var text = "line1\nline2\nline3\nline4\nline5";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.FoldingManager = fm;

        // Fold lines 2-4
        DocumentLine line2 = editor.Document!.GetLineByNumber (2);
        DocumentLine line4 = editor.Document!.GetLineByNumber (4);
        FoldingSection fold = fm.CreateFolding (line2.Offset, line4.EndOffset);
        fold.IsFolded = true;

        // Place caret on line 5
        DocumentLine line5 = editor.Document!.GetLineByNumber (5);
        editor.CaretOffset = line5.Offset;

        // Press Up — should land on fold start line (line 2)
        editor.InvokeCommand (Command.Up);
        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (2, caretLine.LineNumber);

        // Press Up again — should land on line 1
        editor.InvokeCommand (Command.Up);
        caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (1, caretLine.LineNumber);

        // The fold must remain folded
        Assert.True (fold.IsFolded);
    }

    /// <summary>
    ///     When a fold is collapsed via mouse (gutter toggle) and the caret is inside
    ///     the folded region, the caret should move to the fold's start line rather than
    ///     disappearing.
    /// </summary>
    [Fact]
    public void Folding_Caret_Inside_Collapsed_Fold_Moves_To_FoldStart ()
    {
        var text = "line1\nline2\nline3\nline4\nline5";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.FoldingManager = fm;

        // Place caret on line 3 (inside the region we're about to fold)
        DocumentLine line3 = editor.Document!.GetLineByNumber (3);
        editor.CaretOffset = line3.Offset;

        // Fold lines 2-4 (simulating gutter click)
        DocumentLine line2 = editor.Document!.GetLineByNumber (2);
        DocumentLine line4 = editor.Document!.GetLineByNumber (4);
        FoldingSection fold = fm.CreateFolding (line2.Offset, line4.EndOffset);
        fold.IsFolded = true;

        // The caret should now be on line 2 (the fold's start line), not hidden
        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (2, caretLine.LineNumber);

        // The fold must remain folded (not expanded by EnsureCaretNotInFold)
        Assert.True (fold.IsFolded);
    }

    /// <summary>
    ///     When a fold is collapsed and the caret is NOT inside the fold,
    ///     the caret should remain at its current position unchanged.
    /// </summary>
    [Fact]
    public void Folding_Caret_Outside_Fold_Remains_Unchanged ()
    {
        var text = "line1\nline2\nline3\nline4\nline5";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.FoldingManager = fm;

        // Caret on line 5 (outside the fold)
        DocumentLine line5 = editor.Document!.GetLineByNumber (5);
        editor.CaretOffset = line5.Offset;
        var expectedOffset = editor.CaretOffset;

        // Fold lines 2-4
        DocumentLine line2 = editor.Document!.GetLineByNumber (2);
        DocumentLine line4 = editor.Document!.GetLineByNumber (4);
        FoldingSection fold = fm.CreateFolding (line2.Offset, line4.EndOffset);
        fold.IsFolded = true;

        // Caret should remain on line 5 at same offset
        Assert.Equal (expectedOffset, editor.CaretOffset);
    }

    /// <summary>
    ///     CursorDown with folded region: the sticky virtual column should be preserved
    ///     when navigating past the fold.
    /// </summary>
    [Fact]
    public void CursorDown_Over_Fold_Preserves_StickyColumn ()
    {
        // "abcde\nXX\nYY\nZZ\nabcde"
        var text = "abcde\nXX\nYY\nZZ\nabcde";
        Editor editor = new () { Document = new TextDocument (text) };
        FoldingManager fm = new (editor.Document!);
        editor.FoldingManager = fm;

        // Fold lines 2-4 (the short lines)
        DocumentLine line2 = editor.Document!.GetLineByNumber (2);
        DocumentLine line4 = editor.Document!.GetLineByNumber (4);
        FoldingSection fold = fm.CreateFolding (line2.Offset, line4.EndOffset);
        fold.IsFolded = true;

        // Place caret at col 3 on line 1 (offset 3)
        editor.CaretOffset = 3;

        // Press Down twice — first to fold header (line 2), then past fold to line 5
        editor.InvokeCommand (Command.Down);
        editor.InvokeCommand (Command.Down);

        DocumentLine caretLine = editor.Document!.GetLineByOffset (editor.CaretOffset);
        Assert.Equal (5, caretLine.LineNumber);

        // Column should be 3 (preserved sticky column)
        var colInLine = editor.CaretOffset - caretLine.Offset;
        Assert.Equal (3, colInLine);
    }
}
