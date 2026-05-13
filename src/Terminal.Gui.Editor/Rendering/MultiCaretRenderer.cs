using System.Drawing;
using System.Text;
using Terminal.Gui.Document;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Attribute = Terminal.Gui.Drawing.Attribute;

namespace Terminal.Gui.Editor.Rendering;

/// <summary>
///     Renders additional (non-primary) caret positions as inverted-attribute cells.
///     Installed automatically by <see cref="Editor" /> when multi-caret mode is active.
/// </summary>
public sealed class MultiCaretRenderer : IBackgroundRenderer
{
    private readonly Editor _editor;

    /// <summary>Initializes a new <see cref="MultiCaretRenderer" /> bound to the given editor.</summary>
    public MultiCaretRenderer (Editor editor)
    {
        _editor = editor;
    }

    /// <inheritdoc />
    public void Draw (View host, CellVisualLine line, int row, Rectangle viewport)
    {
        if (!_editor.HasMultipleCarets || _editor.Document is null)
        {
            return;
        }

        DocumentLine docLine = line.DocumentLine;
        Attribute normal = host.GetAttributeForRole (VisualRole.Normal);

        // Invert foreground/background to distinguish additional carets from selection.
        Attribute caretAttr = new (normal.Background, normal.Foreground);

        foreach (var offset in _editor.AdditionalCaretOffsets)
        {
            if (offset < docLine.Offset || offset > docLine.Offset + docLine.Length)
            {
                continue;
            }

            var colInLine = offset - docLine.Offset;
            var visualCol = line.GetVisualColumn (colInLine);
            var col = visualCol - viewport.X;

            if (col < 0 || col >= viewport.Width)
            {
                continue;
            }

            host.SetAttribute (caretAttr);
            host.Move (col, row);

            if (offset < docLine.Offset + docLine.Length)
            {
                Rune rune = new (_editor.Document.GetCharAt (offset));
                host.AddRune (rune);
            }
            else
            {
                host.AddRune (new Rune (' '));
            }
        }
    }
}
