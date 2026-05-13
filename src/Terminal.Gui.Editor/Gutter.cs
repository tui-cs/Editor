using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Renders line number and folding UI for an associated <see cref="Editor" />. Hosted as a SubView
///     of the <see cref="Editor" />'s <see cref="Padding" /> so it participates in the View hierarchy
///     and is correctly clipped beneath popovers, menus, and other overlay surfaces.
/// </summary>
/// <remarks>
///     The view tracks its parent <see cref="Editor" />'s viewport and document changes, redrawing
///     itself when either changes. When the editor has a <see cref="FoldingManager" />, the last
///     column of the gutter shows fold indicators: <c>▾</c> for expanded folds, <c>▸</c> for
///     collapsed folds. Clicking the fold indicator toggles the fold.
/// </remarks>
public sealed class Gutter : View
{
    private readonly Editor _editor;
    private int? _selectionAnchorLineNumber;

    /// <summary>Initializes a new <see cref="Gutter" /> for <paramref name="editor" />.</summary>
    public Gutter (Editor editor)
    {
        ArgumentNullException.ThrowIfNull (editor);

        _editor = editor;
        CanFocus = false;
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

        FoldingManager? fm = _editor.FoldingManager;
        List<int> visibleLines = _editor.GetVisibleLineNumbers ();
        var firstVisibleIndex = _editor.Viewport.Y;
        var visibleHeight = _editor.Viewport.Height;
        var hasFolding = fm is not null;
        // Reserve one column for the fold indicator when folding is active.
        var numberWidth = hasFolding ? viewport.Width - 2 : viewport.Width - 1;

        for (var row = 0; row < viewport.Height; row++)
        {
            var visibleIndex = firstVisibleIndex + row;

            if (row >= visibleHeight || visibleIndex < 0 || visibleIndex >= visibleLines.Count)
            {
                Move (0, row);
                AddStr (new string (' ', viewport.Width));

                continue;
            }

            var lineNumber = visibleLines[visibleIndex];

            // PadLeft to right-align the digits, then add the fold indicator column.
            var lineText = lineNumber.ToString ().PadLeft (numberWidth);

            if (hasFolding)
            {
                FoldingSection? fold = fm!.GetFoldingAtLine (lineNumber);

                if (fold is not null)
                {
                    lineText += fold.IsFolded ? " ▸" : " ▾";
                }
                else if (fm.IsLineHidden (lineNumber))
                {
                    lineText += " │";
                }
                else
                {
                    // Check if this line is inside a fold (continuation).
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

                    lineText += isContinuation ? " │" : "  ";
                }
            }
            else
            {
                lineText += " ";
            }

            Move (0, row);
            AddStr (lineText);
        }

        return true;
    }

    /// <inheritdoc />
    protected override bool OnMouseEvent (Mouse mouse)
    {
        if (mouse.Position is not { } pos)
        {
            return false;
        }

        FoldingManager? fm = _editor.FoldingManager;

        // Check if click is on the fold indicator column (last column).
        if (fm is not null && mouse.Flags.HasFlag (MouseFlags.LeftButtonClicked))
        {
            var lineNumber = _editor.ViewRowToLineNumber (pos.Y);
            FoldingSection? fold = fm.GetFoldingAtLine (lineNumber);

            if (fold is not null && pos.X >= Viewport.Width - 2)
            {
                fold.IsFolded = !fold.IsFolded;

                return true;
            }
        }

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed) && mouse.Flags.HasFlag (MouseFlags.PositionReport))
        {
            if (_selectionAnchorLineNumber is not { } anchor)
            {
                anchor = _editor.ViewRowToLineNumber (pos.Y);
                _selectionAnchorLineNumber = anchor;
            }

            _editor.SelectLines (anchor, _editor.ViewRowToLineNumber (pos.Y));

            return true;
        }

        if (mouse.Flags.HasFlag (MouseFlags.LeftButtonPressed))
        {
            _selectionAnchorLineNumber = _editor.ViewRowToLineNumber (pos.Y);
            _editor.SelectLineAtViewRow (pos.Y);
            App?.Mouse.GrabMouse (this);

            return true;
        }

        if (!mouse.Flags.HasFlag (MouseFlags.LeftButtonReleased))
        {
            return false;
        }

        _selectionAnchorLineNumber = null;
        App?.Mouse.UngrabMouse ();

        return true;
    }
}
