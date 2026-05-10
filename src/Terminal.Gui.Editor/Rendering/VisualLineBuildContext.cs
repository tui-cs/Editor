using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>Options and styling inputs used to build a visual line.</summary>
public sealed class VisualLineBuildContext
{
    public VisualLineBuildContext (
        TextDocument document,
        int indentationSize,
        bool showTabs,
        Attribute normalAttribute,
        Attribute selectedAttribute,
        IReadOnlyList<StyledSegment>? styledSegments,
        int selectionStart,
        int selectionEnd,
        IEnumerable<IVisualLineTransformer> lineTransformers)
    {
        Document = document;
        IndentationSize = indentationSize;
        ShowTabs = showTabs;
        NormalAttribute = normalAttribute;
        SelectedAttribute = selectedAttribute;
        StyledSegments = styledSegments;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
        LineTransformers = lineTransformers;
    }

    public TextDocument Document { get; }

    public int IndentationSize { get; }

    public bool ShowTabs { get; }

    public Attribute NormalAttribute { get; }

    public Attribute SelectedAttribute { get; }

    public IReadOnlyList<StyledSegment>? StyledSegments { get; }

    public int SelectionStart { get; }

    public int SelectionEnd { get; }

    public IEnumerable<IVisualLineTransformer> LineTransformers { get; }

    public bool HasSelection => SelectionStart < SelectionEnd;
}
