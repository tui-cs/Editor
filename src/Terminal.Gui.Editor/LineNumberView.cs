using System.Drawing;
using Terminal.Gui.Drawing;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Renders one-based line numbers for an associated <see cref="Editor" />. Hosted as a SubView
///     of the <see cref="Editor" />'s <see cref="Padding" /> so it participates in the View hierarchy
///     and is correctly clipped beneath popovers, menus, and other overlay surfaces.
/// </summary>
/// <remarks>
///     The view tracks its parent <see cref="Editor" />'s viewport and document changes, redrawing
///     itself when either changes. It does not handle input.
/// </remarks>
public sealed class LineNumberView : View
{
    private readonly Editor _editor;

    /// <summary>Initializes a new <see cref="LineNumberView" /> for <paramref name="editor" />.</summary>
    public LineNumberView (Editor editor)
    {
        ArgumentNullException.ThrowIfNull (editor);

        _editor = editor;
        CanFocus = false;
        ViewportSettings |= ViewportSettingsFlags.TransparentMouse;
    }


    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        TextDocument? document = _editor.Document;

        if (document is null)
        {
            return true;
        }

        Rectangle viewport = Viewport;

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return true;
        }

        var firstVisibleLine = _editor.Viewport.Y;
        var visibleHeight = _editor.Viewport.Height;

        for (var row = 0; row < viewport.Height; row++)
        {
            var lineIndex = firstVisibleLine + row;
            string text;

            if (row >= visibleHeight || lineIndex < 0 || lineIndex >= document.LineCount)
            {
                text = new string (' ', viewport.Width);
            }
            else
            {
                // PadLeft to right-align the digits, then PadRight by one to leave a one-cell
                // gutter between the number and the editor content.
                text = (lineIndex + 1).ToString ().PadLeft (viewport.Width - 1).PadRight (viewport.Width);
            }

            Move (0, row);
            AddStr (text);
        }

        return true;
    }

}
