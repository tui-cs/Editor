using Terminal.Gui.Text;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Views.Rendering;

/// <summary>A grapheme cluster rendered as document text.</summary>
public sealed class TextRunElement : CellVisualLineElement
{
    public TextRunElement (
        int documentOffset,
        int documentLength,
        int visualColumn,
        string text,
        Attribute attribute) : base (documentOffset, documentLength, visualColumn, Math.Max (0, text.GetColumns ()), attribute)
    {
        Text = text;
    }

    public string Text { get; }

    public override void Draw (View host, int x, int y, int visibleStart, int visibleEnd)
    {
        if (VisualLength <= 0 || VisualColumn < visibleStart || VisualEndColumn > visibleEnd)
        {
            return;
        }

        host.SetAttribute (Attribute);
        host.AddStr (x + VisualColumn - visibleStart, y, Text);
    }
}
