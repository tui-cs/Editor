// CoPilot - gpt-5.4

using Terminal.Gui.Document;
using Xunit;

namespace Terminal.Gui.Editor.Tests;

public class TextDocumentTextChangedTests
{
    [Fact]
    public void TextChanged_Fires_When_Text_Is_Changed ()
    {
        TextDocument document = new ("alpha");
        var fired = false;

        document.TextChanged += (_, _) => fired = true;

        document.Text = "beta";

        Assert.True (fired);
        Assert.Equal ("beta", document.Text);
    }

    [Fact]
    public void TextChanged_Does_Not_Fire_When_Text_Is_Unchanged ()
    {
        TextDocument document = new ("alpha");
        var textChangedCount = 0;

        document.TextChanged += (_, _) => textChangedCount++;

        document.Text = "alpha";

        Assert.Equal (0, textChangedCount);
    }
}
