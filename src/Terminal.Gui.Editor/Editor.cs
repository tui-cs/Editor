using System.Drawing;
using Terminal.Gui.Text.Document;
using Terminal.Gui.ViewBase;

namespace Terminal.Gui.Views;

/// <summary>
///     Single-document text editor View backed by <see cref="TextDocument"/>. Renders multi-line
///     text from a rope-backed document, tracks a caret offset, dispatches keyboard input to
///     navigate / edit, and scrolls content when it exceeds the viewport. Pre-MVP — selection,
///     folding, syntax highlighting still pending per <c>specs/00-plan.md</c>.
/// </summary>
public partial class Editor : View
{
    private TextDocument _document = null!;
    private int _caretOffset;

    /// <summary>
    ///     Sticky column for vertical caret moves. Tracks the column the user *intends* to be in,
    ///     even when the current line is shorter, so Up/Down across short lines snap back to the
    ///     original column on the first long line.
    /// </summary>
    private int _virtualCaretColumn;

    /// <summary>Initializes a new <see cref="Editor"/> with a placeholder document containing "Hello world".</summary>
    public Editor ()
    {
        CanFocus = true;
        Document = new ("Hello world");
    }

    /// <summary>The backing <see cref="TextDocument"/>. Setting this rewires change handlers and clamps the caret.</summary>
    public TextDocument Document
    {
        get => _document;
        set
        {
            ArgumentNullException.ThrowIfNull (value);

            if (ReferenceEquals (_document, value))
            {
                return;
            }

            if (_document is not null)
            {
                _document.Changed -= OnDocumentChanged;
            }

            _document = value;
            _document.Changed += OnDocumentChanged;

            _caretOffset = Math.Clamp (_caretOffset, 0, _document.TextLength);
            _virtualCaretColumn = GetCaretColumn ();
            UpdateContentSize ();
            SetNeedsDraw ();
        }
    }

    /// <summary>
    ///     Current caret offset. Clamped to <c>[0, Document.TextLength]</c>. Setting this scrolls the
    ///     viewport to keep the caret visible and raises <see cref="CaretChanged"/>.
    /// </summary>
    public int CaretOffset
    {
        get => _caretOffset;
        set => SetCaretOffset (value, resetVirtualColumn: true);
    }

    /// <summary>Raised whenever <see cref="Document"/> raises its own <c>Changed</c> event.</summary>
    public event EventHandler<DocumentChangeEventArgs>? DocumentChanged;

    /// <summary>Raised whenever <see cref="CaretOffset"/> changes.</summary>
    public event EventHandler? CaretChanged;

    private void SetCaretOffset (int value, bool resetVirtualColumn)
    {
        int clamped = Math.Clamp (value, 0, _document.TextLength);

        if (clamped == _caretOffset && resetVirtualColumn == false)
        {
            return;
        }

        bool changed = clamped != _caretOffset;
        _caretOffset = clamped;

        if (resetVirtualColumn)
        {
            _virtualCaretColumn = GetCaretColumn ();
        }

        EnsureCaretVisible ();
        SetNeedsDraw ();

        if (changed)
        {
            CaretChanged?.Invoke (this, EventArgs.Empty);
        }
    }

    private void OnDocumentChanged (object? sender, DocumentChangeEventArgs e)
    {
        // AnchorMovementType.AfterInsertion semantics: an insert at the caret moves the caret past
        // the inserted text; an insert strictly after the caret leaves it alone; a removal that
        // straddles the caret snaps it to the removal start.
        if (_caretOffset >= e.Offset)
        {
            if (_caretOffset < e.Offset + e.RemovalLength)
            {
                _caretOffset = e.Offset;
            }
            else
            {
                _caretOffset = _caretOffset - e.RemovalLength + e.InsertionLength;
            }

            _virtualCaretColumn = GetCaretColumn ();
        }

        UpdateContentSize ();
        EnsureCaretVisible ();
        SetNeedsDraw ();
        DocumentChanged?.Invoke (this, e);
    }

    private void UpdateContentSize ()
    {
        int maxWidth = 0;

        foreach (DocumentLine line in _document.Lines)
        {
            if (line.Length > maxWidth)
            {
                maxWidth = line.Length;
            }
        }

        // +1 column lets the caret sit just past the end-of-line.
        SetContentSize (new (maxWidth + 1, _document.LineCount));
    }

    private int GetCaretColumn ()
    {
        DocumentLine line = _document.GetLineByOffset (_caretOffset);

        return _caretOffset - line.Offset;
    }

    private int GetCaretLineIndex () => _document.GetLineByOffset (_caretOffset).LineNumber - 1;

    private void EnsureCaretVisible ()
    {
        Rectangle viewport = Viewport;

        if (viewport.Width == 0 || viewport.Height == 0)
        {
            return;
        }

        int caretLine = GetCaretLineIndex ();
        int caretCol = GetCaretColumn ();
        int newY = viewport.Y;
        int newX = viewport.X;

        if (caretLine < newY)
        {
            newY = caretLine;
        }
        else if (caretLine >= newY + viewport.Height)
        {
            newY = caretLine - viewport.Height + 1;
        }

        if (caretCol < newX)
        {
            newX = caretCol;
        }
        else if (caretCol >= newX + viewport.Width)
        {
            newX = caretCol - viewport.Width + 1;
        }

        if (newX != viewport.X || newY != viewport.Y)
        {
            Viewport = new (newX, newY, viewport.Width, viewport.Height);
        }
    }
}
