// Codex - GPT-5

using Terminal.Gui.Editor.Document;
using Terminal.Gui.Views.Rendering;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Tests.Rendering;

public class VisualLineBuilderTests
{
    [Fact]
    public void Build_Uses_Grapheme_Cluster_Widths ()
    {
        const string emoji = "👩‍💻";

        CellVisualLine line = Build ($"a{emoji}b");

        Assert.Equal (4, line.VisualLength);
        Assert.Equal (1, line.GetVisualColumn (1));
        Assert.Equal (3, line.GetVisualColumn (1 + emoji.Length));
    }

    [Theory]
    [InlineData ("\tb", 4)]
    [InlineData ("a\tb", 4)]
    [InlineData ("ab\tc", 4)]
    [InlineData ("abc\td", 4)]
    [InlineData ("abcd\te", 8)]
    public void Build_Expands_Tabs_To_Next_Indentation_Stop (string text, int expectedColumnAfterTab)
    {
        var tabOffset = text.IndexOf ('\t');

        CellVisualLine line = Build (text);

        Assert.Equal (expectedColumnAfterTab, line.GetVisualColumn (tabOffset + 1));
    }

    [Fact]
    public void GetRelativeOffset_Inside_Tab_Snaps_To_Nearest_Edge_With_Midpoint_Before_Tab ()
    {
        CellVisualLine line = Build ("a\tb");

        Assert.Equal (1, line.GetRelativeOffset (2));
        Assert.Equal (2, line.GetRelativeOffset (3));
    }

    [Fact]
    public void Build_Applies_Transformers_In_Order ()
    {
        List<int> calls = [];
        CellVisualLine line = Build ("abc", [new RecordingTransformer (calls, 1), new RecordingTransformer (calls, 2)]);

        Assert.Equal ([1, 2], calls);
        Assert.Equal (3, line.VisualLength);
    }

    private static CellVisualLine Build (string text, IReadOnlyList<IVisualLineTransformer>? transformers = null)
    {
        TextDocument document = new (text);
        DocumentLine documentLine = document.GetLineByNumber (1);
        VisualLineBuildContext context = new (
            document,
            4,
            false,
            Attribute.Default,
            Attribute.Default,
            null,
            0,
            0,
            transformers ?? []);

        return new VisualLineBuilder ().Build (documentLine, context);
    }

    private sealed class RecordingTransformer (List<int> calls, int value) : IVisualLineTransformer
    {
        public void Transform (CellVisualLine line)
        {
            calls.Add (value);
        }
    }
}
