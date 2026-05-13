using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Editor;

/// <summary>
///     Renders fold indicators (▸/▾/│) and toggles folds via <see cref="Command.Toggle" /> bound to
///     <see cref="MouseFlags.LeftButtonClicked" />.
/// </summary>
internal sealed class FoldingGutter : View
{
    private readonly Editor _editor;
    private int _lastMouseRow;

    internal FoldingGutter (Editor editor)
    {
        _editor = editor;
        CanFocus = false;

        AddCommand (Command.Toggle, OnToggleFold);
        MouseBindings.Add (MouseFlags.LeftButtonClicked, Command.Toggle);

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
    }

    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (mouse.Position is { } pos)
        {
            _lastMouseRow = pos.Y;
        }

        return base.OnMouseEvent (mouse);
    }

    /// <inheritdoc />
    protected override bool OnDrawingContent (DrawContext? context)
    {
        TextDocument? document = _editor.Document;
        FoldingManager? fm = _editor.FoldingManager;

        if (document is null || fm is null)
        {
            return true;
        }

        Rectangle viewport = Viewport;

        if (viewport.Width <= 0 || viewport.Height <= 0)
        {
            return true;
        }

        List<int> visibleLines = _editor.GetVisibleLineNumbers ();
        var firstVisibleIndex = _editor.Viewport.Y;
        var visibleHeight = _editor.Viewport.Height;

        for (var row = 0; row < viewport.Height; row++)
        {
            var visibleIndex = firstVisibleIndex + row;

            if (row >= visibleHeight || visibleIndex < 0 || visibleIndex >= visibleLines.Count)
            {
                Move (0, row);
                AddStr ("  ");

                continue;
            }

            var lineNumber = visibleLines[visibleIndex];
            FoldingSection? fold = fm.GetFoldingAtLine (lineNumber);
            string indicator;

            if (fold is not null)
            {
                indicator = fold.IsFolded ? "▸ " : "▾ ";
            }
            else
            {
                // Check if line is a continuation of an expanded fold.
                var isContinuation = false;

                foreach (FoldingSection fs in fm.AllFoldings)
                {
                    if (fs.IsFolded)
                    {
                        continue;
                    }

                    DocumentLine startLine =
                        document.GetLineByOffset (Math.Clamp (fs.StartOffset, 0, document.TextLength));

                    DocumentLine endLine =
                        document.GetLineByOffset (Math.Clamp (fs.EndOffset, 0, document.TextLength));

                    if (lineNumber > startLine.LineNumber && lineNumber <= endLine.LineNumber)
                    {
                        isContinuation = true;

                        break;
                    }
                }

                indicator = isContinuation ? "│ " : "  ";
            }

            Move (0, row);
            AddStr (indicator);
        }

        return true;
    }

    private bool? OnToggleFold ()
    {
        FoldingManager? fm = _editor.FoldingManager;

        if (fm is null)
        {
            return false;
        }

        // Determine which row was clicked from the last mouse position.
        var row = _lastMouseRow;
        var lineNumber = _editor.ViewRowToLineNumber (row);
        FoldingSection? fold = fm.GetFoldingAtLine (lineNumber);

        if (fold is null)
        {
            return false;
        }

        fold.IsFolded = !fold.IsFolded;

        return true;
    }
}
