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

        // Use the visual line's element range to scope correctly in word-wrap mode,
        // where a CellVisualLine represents only one wrapped segment of a DocumentLine.
        var segStart = line.Elements.Count > 0 ? line.Elements[0].DocumentOffset : line.DocumentLine.Offset;
        var segEnd = line.Elements.Count > 0 ? line.Elements[^1].DocumentEndOffset : line.DocumentLine.EndOffset;

        Attribute normal = host.GetAttributeForRole (VisualRole.Normal);

        // Invert foreground/background to distinguish additional carets from selection.
        Attribute caretAttr = new (normal.Background, normal.Foreground);

        foreach (var offset in _editor.AdditionalCaretOffsets)
        {
            if (offset < segStart || offset > segEnd)
            {
                continue;
            }

            var colInLine = offset - line.DocumentLine.Offset;
            var visualCol = line.GetVisualColumn (colInLine);
            var col = visualCol - viewport.X;

            if (col < 0 || col >= viewport.Width)
            {
                continue;
            }

            host.SetAttribute (caretAttr);
            host.Move (col, row);

            if (offset < segEnd)
            {
                var ch = _editor.Document.GetCharAt (offset);

                // Handle surrogate pairs for non-BMP characters (e.g. emoji).
                Rune rune;

                if (char.IsHighSurrogate (ch)
                    && offset + 1 < _editor.Document.TextLength)
                {
                    var lo = _editor.Document.GetCharAt (offset + 1);

                    if (char.IsLowSurrogate (lo))
                    {
                        rune = new (char.ConvertToUtf32 (ch, lo));
                    }
                    else
                    {
                        rune = new (' ');
                    }
                }
                else if (char.IsSurrogate (ch))
                {
                    rune = new (' ');
                }
                else
                {
                    rune = new (ch);
                }

                host.AddRune (rune);
            }
            else
            {
                host.AddRune (new Rune (' '));
            }
        }
    }
}
