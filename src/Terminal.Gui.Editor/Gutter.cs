using System.Drawing;
using Terminal.Gui.Document;
using Terminal.Gui.Document.Folding;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Composite gutter hosting an optional <see cref="LineNumberGutter" /> and an optional <see cref="FoldingGutter" />.
///     Hosted as a SubView of the <see cref="Editor" />'s <see cref="Padding" />.
/// </summary>
public sealed class Gutter : View
{
    private readonly Editor _editor;
    private FoldingGutter? _foldingGutter;
    private LineNumberGutter? _lineNumbers;

    /// <summary>Initializes a new <see cref="Gutter" /> for <paramref name="editor" />.</summary>
    public Gutter (Editor editor)
    {
        ArgumentNullException.ThrowIfNull (editor);

        _editor = editor;
        CanFocus = false;

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
    }

    /// <summary>
    ///     Synchronizes the internal layout. Called by the editor when the gutter width changes.
    /// </summary>
    internal void SyncLayout ()
    {
        GutterOptions options = _editor.GutterOptions;
        var showLineNumbers = options.HasFlag (GutterOptions.LineNumbers);
        var showFolding = options.HasFlag (GutterOptions.Folding) && _editor.FoldingManager is not null;

        // --- Line numbers ---
        if (showLineNumbers)
        {
            if (_lineNumbers is null)
            {
                _lineNumbers = new LineNumberGutter (_editor)
                {
                    X = 0,
                    Y = 0,
                    Width = Dim.Fill (),
                    Height = Dim.Fill ()
                };
                Add (_lineNumbers);
            }
        }
        else
        {
            if (_lineNumbers is not null)
            {
                Remove (_lineNumbers);
                _lineNumbers.Dispose ();
                _lineNumbers = null;
            }
        }

        // --- Folding ---
        if (showFolding)
        {
            if (_foldingGutter is null)
            {
                _foldingGutter = new FoldingGutter (_editor)
                {
                    Y = 0,
                    Width = 2,
                    Height = Dim.Fill ()
                };
                Add (_foldingGutter);
            }
        }
        else
        {
            if (_foldingGutter is not null)
            {
                Remove (_foldingGutter);
                _foldingGutter.Dispose ();
                _foldingGutter = null;
            }
        }

        // Adjust widths depending on which subviews are present.
        if (_lineNumbers is not null && _foldingGutter is not null)
        {
            // Line numbers on the left, fold indicators on the right.
            _lineNumbers.X = 0;
            _lineNumbers.Width = Dim.Fill (2);
            _foldingGutter.X = Pos.Right (_lineNumbers);
        }
        else if (_lineNumbers is not null)
        {
            _lineNumbers.X = 0;
            _lineNumbers.Width = Dim.Fill ();
        }
        else if (_foldingGutter is not null)
        {
            _foldingGutter.X = 0;
        }
    }
}

/// <summary>
///     Renders line numbers and handles line-selection mouse interactions.
/// </summary>
internal sealed class LineNumberGutter : View
{
    private readonly Editor _editor;
    private int? _selectionAnchorLineNumber;

    internal LineNumberGutter (Editor editor)
    {
        _editor = editor;
        CanFocus = false;

        MouseBindings.Add (MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add (MouseFlags.WheeledDown, Command.ScrollDown);
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

        List<int> visibleLines = _editor.GetVisibleLineNumbers ();
        var firstVisibleIndex = _editor.Viewport.Y;
        var visibleHeight = _editor.Viewport.Height;

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
            var lineText = lineNumber.ToString ().PadLeft (viewport.Width - 1) + " ";

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
