// Copilot - Claude Opus 4.6

using Terminal.Gui.Editor.Document;
using Terminal.Gui.Editor.Document.Folding;
using Terminal.Gui.Editor.Rendering;
using Xunit;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Tests.Folding;

public class FoldingTransformerTests
{
    [Fact]
    public void Transform_Replaces_Folded_Range_With_Marker ()
    {
        TextDocument doc = new ("start { hidden content } end");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (6, 24);
        section.IsFolded = true;
        section.Title = "{...}";

        FoldingTransformer transformer = new (fm);
        DocumentLine line = doc.GetLineByNumber (1);

        // Build a visual line with some elements.
        VisualLineBuilder builder = new ();
        VisualLineBuildContext context = new (
            doc,
            4,
            false,
            Attribute.Default,
            Attribute.Default,
            null,
            0,
            0,
            []);

        CellVisualLine visualLine = builder.Build (line, context);
        var originalElementCount = visualLine.Elements.Count;

        transformer.Transform (visualLine);

        // After transformation, there should be fewer elements (folded range replaced by marker).
        Assert.True (visualLine.Elements.Count < originalElementCount);

        // One of the elements should be a FoldingMarkerElement.
        Assert.Contains (visualLine.Elements, e => e is FoldingMarkerElement);
    }

    [Fact]
    public void Transform_Does_Nothing_When_Not_Folded ()
    {
        TextDocument doc = new ("start { hidden content } end");
        FoldingManager fm = new (doc);
        FoldingSection section = fm.CreateFolding (6, 24);
        section.IsFolded = false;

        FoldingTransformer transformer = new (fm);
        DocumentLine line = doc.GetLineByNumber (1);

        VisualLineBuilder builder = new ();
        VisualLineBuildContext context = new (
            doc,
            4,
            false,
            Attribute.Default,
            Attribute.Default,
            null,
            0,
            0,
            []);

        CellVisualLine visualLine = builder.Build (line, context);
        var originalElementCount = visualLine.Elements.Count;

        transformer.Transform (visualLine);

        // No folding marker should appear.
        Assert.DoesNotContain (visualLine.Elements, e => e is FoldingMarkerElement);
        Assert.Equal (originalElementCount, visualLine.Elements.Count);
    }
}
