// CoPilot - claude-opus-4.6

using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Indentation;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Indentation;

/// <summary>
///     Tests for <see cref="DefaultIndentationStrategy" /> — the AvaloniaEdit-derived strategy
///     that copies leading whitespace from the previous line.
/// </summary>
public class DefaultIndentationStrategyTests
{
    [Fact]
    public void IndentLine_Copies_Spaces_From_Previous_Line ()
    {
        TextDocument doc = new ("    hello\n");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line2 = doc.GetLineByNumber (2);
        strategy.IndentLine (doc, line2);

        Assert.Equal ("    hello\n    ", doc.Text);
    }

    [Fact]
    public void IndentLine_Copies_Tab_From_Previous_Line ()
    {
        TextDocument doc = new ("\thello\n");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line2 = doc.GetLineByNumber (2);
        strategy.IndentLine (doc, line2);

        Assert.Equal ("\thello\n\t", doc.Text);
    }

    [Fact]
    public void IndentLine_Copies_Mixed_Whitespace_From_Previous_Line ()
    {
        TextDocument doc = new ("\t  hello\n");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line2 = doc.GetLineByNumber (2);
        strategy.IndentLine (doc, line2);

        Assert.Equal ("\t  hello\n\t  ", doc.Text);
    }

    [Fact]
    public void IndentLine_NoOp_On_First_Line ()
    {
        TextDocument doc = new ("hello");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line1 = doc.GetLineByNumber (1);
        strategy.IndentLine (doc, line1);

        Assert.Equal ("hello", doc.Text);
    }

    [Fact]
    public void IndentLine_NoOp_When_Previous_Line_Has_No_Indentation ()
    {
        TextDocument doc = new ("hello\n");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line2 = doc.GetLineByNumber (2);
        strategy.IndentLine (doc, line2);

        Assert.Equal ("hello\n", doc.Text);
    }

    [Fact]
    public void IndentLine_Replaces_Existing_Whitespace_On_Target_Line ()
    {
        TextDocument doc = new ("    hello\n  world");
        DefaultIndentationStrategy strategy = new ();

        DocumentLine line2 = doc.GetLineByNumber (2);
        strategy.IndentLine (doc, line2);

        Assert.Equal ("    hello\n    world", doc.Text);
    }

    [Fact]
    public void IndentLines_Does_Nothing ()
    {
        TextDocument doc = new ("hello\nworld\n");
        DefaultIndentationStrategy strategy = new ();

        // IndentLines is a no-op for DefaultIndentationStrategy
        strategy.IndentLines (doc, 1, 2);

        Assert.Equal ("hello\nworld\n", doc.Text);
    }

    [Fact]
    public void IndentLine_Throws_On_Null_Document ()
    {
        DefaultIndentationStrategy strategy = new ();

        Assert.Throws<ArgumentNullException> (() => strategy.IndentLine (null!, null!));
    }

    [Fact]
    public void IndentLine_Throws_On_Null_Line ()
    {
        TextDocument doc = new ("hello");
        DefaultIndentationStrategy strategy = new ();

        Assert.Throws<ArgumentNullException> (() => strategy.IndentLine (doc, null!));
    }
}
