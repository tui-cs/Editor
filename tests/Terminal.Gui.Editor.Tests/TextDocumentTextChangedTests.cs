// CoPilot - gpt-5.5

using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class TextDocumentTextChangedTests
{
    [Fact]
    public void TextChanged_And_ChangeCompleted_Fire_Once_For_Update_Group ()
    {
        TextDocument document = new ();
        IDocument idocument = document;
        var changedCount = 0;
        var interfaceTextChangedCount = 0;
        var publicTextChangedCount = 0;
        var changeCompletedCount = 0;

        document.Changed += (_, _) => changedCount++;
        idocument.TextChanged += (_, _) => interfaceTextChangedCount++;
        document.TextChanged += (_, _) => publicTextChangedCount++;
        idocument.ChangeCompleted += (_, _) => changeCompletedCount++;

        document.BeginUpdate ();
        document.Insert (0, "a");
        document.Insert (1, "b");
        document.EndUpdate ();

        Assert.Equal (2, changedCount);
        Assert.Equal (2, interfaceTextChangedCount);
        Assert.Equal (1, publicTextChangedCount);
        Assert.Equal (1, changeCompletedCount);
        Assert.Equal ("ab", document.Text);
    }
}
