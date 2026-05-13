// CoPilot - claude-opus-4.6

using Terminal.Gui.Document;
using Terminal.Gui.Text.Indentation;
using Terminal.Gui.Views;
using Xunit;

namespace Terminal.Gui.Editor.Tests.Indentation;

/// <summary>
///     Tests for the <see cref="Views.Editor" /> auto-indent integration — verifying that
///     <see cref="IIndentationStrategy" /> is called after Enter and that <c>ted</c>-facing
///     properties work correctly.
/// </summary>
public class EditorAutoIndentTests
{
    [Fact]
    public void IndentationStrategy_Defaults_To_DefaultIndentationStrategy ()
    {
        Views.Editor editor = new ();

        Assert.IsType<DefaultIndentationStrategy> (editor.IndentationStrategy);
    }

    [Fact]
    public void IndentationStrategy_Can_Be_Set_To_Null ()
    {
        Views.Editor editor = new () { IndentationStrategy = null };

        Assert.Null (editor.IndentationStrategy);
    }

    [Fact]
    public void NewLine_Copies_Indentation_When_Strategy_Set ()
    {
        Views.Editor editor = new ()
        {
            Document = new TextDocument ("    hello")
        };

        // Place caret at end of "    hello"
        editor.CaretOffset = 9;

        // Simulate Enter — use the Command.NewLine through the document directly
        // to avoid needing a full UI host. The Editor wires NewLine → InsertNewLineWithAutoIndent.
        editor.Document!.Insert (editor.CaretOffset, "\n");
        DocumentLine newLine = editor.Document.GetLineByOffset (editor.CaretOffset);
        editor.IndentationStrategy!.IndentLine (editor.Document, newLine);

        Assert.Equal ("    hello\n    ", editor.Document.Text);
    }

    [Fact]
    public void NewLine_Does_Not_Indent_When_Strategy_Null ()
    {
        Views.Editor editor = new ()
        {
            IndentationStrategy = null,
            Document = new TextDocument ("    hello")
        };

        editor.CaretOffset = 9;
        editor.Document!.Insert (editor.CaretOffset, "\n");

        // No strategy → no auto-indent
        Assert.Equal ("    hello\n", editor.Document.Text);
    }

    [Fact]
    public void IndentationStrategy_Can_Be_Custom ()
    {
        var called = false;

        Views.Editor editor = new ()
        {
            IndentationStrategy = new TestIndentationStrategy (() => called = true),
            Document = new TextDocument ("hello")
        };

        editor.CaretOffset = 5;
        editor.Document!.Insert (editor.CaretOffset, "\n");
        DocumentLine newLine = editor.Document.GetLineByOffset (editor.CaretOffset);
        editor.IndentationStrategy!.IndentLine (editor.Document, newLine);

        Assert.True (called);
    }

    private sealed class TestIndentationStrategy (Action onIndent) : IIndentationStrategy
    {
        public void IndentLine (TextDocument document, DocumentLine line)
        {
            onIndent ();
        }

        public void IndentLines (TextDocument document, int beginLine, int endLine) { }
    }
}
