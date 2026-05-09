using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>A drawable span in a terminal-cell visual line.</summary>
public abstract class CellVisualLineElement
{
    protected CellVisualLineElement (
        int documentOffset,
        int documentLength,
        int visualColumn,
        int visualLength,
        Attribute attribute)
    {
        DocumentOffset = documentOffset;
        DocumentLength = documentLength;
        VisualColumn = visualColumn;
        VisualLength = visualLength;
        Attribute = attribute;
    }

    public int DocumentOffset { get; }

    public int DocumentLength { get; }

    public int DocumentEndOffset => DocumentOffset + DocumentLength;

    public int VisualColumn { get; }

    public int VisualLength { get; }

    public int VisualEndColumn => VisualColumn + VisualLength;

    public Attribute Attribute { get; set; }

    public abstract void Draw (View host, int x, int y, int visibleStart, int visibleEnd);
}
