// Copilot - Claude Opus 4.6

using System.Collections.ObjectModel;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Folding;

public class FoldingManagerTests
{
    [Fact]
    public void CreateFolding_Returns_FoldingSection ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);

        FoldingSection section = fm.CreateFolding (0, 20);

        Assert.NotNull (section);
        Assert.Equal (0, section.StartOffset);
        Assert.Equal (20, section.EndOffset);
        Assert.False (section.IsFolded);
    }

    [Fact]
    public void CreateFolding_InvalidRange_Throws ()
    {
        TextDocument doc = new ("hello");
        FoldingManager fm = new (doc);

        Assert.Throws<ArgumentException> (() => fm.CreateFolding (5, 3));
    }

    [Fact]
    public void RemoveFolding_Clears_IsFolded ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (0, 15);
        section.IsFolded = true;

        fm.RemoveFolding (section);

        Assert.False (section.IsFolded);
        Assert.Empty (fm.AllFoldings);
    }

    [Fact]
    public void Clear_Removes_All_Foldings ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        fm.CreateFolding (0, 10);
        fm.CreateFolding (10, 20);

        fm.Clear ();

        Assert.Empty (fm.AllFoldings);
    }

    [Fact]
    public void IsFolded_Toggle_Raises_FoldingChanged ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (0, 15);
        var raised = 0;
        fm.FoldingChanged += (_, _) => raised++;

        section.IsFolded = true;

        Assert.True (raised > 0);
    }

    [Fact]
    public void GetFoldingsContaining_Returns_Correct_Folds ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        fm.CreateFolding (0, 20);
        fm.CreateFolding (7, 14);

        ReadOnlyCollection<FoldingSection> folds = fm.GetFoldingsContaining (10);

        Assert.Equal (2, folds.Count);
    }

    [Fact]
    public void GetFoldingAtLine_Returns_Section_Starting_On_Line ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (7, 21); // starts on line 2

        FoldingSection? found = fm.GetFoldingAtLine (2);

        Assert.Same (section, found);
    }

    [Fact]
    public void GetFoldingAtLine_Returns_Null_For_No_Fold ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\n");
        FoldingManager fm = new (doc);
        fm.CreateFolding (0, 7); // line 1 only

        Assert.Null (fm.GetFoldingAtLine (3));
    }

    [Fact]
    public void GetHiddenLineCount_Counts_Folded_Lines ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (0, 20); // lines 1-3 (offset 20 = end of line 3)
        section.IsFolded = true;

        // Lines 2-3 are hidden (line 1 is the fold header, shown with marker)
        Assert.Equal (2, fm.GetHiddenLineCount ());
    }

    [Fact]
    public void IsLineHidden_Returns_True_For_Folded_Interior_Lines ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (0, 20); // lines 1-3 (offset 20 = end of line 3)
        section.IsFolded = true;

        Assert.False (fm.IsLineHidden (1)); // fold start line is visible
        Assert.True (fm.IsLineHidden (2));
        Assert.True (fm.IsLineHidden (3));
        Assert.False (fm.IsLineHidden (4));
    }

    [Fact]
    public void UpdateFoldings_Preserves_IsFolded_State ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\nline 4\n");
        FoldingManager fm = new (doc);
        FoldingSection original = fm.CreateFolding (0, 14);
        original.IsFolded = true;

        // Update with same start offset — should reuse the section.
        fm.UpdateFoldings (
        [
            new NewFolding (0, 21) { Name = "updated" }
        ], -1);

        FoldingSection? reused = fm.AllFoldings.FirstOrDefault ();
        Assert.NotNull (reused);
        Assert.True (reused!.IsFolded);
        Assert.Equal ("updated", reused.Title);
    }

    [Fact]
    public void Edit_Inside_Fold_Updates_EndOffset ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (7, 14); // "line 2\n"
        section.IsFolded = true;

        // Insert " extra" at offset 11 (inside the fold)
        doc.Insert (11, " extra");

        // The section's end offset should grow by the insertion length.
        Assert.Equal (7, section.StartOffset);
        Assert.Equal (20, section.EndOffset);
    }

    [Fact]
    public void Edit_Before_Unfolded_Fold_Updates_Offsets ()
    {
        TextDocument doc = new ("line 1\nline 2\nline 3\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (7, 14); // starts on line 2
        var raised = 0;
        fm.FoldingChanged += (_, _) => raised++;

        doc.Insert (0, "prefix");

        Assert.Equal (13, section.StartOffset);
        Assert.Equal (20, section.EndOffset);
        Assert.Same (section, fm.GetFoldingAtLine (2));
        Assert.Equal (0, raised);
    }

    [Fact]
    public void Whole_Document_Replace_Clears_Zero_Length_Folds ()
    {
        TextDocument doc = new ("line 1\nline 2\n");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (0, 14);

        doc.Replace (0, doc.TextLength, "new text");

        // Zero-length folds should be auto-removed.
        // The section may have become zero-length or been repositioned.
        Assert.True (!fm.AllFoldings.Any () || fm.AllFoldings.All (f => f.Length > 0));
    }
}
