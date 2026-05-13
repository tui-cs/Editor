// Copilot - Claude Opus 4.6

using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Folding;

public class BraceFoldingStrategyTests
{
    [Fact]
    public void Detects_Brace_Folds ()
    {
        TextDocument doc = new ("{\n  content\n}\n");
        FoldingManager fm = new (doc);
        BraceFoldingStrategy strategy = new ();

        strategy.UpdateFoldings (fm, doc);

        FoldingSection? fold = fm.AllFoldings.FirstOrDefault ();
        Assert.NotNull (fold);
        Assert.Equal (0, fold!.StartOffset);
        Assert.Equal (13, fold.EndOffset);
    }

    [Fact]
    public void Ignores_Single_Line_Braces ()
    {
        TextDocument doc = new ("{ content }");
        FoldingManager fm = new (doc);
        BraceFoldingStrategy strategy = new ();

        strategy.UpdateFoldings (fm, doc);

        Assert.Empty (fm.AllFoldings);
    }

    [Fact]
    public void Handles_Nested_Braces ()
    {
        TextDocument doc = new ("{\n  {\n    inner\n  }\n}\n");
        FoldingManager fm = new (doc);
        BraceFoldingStrategy strategy = new ();

        strategy.UpdateFoldings (fm, doc);

        Assert.Equal (2, fm.AllFoldings.Count ());
    }

    [Fact]
    public void Empty_Document_No_Folds ()
    {
        TextDocument doc = new ("");
        FoldingManager fm = new (doc);
        BraceFoldingStrategy strategy = new ();

        strategy.UpdateFoldings (fm, doc);

        Assert.Empty (fm.AllFoldings);
    }
}
